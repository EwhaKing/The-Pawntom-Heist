using System;
using System.Collections.Generic;
using UnityEngine;

namespace Pawntom.Enemy.Core
{
    /// <summary>
    /// K-9 상태 머신의 본체.
    /// <para>
    /// 책임은 하나다 — <b>상태 전환을 결정하는 것</b>.
    /// 이동은 <see cref="IK9Motor"/>, 감지는 <see cref="IK9PerceptionSource"/>,
    /// 하울링은 <see cref="IK9AlertChannel"/> 에 위임한다(SRP).
    /// </para>
    /// <para>
    /// 엔진 타입에 의존하지 않으므로 에디터 모드 테스트에서 그대로 검증된다.
    /// </para>
    /// </summary>
    public sealed class K9Brain
    {
        private readonly K9Settings _settings;
        private readonly IK9Motor _motor;
        private readonly IReadOnlyList<IK9PerceptionSource> _sources;
        private readonly IK9AlertChannel _alert;

        // 시야 밖에서도 대상을 쫓기 위한 좌표 통로(요구 3). null 이면 추격 중 추가 이동 명령을 내지 않는다.
        private readonly IK9TargetTracker _tracker;

        // 순찰 경로 순회. 생성자에서 한 번만 만든다 — Tick 안에서 만들면 GC 할당이 생긴다.
        private readonly K9PatrolCursor _patrol = new K9PatrolCursor();

        // 조사 배회. 지점 제공자(요구 4)를 안에 들고 있다 — 제공자가 null 이면 배회하지 않는다.
        private readonly K9WanderCursor _wanderCursor;

        private K9State _state = K9State.Patrol;
        private Vector3 _target;
        private bool _hasTarget;

        private float _noDetectionTimer;

        // 경계 진입 이후의 경과 시간 전체. 경계 유지 게이트와 조사 전환 게이트가 함께 읽는다.
        private float _alertTimer;
        private float _lostSightTimer;
        private Vector3 _lastContactPoint;

        // Alert 중에 받은 소집 좌표. 소비만 해 두었다가 전환 7 의 목표로 쓴다(TASK-003 5.2).
        private Vector3 _pendingSummon;
        private bool _hasPendingSummon;

        // 틱마다 재사용한다. 새로 만들지 않으므로 힙 할당이 없다.
        private K9PerceptionSnapshot _snapshot;

        // 시야의 잔상. 생성자에서 한 번만 만든다 — Tick 안에서 만들면 GC 할당이 생긴다.
        private readonly K9SightMemory _sight = new K9SightMemory();

        // 하울링 이후의 경과 시계. 마찬가지로 생성자에서 한 번만 만든다.
        private readonly K9HowlClock _howl = new K9HowlClock();

        /// <param name="settings">수치 설정. null 이면 예외.</param>
        /// <param name="motor">이동 담당. null 이면 예외.</param>
        /// <param name="sources">감지 소스 목록. null 이면 감지가 없는 것으로 다룬다.</param>
        /// <param name="alert">하울링 통로. null 이면 송수신이 없는 것으로 다룬다.</param>
        public K9Brain(
            K9Settings settings,
            IK9Motor motor,
            IReadOnlyList<IK9PerceptionSource> sources,
            IK9AlertChannel alert)
            : this(settings, motor, sources, alert, null, null)
        {
        }

        /// <param name="settings">수치 설정. null 이면 예외.</param>
        /// <param name="motor">이동 담당. null 이면 예외.</param>
        /// <param name="sources">감지 소스 목록. null 이면 감지가 없는 것으로 다룬다.</param>
        /// <param name="alert">하울링 통로. null 이면 송수신이 없는 것으로 다룬다.</param>
        /// <param name="tracker">시야 밖 추격용 좌표 제공자. null 이면 추격 중 추가 이동 명령을 내지 않는다.</param>
        /// <param name="wander">조사 배회 지점 제공자. null 이면 배회하지 않고 제자리에 선다.</param>
        public K9Brain(
            K9Settings settings,
            IK9Motor motor,
            IReadOnlyList<IK9PerceptionSource> sources,
            IK9AlertChannel alert,
            IK9TargetTracker tracker,
            IK9WanderPointProvider wander)
        {
            if (settings == null)
            {
                throw new ArgumentNullException("settings");
            }

            if (motor == null)
            {
                throw new ArgumentNullException("motor");
            }

            _settings = settings;
            _motor = motor;
            _sources = sources;
            _alert = alert;
            _tracker = tracker;
            _wanderCursor = new K9WanderCursor(wander);
        }

        /// <summary>현재 상태.</summary>
        public K9State State
        {
            get { return _state; }
        }

        /// <summary>현재 이동 목표. 목표가 없으면 null.</summary>
        public Vector3? CurrentTarget
        {
            get
            {
                if (!_hasTarget)
                {
                    return null;
                }

                return _target;
            }
        }

        /// <summary>현재 향하고 있는 웨이포인트 번호. 경로가 없으면 0.</summary>
        public int WaypointIndex
        {
            get { return _patrol.WaypointIndex; }
        }

        /// <summary>
        /// 순찰 경로를 지정한다. 씬의 개체마다 다른 경로를 넣을 수 있다.
        /// 웨이포인트가 0개면 순찰 상태에서 제자리 대기한다.
        /// </summary>
        public void SetPatrolRoute(IReadOnlyList<Vector3> waypoints, bool loop)
        {
            _patrol.SetRoute(waypoints, loop);
        }

        /// <summary>
        /// 시간을 진행시킨다. 이번 호출에서 상태가 바뀌었으면 true.
        /// <para>한 번의 호출에서 상태 전환은 최대 한 번만 일어난다.</para>
        /// </summary>
        public bool Tick(float deltaTime)
        {
            // 0 이하의 시간에서는 아무 것도 판단하지 않는다.
            if (deltaTime <= 0f)
            {
                return false;
            }

            Perceive(deltaTime);

            // 하울링 이후의 경과 시간은 상태와 무관하게 흐른다. 상태 분기보다 먼저 누적한다.
            // 이 틱에 새로 하울링하면 아래 EnterAlert 가 다시 0 으로 되돌린다.
            _howl.Tick(deltaTime);

            switch (_state)
            {
                case K9State.Patrol:
                    return TickPatrol(deltaTime);

                case K9State.Investigate:
                    return TickInvestigate(deltaTime);

                case K9State.Alert:
                    return TickAlert(deltaTime);

                case K9State.Chase:
                    return TickChase(deltaTime);

                default:
                    return false;
            }
        }

        /// <summary>
        /// 모든 감지 소스를 훑어 이번 틱의 단서를 모은다.
        /// 소스가 몇 개든, 어떤 구현이든 이 반복문은 바뀌지 않는다(OCP).
        /// <para>
        /// 스냅샷을 다 채운 뒤 시야 기억을 한 번 굴린다. 호출부는 <see cref="Tick"/> 하나뿐이라
        /// 기억이 한 틱에 두 번 흐를 여지가 없다.
        /// </para>
        /// </summary>
        private void Perceive(float deltaTime)
        {
            _snapshot.Clear();

            // foreach 를 쓰면 열거자 할당이 생기므로 인덱스로 돈다.
            int count = _sources == null ? 0 : _sources.Count;
            for (int i = 0; i < count; i++)
            {
                IK9PerceptionSource source = _sources[i];
                if (source == null)
                {
                    continue;
                }

                K9Detection detection;
                if (source.TryDetect(out detection))
                {
                    _snapshot.Add(detection);
                }
            }

            if (_snapshot.HasContact)
            {
                _lastContactPoint = _snapshot.ContactPosition;
            }

            _sight.Tick(deltaTime, _snapshot.HasSight, _snapshot.SightPosition);
        }

        // 순찰 -----------------------------------------------------------
        private bool TickPatrol(float deltaTime)
        {
            // 전환 3: 접촉·초근접 감지 → Alert
            if (_snapshot.HasContact)
            {
                return EnterAlert(_snapshot.ContactPosition);
            }

            // 전환 3b: 시야 포착 → Alert.
            // 포착은 언제나 Alert 을 거친다(TASK-005 2.3 답변 2). 접촉이 시야보다 우선한다.
            if (_snapshot.HasSight)
            {
                return EnterAlert(_snapshot.SightPosition);
            }

            // 전환 1: 간접 단서 → Investigate
            if (_snapshot.HasTrace)
            {
                return EnterInvestigate(_snapshot.TracePosition);
            }

            // 전환 2: 하울링 소집 수신 → Investigate.
            // 순찰에서는 확인 순서를 바꾸지 않는다 — 감지 단서가 먼저 걸리면 소집은 읽지 않는다.
            Vector3 summon;
            if (TryConsumeSummon(out summon))
            {
                return EnterInvestigate(SpreadSummon(summon));
            }

            AdvancePatrol(deltaTime);
            return false;
        }

        /// <summary>
        /// 순찰 커서가 고른 행동을 실행한다. 순회 규칙 자체는 <see cref="K9PatrolCursor"/> 가 안다.
        /// <para>
        /// 정지에는 목표 해제가 따라붙는다. <c>_hasTarget</c> 은 커서가 모르는 두뇌 고유 필드라
        /// 커서가 대신 지워 줄 수 없다(TASK-009 5.3-8).
        /// </para>
        /// </summary>
        private void AdvancePatrol(float deltaTime)
        {
            Vector3 destination;
            K9PatrolAction action = _patrol.Advance(
                deltaTime, _motor.HasArrived, _settings.Patrol.WaitSeconds, out destination);

            if (action == K9PatrolAction.MoveTo)
            {
                SetTarget(destination);
                _motor.MoveTo(destination, _settings.Movement.PatrolSpeed);
                return;
            }

            if (action == K9PatrolAction.Stop)
            {
                _motor.Stop();
                _hasTarget = false;
            }
        }

        // 조사 -----------------------------------------------------------
        private bool TickInvestigate(float deltaTime)
        {
            // 감지 여부와 무관하게 매 틱 소집을 확인한다.
            // 읽지 않고 두면 채널에 남아 있다가 한참 뒤에 뒤늦게 발동한다.
            Vector3 summon;
            bool hasSummon = TryConsumeSummon(out summon);

            // 전환 5: 접촉·초근접 감지 → Alert.
            // 접촉은 하울링 쿨다운과 무관하게 언제나 Alert 다 —
            // "Chase 의 유일한 근거는 시야"라는 기획 규칙(GAME_DESIGN.md 3.3)을 깨지 않기 위해서다.
            // 그래서 이 분기가 시야 분기보다 앞에 있어야 한다.
            if (_snapshot.HasContact)
            {
                return EnterAlert(_snapshot.ContactPosition);
            }

            // 전환 5b: 시야 포착 → Alert.
            // 조사 중에 다시 눈으로 확인했으면 하울링부터 다시 나간다(TASK-005 2.3 답변 2).
            if (_snapshot.HasSight)
            {
                // 방금 하울링했다면 동료는 이미 불렀다. 다시 짖지 않고 바로 달려든다(TASK-007 2.3 원인 E).
                // 이 우회가 없으면 콘 경계의 대상 앞에서 Alert → Investigate → Alert 이 무한히 반복된다.
                if (_howl.HowledWithin(_settings.Alert.HowlCooldownSeconds))
                {
                    return EnterChase(_snapshot.SightPosition);
                }

                return EnterAlert(_snapshot.SightPosition);
            }

            // 신규 흔적을 받으면 목표를 갱신하고 무감지 타이머를 되돌린다.
            // 같은 틱에 소집이 겹쳐도 감지가 이긴다 — 직접 본 것이 전해 들은 것보다 신뢰도가 높다.
            // 이때도 소집은 위에서 이미 소비했으므로 채널에 남지 않는다.
            if (_snapshot.HasTrace)
            {
                Vector3 next = _snapshot.TracePosition;
                _noDetectionTimer = 0f;
                SetInvestigateTarget(next);
                return false;
            }

            // 소집 수신 — 목표를 새 좌표로 교체하고 무감지 타이머를 되돌린다. 상태는 유지된다.
            if (hasSummon)
            {
                _noDetectionTimer = 0f;
                SetInvestigateTarget(SpreadSummon(summon));
                return false;
            }

            // 목표에 도달했더라도 타이머는 계속 흐른다.
            _noDetectionTimer += deltaTime;

            // 전환 4: 20초간 신규 감지 없음 → Patrol
            if (_noDetectionTimer >= _settings.Investigate.GiveUpSeconds)
            {
                return EnterPatrol();
            }

            // 단서가 없는 동안에는 기준점 주변을 조금씩 돌아다닌다(요구 4).
            AdvanceWander(deltaTime);
            return false;
        }

        /// <summary>
        /// 조사 목표를 새 좌표로 옮긴다. 배회 기준점도 함께 따라간다.
        /// </summary>
        private void SetInvestigateTarget(Vector3 destination)
        {
            _wanderCursor.SetAnchor(destination);
            SetTarget(destination);
            _motor.MoveTo(destination, _settings.Movement.InvestigateSpeed);
        }

        /// <summary>
        /// 소집 좌표를 그대로 쓰지 않고 주변으로 흩어 준다.
        /// <para>
        /// 같은 좌표를 받은 동료들이 한 점에 몰려 서로 막는 것을 피한다.
        /// 반경이 0 이하거나 지점 제공자가 없거나 지점을 못 고르면 받은 좌표를 그대로 돌려준다.
        /// </para>
        /// <para>
        /// <b>소집 좌표에만 쓴다.</b> 흔적·시야·마지막 접촉 좌표는 이미 개별 좌표라 흩을 이유가 없다.
        /// </para>
        /// </summary>
        private Vector3 SpreadSummon(Vector3 summon)
        {
            return _wanderCursor.Spread(summon, _settings.Alert.SummonSpreadRadius);
        }

        /// <summary>
        /// 조사 기준점 주변을 무작위로 배회한다.
        /// <para>
        /// <b>무감지 타이머(<c>_noDetectionTimer</c>)를 절대 건드리지 않는다.</b>
        /// 건드리면 개가 조사 상태를 영원히 떠나지 못한다.
        /// </para>
        /// </summary>
        private void AdvanceWander(float deltaTime)
        {
            Vector3 point;
            bool picked = _wanderCursor.TryAdvance(
                deltaTime,
                _motor.HasArrived,
                _settings.Investigate.WanderIntervalSeconds,
                _settings.Investigate.WanderRadius,
                out point);

            if (!picked)
            {
                return;
            }

            SetTarget(point);
            _motor.MoveTo(point, _settings.Movement.InvestigateSpeed);
        }

        // 경계 -----------------------------------------------------------
        private bool TickAlert(float deltaTime)
        {
            // 감지 여부와 무관하게 매 틱 소집을 확인한다.
            // 소비만 하고 상태는 바꾸지 않는다 — 자기 하울링이 남의 하울링보다 우선이다.
            Vector3 summon;
            if (TryConsumeSummon(out summon))
            {
                _pendingSummon = summon;
                _hasPendingSummon = true;
            }

            _alertTimer += deltaTime;

            // 경계 내내 경보 지점 쪽으로 몸만 돌린다(TASK-006 요구 A). 위치는 그대로다.
            _motor.Face(SelectAlertAim(), _settings.Alert.TurnSpeedDegrees, deltaTime);

            // 경계 유지 시간 안에는 어디로도 가지 않는다. 0 이면 이 구속이 사실상 없다.
            // 조준은 위에서 이미 했다 — 여기서 막는 것은 이동 명령뿐이다(조준은 이동이 아니다).
            if (_alertTimer < _settings.Alert.HoldSeconds)
            {
                return false;
            }

            // 전환 6: 유지 시간이 지났고 시야가 있으면 하울링이 남아 있어도 즉시 추격한다(TASK-008 2.4).
            // 시야가 없으면 절대 Chase 로 가지 않는다(GAME_DESIGN.md 3.3 원문).
            if (_snapshot.HasSight)
            {
                return EnterChase(_snapshot.SightPosition);
            }

            // 이번 틱에 안 보여도 최근에 봤으면 추격한다(TASK-007 2.3 원인 B·D).
            // 판정이 단 한 틱만 열려 있으면 차폐 레이 한 번, 각도 흔들림 한 번에
            // 기다린 하울링이 통째로 무효가 된다. 기억이 그 창을 넓힌다.
            // 마지막으로 본 좌표를 향해 달린다 — Chase 의 시야 소실 타이머가 여기서부터 돈다.
            if (_sight.IsWarm(_settings.Perception.SightMemorySeconds))
            {
                return EnterChase(_sight.LastSeenPosition);
            }

            // 전환 7: 시야도 기억도 없다 — 하울링 시간을 다 채운 뒤에 조사로 넘어간다.
            // 이 게이트만 Alert.HowlDurationSeconds 를 쓴다.
            // 두 게이트가 순차 평가되므로 실제 조사 전환 시점은 max(Alert.HoldSeconds, Alert.HowlDurationSeconds) 다.
            // 유지 시간을 하울링 시간보다 크게 주면 조사 전환도 그만큼 늦어진다 — 갇히지는 않는다.
            if (_alertTimer < _settings.Alert.HowlDurationSeconds)
            {
                return false;
            }

            // 전환 7 본문.
            // 기획서에 없는 보완이다(TASK-002 5.2). 없으면 Alert 에서 영구 정지한다.
            // Alert 중에 소집을 받아 두었으면 그 좌표를 조사한다 — 이때만 분산이 붙는다.
            // 소집이 없을 때의 마지막 접촉 좌표에는 분산을 붙이지 않는다.
            Vector3 destination = _hasPendingSummon ? SpreadSummon(_pendingSummon) : _lastContactPoint;
            _hasPendingSummon = false;
            return EnterInvestigate(destination);
        }

        // 추격 -----------------------------------------------------------
        private bool TickChase(float deltaTime)
        {
            // 추격 중에는 소집을 무시한다. 다만 읽지 않고 두면 채널에 남아 있다가
            // 한참 뒤 순찰로 돌아간 순간 발동하므로, 소비해서 버린다(TASK-003 A-3).
            Vector3 discarded;
            TryConsumeSummon(out discarded);

            if (_snapshot.HasSight)
            {
                // 시야가 유지되는 동안 매 틱 목표 좌표를 갱신한다.
                _lostSightTimer = 0f;
                SetTarget(_snapshot.SightPosition);
                _motor.MoveTo(_snapshot.SightPosition, _settings.Movement.ChaseSpeed);
                return false;
            }

            _lostSightTimer += deltaTime;

            // 전환 8: 5초 이상 시야 소실 → Investigate.
            // 이 검사는 반드시 추적 이동보다 먼저다 — 상태를 떠나는 틱에
            // 쓸모없는 이동 명령이 나가면 마지막 목격 좌표가 덮여 버린다.
            if (_lostSightTimer >= _settings.Chase.LoseSightSeconds)
            {
                return EnterInvestigate(LastKnownPoint);
            }

            // 요구 3: 시야가 없어도 소실 제한 시간까지는 대상의 현재 좌표를 계속 쫓는다.
            // _lostSightTimer 는 되돌리지 않는다 — 되돌리면 5초가 영원히 오지 않는다.
            if (_tracker != null)
            {
                Vector3 tracked;
                if (_tracker.TryTrack(LastKnownPoint, out tracked))
                {
                    SetTarget(tracked);
                    _motor.MoveTo(tracked, _settings.Movement.ChaseSpeed);
                }
            }

            return false;
        }

        // 상태 진입 시 행동 ------------------------------------------------

        /// <summary>
        /// 이동의 성격을 상태에 맞게 한 번 지정한다.
        /// <para>
        /// <b>상태 진입에서만 부른다.</b> 매 틱 부르면 같은 값을 반복해서 쓰게 되고,
        /// 이동 구현이 그때마다 내부 상태를 되돌릴 여지가 생긴다.
        /// 경계는 이동하지 않으므로 <see cref="EnterAlert"/> 에서는 부르지 않는다.
        /// </para>
        /// </summary>
        private void ApplyMotionProfile(bool brakeNearDestination)
        {
            _motor.SetMotionProfile(
                _settings.Movement.Acceleration, _settings.Movement.TurnSpeedDegrees, brakeNearDestination);
        }

        private bool EnterPatrol()
        {
            _state = K9State.Patrol;

            // 웨이포인트에는 곱게 서야 하므로 목적지 제동을 켠다.
            ApplyMotionProfile(true);

            // 진행 플래그만 되돌린다. 웨이포인트 번호는 유지된다 — 가던 지점부터 이어 간다.
            _patrol.Reset();
            _hasTarget = false;

            // 순찰 복귀는 완전한 포기다. 잔상까지 지운다.
            _sight.Forget();
            return true;
        }

        private bool EnterInvestigate(Vector3 target)
        {
            _state = K9State.Investigate;

            // 조사 지점에도 곱게 선다.
            ApplyMotionProfile(true);

            _noDetectionTimer = 0f;
            _wanderCursor.SetAnchor(target);
            SetTarget(target);
            _motor.MoveTo(target, _settings.Movement.InvestigateSpeed);
            return true;
        }

        private bool EnterAlert(Vector3 contactPoint)
        {
            _state = K9State.Alert;
            _lastContactPoint = contactPoint;
            _alertTimer = 0f;
            _hasPendingSummon = false;

            // 진입 시 행동 — 즉시 정지 후 하울링 1회.
            _motor.Stop();
            _patrol.NotifyStopped();
            SetTarget(contactPoint);

            if (_alert != null)
            {
                _alert.Broadcast(_motor.Position, _settings.Alert.HowlRadius);
            }

            // 짖은 시각을 되돌린다. 채널이 없어도(소집을 못 보내도) 하울링 자체는 일어난 것으로 본다 —
            // 쿨다운이 재는 것은 "동료를 불렀는가"가 아니라 "방금 짖어서 대상을 놓쳤는가"다.
            _howl.MarkHowled();

            return true;
        }

        private bool EnterChase(Vector3 target)
        {
            _state = K9State.Chase;

            // 추격의 목적지는 대상 본인이라 늘 목적지 근처다. 제동을 켜면 내내 브레이크가 걸린다(TASK-008 2.3).
            // 이동 명령보다 먼저 걸어야 이번 이동부터 새 성격이 적용된다.
            ApplyMotionProfile(false);

            _lostSightTimer = 0f;
            SetTarget(target);
            _motor.MoveTo(target, _settings.Movement.ChaseSpeed);
            return true;
        }

        /// <summary>
        /// 소집 채널을 한 번 읽는다. 채널이 없으면 소집이 없는 것으로 다룬다.
        /// <para>호출한 상태는 결과를 쓰든 버리든 <b>읽은 것 자체로 채널을 비운다</b>.</para>
        /// </summary>
        private bool TryConsumeSummon(out Vector3 target)
        {
            if (_alert == null)
            {
                target = default(Vector3);
                return false;
            }

            return _alert.TryConsumeSummon(out target);
        }

        /// <summary>
        /// 경계 중 몸을 돌릴 좌표를 고른다.
        /// <para>
        /// 3단이다 — 이번 틱 시야 &gt; 아직 따뜻한 마지막 목격 좌표 &gt; 마지막 경보 지점.
        /// 시야가 살아 있으면 그쪽이 가장 정확하고, 한 프레임 깜빡였을 뿐이라면
        /// 경보 지점으로 홱 돌아가는 대신 방금 본 쪽을 계속 본다(TASK-007 2.3 원인 D).
        /// </para>
        /// </summary>
        private Vector3 SelectAlertAim()
        {
            if (_snapshot.HasSight)
            {
                return _snapshot.SightPosition;
            }

            if (_sight.IsWarm(_settings.Perception.SightMemorySeconds))
            {
                return _sight.LastSeenPosition;
            }

            return _lastContactPoint;
        }

        /// <summary>
        /// 대상에 대해 마지막으로 아는 좌표.
        /// 이동 목표가 살아 있으면 그 좌표이고, 없으면 마지막 접촉 지점이다.
        /// </summary>
        private Vector3 LastKnownPoint
        {
            get { return _hasTarget ? _target : _lastContactPoint; }
        }

        private void SetTarget(Vector3 target)
        {
            _target = target;
            _hasTarget = true;
        }
    }
}

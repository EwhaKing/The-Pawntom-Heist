using System;
using Pawntom.Enemy.Core;
using UnityEngine;
using UnityEngine.AI;

namespace Pawntom.Enemy.Adapters
{
    /// <summary>
    /// 내비게이션 이동 어댑터. 두뇌의 이동 명령을 엔진 쪽으로 옮긴다.
    /// <para>두뇌는 이 클래스의 존재를 모른다. 다른 구현으로 갈아 끼워도 동작이 같아야 한다(LSP).</para>
    /// </summary>
    public sealed class NavMeshEnemyMotor : IEnemyMotor
    {
        // 방향 벡터가 사실상 0 인지 판정하는 부동소수점 방어값. SightPerceptionSource 와 같은 값을 쓴다.
        private const float DirectionEpsilon = 0.0001f;

        private readonly NavMeshAgent _agent;
        private readonly Transform _transform;
        private readonly float _arriveDistance;

        // 겹침 방지: 다른 적들의 현재 위치를 채워 주는 공급자. null 이면 비키지 않는다.
        private readonly Action<Transform, System.Collections.Generic.List<Vector3>> _fillOthers;
        private readonly System.Collections.Generic.List<Vector3> _others = new System.Collections.Generic.List<Vector3>(8);
        private readonly float _separation;

        // 플레이어(살아 있는 추격 대상) 앞 멈춤 거리. 흔적·웨이포인트에는 쓰지 않는다 —
        // 흔적은 "도달 거리(ReachDistance) 안에 들어가야" 지워지므로 그 앞에서 멈추면 영영 못 지운다.
        private readonly float _targetStopDistance;
        private readonly Func<Vector3, bool> _isLiveTarget;

        public NavMeshEnemyMotor(NavMeshAgent agent, float arriveDistance)
            : this(agent, arriveDistance, 0f, null, 0f, null)
        {
        }

        /// <param name="targetStopDistance">
        /// 목적지가 플레이어일 때 그 앞에서 멈추는 거리. 0 이면 대상 몸 안까지 파고들어 CharacterController 가 계속 밀려난다.
        /// </param>
        /// <param name="isLiveTarget">목적지가 플레이어 위치인지 판정. null 이면 멈춤 거리를 쓰지 않는다.</param>
        /// <param name="separation">다른 적과 목적지가 겹칠 때 둘 최소 간격. 0 이면 비키지 않는다.</param>
        /// <param name="fillOthers">(나, 결과 버퍼) → 다른 적들의 현재 위치를 버퍼에 채운다. 할당 없이 재사용 버퍼를 쓴다.</param>
        public NavMeshEnemyMotor(
            NavMeshAgent agent,
            float arriveDistance,
            float targetStopDistance,
            Func<Vector3, bool> isLiveTarget,
            float separation,
            Action<Transform, System.Collections.Generic.List<Vector3>> fillOthers)
            : this(agent, arriveDistance, true)
        {
            _targetStopDistance = Mathf.Max(0f, targetStopDistance);
            _isLiveTarget = isLiveTarget;
            _separation = Mathf.Max(0f, separation);
            _fillOthers = fillOthers;
        }

        private NavMeshEnemyMotor(NavMeshAgent agent, float arriveDistance, bool _)
        {
            if (agent == null)
            {
                throw new ArgumentNullException("agent");
            }

            _agent = agent;
            _transform = agent.transform;
            _arriveDistance = Mathf.Max(0.01f, arriveDistance);
        }

        /// <inheritdoc/>
        public Vector3 Position
        {
            get { return _transform.position; }
        }

        /// <inheritdoc/>
        public bool HasArrived
        {
            get
            {
                if (!IsUsable)
                {
                    // 내비메시 위가 아니면 이동 명령 자체가 나가지 않는다.
                    // 도달했다고 답하면 웨이포인트만 헛돌게 되므로 false 를 준다.
                    return false;
                }

                if (_agent.pathPending)
                {
                    return false;
                }

                float threshold = Mathf.Max(_agent.stoppingDistance, _arriveDistance);
                if (_agent.remainingDistance > threshold)
                {
                    return false;
                }

                return !_agent.hasPath || _agent.velocity.sqrMagnitude < 0.01f;
            }
        }

        /// <inheritdoc/>
        public void MoveTo(Vector3 destination, float speed)
        {
            if (!IsUsable)
            {
                return;
            }

            // 조준(Face)이 가져갔던 회전 권한을 돌려준다(TASK-007 2.3 원인 A).
            // 두뇌가 경계를 떠나는 경로는 전부 이 메서드를 거치므로 여기서 복구하면 빠짐이 없다.
            _agent.updateRotation = true;

            _agent.speed = speed;
            _agent.isStopped = false;

            // 플레이어를 쫓을 때만 그 앞에서 멈춘다(몸으로 밀어붙여 플레이어가 못 움직이게 되는 것 방지).
            _agent.stoppingDistance = _targetStopDistance > 0f && _isLiveTarget != null && _isLiveTarget(destination)
                ? _targetStopDistance
                : 0f;

            // 같은 지점을 노리는 다른 적이 이미 그 근처에 있으면 그 앞에서 멈추도록 비켜 준다(겹침 방지).
            if (_fillOthers != null && _separation > 0f)
            {
                _others.Clear();
                _fillOthers(_transform, _others);
                destination = EnemySeparation.AdjustDestination(_transform.position, destination, _others, _separation);
            }

            _agent.SetDestination(destination);
        }

        /// <inheritdoc/>
        public void Stop()
        {
            if (!IsUsable)
            {
                return;
            }

            _agent.isStopped = true;
            _agent.velocity = Vector3.zero;
        }

        /// <inheritdoc/>
        public void SetMotionProfile(float acceleration, float turnSpeedDegrees, bool brakeNearDestination)
        {
            if (!IsUsable)
            {
                return;
            }

            // 0 이하를 그대로 넣으면 개가 영영 가속하지 못하거나 몸을 돌리지 못한다.
            // 잘못된 값 하나 때문에 나머지까지 버리지는 않으므로, 해당 값만 건너뛴다.
            if (acceleration > 0f)
            {
                _agent.acceleration = acceleration;
            }

            if (turnSpeedDegrees > 0f)
            {
                _agent.angularSpeed = turnSpeedDegrees;
            }

            // speed 는 여기서 건드리지 않는다 — 속도는 MoveTo 가 매 명령마다 따로 넘긴다.
            _agent.autoBraking = brakeNearDestination;
        }

        /// <inheritdoc/>
        public void Face(Vector3 target, float degreesPerSecond, float deltaTime)
        {
            if (!IsUsable)
            {
                return;
            }

            if (degreesPerSecond <= 0f)
            {
                return;
            }

            // 개가 위아래로 기울면 안 되므로 수직 성분을 버리고 평면 방향만 쓴다.
            Vector3 direction = target - _transform.position;
            direction.y = 0f;

            // 목표가 사실상 발밑이면 방향이 정해지지 않는다. 0 벡터로 회전을 만들면 경고가 난다.
            if (direction.sqrMagnitude < DirectionEpsilon)
            {
                return;
            }

            // 여기서부터 회전을 직접 쓴다. 에이전트의 자동 회전이 켜져 있으면
            // 이 프레임의 조준이 그대로 덮어써진다 — 에이전트 내부 갱신이 나중에 돌기 때문이다.
            // 조기 반환 경로에서는 권한을 건드리지 않는다. 조준하지 않는 프레임까지 뺏을 이유가 없다.
            _agent.updateRotation = false;

            Quaternion desired = Quaternion.LookRotation(direction, Vector3.up);

            // 한 번에 스냅하지 않는다 — 이번 틱에 허용된 각도만큼만 돌린다.
            _transform.rotation = Quaternion.RotateTowards(
                _transform.rotation, desired, degreesPerSecond * deltaTime);
        }

        private bool IsUsable
        {
            get { return _agent != null && _agent.isActiveAndEnabled && _agent.isOnNavMesh; }
        }
    }
}

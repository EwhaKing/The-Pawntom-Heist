using System;
using UnityEngine;

namespace Pawntom.Player.Core
{
    /// <summary>1인칭 팔 동작 수치. 인스펙터에서 손맛을 조정한다(L3).</summary>
    [Serializable]
    public sealed class FirstPersonArmSettings
    {
        [Header("대기(호흡)")]
        [Tooltip("숨 쉬는 주기(Hz)")]
        public float breathFrequency = 0.35f;
        [Tooltip("숨 쉴 때 위아래로 움직이는 폭(m)")]
        public float breathAmplitude = 0.004f;

        [Header("걷기·달리기 흔들림")]
        [Tooltip("걸을 때 걸음 주기(Hz)")]
        public float walkStepFrequency = 1.8f;
        [Tooltip("달릴 때 걸음 주기(Hz)")]
        public float runStepFrequency = 2.6f;
        [Tooltip("걸을 때 흔들림 폭(m) — x 좌우, y 위아래")]
        public Vector2 walkBobAmplitude = new Vector2(0.008f, 0.010f);
        [Tooltip("달릴 때 흔들림 폭(m) — x 좌우, y 위아래")]
        public Vector2 runBobAmplitude = new Vector2(0.016f, 0.024f);
        [Tooltip("걸음마다 손목이 기우는 각도(도)")]
        public float bobRollDegrees = 1.6f;

        [Header("달리기 자세")]
        [Tooltip("달릴 때 팔이 옮겨 가는 위치(m). 조금 내리고 당긴다")]
        public Vector3 runPoseOffset = new Vector3(0.01f, -0.025f, -0.03f);
        [Tooltip("달릴 때 팔 각도(도, x = 아래로 숙임)")]
        public Vector3 runPoseRotation = new Vector3(10f, 0f, 0f);

        [Header("점프·착지")]
        [Tooltip("세로 속도 1m/s 당 팔이 반대로 밀리는 거리(m). 올라갈 때 처지고 떨어질 때 뜬다")]
        public float airLagPerSpeed = 0.006f;
        [Tooltip("공중 지연의 최대 거리(m)")]
        public float airLagMax = 0.035f;
        [Tooltip("착지 충격: 낙하 속도 1m/s 당 아래로 꺼지는 속도")]
        public float landingImpulsePerSpeed = 0.12f;
        [Tooltip("착지 충격 스프링 강성 / 감쇠")]
        public float landingStiffness = 220f;
        public float landingDamping = 18f;

        [Header("시점 회전 지연")]
        [Tooltip("초당 1도 회전마다 팔이 반대로 밀리는 거리(m)")]
        public float swayPerDegreePerSecond = 0.00012f;
        [Tooltip("시점 지연 최대 거리(m)")]
        public float swayMax = 0.03f;
        [Tooltip("시점 지연이 따라오는 시간(초). 작을수록 빠르게 제자리로")]
        public float swaySmoothTime = 0.08f;

        [Header("톡 뻗기(상호작용·줍기·버리기)")]
        [Tooltip("뻗는 데 걸리는 시간(초)")]
        public float reachOutSeconds = 0.09f;
        [Tooltip("다 뻗은 채 머무는 시간(초)")]
        public float reachHoldSeconds = 0.04f;
        [Tooltip("돌아오는 데 걸리는 시간(초)")]
        public float reachBackSeconds = 0.2f;
        [Tooltip("다 뻗었을 때 위치(m). z = 앞으로, x 음수 = 화면 가운데 쪽")]
        public Vector3 reachOffset = new Vector3(-0.04f, 0.03f, 0.12f);
        [Tooltip("다 뻗었을 때 각도(도)")]
        public Vector3 reachRotation = new Vector3(-10f, -8f, 0f);
        [Tooltip("내려놓을 때(버리기) 뻗는 위치(m). 앞쪽 아래로")]
        public Vector3 placeOffset = new Vector3(-0.03f, -0.035f, 0.1f);
        [Tooltip("내려놓을 때 각도(도)")]
        public Vector3 placeRotation = new Vector3(16f, -6f, 0f);

        [Header("슬롯 바꾸기")]
        [Tooltip("팔을 내렸다 올리는 시간(초)")]
        public float switchSeconds = 0.3f;
        [Tooltip("가장 깊이 내려갈 때 위치(m)")]
        public Vector3 switchOffset = new Vector3(0f, -0.09f, -0.02f);
        [Tooltip("가장 깊이 내려갈 때 각도(도)")]
        public Vector3 switchRotation = new Vector3(20f, 0f, 0f);
    }

    /// <summary>1인칭 팔 한 프레임 입력.</summary>
    public struct ArmMotionInput
    {
        public float DeltaTime;
        /// <summary>0 = 정지, 1 = 걷기 속도, 2 = 달리기 속도.</summary>
        public float Locomotion;
        public bool Grounded;
        /// <summary>세로 속도(m/s). 위가 양수.</summary>
        public float VerticalSpeed;
        /// <summary>이번 프레임 시점 회전(도). x = 좌우(yaw), y = 위아래(pitch, 위가 양수).</summary>
        public Vector2 LookDelta;
    }

    /// <summary>
    /// 1인칭 팔(통메시) 절차적 동작. 카메라 기준 위치·각도 오프셋을 계산한다 — 엔진 오브젝트에 의존하지 않는다.
    /// <para>
    /// 겹쳐 더하는 층: 호흡 + 걸음 흔들림 + 달리기 자세 + 공중 지연 + 착지 충격 + 시점 지연 + 톡 뻗기 + 슬롯 교체.
    /// 각 층은 목표값을 스프링·감쇠로 따라가서 상태가 바뀌어도 툭 튀지 않는다.
    /// </para>
    /// </summary>
    public sealed class FirstPersonArmMotion
    {
        public enum ReachKind
        {
            Touch,
            Place
        }

        private readonly FirstPersonArmSettings _s;

        private float _time;
        private float _stepPhase;
        private float _bobWeight;
        private float _runWeight;
        private Vector3 _airLag;
        private Vector3 _airLagVelocity;
        private float _landing;
        private float _landingVelocity;
        private bool _wasGrounded = true;
        private float _lastVerticalSpeed;
        private Vector3 _sway;
        private Vector3 _swayVelocity;

        private float _reachTime = -1f;
        private ReachKind _reachKind;
        private float _switchTime = -1f;

        public FirstPersonArmMotion(FirstPersonArmSettings settings)
        {
            _s = settings ?? new FirstPersonArmSettings();
        }

        /// <summary>카메라 기준 위치 오프셋(m).</summary>
        public Vector3 PositionOffset { get; private set; }

        /// <summary>카메라 기준 각도 오프셋(도, 오일러).</summary>
        public Vector3 RotationOffset { get; private set; }

        /// <summary>뻗기 진행도 0~1(다 뻗음 = 1).</summary>
        public float ReachWeight { get; private set; }

        /// <summary>슬롯 교체 깊이 0~1(가장 깊이 내려감 = 1). 이때 손에 든 물건을 바꾸면 화면에서 바뀌는 순간이 가려진다.</summary>
        public float SwitchWeight { get; private set; }

        public bool IsReaching
        {
            get { return _reachTime >= 0f; }
        }

        public bool IsSwitching
        {
            get { return _switchTime >= 0f; }
        }

        /// <summary>뻗기 전체 시간(초).</summary>
        public float ReachDuration
        {
            get { return _s.reachOutSeconds + _s.reachHoldSeconds + _s.reachBackSeconds; }
        }

        /// <summary>뻗기가 다 뻗은 순간까지 걸리는 시간(초). 물건을 놓는 시점.</summary>
        public float ReachPeakSeconds
        {
            get { return _s.reachOutSeconds; }
        }

        /// <summary>톡 뻗기 시작. 진행 중이면 처음부터 다시.</summary>
        public void Reach(ReachKind kind)
        {
            _reachKind = kind;
            _reachTime = 0f;
        }

        /// <summary>슬롯 교체(내렸다 올리기) 시작.</summary>
        public void Switch()
        {
            _switchTime = 0f;
        }

        /// <summary>
        /// 뻗기 곡선(0~1). 빠르게 나가고(ease-out), 잠깐 머물고, 부드럽게 돌아온다(ease-in-out). 범위 밖은 0.
        /// </summary>
        public static float ReachCurve(float t, float outSeconds, float holdSeconds, float backSeconds)
        {
            if (t < 0f)
            {
                return 0f;
            }

            if (t < outSeconds)
            {
                float u = outSeconds > 0f ? t / outSeconds : 1f;
                float inv = 1f - u;
                return 1f - inv * inv * inv;
            }

            t -= outSeconds;

            if (t < holdSeconds)
            {
                return 1f;
            }

            t -= holdSeconds;

            if (t < backSeconds)
            {
                float u = backSeconds > 0f ? t / backSeconds : 1f;
                return 1f - u * u * (3f - 2f * u);
            }

            return 0f;
        }

        /// <summary>슬롯 교체 곡선(0~1~0, 가운데가 가장 깊다).</summary>
        public static float SwitchCurve(float t, float seconds)
        {
            if (t < 0f || seconds <= 0f || t >= seconds)
            {
                return 0f;
            }

            return Mathf.Sin(t / seconds * Mathf.PI);
        }

        public void Tick(ArmMotionInput input)
        {
            float dt = Mathf.Max(0f, input.DeltaTime);

            if (dt <= 0f)
            {
                return;
            }

            _time += dt;

            float walk = Mathf.Clamp01(input.Locomotion);
            float run = Mathf.Clamp01(input.Locomotion - 1f);

            // 걸음 흔들림: 땅에 있고 움직일 때만. 공중에선 서서히 사라진다.
            _bobWeight = Approach(_bobWeight, input.Grounded ? walk : 0f, 8f, dt);
            _runWeight = Approach(_runWeight, input.Grounded ? run : 0f, 6f, dt);

            float stepFrequency = Mathf.Lerp(_s.walkStepFrequency, _s.runStepFrequency, _runWeight);
            _stepPhase = Mathf.Repeat(_stepPhase + dt * stepFrequency * Mathf.PI * 2f, Mathf.PI * 4f);
            Vector2 amplitude = Vector2.Lerp(_s.walkBobAmplitude, _s.runBobAmplitude, _runWeight) * _bobWeight;

            // 8자 흔들림: 좌우는 한 걸음 주기, 위아래는 걸음마다(두 배 주기)
            Vector3 bob = new Vector3(
                Mathf.Sin(_stepPhase * 0.5f) * amplitude.x,
                -Mathf.Abs(Mathf.Sin(_stepPhase * 0.5f)) * amplitude.y,
                0f);
            float bobRoll = Mathf.Sin(_stepPhase * 0.5f) * _s.bobRollDegrees * _bobWeight;

            // 호흡: 멈춰 있을수록 또렷하게
            float breathe = Mathf.Sin(_time * _s.breathFrequency * Mathf.PI * 2f) * _s.breathAmplitude * (1f - _bobWeight * 0.7f);

            // 공중 지연: 올라갈 때 팔이 처지고, 떨어질 때 뜬다
            float airTarget = input.Grounded ? 0f : Mathf.Clamp(-input.VerticalSpeed * _s.airLagPerSpeed, -_s.airLagMax, _s.airLagMax);
            _airLag = Vector3.SmoothDamp(_airLag, new Vector3(0f, airTarget, 0f), ref _airLagVelocity, 0.08f, Mathf.Infinity, dt);

            // 착지 충격: 떨어지다 땅에 닿는 순간 아래로 꺼졌다가 스프링으로 돌아온다
            if (input.Grounded && !_wasGrounded && _lastVerticalSpeed < -0.5f)
            {
                _landingVelocity -= Mathf.Min(-_lastVerticalSpeed * _s.landingImpulsePerSpeed, 0.8f);
            }

            float accel = -_s.landingStiffness * _landing - _s.landingDamping * _landingVelocity;
            _landingVelocity += accel * dt;
            _landing += _landingVelocity * dt;
            _wasGrounded = input.Grounded;
            _lastVerticalSpeed = input.VerticalSpeed;

            // 시점 지연: 고개를 돌린 반대쪽으로 살짝 밀렸다가 따라온다
            Vector2 angularSpeed = input.LookDelta / dt;
            Vector3 swayTarget = new Vector3(
                Mathf.Clamp(-angularSpeed.x * _s.swayPerDegreePerSecond, -_s.swayMax, _s.swayMax),
                Mathf.Clamp(-angularSpeed.y * _s.swayPerDegreePerSecond, -_s.swayMax, _s.swayMax),
                0f);
            _sway = Vector3.SmoothDamp(_sway, swayTarget, ref _swayVelocity, _s.swaySmoothTime, Mathf.Infinity, dt);

            // 톡 뻗기
            ReachWeight = 0f;

            if (_reachTime >= 0f)
            {
                _reachTime += dt;
                ReachWeight = ReachCurve(_reachTime, _s.reachOutSeconds, _s.reachHoldSeconds, _s.reachBackSeconds);

                if (_reachTime >= ReachDuration)
                {
                    _reachTime = -1f;
                    ReachWeight = 0f;
                }
            }

            // 슬롯 교체
            SwitchWeight = 0f;

            if (_switchTime >= 0f)
            {
                _switchTime += dt;
                SwitchWeight = SwitchCurve(_switchTime, _s.switchSeconds);

                if (_switchTime >= _s.switchSeconds)
                {
                    _switchTime = -1f;
                }
            }

            Vector3 reachOffset = _reachKind == ReachKind.Place ? _s.placeOffset : _s.reachOffset;
            Vector3 reachRotation = _reachKind == ReachKind.Place ? _s.placeRotation : _s.reachRotation;

            PositionOffset =
                bob +
                new Vector3(0f, breathe, 0f) +
                _s.runPoseOffset * _runWeight +
                _airLag +
                new Vector3(0f, _landing, 0f) +
                _sway +
                reachOffset * ReachWeight +
                _s.switchOffset * SwitchWeight;

            RotationOffset =
                new Vector3(breathe * 120f, 0f, bobRoll) +
                _s.runPoseRotation * _runWeight +
                new Vector3(-_landing * 150f, 0f, 0f) +
                reachRotation * ReachWeight +
                _s.switchRotation * SwitchWeight;
        }

        private static float Approach(float current, float target, float speed, float dt)
        {
            return Mathf.Lerp(current, target, 1f - Mathf.Exp(-speed * dt));
        }
    }
}

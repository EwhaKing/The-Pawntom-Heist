using System;

namespace Pawntom.PlayerAnimation
{
    /// <summary>
    /// 플레이어 이동 상태 → 애니메이터 파라미터 계산. 엔진에 의존하지 않는다.
    /// <para>
    /// <see cref="Locomotion"/> 은 0 = 정지(Idle), 1 = 걷기 속도(Walk), 2 = 달리기 속도(Run) 로 정규화한 값이라
    /// 월드 단위·플레이어 스케일과 무관하다. 블렌드 트리 임계값도 0·1·2 를 쓴다.
    /// </para>
    /// <para>
    /// NetworkCharacterController 는 속도를 "실제 이동량 × TickRate" 로 다시 계산해 충돌·계단에서 값이 튄다.
    /// 그래서 속도는 지수 감쇠로 부드럽게 따라가고, 공중 판정은 짧은 유예를 둬서 계단 한 칸에 점프 동작이 나오지 않게 한다.
    /// </para>
    /// </summary>
    public sealed class PlayerLocomotionModel
    {
        public const float IdleValue = 0f;
        public const float WalkValue = 1f;
        public const float RunValue = 2f;

        private readonly float _walkSpeed;
        private readonly float _sprintSpeed;
        private readonly float _speedSmoothing;
        private readonly float _airborneGraceSeconds;
        private readonly float _jumpVelocityThreshold;

        private float _ungroundedSeconds;

        /// <param name="walkSpeed">걷기 속도(월드 단위/초). 0 이하면 1 로 본다.</param>
        /// <param name="sprintSpeed">달리기 속도. 걷기 이하면 걷기 속도의 2배로 본다.</param>
        /// <param name="speedSmoothing">속도 추종 빠르기(1/초). 0 이하면 즉시 따라간다.</param>
        /// <param name="airborneGraceSeconds">발이 떨어진 뒤 이 시간이 지나야 공중으로 본다(계단·턱 무시).</param>
        /// <param name="jumpVelocityThreshold">이 값보다 빠르게 올라가면 유예 없이 즉시 공중으로 본다(점프).</param>
        public PlayerLocomotionModel(
            float walkSpeed,
            float sprintSpeed,
            float speedSmoothing,
            float airborneGraceSeconds,
            float jumpVelocityThreshold)
        {
            _walkSpeed = walkSpeed > 0f ? walkSpeed : 1f;
            _sprintSpeed = sprintSpeed > _walkSpeed ? sprintSpeed : _walkSpeed * 2f;
            _speedSmoothing = speedSmoothing;
            _airborneGraceSeconds = Math.Max(0f, airborneGraceSeconds);
            _jumpVelocityThreshold = jumpVelocityThreshold;
        }

        /// <summary>0 = Idle, 1 = Walk, 2 = Run. 블렌드 트리 입력.</summary>
        public float Locomotion { get; private set; }

        /// <summary>공중에 떠 있는지. 점프 상태 전이 조건.</summary>
        public bool Airborne { get; private set; }

        /// <summary>목표 정규화 속도(감쇠 전). 0~2.</summary>
        public float TargetLocomotion(float horizontalSpeed)
        {
            if (horizontalSpeed <= 0f)
            {
                return IdleValue;
            }

            if (horizontalSpeed <= _walkSpeed)
            {
                return horizontalSpeed / _walkSpeed;
            }

            float run = WalkValue + (horizontalSpeed - _walkSpeed) / (_sprintSpeed - _walkSpeed);
            return Math.Min(RunValue, run);
        }

        public void Tick(float horizontalSpeed, float verticalSpeed, bool grounded, float deltaTime)
        {
            float target = TargetLocomotion(horizontalSpeed);
            if (_speedSmoothing <= 0f || deltaTime <= 0f)
            {
                Locomotion = target;
            }
            else
            {
                float t = 1f - (float)Math.Exp(-_speedSmoothing * deltaTime);
                Locomotion += (target - Locomotion) * t;
            }

            if (grounded)
            {
                _ungroundedSeconds = 0f;
                Airborne = false;
                return;
            }

            _ungroundedSeconds += Math.Max(0f, deltaTime);
            Airborne = verticalSpeed > _jumpVelocityThreshold || _ungroundedSeconds >= _airborneGraceSeconds;
        }
    }
}

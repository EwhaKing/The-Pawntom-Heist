using System.Collections.Generic;
using Pawntom.Enemy.Adapters;
using Pawntom.Enemy.Authoring;
using Pawntom.Enemy.Core;
using Pawntom.PlayerAnimation;
using UnityEngine;

namespace Pawntom.EnemyBridge
{
    /// <summary>
    /// 적 모델 연출: Idle / 이동(Walk↔Run) / 물기(Attack), 그리고 내비메시와 실제 바닥의 높이 차 보정.
    /// <para>
    /// 동기화 없이 모든 피어에서 같은 모습이 나오도록 피어마다 볼 수 있는 값만 쓴다 —
    /// 속도는 이 오브젝트의 실제 이동량(클라이언트는 NetworkTransform 이 옮겨 준다),
    /// 상태는 <see cref="NetworkEnemyAgent.State"/>(복제값), 대상 위치는 <see cref="SceneTargetProvider"/>.
    /// 물기 판정 거리·간격은 실제 피해(PlayerData)와 같은 <see cref="EnemyAttackSettings"/> 를 쓴다.
    /// </para>
    /// <para>
    /// 이동 재생 속도: 실제 이동 속도 ÷ 클립 고유 속도(실측 Walk 1.31·Run 1.75 m/s)를 <c>MoveSpeed</c> 로 넘겨,
    /// 어느 속도에서도 발이 미끄러지지 않게 다리를 그만큼 빨리 움직인다.
    /// Idle 을 이동 블렌드에 섞지 않는다 — 8초짜리 Idle 이 섞이면 블렌드 주기가 길어져 걸음이 몇 배 느려진다.
    /// </para>
    /// <para>
    /// 높이: 내비메시는 바닥보다 조금 위에 구워진다(튜토리얼 씬 7.8cm). 그 차이만 천천히 맞춘다 —
    /// 프레임마다 발 정점을 따라가면 걸음마다 몸이 들썩였다. 발 자체는 휴머노이드 Foot IK 가 잡는다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Pawntom/Enemy/Enemy Animation View")]
    public sealed class EnemyAnimationView : MonoBehaviour
    {
        private static readonly int LocomotionHash = Animator.StringToHash("Locomotion");
        private static readonly int MoveSpeedHash = Animator.StringToHash("MoveSpeed");
        private static readonly int AttackHash = Animator.StringToHash("Attack");

        [SerializeField] private Animator animator;

        [Tooltip("보이는 모델. 내비메시와 실제 바닥의 높이 차만큼 위아래로만 옮긴다")]
        [SerializeField] private Transform modelRoot;

        [Tooltip("속도 추종 빠르기(1/초). 클수록 걷기↔달리기 전환이 빠르다")]
        [SerializeField, Min(0f)] private float speedSmoothing = 8f;

        [Tooltip("Walk 클립을 1배속으로 틀 때 몸이 나아가는 속도(m/s). 실측값. 크게 하면 다리가 느려진다")]
        [SerializeField, Min(0.1f)] private float walkClipSpeed = 1.31f;

        [Tooltip("Run 클립을 1배속으로 틀 때 몸이 나아가는 속도(m/s). 실측값. 크게 하면 다리가 느려진다")]
        [SerializeField, Min(0.1f)] private float runClipSpeed = 1.75f;

        [Tooltip("발밑 바닥을 찾는 최대 거리(m). 이보다 바닥이 멀면(공중·계단 끝) 맞추지 않는다")]
        [SerializeField, Min(0.01f)] private float groundSearchDistance = 0.4f;

        private EnemyAgent _agent;
        private NetworkEnemyAgent _network;
        private PlayerLocomotionModel _model;
        private float _walkSpeed;
        private float _runSpeed;
        private Vector3 _lastPosition;
        private bool _hasLast;
        private float _attackCooldown;

        private Vector3 _modelHome;
        private float _soleLocalY;
        private float _groundOffset;
        private readonly RaycastHit[] _hits = new RaycastHit[8];

        private void Awake()
        {
            _agent = GetComponent<EnemyAgent>();
            _network = GetComponent<NetworkEnemyAgent>();

            _walkSpeed = _agent != null ? _agent.Settings.Movement.PatrolSpeed : 2f;
            _runSpeed = _agent != null ? _agent.Settings.Movement.ChaseSpeed : 4f;
            _model = new PlayerLocomotionModel(_walkSpeed, _runSpeed, speedSmoothing, 0f, float.MaxValue);

            if (animator != null)
            {
                // 이동은 NavMeshAgent·NetworkTransform 이 한다. 클립이 몸을 끌고 가면 안 된다.
                animator.applyRootMotion = false;
            }

            if (modelRoot != null)
            {
                _modelHome = modelRoot.localPosition;
            }

            // 프리팹에서 발바닥을 몸통 기준 -baseOffset(=내비메시 높이)에 맞춰 두었다.
            UnityEngine.AI.NavMeshAgent navAgent = GetComponent<UnityEngine.AI.NavMeshAgent>();
            _soleLocalY = navAgent != null ? -navAgent.baseOffset : 0f;
        }

        private void OnEnable()
        {
            _hasLast = false;
            _groundOffset = 0f;
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;

            if (deltaTime <= 0f)
            {
                return;
            }

            UpdateLocomotion(deltaTime);
            UpdateAttack(deltaTime);
        }

        private void LateUpdate()
        {
            MatchGroundHeight(Time.deltaTime);
        }

        private void UpdateLocomotion(float deltaTime)
        {
            Vector3 position = transform.position;

            if (!_hasLast)
            {
                _lastPosition = position;
                _hasLast = true;
                return;
            }

            float dx = position.x - _lastPosition.x;
            float dz = position.z - _lastPosition.z;
            _lastPosition = position;

            if (animator == null)
            {
                return;
            }

            float horizontalSpeed = Mathf.Sqrt(dx * dx + dz * dz) / deltaTime;
            _model.Tick(horizontalSpeed, 0f, true, deltaTime);

            float locomotion = _model.Locomotion;
            animator.SetFloat(LocomotionHash, locomotion);
            animator.SetFloat(MoveSpeedHash, MoveAnimationSpeed(locomotion));
        }

        /// <summary>
        /// 이동 상태 재생 배율 = 지금 속도 ÷ 지금 블렌드(Walk↔Run)가 1배속일 때 나아가는 속도.
        /// 순찰 속도면 Walk 1.9배, 추격 속도면 Run 2.9배 — 다리 움직임이 실제 이동과 맞는다.
        /// </summary>
        private float MoveAnimationSpeed(float locomotion)
        {
            float speed = locomotion <= 1f
                ? locomotion * _walkSpeed
                : _walkSpeed + (locomotion - 1f) * (_runSpeed - _walkSpeed);

            float blend = Mathf.Clamp01(locomotion - 1f);
            float clipSpeed = Mathf.Lerp(walkClipSpeed, runClipSpeed, blend);

            // 멈추는 중(Idle 로 넘어가는 짧은 순간)에 다리가 거의 멈춰 보이지 않게 아래를 받친다.
            return Mathf.Clamp(speed / clipSpeed, 0.6f, 4f);
        }

        /// <summary>추격 중 공격 거리 안에 플레이어가 있으면 공격 간격마다 물기 동작을 낸다(피해 판정과 같은 거리·간격).</summary>
        private void UpdateAttack(float deltaTime)
        {
            if (_attackCooldown > 0f)
            {
                _attackCooldown -= deltaTime;
                return;
            }

            if (animator == null || _agent == null || CurrentState() != EnemyState.Chase)
            {
                return;
            }

            SceneTargetProvider provider = SceneTargetProvider.Instance;

            if (provider == null)
            {
                return;
            }

            EnemyAttackSettings attack = _agent.Settings.Attack;
            float rangeSqr = attack.Range * attack.Range;
            Vector3 self = transform.position;
            IReadOnlyList<Transform> targets = provider.Targets;

            for (int i = 0; i < targets.Count; i++)
            {
                Transform target = targets[i];

                if (target == null)
                {
                    continue;
                }

                float dx = target.position.x - self.x;
                float dz = target.position.z - self.z;

                if (dx * dx + dz * dz <= rangeSqr)
                {
                    animator.SetTrigger(AttackHash);
                    _attackCooldown = attack.CooldownSeconds;
                    return;
                }
            }
        }

        private EnemyState CurrentState()
        {
            if (_network != null && _network.Object != null && _network.Object.IsValid)
            {
                return _network.State;
            }

            return _agent != null ? _agent.State : EnemyState.Patrol;
        }

        /// <summary>
        /// 발바닥 기준 높이(내비메시)와 발밑 실제 바닥(적·플레이어·트리거 제외)의 차이만큼 모델을 옮긴다.
        /// 애니메이션과 무관한 값이라 걸음마다 들썩이지 않는다. 계단·턱은 천천히 따라간다. 할당 없음.
        /// </summary>
        private void MatchGroundHeight(float deltaTime)
        {
            if (modelRoot == null || deltaTime <= 0f)
            {
                return;
            }

            float soleY = transform.TransformPoint(0f, _soleLocalY, 0f).y;
            Vector3 origin = new Vector3(transform.position.x, soleY + 0.5f, transform.position.z);
            int count = Physics.RaycastNonAlloc(origin, Vector3.down, _hits, 0.5f + groundSearchDistance, ~0, QueryTriggerInteraction.Ignore);

            float best = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                Collider hitCollider = _hits[i].collider;

                if (hitCollider == null || hitCollider is CharacterController ||
                    hitCollider.GetComponentInParent<EnemyAgent>() != null)
                {
                    continue;
                }

                best = Mathf.Min(best, _hits[i].distance);
            }

            float target = best < float.MaxValue ? Mathf.Clamp(0.5f - best, -groundSearchDistance, groundSearchDistance) : 0f;
            _groundOffset = Mathf.Lerp(_groundOffset, target, 1f - Mathf.Exp(-6f * deltaTime));

            Transform parent = modelRoot.parent;
            float parentScale = parent != null ? Mathf.Max(0.0001f, parent.lossyScale.y) : 1f;
            modelRoot.localPosition = _modelHome + Vector3.up * (_groundOffset / parentScale);
        }
    }
}

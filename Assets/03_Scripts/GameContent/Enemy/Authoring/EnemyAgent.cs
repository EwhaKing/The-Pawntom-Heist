using System.Collections.Generic;
using Pawntom.Enemy.Adapters;
using Pawntom.Enemy.Core;
using UnityEngine;
using UnityEngine.AI;

namespace Pawntom.Enemy.Authoring
{
    /// <summary>
    /// K-9 한 마리를 조립하는 어댑터.
    /// <para>
    /// 하는 일은 셋뿐이다 — (1) 부품을 모아 <see cref="EnemyBrain"/> 에 주입하고,
    /// (2) 지정된 순찰 경로를 넘기고, (3) 매 프레임 시간을 흘려보낸다.
    /// <b>상태 판단은 전부 두뇌 안에 있다. 이 클래스에는 상태 분기가 없다.</b>
    /// </para>
    /// <para>
    /// 네트워크 동기화가 필요해지면 이 클래스를 고치는 대신
    /// 같은 조립을 하는 네트워크용 어댑터를 하나 더 만들면 된다. 두뇌는 그대로 쓴다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(EnemyAlertChannelBehaviour))]
    [AddComponentMenu("Pawntom/Enemy/Agent")]
    public sealed class EnemyAgent : MonoBehaviour
    {
        [Header("설정")]
        [Tooltip("이 개체만의 수치. 다른 K-9 과 다르게 줘도 된다")]
        [SerializeField] private EnemySettings _settings = new EnemySettings();

        [Header("순찰")]
        [Tooltip("이 개체가 걸어갈 경로. 개체마다 서로 다른 경로를 지정한다")]
        [SerializeField] private PatrolRoute _patrolRoute;

        [Header("감지 대상")]
        [Tooltip("비워 두면 씬에서 가장 먼저 활성화된 제공자를 쓴다")]
        [SerializeField] private SceneTargetProvider _targetProvider;

        private readonly List<EnemyPerceptionSourceBehaviour> _sourceBehaviours =
            new List<EnemyPerceptionSourceBehaviour>(4);

        private readonly List<IEnemyPerceptionSource> _sources =
            new List<IEnemyPerceptionSource>(4);

        private NavMeshAgent _navMeshAgent;
        private EnemyAlertChannelBehaviour _alertChannel;

        // 교전 전이 규칙. 붙어 있지 않으면 null 이고, 그대로 두뇌에 넘긴다 —
        // 기본값(하울링)을 여기서 만들지 않는다. 기본값의 정본은 EnemyBrain 한 곳이다.
        private EnemyEngagementPolicyBehaviour _engagementPolicy;
        private EnemyBrain _brain;

        // 시야 밖 추격용 좌표 통로. 대상 제공자가 뒤늦게 해결될 수 있어 재주입을 받는다.
        private readonly NearestTargetTracker _targetTracker = new NearestTargetTracker();

        /// <summary>현재 상태. 디버그 표시용이다.</summary>
        public EnemyState State
        {
            get { return _brain == null ? EnemyState.Patrol : _brain.State; }
        }

        /// <summary>조립된 두뇌. 테스트 씬에서 들여다볼 때 쓴다.</summary>
        public EnemyBrain Brain
        {
            get { return _brain; }
        }

        /// <summary>
        /// 이 개체의 수치 설정. 읽기 전용이다.
        /// <para>
        /// 감지 소스가 <b>에디트 모드에서</b> 기즈모를 그릴 때 값을 읽는 경로다.
        /// <c>Configure</c> 주입은 <c>Awake</c> 에서만 일어나 Play 전에는 값이 없기 때문에,
        /// 씬 편집 중에는 이 접근자를 통해야 한다.
        /// </para>
        /// </summary>
        public EnemySettings Settings
        {
            get { return _settings; }
        }

        /// <summary>
        /// 스스로 시간을 흘려보낼지 여부. 기본값은 <c>true</c> 다.
        /// <para>
        /// 네트워크 어댑터가 붙으면 <c>false</c> 로 내리고, 어댑터가 <see cref="Tick"/> 을 대신 부른다.
        /// <b>직렬화 필드로 만들지 않는다</b> — 인스펙터에서 잘못 꺼 두면 로컬 씬이 조용히 멈춘다.
        /// </para>
        /// </summary>
        public bool SelfTick { get; set; } = true;

        /// <summary>두뇌에 시간을 흘려보낸다. 두뇌가 없으면 아무것도 하지 않는다.</summary>
        public void Tick(float deltaTime)
        {
            // 두뇌는 직렬화되지 않는다. 플레이 중에 스크립트를 고쳐 리컴파일이 걸리면
            // 이 값이 null 이 되고 Awake 는 다시 불리지 않는다 — 그 뒤로 매 프레임 예외가 난다.
            if (_brain == null)
            {
                return;
            }

            _brain.Tick(deltaTime);
        }

        /// <summary>런타임에 순찰 경로를 갈아 끼운다.</summary>
        public void SetPatrolRoute(PatrolRoute route)
        {
            _patrolRoute = route;
            ApplyPatrolRoute();
        }

        private void Awake()
        {
            _navMeshAgent = GetComponent<NavMeshAgent>();
            _alertChannel = GetComponent<EnemyAlertChannelBehaviour>();

            // 붙어 있으면 그 정책으로, 없으면 null 그대로 넘긴다.
            // 유닛 종류를 가르는 플래그는 없다 — 이 컴포넌트의 유무가 곧 종류다(OCP).
            _engagementPolicy = GetComponent<EnemyEngagementPolicyBehaviour>();

            if (_targetProvider == null)
            {
                _targetProvider = SceneTargetProvider.Instance;
            }

            CollectPerceptionSources();

            // 두뇌를 만들기 전에 걸어 둔다. 두뇌는 첫 Enter* 에서 상태에 맞는 값으로 다시 덮어쓴다.
            ApplyMovementSettings();

            BuildBrain();
        }

        private void BuildBrain()
        {

            // 멈춤 거리: 추격 대상 몸 안까지 파고들어 플레이어를 계속 밀어내지 않도록.
            // 겹침 방지: 같은 지점을 노리는 다른 적이 있으면 그 앞에서 멈추도록 목적지를 비킨다.
            NavMeshEnemyMotor motor = new NavMeshEnemyMotor(
                _navMeshAgent,
                _settings.Patrol.ArriveDistance,
                _settings.Movement.StopDistance,
                IsLiveTargetPosition,
                _settings.Movement.SeparationDistance,
                FillOtherAgentPositions);
            NavMeshWanderPointProvider wander =
                new NavMeshWanderPointProvider(_settings.Investigate.WanderSampleDistance);

            _brain = new EnemyBrain(
                _settings,
                motor,
                _sources,
                _alertChannel,
                _targetTracker,
                wander,
                _engagementPolicy == null ? null : _engagementPolicy.Policy);

            // 본부 등 출입 금지 구역. 구역이 하나도 없으면 판정은 늘 false 다.
            _brain.SetSafeZone(EnemySafeZone.Registry);
        }

        /// <summary>단계 재시도처럼 외부에서 위치를 복원할 때 추격 기억과 경로도 새로 시작합니다.</summary>
        public void ResetAfterTeleport(Vector3 position, Quaternion rotation)
        {
            if (_navMeshAgent != null && _navMeshAgent.enabled)
            {
                _navMeshAgent.Warp(position);
                if (_navMeshAgent.isOnNavMesh)
                {
                    _navMeshAgent.ResetPath();
                    _navMeshAgent.velocity = Vector3.zero;
                }
            }
            else transform.position = position;
            transform.rotation = rotation;
            BuildBrain();
            ApplyPatrolRoute();
        }

        private static readonly List<EnemyAgent> ActiveAgents = new List<EnemyAgent>(8);

        /// <summary>
        /// 씬에 켜져 있는 적 전체. CCTV 평면도의 적 마커처럼 적 위치를 보여 주는 쪽이 매 프레임 할당 없이 읽는다.
        /// </summary>
        public static IReadOnlyList<EnemyAgent> Active
        {
            get { return ActiveAgents; }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetActiveAgents()
        {
            ActiveAgents.Clear();
        }

        private void OnEnable()
        {
            if (!ActiveAgents.Contains(this))
            {
                ActiveAgents.Add(this);
            }

            // 회피 우선순위가 모두 같으면(기본 50) 마주친 두 마리가 서로 양보하지 못해 겹친다.
            // 개체마다 다른 값을 줘 한쪽이 비켜 가게 한다(낮을수록 우선 — 30~68 사이에서 겹치지 않게 분산).
            if (_navMeshAgent == null)
            {
                _navMeshAgent = GetComponent<NavMeshAgent>();
            }

            if (_navMeshAgent != null)
            {
                _navMeshAgent.avoidancePriority = 30 + (ActiveAgents.IndexOf(this) * 13) % 39;
            }
        }

        /// <summary>
        /// 이 좌표가 지금 플레이어(감지 대상)가 서 있는 곳인가. 추격 목적지일 때만 멈춤 거리를 쓰기 위한 판정(할당 없음).
        /// 시야 좌표는 대상의 현재 위치라 수평 거리로 비교한다.
        /// </summary>
        private bool IsLiveTargetPosition(Vector3 position)
        {
            if (_targetProvider == null)
            {
                return false;
            }

            IReadOnlyList<Transform> targets = _targetProvider.Targets;
            const float matchDistance = 0.75f;

            for (int i = 0; i < targets.Count; i++)
            {
                Transform target = targets[i];

                if (target == null)
                {
                    continue;
                }

                Vector3 delta = target.position - position;
                delta.y = 0f;

                if (delta.sqrMagnitude <= matchDistance * matchDistance)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>나를 뺀 다른 활성 적들의 위치를 버퍼에 채운다(이동 모터의 겹침 방지용, 할당 없음).</summary>
        private static void FillOtherAgentPositions(Transform self, List<Vector3> buffer)
        {
            for (int i = 0; i < ActiveAgents.Count; i++)
            {
                EnemyAgent other = ActiveAgents[i];

                if (other == null || other.transform == self || !other.isActiveAndEnabled)
                {
                    continue;
                }

                buffer.Add(other.transform.position);
            }
        }

        private void OnDisable()
        {
            ActiveAgents.Remove(this);
        }

#if UNITY_EDITOR
        /// <summary>
        /// 인스펙터에서 값을 고치는 즉시 <c>Nav Mesh Agent</c> 의 Speed·가속도·회전 속도를 따라오게 한다.
        /// 어느 쪽이 정본인지가 눈에 보여야 하기 때문이다(정본은 <see cref="Settings"/>).
        /// </summary>
        private void OnValidate()
        {
            ApplyMovementSettings();
        }
#endif

        /// <summary>
        /// 순찰 속도와 이동 프로파일(가속도·회전 속도·목적지 제동)을 이동 컴포넌트에 반영한다.
        /// <para>
        /// <c>OnValidate</c> 는 <c>Awake</c> 보다 먼저 돌 수 있어 캐시가 비어 있을 수 있다.
        /// 그때는 여기서 한 번 찾아 채우고, 그래도 없으면 조용히 넘어간다(예외를 내지 않는다).
        /// </para>
        /// <para>
        /// 여기서 넣는 값은 <b>시작값</b>이다. 실행 중에는 상태에 진입할 때마다
        /// <see cref="EnemyBrain"/> 이 <see cref="IEnemyMotor.SetMotionProfile"/> 로 다시 지정한다.
        /// 순찰이 기본 상태이므로 목적지 제동은 켜 둔다.
        /// </para>
        /// </summary>
        private void ApplyMovementSettings()
        {
            if (_navMeshAgent == null)
            {
                _navMeshAgent = GetComponent<NavMeshAgent>();
            }

            if (_navMeshAgent == null || _settings == null)
            {
                return;
            }

            _navMeshAgent.speed = _settings.Movement.PatrolSpeed;

            // 0 이하는 넣지 않는다 — 넣으면 개가 가속하지 못하거나 몸을 돌리지 못한다.
            if (_settings.Movement.Acceleration > 0f)
            {
                _navMeshAgent.acceleration = _settings.Movement.Acceleration;
            }

            if (_settings.Movement.TurnSpeedDegrees > 0f)
            {
                _navMeshAgent.angularSpeed = _settings.Movement.TurnSpeedDegrees;
            }

            _navMeshAgent.autoBraking = true;
        }

        private void Start()
        {
            if (_targetProvider == null)
            {
                _targetProvider = SceneTargetProvider.Instance;
                ConfigureSources();
            }

            ApplyPatrolRoute();
        }

        private void Update()
        {
            // 외부(네트워크 어댑터)가 시간을 대신 흘려보내는 중이면 여기서는 아무것도 하지 않는다.
            if (!SelfTick)
            {
                return;
            }

            Tick(Time.deltaTime);
        }

        /// <summary>
        /// 이 오브젝트에 붙어 있는 감지 소스를 전부 모은다.
        /// 새 감지 수단을 추가할 때 이 메서드는 바뀌지 않는다(OCP).
        /// </summary>
        private void CollectPerceptionSources()
        {
            GetComponents(_sourceBehaviours);

            _sources.Clear();
            for (int i = 0; i < _sourceBehaviours.Count; i++)
            {
                _sources.Add(_sourceBehaviours[i]);
            }

            ConfigureSources();
        }

        /// <summary>
        /// 대상 제공자에 의존하는 부품을 한자리에서 다시 주입한다.
        /// <para>
        /// 감지 소스와 추격 좌표 통로가 서로 다른 시점의 제공자를 들고 있으면
        /// 시야는 잡히는데 좌표는 못 얻는 상태가 되므로 반드시 함께 갱신한다.
        /// </para>
        /// </summary>
        private void ConfigureSources()
        {
            for (int i = 0; i < _sourceBehaviours.Count; i++)
            {
                _sourceBehaviours[i].Configure(_settings, _targetProvider);
            }

            _targetTracker.Configure(_targetProvider);
        }

        private void ApplyPatrolRoute()
        {
            if (_brain == null)
            {
                return;
            }

            if (_patrolRoute == null)
            {
                _brain.SetPatrolRoute(null, false);
                return;
            }

            _brain.SetPatrolRoute(_patrolRoute.GetWaypointPositions(), _patrolRoute.Loop);
        }
    }
}

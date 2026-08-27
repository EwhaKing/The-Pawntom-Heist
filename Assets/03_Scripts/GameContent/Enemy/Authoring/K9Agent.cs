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
    /// 하는 일은 셋뿐이다 — (1) 부품을 모아 <see cref="K9Brain"/> 에 주입하고,
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
    [RequireComponent(typeof(K9AlertChannelBehaviour))]
    [AddComponentMenu("Pawntom/Enemy/K9 Agent")]
    public sealed class K9Agent : MonoBehaviour
    {
        [Header("설정")]
        [Tooltip("이 개체만의 수치. 다른 K-9 과 다르게 줘도 된다")]
        [SerializeField] private K9Settings _settings = new K9Settings();

        [Header("순찰")]
        [Tooltip("이 개체가 걸어갈 경로. 개체마다 서로 다른 경로를 지정한다")]
        [SerializeField] private PatrolRoute _patrolRoute;

        [Header("감지 대상")]
        [Tooltip("비워 두면 씬에서 가장 먼저 활성화된 제공자를 쓴다")]
        [SerializeField] private SceneTargetProvider _targetProvider;

        private readonly List<K9PerceptionSourceBehaviour> _sourceBehaviours =
            new List<K9PerceptionSourceBehaviour>(4);

        private readonly List<IK9PerceptionSource> _sources =
            new List<IK9PerceptionSource>(4);

        private NavMeshAgent _navMeshAgent;
        private K9AlertChannelBehaviour _alertChannel;
        private K9Brain _brain;

        // 시야 밖 추격용 좌표 통로. 대상 제공자가 뒤늦게 해결될 수 있어 재주입을 받는다.
        private readonly NearestTargetTracker _targetTracker = new NearestTargetTracker();

        /// <summary>현재 상태. 디버그 표시용이다.</summary>
        public K9State State
        {
            get { return _brain == null ? K9State.Patrol : _brain.State; }
        }

        /// <summary>조립된 두뇌. 테스트 씬에서 들여다볼 때 쓴다.</summary>
        public K9Brain Brain
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
        public K9Settings Settings
        {
            get { return _settings; }
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
            _alertChannel = GetComponent<K9AlertChannelBehaviour>();

            if (_targetProvider == null)
            {
                _targetProvider = SceneTargetProvider.Instance;
            }

            CollectPerceptionSources();

            // 두뇌를 만들기 전에 걸어 둔다. 두뇌는 첫 Enter* 에서 상태에 맞는 값으로 다시 덮어쓴다.
            ApplyMovementSettings();

            NavMeshK9Motor motor = new NavMeshK9Motor(_navMeshAgent, _settings.Patrol.ArriveDistance);
            NavMeshWanderPointProvider wander =
                new NavMeshWanderPointProvider(_settings.Investigate.WanderSampleDistance);

            _brain = new K9Brain(_settings, motor, _sources, _alertChannel, _targetTracker, wander);
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
        /// <see cref="K9Brain"/> 이 <see cref="IK9Motor.SetMotionProfile"/> 로 다시 지정한다.
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
            // 두뇌는 직렬화되지 않는다. 플레이 중에 스크립트를 고쳐 리컴파일이 걸리면
            // 이 값이 null 이 되고 Awake 는 다시 불리지 않는다 — 그 뒤로 매 프레임 예외가 난다.
            if (_brain == null)
            {
                return;
            }

            _brain.Tick(Time.deltaTime);
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

using System.Collections.Generic;
using Pawntom.Enemy.Adapters;
using Pawntom.Enemy.Authoring;
using Pawntom.Enemy.Core;
using Pawntom.EnemyBridge;
using UnityEngine;
using UnityEngine.AI;

namespace Pawntom.TutorialBridge
{
    /// <summary>
    /// 캣닢 경보가 울리면 순찰견(적)들을 금고방으로 모으고, 도착한 뒤 근처를 수색하게 한 다음 맵을 자유롭게 돌아다니게 한다.
    /// <para>
    /// 적 두뇌(<see cref="EnemyBrain"/>)는 고치지 않는다. 두뇌가 이미 가진 "소집 좌표를 받으면 그곳으로 조사하러 간다 /
    /// 조사 중에는 기준점 주변을 배회한다"는 동작을 하울링 채널(<see cref="EnemyAlertChannelBehaviour.Receive"/>)로
    /// 부려 먹는다. 시야에 플레이어가 잡혀 Alert/Chase 가 되면 그 판단이 늘 우선이고, 끝나면 이어서 따른다.
    /// </para>
    /// <list type="number">
    /// <item>모이는 중: 도착할 때까지 몇 초마다 금고방 좌표를 다시 알려 준다(조사 포기 타이머도 함께 되돌려진다).</item>
    /// <item>수색: 도착하면 <see cref="searchSeconds"/> 동안 금고방 주변 지점을 짧은 간격으로 오가며 훑는다.</item>
    /// <item>자유 순찰: 수색이 끝나면 맵 곳곳의 무작위 지점을 차례로 알려 줘 이리저리 돌아다니게 한다.</item>
    /// </list>
    /// 이 컴포넌트가 꺼지거나 <see cref="StopRecall"/> 이 불리면 두뇌가 스스로 조사를 포기하고 원래 순찰로 돌아간다.
    /// 금고 <c>onOpened</c> 에 <see cref="BeginRecall"/> 을 연결해 쓴다. 튜토리얼은 혼자 Host 로 진행하므로 상태 권한이 있는 적만 건드린다.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Pawntom/Tutorial/Tutorial Enemy Recall")]
    public sealed class TutorialEnemyRecall : MonoBehaviour
    {
        private enum Phase
        {
            Gather,
            Search,
            Roam
        }

        private sealed class Plan
        {
            public Phase Phase;
            public float NextSendTime;
            public float PhaseEndTime;
            public NavMeshAgent Nav;
        }

        [Header("집결")]
        [Tooltip("금고방 한가운데. 부서질 금고와 별개의 빈 오브젝트를 둔다")]
        [SerializeField] private Transform gatherPoint;
        [Tooltip("금고방 바닥(Floor_Vault). 이 바닥 위에 들어서야 '방 안에 도착'으로 본다. 복도는 해당하지 않는다")]
        [SerializeField] private Renderer roomFloor;
        [Tooltip("격벽이 닫혀 길이 끊겼을 때, 이 거리 안(수평)까지 와서 멈추면 '문 앞에 도착'으로 본다")]
        [SerializeField, Min(0.5f)] private float doorArriveRadius = 9f;
        [Tooltip("도착 전까지 집결 좌표를 다시 알려 주는 간격(초)")]
        [SerializeField, Min(0.5f)] private float resendSeconds = 3f;

        [Tooltip("경보 중(모이기·수색·자유 순찰) 적 개체의 조사 속도에 곱하는 배율. 추격 속도를 넘지 않게 자동으로 제한한다")]
        [SerializeField, Range(1f, 3f)] private float gatherSpeedMultiplier = 1.8f;

        [Tooltip("모이는 동안 한 지점에 도착한 뒤 주변을 서성이는 반경(m). 적 개체 기본값(배회 반경)은 넓어서 줄여 쓴다")]
        [SerializeField, Min(0f)] private float gatherWanderRadius = 2.5f;

        [Tooltip("알려 준 지점 주변으로 적끼리 흩어지는 반경(m). 적 개체 기본값(소집 흩어짐)은 매우 커서 지정 지점을 벗어나므로 줄여 쓴다")]
        [SerializeField, Min(0f)] private float spreadAroundPoint = 2.5f;

        [Header("수색")]
        [SerializeField, Min(0f)] private float searchSeconds = 5f;
        [Tooltip("수색·자유 순찰 중 한 지점 주변을 서성이는 반경(m)")]
        [SerializeField, Min(0f)] private float searchWanderRadius = 3f;
        [Tooltip("수색 중 금고방 주변의 새 지점을 다시 고르는 간격(초)")]
        [SerializeField, Min(0.5f)] private float searchRepickSeconds = 1.5f;

        [Header("자유 순찰")]
        [Tooltip("다음 무작위 지점을 알려 주는 간격(초)의 최소/최대")]
        [SerializeField, Min(1f)] private float roamIntervalMin = 7f;
        [SerializeField, Min(1f)] private float roamIntervalMax = 12f;
        [Tooltip("자유 순찰을 허용할 건물 안 구역. 이 아래의 'Floor' 로 시작하는 바닥만 돌아다닌다(맵 바깥 지면은 제외). 비우면 내비메시 전체")]
        [SerializeField] private Transform roamAreaRoot;
        [Tooltip("이보다 가까운 지점은 고르지 않는다(m)")]
        [SerializeField, Min(0f)] private float roamMinDistance = 6f;

        [Header("검사 주기")]
        [SerializeField, Min(0.1f)] private float tickSeconds = 0.4f;

        private readonly Dictionary<EnemyAgent, Plan> _plans = new Dictionary<EnemyAgent, Plan>(8);
        private readonly List<EnemyAgent> _gone = new List<EnemyAgent>(4);
        private bool _active;
        private float _nextTick;
        private Bounds _navBounds;
        private bool _hasNavBounds;
        private Bounds _roomBounds;
        private bool _hasRoomBounds;
        // 필드 초기화에서 만들면 씬 로드(직렬화) 중 예외가 난다. 처음 쓸 때 만든다.
        private NavMeshPath _path;
        private readonly List<Bounds> _roamFloors = new List<Bounds>(8);

        public bool IsActive
        {
            get { return _active; }
        }

        /// <summary>금고 onOpened 에 연결한다. 이미 시작했으면 무시한다.</summary>
        public void BeginRecall()
        {
            if (_active)
            {
                return;
            }

            if (gatherPoint == null)
            {
                Debug.LogWarning("[TutorialEnemyRecall] 집결 지점(Gather Point)이 비어 있어 적을 모을 수 없습니다.", this);
                return;
            }

            _active = true;
            _plans.Clear();
            _nextTick = 0f;
            CacheNavBounds();

            _hasRoomBounds = roomFloor != null;
            if (_hasRoomBounds)
            {
                _roomBounds = roomFloor.bounds;
            }
        }

        /// <summary>소집을 멈춘다. 적은 남은 조사 시간이 지나면 원래 순찰로 돌아간다.</summary>
        public void StopRecall()
        {
            _active = false;
            ReleaseAll();
        }

        private void OnEnable()
        {
            TutorialDirector.LocalPlayerDied += HandlePlayerDied;
            TutorialDirector.StageResumed += HandleStageResumed;
        }

        private void OnDisable()
        {
            TutorialDirector.LocalPlayerDied -= HandlePlayerDied;
            TutorialDirector.StageResumed -= HandleStageResumed;
            ReleaseAll();
        }

        // 사망 재시도로 단계가 다시 시작되면 소집을 풀고, 모든 적을 순찰 시작 지점으로 되돌린다.
        // 단계 체크포인트(TutorialStageCheckpoint)가 적을 체크포인트 때의 자리로 옮겨 둔 뒤라, 그 위에서 처음 자리로 다시 보낸다.
        // 같은 프레임에 다른 곳이 적을 다시 움직여도 처음 자리가 이기도록 한 프레임 뒤에 한 번 더 확인한다.
        private void HandleStageResumed()
        {
            StopRecall();
            ResetAllEnemies();

            if (isActiveAndEnabled)
            {
                StartCoroutine(ResetEnemiesNextFrame());
            }
        }

        private System.Collections.IEnumerator ResetEnemiesNextFrame()
        {
            yield return null;
            ResetAllEnemies();
        }

        private void ResetAllEnemies()
        {
            int reset = 0;
            IReadOnlyList<EnemyAgent> agents = EnemyAgent.Active;

            for (int i = 0; i < agents.Count; i++)
            {
                EnemyAgent agent = agents[i];
                if (agent != null && agent.isActiveAndEnabled && HasAuthority(agent))
                {
                    agent.ResetToPatrolStart();
                    reset++;
                }
            }

            Debug.Log("[TutorialEnemyRecall] 단계 재시도: 적 " + reset + "/" + agents.Count + "마리를 순찰 시작 지점으로 되돌렸습니다.");
        }

        // 플레이어가 죽으면 소집을 풀어 적이 평소 행동으로 돌아가게 한다.
        private void HandlePlayerDied()
        {
            StopRecall();
        }

        // 두뇌에 걸어 둔 조사 덮어쓰기를 모두 풀어 설정값으로 되돌린다.
        private void ReleaseAll()
        {
            foreach (KeyValuePair<EnemyAgent, Plan> pair in _plans)
            {
                if (pair.Key != null && pair.Key.Brain != null)
                {
                    pair.Key.Brain.ClearInvestigateOverride();
                }
            }

            _plans.Clear();
        }

        // 단계가 바뀔 때마다 두뇌의 조사 속도·배회 반경을 그 단계에 맞춘다.
        private void ApplyPhase(EnemyAgent agent, Plan plan, Phase phase)
        {
            plan.Phase = phase;
            if (agent.Brain == null)
            {
                return;
            }

            // 경보가 울린 뒤에는 수색·자유 순찰도 같은 빠른 속도를 유지한다. 배회 반경만 단계마다 다르다.
            float radius = phase == Phase.Gather ? gatherWanderRadius : searchWanderRadius;
            float spreadRadius = spreadAroundPoint;
            agent.Brain.SetInvestigateOverride(gatherSpeedMultiplier, radius, spreadRadius, true);
        }

        private void Update()
        {
            if (!_active || Time.time < _nextTick)
            {
                return;
            }

            _nextTick = Time.time + tickSeconds;

            IReadOnlyList<EnemyAgent> agents = EnemyAgent.Active;
            for (int i = 0; i < agents.Count; i++)
            {
                EnemyAgent agent = agents[i];
                if (agent == null || !agent.isActiveAndEnabled || !HasAuthority(agent))
                {
                    continue;
                }

                Plan plan;
                if (!_plans.TryGetValue(agent, out plan))
                {
                    plan = new Plan { NextSendTime = 0f, Nav = agent.GetComponent<NavMeshAgent>() };
                    _plans[agent] = plan;
                    ApplyPhase(agent, plan, Phase.Gather);
                }

                Step(agent, plan);
            }

            // 사라진 적(사망·파괴) 정리
            _gone.Clear();
            foreach (KeyValuePair<EnemyAgent, Plan> pair in _plans)
            {
                if (pair.Key == null)
                {
                    _gone.Add(pair.Key);
                }
            }

            for (int i = 0; i < _gone.Count; i++)
            {
                _plans.Remove(_gone[i]);
            }
        }

        private void Step(EnemyAgent agent, Plan plan)
        {
            EnemyState state = agent.State;
            bool engaged = state == EnemyState.Alert || state == EnemyState.Chase;
            float now = Time.time;

            switch (plan.Phase)
            {
                case Phase.Gather:
                    if (!engaged && HasArrived(agent, plan))
                    {
                        ApplyPhase(agent, plan, Phase.Search);
                        plan.PhaseEndTime = now + searchSeconds;
                        plan.NextSendTime = now + searchRepickSeconds;
                        break;
                    }

                    if (!engaged && now >= plan.NextSendTime)
                    {
                        Send(agent, gatherPoint.position);
                        plan.NextSendTime = now + resendSeconds;
                    }

                    break;

                case Phase.Search:
                    // 플레이어를 발견해 경계·추격에 들어갔다 풀렸으면 처음부터 다시 모인다.
                    if (engaged)
                    {
                        ApplyPhase(agent, plan, Phase.Gather);
                        plan.NextSendTime = 0f;
                        break;
                    }

                    if (now >= plan.PhaseEndTime)
                    {
                        ApplyPhase(agent, plan, Phase.Roam);
                        plan.NextSendTime = 0f;
                        break;
                    }

                    // 적 두뇌의 배회 반경이 넓어도 금고방 근처를 수색하도록, 몇 초마다 금고방 주변 지점을 다시 알려 준다.
                    if (now >= plan.NextSendTime)
                    {
                        Send(agent, gatherPoint.position);
                        plan.NextSendTime = now + searchRepickSeconds;
                    }

                    break;

                case Phase.Roam:
                    if (!engaged && now >= plan.NextSendTime)
                    {
                        Vector3 point;
                        if (TryPickRoamPoint(agent.transform.position, out point))
                        {
                            Send(agent, point);
                        }

                        plan.NextSendTime = now + Random.Range(roamIntervalMin, roamIntervalMax);
                    }

                    break;
            }
        }

        // 금고방 안에 들어섰거나, 격벽이 닫혀 길이 끊긴 채 문 앞 끝까지 와서 멈췄으면 도착이다.
        private bool HasArrived(EnemyAgent agent, Plan plan)
        {
            Vector3 position = agent.transform.position;

            if (_hasRoomBounds)
            {
                if (position.x >= _roomBounds.min.x && position.x <= _roomBounds.max.x &&
                    position.z >= _roomBounds.min.z && position.z <= _roomBounds.max.z)
                {
                    return true;
                }
            }
            else if (HorizontalDistance(position, gatherPoint.position) <= doorArriveRadius * 0.5f)
            {
                return true;
            }

            // 문이 닫혀 있으면 경로가 끝까지 이어지지 않는다(PartialPath). 가능한 데까지 가서 멈췄을 때를 문 앞 도착으로 본다.
            NavMeshAgent nav = plan.Nav;
            if (nav != null && nav.isOnNavMesh && !nav.pathPending && nav.pathStatus == NavMeshPathStatus.PathPartial &&
                nav.remainingDistance <= nav.stoppingDistance + 0.8f &&
                HorizontalDistance(position, gatherPoint.position) <= doorArriveRadius)
            {
                return true;
            }

            return false;
        }

        private static void Send(EnemyAgent agent, Vector3 point)
        {
            EnemyAlertChannelBehaviour channel = agent.GetComponent<EnemyAlertChannelBehaviour>();
            if (channel != null)
            {
                channel.Receive(point);
            }
        }

        private static bool HasAuthority(EnemyAgent agent)
        {
            NetworkEnemyAgent network = agent.GetComponent<NetworkEnemyAgent>();
            return network == null || !network.Object || network.HasStateAuthority;
        }

        private static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        private void CacheNavBounds()
        {
            NavMeshTriangulation tri = NavMesh.CalculateTriangulation();
            if (tri.vertices == null || tri.vertices.Length == 0)
            {
                _hasNavBounds = false;
                return;
            }

            _navBounds = new Bounds(tri.vertices[0], Vector3.zero);
            for (int i = 1; i < tri.vertices.Length; i++)
            {
                _navBounds.Encapsulate(tri.vertices[i]);
            }

            _hasNavBounds = true;

            _roamFloors.Clear();
            if (roamAreaRoot != null)
            {
                foreach (MeshRenderer renderer in roamAreaRoot.GetComponentsInChildren<MeshRenderer>(false))
                {
                    if (renderer.name.StartsWith("Floor"))
                    {
                        _roamFloors.Add(renderer.bounds);
                    }
                }
            }
        }

        // 걸을 수 있는 무작위 지점. 건물 안 바닥 위에서만 고르고, 출입 금지 구역(본부)과 지금 위치 코앞은 고르지 않는다.
        private bool TryPickRoamPoint(Vector3 from, out Vector3 point)
        {
            point = from;
            if (!_hasNavBounds)
            {
                return false;
            }

            if (_path == null)
            {
                _path = new NavMeshPath();
            }

            for (int attempt = 0; attempt < 10; attempt++)
            {
                Bounds area = _navBounds;
                if (_roamFloors.Count > 0)
                {
                    area = PickFloorByArea();
                }

                Vector3 random = new Vector3(
                    Random.Range(area.min.x, area.max.x),
                    area.center.y,
                    Random.Range(area.min.z, area.max.z));

                NavMeshHit hit;
                if (!NavMesh.SamplePosition(random, out hit, 2f, NavMesh.AllAreas))
                {
                    continue;
                }

                if (_roamFloors.Count > 0 && !InsideAnyFloor(hit.position))
                {
                    continue;
                }

                if (HorizontalDistance(hit.position, from) < roamMinDistance ||
                    EnemySafeZone.Registry.Contains(hit.position))
                {
                    continue;
                }

                // 닫힌 격벽 너머처럼 실제로 갈 수 없는 지점은 고르지 않는다(가다 말고 벽 앞에서 서성이는 것을 막는다).
                if (!NavMesh.CalculatePath(from, hit.position, NavMesh.AllAreas, _path) ||
                    _path.status != NavMeshPathStatus.PathComplete)
                {
                    continue;
                }

                point = hit.position;
                return true;
            }

            return false;
        }

        // 넓은 바닥일수록 더 자주 고른다.
        private Bounds PickFloorByArea()
        {
            float total = 0f;
            for (int i = 0; i < _roamFloors.Count; i++)
            {
                total += _roamFloors[i].size.x * _roamFloors[i].size.z;
            }

            float pick = Random.value * total;
            for (int i = 0; i < _roamFloors.Count; i++)
            {
                pick -= _roamFloors[i].size.x * _roamFloors[i].size.z;
                if (pick <= 0f)
                {
                    return _roamFloors[i];
                }
            }

            return _roamFloors[_roamFloors.Count - 1];
        }

        private bool InsideAnyFloor(Vector3 position)
        {
            for (int i = 0; i < _roamFloors.Count; i++)
            {
                Bounds b = _roamFloors[i];
                if (position.x >= b.min.x && position.x <= b.max.x && position.z >= b.min.z && position.z <= b.max.z)
                {
                    return true;
                }
            }

            return false;
        }

        private void OnDrawGizmosSelected()
        {
            if (gatherPoint == null)
            {
                return;
            }

            Gizmos.color = new Color(1f, 0.25f, 0.2f, 0.8f);
            Gizmos.DrawWireSphere(gatherPoint.position, doorArriveRadius);
        }
    }
}

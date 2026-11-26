using UnityEngine;
using Fusion;
using System.Collections.Generic;
using Unity.VisualScripting;
using Pawntom.Enemy.Authoring;
using Pawntom.EnemyBridge;
using Pawntom.Stage;

/// <summary>
/// SpawnManager
///
/// 담당:
/// - Network Object 생성 관리
/// - Player Spawn / Despawn (스폰만 관리. 각 Player의 상태나 행동은 여기 담당x)
/// - Enemy Spawn
///   - 레벨 데이터 씬(Map_Scene): 호스트 시드 계획(StageNetworkState)의 순찰 경로마다 K-9 1마리
///   - 레벨 데이터가 없는 씬(Tutorial_Scene 등): 씬에 놓인 EnemySpawnPoint 마커 기준(기존 경로)
/// - 캣닢 경보 시 브루투스 1회 스폰(CatnipAlarm.BrutusSpawnRequested)
/// - Spawn된 객체 추적
///
/// 순서(호스트): 레벨 데이터 씬에서는 계획이 준비되고 본부 도킹이 적용된 뒤에만 플레이어를 만든다.
/// 계획이 아직이면 요청을 미뤘다가 StageNetworkState.PlanReady 에서 플레이어 → 적 순서로 처리한다(StageSpawnGate).
/// </summary>
public class SpawnManager : MonoBehaviour
{
    #region SceneSingleton
    // ==================================================
    // Scene Local Singleton
    // ==================================================

    public static SpawnManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("[SpawnManager] SpawnManager가 씬에 두 개 이상 존재합니다.");
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // 중요:
        // DontDestroyOnLoad를 호출하지 않음.
        // SpawnManager는 현재 Map_Scene에만 존재함.
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
    #endregion

    [Header("Player Spawn")]
    [SerializeField] private NetworkPrefabRef playerPrefab; //인스펙터 창에서 플레이어 프리팹 연결
    [Tooltip("레벨 데이터가 없는 씬의 플레이어 스폰 위치. 레벨 데이터 씬은 본부(Mainbase/PlayerSpawns) 지점을 쓰고, 그것이 없을 때만 이 목록을 쓴다")]
    [SerializeField] private List<Transform> _playerSpawnPoints = new List<Transform>(); // 씬에 배치된 플레이어 스폰 위치들을 Inspector에서 순서대로 등록

    [Header("Stage (레벨 데이터 씬)")]
    [Tooltip("계획된 순찰 경로마다 스폰할 K-9 프리팹. 비우면 씬 EnemySpawnPoint(비활성 포함)의 프리팹을 쓴다")]
    [SerializeField] private NetworkPrefabRef _k9Prefab;

    [Tooltip("캣닢 경보 때 1회 스폰할 브루투스 프리팹(Brutus_Prefab_Net)")]
    [SerializeField] private NetworkPrefabRef _brutusPrefab;

    // PlayerRef와 생성된 NetworkObject 연결
    private Dictionary<PlayerRef, NetworkObject> spawnedPlayers = new Dictionary<PlayerRef, NetworkObject>();

    // 사용 중인 플레이어 스폰 지점(입장 순서가 아니라 점유 추적 — 나간 사람 자리를 다음 사람이 받는다).
    private readonly SpawnSlotAllocator _playerSlots = new SpawnSlotAllocator();

    // 계획 준비 전에 들어온 스폰 요청(호스트). 준비되면 플레이어 → 적 순서로 한 번씩.
    private readonly StagePendingSpawns _pending = new StagePendingSpawns();

    // 스폰 마커·계획으로 생성된 적들. 중복 스폰 방지와 추적에 쓴다.
    private readonly List<NetworkObject> _spawnedEnemies = new List<NetworkObject>();

    // 브루투스는 판당 1회.
    private readonly StageOnceFlag _brutusClaim = new StageOnceFlag();
    private NetworkObject _brutus;

    private bool _warnedMissingState;

    /// <summary>캣닢 경보로 스폰된 브루투스(호스트). 아직 없으면 null.</summary>
    public NetworkObject Brutus
    {
        get { return _brutus; }
    }

    private void OnEnable()
    {
        StageNetworkState.PlanReady += HandlePlanReady;
        CatnipAlarm.BrutusSpawnRequested += HandleBrutusSpawnRequested;
    }

    private void OnDisable()
    {
        StageNetworkState.PlanReady -= HandlePlanReady;
        CatnipAlarm.BrutusSpawnRequested -= HandleBrutusSpawnRequested;
        _pending.Clear();
    }

    public void SpawnAllPlayers(NetworkRunner runner)
    {
        foreach (PlayerRef player in runner.ActivePlayers)
        {
            SpawnPlayer(runner, player);
        }
    }

    /// <summary>
    /// 플레이어 생성
    /// 맵 로드 완료(OnSceneLoadDone) 또는 재스폰에서 호출
    /// </summary>
    public void SpawnPlayer(NetworkRunner runner, PlayerRef player)
    {
        // Spawn 권한은 Server/Host만 보유
        if (!runner.IsServer)
            return;

        // 같은 요청이 두 번 와도(씬 로드 콜백 중복·계획 대기 후 재처리) 아바타는 하나다.
        if (spawnedPlayers.TryGetValue(player, out NetworkObject existing) && existing != null)
        {
            Debug.LogWarning($"[SpawnManager] 이미 스폰된 플레이어라 건너뜁니다. Player={player}");
            return;
        }

        StageSpawnMode mode = StageNetworkState.ResolveSpawnMode(out StageNetworkState stage);

        if (mode == StageSpawnMode.Wait)
        {
            // 본부가 선택된 도킹으로 옮겨지기 전이다. 계획이 준비되면 전원을 다시 스폰한다.
            _pending.RequestPlayers();
            Debug.Log($"[SpawnManager] 스테이지 계획 대기 — 플레이어 스폰을 미룹니다. Player={player}");
            return;
        }

        WarnIfMissingState(mode);

        IReadOnlyList<Transform> points = ResolvePlayerSpawnPoints(mode, stage);
        int spawnIndex = _playerSlots.Acquire(player.PlayerId, points.Count);

        if (spawnIndex < 0)
        {
            Debug.LogError(
                $"[SpawnManager] Spawn Point 부족. " +
                $"Player={player}, InUse={_playerSlots.UsedCount}, Count={points.Count}"
            );
            return;
        }

        Transform spawnPoint = points[spawnIndex];
        if (spawnPoint == null)
        {
            _playerSlots.Release(player.PlayerId);
            Debug.LogError($"[SpawnManager] Spawn Point {spawnIndex}가 비어 있습니다.");
            return;
        }
        CatType selectedCatType = CatType.BlackCat;

        if (NetworkManager.Instance == null ||
            !NetworkManager.Instance.TryGetSelectedCatType(player, out selectedCatType))
        {
            Debug.LogWarning(
                $"[SpawnManager] 저장된 고양이 선택 정보를 찾지 못해 기본 품종을 사용합니다. " +
                $"Player={player}, Default={selectedCatType}"
            );
        }

        NetworkObject playerObject = runner.Spawn(
            playerPrefab,
            spawnPoint.position,
            spawnPoint.rotation,
            player,
            onBeforeSpawned: (networkRunner, networkObject) =>
            {
                PlayerData playerData = networkObject.GetComponent<PlayerData>();

                if (playerData == null)
                {
                    Debug.LogError("[SpawnManager] 플레이어 프리팹에 PlayerData가 없습니다.");
                    return;
                }

                playerData.SelectedCatType = selectedCatType;
            }
        );
        spawnedPlayers[player] = playerObject;

        Debug.Log(
            $"[SpawnManager] Player Spawn : {player}, " +
            $"SelectedCat={selectedCatType}, Point={spawnPoint.name}(#{spawnIndex})"
        );
    }

    /// <summary>
    /// 죽은 플레이어를 지우고 새로 생성합니다(튜토리얼 사망 후 재개용).
    /// 위치를 주면 그곳으로, 없으면 기본 스폰 지점에서 시작합니다.
    /// 새 오브젝트라 체력·스태미나·가방이 초기 상태가 됩니다.
    /// </summary>
    public void RespawnPlayer(NetworkRunner runner, PlayerRef player, Vector3? position = null, Quaternion? rotation = null)
    {
        if (!runner.IsServer)
            return;

        // 스폰 지점은 놓지 않는다 — 같은 플레이어는 같은 지점에서 다시 태어난다.
        if (spawnedPlayers.TryGetValue(player, out NetworkObject oldObject))
        {
            runner.Despawn(oldObject);
            spawnedPlayers.Remove(player);
        }

        SpawnPlayer(runner, player);

        if (position == null ||
            !spawnedPlayers.TryGetValue(player, out NetworkObject newObject) ||
            newObject == null)
            return;

        // 기본 스폰 지점에서 태어난 직후 지정 위치로 옮긴다. 컨트롤러가 있으면 텔레포트로 옮겨야 보간이 튀지 않는다.
        NetworkCharacterController controller = newObject.GetComponent<NetworkCharacterController>();
        Quaternion facing = rotation ?? newObject.transform.rotation;

        if (controller != null)
        {
            controller.Teleport(position.Value, facing);
        }
        else
        {
            newObject.transform.SetPositionAndRotation(position.Value, facing);
        }
    }

    /// <summary>
    /// 플레이어 제거
    /// Fusion OnPlayerLeft에서 호출
    /// </summary>
    public void DespawnPlayer(NetworkRunner runner, PlayerRef player)
    {
        // DeSpawn 권한은 Server/Host만 보유
        if (!runner.IsServer)
            return;

        if (spawnedPlayers.TryGetValue(player, out NetworkObject playerObject))
        {
            runner.Despawn(playerObject);
            spawnedPlayers.Remove(player);
        }

        // 나간 사람의 지점을 비워 다음 입장자가 겹치지 않게 받는다.
        _playerSlots.Release(player.PlayerId);

        Debug.Log($"[SpawnManager] Player Despawn : {player}");
    }

    private IReadOnlyList<Transform> ResolvePlayerSpawnPoints(StageSpawnMode mode, StageNetworkState stage)
    {
        if (mode == StageSpawnMode.UsePlan && stage != null && stage.PlayerSpawnPoints.Count > 0)
        {
            return stage.PlayerSpawnPoints;
        }

        return _playerSpawnPoints;
    }

    private void WarnIfMissingState(StageSpawnMode mode)
    {
        if (mode != StageSpawnMode.LegacyMissingState || _warnedMissingState)
        {
            return;
        }

        _warnedMissingState = true;
        Debug.LogError(
            "[SpawnManager] 레벨 데이터는 있는데 StageNetworkState(NetworkObject)가 씬에 없습니다. " +
            "시드 배치 없이 기존 스폰 경로를 씁니다 — 피어마다 본부 위치가 맞지 않을 수 있습니다."
        );
    }

    // 계획이 준비됐다(이 피어에서 본부 도킹 적용 끝). 호스트만 미뤄 둔 스폰을 처리한다.
    private void HandlePlanReady(StageNetworkState stage)
    {
        if (Instance != this || stage == null)
        {
            return;
        }

        NetworkRunner runner = stage.Runner;

        if (runner == null || !runner.IsServer || !_pending.HasAny)
        {
            return;
        }

        for (StagePendingStep step = _pending.Next(); step != StagePendingStep.None; step = _pending.Next())
        {
            if (step == StagePendingStep.Players)
            {
                SpawnAllPlayers(runner);
            }
            else if (step == StagePendingStep.Enemies)
            {
                SpawnAllEnemies(runner);
            }
        }
    }

    // ==================================================
    // Enemy Spawn
    // ==================================================

    /// <summary>
    /// 적을 생성한다. 레벨 데이터 씬은 계획된 순찰 경로마다 K-9 1마리, 아니면 씬의 EnemySpawnPoint 마커마다 1마리.
    /// 맵 로드 직후 호스트에서 1회만 호출한다(플레이어 다음).
    /// </summary>
    public void SpawnAllEnemies(NetworkRunner runner)
    {
        // Spawn 권한은 Server/Host만 보유
        if (!runner.IsServer)
            return;

        // OnSceneLoadDone이 두 번 와도 적이 두 배가 되면 안 된다.
        if (_spawnedEnemies.Count > 0)
        {
            Debug.LogWarning(
                $"[SpawnManager] 적이 이미 스폰되어 있어 건너뜁니다. Count={_spawnedEnemies.Count}"
            );
            return;
        }

        StageSpawnMode mode = StageNetworkState.ResolveSpawnMode(out StageNetworkState stage);

        switch (mode)
        {
            case StageSpawnMode.Wait:
                _pending.RequestEnemies();
                Debug.Log("[SpawnManager] 스테이지 계획 대기 — 적 스폰을 미룹니다.");
                return;

            case StageSpawnMode.UsePlan:
                SpawnK9FromPlan(runner, stage);
                return;

            default:
                WarnIfMissingState(mode);
                SpawnEnemiesFromMarkers(runner);
                return;
        }
    }

    // 레벨 데이터 씬: 계획의 순찰 경로(K-9 번호 순)마다 경로 첫 웨이포인트에서 1마리. 경로는 씬 PatrolRoute 를 이름(= 레벨 데이터 경로 Id)으로 찾는다.
    private void SpawnK9FromPlan(NetworkRunner runner, StageNetworkState stage)
    {
        if (stage.Layout.PatrolRoutes.Count == 0)
        {
            // 레벨 데이터가 아직 구워지지 않았다(순찰 경로 0). 맵이 텅 비지 않게 씬 마커 경로로 대신한다.
            Debug.LogWarning("[SpawnManager] 레벨 데이터에 순찰 경로가 없어 씬 EnemySpawnPoint 경로로 K-9 을 스폰합니다. 레벨 데이터를 구우세요.");
            SpawnEnemiesFromMarkers(runner);
            return;
        }

        NetworkPrefabRef prefab = ResolveK9Prefab();

        if (!prefab.IsValid)
        {
            Debug.LogError("[SpawnManager] K-9 프리팹이 없습니다. SpawnManager 의 K9 Prefab 을 지정하세요.");
            return;
        }

        var routeIds = new List<string>(stage.Plan.PatrolRouteIndices.Count);
        StageSpawnList.BuildPatrolRouteIds(stage.Layout, stage.Plan, routeIds);

        Dictionary<string, PatrolRoute> sceneRoutes = CollectSceneRoutes();

        for (int i = 0; i < routeIds.Count; i++)
        {
            string routeId = routeIds[i];

            if (!sceneRoutes.TryGetValue(routeId, out PatrolRoute route) || route == null)
            {
                Debug.LogError($"[SpawnManager] 레벨 데이터의 순찰 경로 '{routeId}' 가 씬에 없습니다(PatrolRoute 이름 = 경로 Id). 레벨 데이터를 다시 구우세요.");
                continue;
            }

            IReadOnlyList<Vector3> waypoints = route.GetWaypointPositions();

            if (waypoints == null || waypoints.Count == 0)
            {
                Debug.LogError($"[SpawnManager] 순찰 경로 '{routeId}' 에 웨이포인트가 없어 K-9 을 스폰하지 않습니다.");
                continue;
            }

            Vector3 position = waypoints[0];
            Quaternion rotation = Quaternion.identity;

            if (waypoints.Count > 1)
            {
                Vector3 toNext = waypoints[1] - waypoints[0];
                toNext.y = 0f;

                if (toNext.sqrMagnitude > 0.0001f)
                {
                    rotation = Quaternion.LookRotation(toNext.normalized, Vector3.up);
                }
            }

            NetworkObject enemyObject = SpawnEnemy(runner, prefab, position, rotation, route, "K9#" + (i + 1) + "/" + routeId);

            if (enemyObject != null)
            {
                _spawnedEnemies.Add(enemyObject);
            }
        }

        Debug.Log(
            $"[SpawnManager] Enemy Spawn (plan seed {stage.Plan.Seed}) : {_spawnedEnemies.Count} / Routes={routeIds.Count}"
        );
    }

    // 레벨 데이터가 없는 씬: 씬에 놓인 스폰 마커(EnemySpawnPoint)마다 적을 한 마리씩.
    private void SpawnEnemiesFromMarkers(NetworkRunner runner)
    {
        // 인스펙터 목록을 쓰지 않는다. 마커를 씬에 놓기만 하면 동작해야 한다.
        // 맵 로드 시 1회만 도는 경로이므로 탐색 비용은 문제되지 않는다 —
        // 다만 Update 계열에서는 절대 부르지 않는다.
        EnemySpawnPoint[] points = FindObjectsByType<EnemySpawnPoint>(FindObjectsSortMode.None);

        if (points.Length == 0)
        {
            // 적이 아직 배치되지 않은 씬에서 게임이 시작되는 것은 정상 상황이다.
            Debug.LogWarning("[SpawnManager] 씬에 EnemySpawnPoint가 없어 적을 스폰하지 않습니다.");
            return;
        }

        foreach (EnemySpawnPoint point in points)
        {
            if (point == null)
            {
                continue;
            }

            NetworkPrefabRef enemyPrefab = point.EnemyPrefab;

            if (!enemyPrefab.IsValid)
            {
                Debug.LogError(
                    $"[SpawnManager] 스폰 마커에 적 프리팹이 지정되지 않았습니다. Marker={point.name}"
                );
                continue;
            }

            NetworkObject enemyObject = SpawnEnemy(
                runner,
                enemyPrefab,
                point.transform.position,
                point.transform.rotation,
                point.PatrolRoute,
                point.name);

            if (enemyObject != null)
            {
                _spawnedEnemies.Add(enemyObject);
            }
        }

        // 배치가 반영됐는지 확인하는 유일한 단서다.
        Debug.Log(
            $"[SpawnManager] Enemy Spawn : {_spawnedEnemies.Count} / Marker={points.Length}"
        );
    }

    private static NetworkObject SpawnEnemy(
        NetworkRunner runner,
        NetworkPrefabRef prefab,
        Vector3 position,
        Quaternion rotation,
        PatrolRoute route,
        string label)
    {
        // PatrolRoute는 씬 오브젝트 참조라 네트워크로 보낼 수 없다.
        // 호스트에서만 주입되고 클라이언트에는 넘어가지 않는다 —
        // 클라이언트는 두뇌를 돌리지 않으므로(NetworkEnemyAgent) 경로가 필요 없다.
        return runner.Spawn(
            prefab,
            position,
            rotation,
            null,
            onBeforeSpawned: (networkRunner, networkObject) =>
            {
                // GetComponent가 아니라 GetComponentInChildren이다.
                // NetworkObject는 프리팹 루트에 두고 두뇌·NavMeshAgent·감지 소스는
                // 모델이 있는 자식에 얹는 구성이 정상이다(K-9_Prefab_Net). 루트만 보면
                // 경로 주입이 조용히 건너뛰어져 적이 제자리에 선다.
                // 비활성 자식까지 훑는다 — 스폰 시점의 활성 상태에 기대지 않는다.
                EnemyAgent agent = networkObject.GetComponentInChildren<EnemyAgent>(true);

                if (agent == null)
                {
                    Debug.LogError(
                        $"[SpawnManager] 적 프리팹에 EnemyAgent가 없습니다. Spawn={label}"
                    );
                    return;
                }

                agent.SetPatrolRoute(route);
            }
        );
    }

    private NetworkPrefabRef ResolveK9Prefab()
    {
        if (_k9Prefab.IsValid)
        {
            return _k9Prefab;
        }

        // 기존 씬의 K-9 마커(비활성이어도)에서 프리팹만 빌려 온다.
        EnemySpawnPoint[] points = FindObjectsByType<EnemySpawnPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int i = 0; i < points.Length; i++)
        {
            if (points[i] != null && points[i].EnemyPrefab.IsValid)
            {
                return points[i].EnemyPrefab;
            }
        }

        return default;
    }

    // 굽기 도구 규칙: 레벨 데이터 경로 Id = 켜져 있는 씬 PatrolRoute 의 GameObject 이름.
    private static Dictionary<string, PatrolRoute> CollectSceneRoutes()
    {
        PatrolRoute[] routes = FindObjectsByType<PatrolRoute>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        var byId = new Dictionary<string, PatrolRoute>(routes.Length, System.StringComparer.Ordinal);

        for (int i = 0; i < routes.Length; i++)
        {
            PatrolRoute route = routes[i];

            if (route == null)
            {
                continue;
            }

            string id = route.gameObject.name;

            if (byId.ContainsKey(id))
            {
                Debug.LogWarning($"[SpawnManager] 같은 이름의 PatrolRoute 가 둘 이상입니다: '{id}'. 하나만 씁니다.");
                continue;
            }

            byId.Add(id, route);
        }

        return byId;
    }

    // ==================================================
    // Brutus (캣닢 경보)
    // ==================================================

    // CatnipAlarm 이 호스트에서만 1회 발행한다. 그래도 여기서 권한·중복을 다시 검사한다.
    private void HandleBrutusSpawnRequested(CatnipAlarm alarm)
    {
        if (Instance != this || alarm == null)
        {
            return;
        }

        NetworkRunner runner = alarm.Runner;

        if (runner == null || !runner.IsServer)
        {
            return;
        }

        if (!_brutusClaim.TryClaim())
        {
            Debug.LogWarning("[SpawnManager] 브루투스 스폰 요청이 다시 왔지만 판당 1회라 무시합니다.");
            return;
        }

        if (!_brutusPrefab.IsValid)
        {
            Debug.LogError("[SpawnManager] 브루투스 프리팹이 없습니다. SpawnManager 의 Brutus Prefab 을 지정하세요.");
            return;
        }

        if (!TryResolveBrutusPose(out Vector3 position, out Quaternion rotation, out string source))
        {
            Debug.LogError("[SpawnManager] 브루투스 스폰 지점이 없습니다(레벨 데이터 BrutusSpawns·씬 BrutusSpawnMarker 모두 없음).");
            return;
        }

        _brutus = runner.Spawn(_brutusPrefab, position, rotation, null);
        Debug.Log($"[SpawnManager] Brutus Spawn : {source} {position}");
    }

    private static bool TryResolveBrutusPose(out Vector3 position, out Quaternion rotation, out string source)
    {
        // 레벨 데이터 우선(Id 순 첫 항목).
        StageLayout layout = StageNetworkState.CurrentLayout;

        if (layout != null && layout.BrutusSpawns.Count > 0 && layout.BrutusSpawns[0] != null)
        {
            BrutusSpawn spawn = layout.BrutusSpawns[0];
            position = LevelLayoutData.ToUnity(spawn.Position);
            rotation = Quaternion.Euler(0f, spawn.Yaw, 0f);
            source = spawn.Id;
            return true;
        }

        BrutusSpawnMarker marker = FindFirstObjectByType<BrutusSpawnMarker>(FindObjectsInactive.Include);

        if (marker != null)
        {
            position = marker.transform.position;
            rotation = Quaternion.Euler(0f, LevelLayoutIds.Yaw(marker.transform), 0f);
            source = marker.Id;
            return true;
        }

        position = Vector3.zero;
        rotation = Quaternion.identity;
        source = null;
        return false;
    }
}

using UnityEngine;
using Fusion;
using System.Collections.Generic;
using Unity.VisualScripting;
using Pawntom.Enemy.Authoring;
using Pawntom.EnemyBridge;

/// <summary>
/// SpawnManager
///
/// 담당:
/// - Network Object 생성 관리
/// - Player Spawn / Despawn (스폰만 관리. 각 Player의 상태나 행동은 여기 담당x)
/// - Enemy Spawn (씬에 놓인 EnemySpawnPoint 마커 기준)
/// - Spawn된 객체 추적
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
    [SerializeField] private List<Transform> _playerSpawnPoints = new List<Transform>(); // 씬에 배치된 플레이어 스폰 위치들을 Inspector에서 순서대로 등록


    // PlayerRef와 생성된 NetworkObject 연결
    private Dictionary<PlayerRef, NetworkObject> spawnedPlayers = new Dictionary<PlayerRef, NetworkObject>();

    // 스폰 마커로 생성된 적들. 중복 스폰 방지와 추적에 쓴다.
    private readonly List<NetworkObject> _spawnedEnemies = new List<NetworkObject>();

    public void SpawnAllPlayers(NetworkRunner runner)
    {
        foreach (PlayerRef player in runner.ActivePlayers)
        {
            SpawnPlayer(runner, player);
        }
    }

    /// <summary>
    /// 플레이어 생성
    /// Fusion OnPlayerJoined에서 호출
    /// </summary>
    public void SpawnPlayer(NetworkRunner runner, PlayerRef player)
    {
        // Spawn 권한은 Server/Host만 보유
        if (!runner.IsServer)
            return;

        // 플레이어 스폰 위치를 PlayerRef 순서대로 할당(TODO : 기획에 따라 수정 가능)
        int spawnIndex = spawnedPlayers.Count;

        if (spawnIndex < 0 || spawnIndex >= _playerSpawnPoints.Count)
        {
            Debug.LogError(
                $"[SpawnManager] Spawn Point 부족. " +
                $"Player={player}, Index={spawnIndex}, Count={_playerSpawnPoints.Count}"
            );
            return;
        }

        Transform spawnPoint = _playerSpawnPoints[spawnIndex];
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
            $"SelectedCat={selectedCatType}"
        );
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

        Debug.Log($"[SpawnManager] Player Despawn : {player}");
    }

    // ==================================================
    // Enemy Spawn
    // ==================================================

    /// <summary>
    /// 씬에 놓인 스폰 마커(EnemySpawnPoint)마다 적을 한 마리씩 생성한다.
    /// 맵 로드 직후 호스트에서 1회만 호출한다.
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

            // PatrolRoute는 씬 오브젝트 참조라 네트워크로 보낼 수 없다.
            // 호스트에서만 주입되고 클라이언트에는 넘어가지 않는다 —
            // 클라이언트는 두뇌를 돌리지 않으므로(NetworkEnemyAgent) 경로가 필요 없다.
            PatrolRoute route = point.PatrolRoute;
            string markerName = point.name;

            NetworkObject enemyObject = runner.Spawn(
                enemyPrefab,
                point.transform.position,
                point.transform.rotation,
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
                            $"[SpawnManager] 적 프리팹에 EnemyAgent가 없습니다. Marker={markerName}"
                        );
                        return;
                    }

                    agent.SetPatrolRoute(route);
                }
            );

            _spawnedEnemies.Add(enemyObject);
        }

        // 배치가 반영됐는지 확인하는 유일한 단서다.
        Debug.Log(
            $"[SpawnManager] Enemy Spawn : {_spawnedEnemies.Count} / Marker={points.Length}"
        );
    }
}

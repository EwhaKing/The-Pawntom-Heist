using UnityEngine;
using Fusion;
using System.Collections.Generic;
using Unity.VisualScripting;

/// <summary>
/// SpawnManager
///
/// 담당:
/// - Network Object 생성 관리
/// - Player Spawn / Despawn (스폰만 관리. 각 Player의 상태나 행동은 여기 담당x)
/// - Spawn된 객체 추적
///
/// TODO:
/// - Enemy Spawn
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
    // 추후 추가 예정. 적 스폰도 여기서 관리.
    // ==================================================

    // TODO:
    // Enemy Spawn
    //
    // public void SpawnEnemy(...)
    // {
    //
    // }

}

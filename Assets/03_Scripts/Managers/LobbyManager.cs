using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>
/// 로비 플레이어 데이터와 UI 슬롯의 연결을 관리합니다.
/// </summary>
public class LobbyManager : MonoBehaviour
{
    public static LobbyManager Instance { get; private set; }

    [SerializeField] private LobbyPlayerSlotContainer slotContainer;
    [SerializeField] private NetworkObject lobbyPlayerDataPrefab;

    private readonly Dictionary<PlayerRef, int> _playerSlotMap = new();
    private readonly Dictionary<PlayerRef, LobbyPlayerData> _playerDataMap = new();

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void RegisterPlayer(LobbyPlayerData playerData)
    {
        PlayerRef player = playerData.PlayerRef;
        int slotIndex = playerData.SlotIndex;

        if (_playerDataMap.ContainsKey(player))
        {
            return;
        }

        if (slotIndex < 0 || slotIndex >= 4)
        {
            Debug.LogError(
                $"[LobbyManager] 유효하지 않은 슬롯 번호입니다. " +
                $"Player={player}, Slot={slotIndex}"
            );
            return;
        }

        _playerDataMap.Add(player, playerData);
        _playerSlotMap.Add(player, slotIndex);

        RefreshPlayerSlot(player);

        // 플레이어 등록이 끝난 뒤 로컬의 Host 여부를 다시 판정
        LobbyUI.Instance?.RefreshButtons();
    }

    public void UnregisterPlayer(LobbyPlayerData playerData)
    {
        PlayerRef player = playerData.PlayerRef;

        if (!_playerSlotMap.TryGetValue(player, out int slotIndex))
        {
            return;
        }

        slotContainer.GetSlot(slotIndex).SetEmpty();

        _playerSlotMap.Remove(player);
        _playerDataMap.Remove(player);
    }

    public void RefreshPlayerSlot(PlayerRef player)
    {
        if (!_playerDataMap.TryGetValue(player, out LobbyPlayerData playerData))
        {
            return;
        }

        if (!_playerSlotMap.TryGetValue(player, out int slotIndex))
        {
            return;
        }

        LobbyPlayerSlotUI slot = slotContainer.GetSlot(slotIndex);

        bool isLocalPlayer = playerData.Object.HasInputAuthority;
        bool isHost = LobbyState.Instance != null && LobbyState.Instance.IsHost(player);

        Debug.Log(
            $"[LobbyManager] 슬롯 갱신 | " +
            $"Player={player}, " +
            //$"PlayerValid={player.InValid}, " +
            $"InputAuthority={playerData.Object.InputAuthority}, " +
            $"HostPlayer={LobbyState.Instance?.HostPlayerRef}, " +
            //$"HostValid={LobbyState.Instance != null && LobbyState.Instance.HostPlayerRef.IsValid}, " +
            $"IsHost={isHost}, " +
            $"IsLocal={isLocalPlayer}, " +
            $"Slot={slotIndex}"
        );

        slot.SetPlayer(
            playerData.PlayerName.ToString(),
            isHost,
            isLocalPlayer,
            playerData.SelectedCatType,
            playerData.IsReady
        );
    }

    /// <summary>
    /// 플레이어가 로비에 입장했을 때,
    /// 호스트가 해당 플레이어의 LobbyPlayerData를 생성합니다.
    /// </summary>
    public void HandlePlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (!runner.IsServer)
        {
            return;
        }

        if (lobbyPlayerDataPrefab == null)
        {
            Debug.LogError(
                "[LobbyManager] LobbyPlayerData 프리팹이 연결되지 않았습니다."
            );
            return;
        }

        if (_playerDataMap.ContainsKey(player))
        {
            Debug.LogWarning(
                $"[LobbyManager] 이미 등록된 플레이어입니다. Player: {player}"
            );
            return;
        }
        int slotIndex = FindEmptySlotIndex();

        if (slotIndex < 0)
        {
            Debug.LogError(
                "[LobbyManager] 비어 있는 로비 슬롯이 없습니다."
            );
            return;
        }

        Debug.Log($"[LobbyManager] LobbyPlayerData를 생성합니다. Player: {player}");

        runner.Spawn(
       lobbyPlayerDataPrefab,
       Vector3.zero,
       Quaternion.identity,
       player,
       onBeforeSpawned: (networkRunner, networkObject) =>
       {
           LobbyPlayerData playerData =
               networkObject.GetComponent<LobbyPlayerData>();

           playerData.PlayerRef = player;
           playerData.SlotIndex = slotIndex;
           playerData.IsReady = false;
       }
   );
    }

    /// <summary>
    /// 플레이어가 로비에서 나갔을 때,
    /// 해당 플레이어의 LobbyPlayerData를 제거합니다.
    /// </summary>
    public void HandlePlayerLeft(
        NetworkRunner runner,
        PlayerRef player)
    {
        if (!runner.IsServer)
        {
            return;
        }

        if (!_playerDataMap.TryGetValue(
                player,
                out LobbyPlayerData playerData))
        {
            Debug.LogWarning(
                $"[LobbyManager] 퇴장한 플레이어의 LobbyPlayerData를 찾지 못했습니다. " +
                $"Player: {player}"
            );
            return;
        }

        Debug.Log(
            $"[LobbyManager] LobbyPlayerData를 제거합니다. Player: {player}"
        );

        runner.Despawn(playerData.Object);
    }

    public LobbyPlayerData GetLocalPlayerData()
    {
        foreach (LobbyPlayerData playerData in _playerDataMap.Values)
        {
            if (playerData.Object.HasInputAuthority)
            {
                return playerData;
            }
        }

        return null;
    }

    private int FindEmptySlotIndex()
    {
        for (int i = 0; i < 4; i++)
        {
            if (!_playerSlotMap.ContainsValue(i))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// 모두가 ready를 눌렀는지 검사합니다.
    /// </summary>
    public bool CanStartGame()
    {
        foreach (LobbyPlayerData playerData in _playerDataMap.Values)
        {
            // Host는 Ready 검사 제외
            if (LobbyState.Instance.IsHost(playerData.PlayerRef))
                continue;

            if (!playerData.IsReady)
                return false;
        }

        return true;
    }

    public bool IsLocalPlayerHost()
    {
        LobbyPlayerData localPlayer = GetLocalPlayerData();

        if (localPlayer == null)
            return false;

        return LobbyState.Instance.IsHost(localPlayer.PlayerRef);
    }
}
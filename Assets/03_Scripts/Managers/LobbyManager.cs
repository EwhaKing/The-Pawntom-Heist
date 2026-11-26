using System.Collections.Generic;
using Fusion;
using Pawntom.Lobby;
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

    private const int MaxSlots = 4;
    // CanStartGame은 버튼 갱신마다 불리므로 판정용 버퍼를 미리 잡아 둡니다(할당 없음).
    private readonly LobbyParticipant[] _participantBuffer = new LobbyParticipant[MaxSlots];

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

        // 준비 안 된 참가자가 나가면 시작 가능 여부가 바뀌므로 버튼을 다시 판정합니다.
        LobbyUI.Instance?.RefreshButtons();
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
            playerData.HasSelectedCat,
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
           playerData.PlayerName = LobbyRules.DefaultPlayerName(slotIndex);
           playerData.HasSelectedCat = false;
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

    public bool TryGetSelectedCatType(
        PlayerRef player,
        out CatType selectedCatType)
    {
        if (_playerDataMap.TryGetValue(player, out LobbyPlayerData playerData))
        {
            selectedCatType = playerData.SelectedCatType;
            return true;
        }

        selectedCatType = default;
        return false;
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
    /// 게임 시작 가능 여부: 모두(호스트 포함) 캐릭터를 골랐고, 호스트를 뺀 모두가 Ready인지 검사합니다.
    /// </summary>
    public bool CanStartGame()
    {
        LobbyState lobbyState = LobbyState.Instance;

        if (lobbyState == null)
            return false;

        int count = 0;

        foreach (LobbyPlayerData playerData in _playerDataMap.Values)
        {
            if (count >= _participantBuffer.Length)
                break;

            _participantBuffer[count++] = new LobbyParticipant(
                lobbyState.IsHost(playerData.PlayerRef),
                playerData.HasSelectedCat,
                playerData.IsReady
            );
        }

        return LobbyRules.CanStartGame(_participantBuffer, count);
    }

    public bool IsLocalPlayerHost()
    {
        LobbyPlayerData localPlayer = GetLocalPlayerData();

        if (localPlayer == null)
            return false;

        return LobbyState.Instance.IsHost(localPlayer.PlayerRef);
    }
}

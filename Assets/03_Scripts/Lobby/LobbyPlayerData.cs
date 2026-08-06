using Fusion;
using UnityEngine;

/// <summary>
/// 플레이어 한 명의 로비 상태를 네트워크로 동기화합니다.
/// PlayerData와 분리됨
/// </summary>
public class LobbyPlayerData : NetworkBehaviour
{
    [Networked] public PlayerRef PlayerRef { get; set; }
    [Networked] public NetworkString<_16> PlayerName { get; set; }
    [Networked, OnChangedRender(nameof(OnSelectedCatChanged))] public CatType SelectedCatType { get; set; }
    [Networked, OnChangedRender(nameof(OnReadyChanged))] public NetworkBool IsReady { get; set; }
    [Networked] public int SlotIndex { get; set; }

    public override void Spawned()
    {
        LobbyManager.Instance?.RegisterPlayer(this);
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        LobbyManager.Instance?.UnregisterPlayer(this);
    }

    private void OnReadyChanged()
    {
        LobbyManager.Instance?.RefreshPlayerSlot(PlayerRef);
        LobbyUI.Instance?.RefreshButtons();
    }

    private void OnSelectedCatChanged()
    {
        LobbyManager.Instance?.RefreshPlayerSlot(PlayerRef);
    }

    /// <summary>
    /// Ready 선택 시 호출되는 RPC
    /// </summary>
    public void RequestToggleReady()
    {
        if (!Object.HasInputAuthority)
        {
            return;
        }

        RPC_ToggleReady();
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_ToggleReady()
    {
        IsReady = !IsReady;
    }

    /// <summary>
    /// 캐릭터 고를 때 호출되는 RPC
    /// </summary>
    public void RequestSelectCat(CatType catType)
    {
        if (!Object.HasInputAuthority)
        {
            return;
        }

        RPC_SelectCat(catType);
    }

    [Rpc(
        RpcSources.InputAuthority,
        RpcTargets.StateAuthority
    )]
    private void RPC_SelectCat(CatType catType)
    {
        SelectedCatType = catType;
        IsReady = false; //TODO : 수정(캐릭터 변경 isReady 처리?)
    }
}
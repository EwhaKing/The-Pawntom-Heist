using System;
using Fusion;
using Pawntom.Lobby;
using UnityEngine;

/// <summary>
/// 플레이어 한 명의 로비 상태를 네트워크로 동기화합니다.
/// PlayerData와 분리됨
/// </summary>
public class LobbyPlayerData : NetworkBehaviour
{
    // CatType은 직렬화 값(0~3)이다. 범위 검증용 종류 수를 한 번만 계산합니다.
    private static readonly int CatTypeCount = Enum.GetValues(typeof(CatType)).Length;

    [Networked] public PlayerRef PlayerRef { get; set; }
    [Networked] public NetworkString<_16> PlayerName { get; set; }
    [Networked, OnChangedRender(nameof(OnSelectionChanged))] public CatType SelectedCatType { get; set; }
    // SelectedCatType 기본값 0(BlackCat)과 '미선택'을 구분합니다.
    [Networked, OnChangedRender(nameof(OnSelectionChanged))] public NetworkBool HasSelectedCat { get; set; }
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

    private void OnSelectionChanged()
    {
        LobbyManager.Instance?.RefreshPlayerSlot(PlayerRef);
        // 시작 가능 여부(전원 선택 필수)와 Ready 버튼이 선택 상태에 따라 달라집니다.
        LobbyUI.Instance?.RefreshButtons();
    }

    /// <summary>
    /// 이 데이터의 주인이 호스트인가. 상태 권한(호스트) 쪽에서만 의미가 있습니다.
    /// Host 모드에서 호스트의 LocalPlayer가 곧 방장입니다.
    /// </summary>
    private bool IsOwnedByHost()
    {
        return PlayerRef == Runner.LocalPlayer;
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
        // 호스트가 다시 검증: 호스트는 Ready 불필요, 캐릭터 미선택이면 거부.
        if (!LobbyRules.CanToggleReady(IsOwnedByHost(), HasSelectedCat))
        {
            Debug.LogWarning(
                $"[LobbyPlayerData] Ready 요청 거부 | Player={PlayerRef}, " +
                $"IsHost={IsOwnedByHost()}, HasSelectedCat={(bool)HasSelectedCat}"
            );
            return;
        }

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
        // 호스트가 다시 검증: 범위 밖 값은 무시합니다. 같은 고양이 중복 선택은 허용합니다.
        if (!LobbyRules.IsValidCat((int)catType, CatTypeCount))
        {
            Debug.LogWarning(
                $"[LobbyPlayerData] 잘못된 캐릭터 요청 무시 | Player={PlayerRef}, Value={(int)catType}"
            );
            return;
        }

        SelectedCatType = catType;
        HasSelectedCat = true;
        IsReady = false; // 캐릭터를 바꾸면 Ready가 풀립니다.
    }
}

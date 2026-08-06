using Fusion;
using UnityEngine;

/// <summary>
/// 로비 전체에서 공유되는 네트워크 상태입니다.
/// 
/// - 현재 호스트가 누구인지
/// - 로비의 전체 플레이어들 > 준비 상태
/// - 게임 시작 가능 여부 를 담고 있음.
/// </summary>
public class LobbyState : NetworkBehaviour
{
    public static LobbyState Instance { get; private set; }

    [Networked]
    public PlayerRef HostPlayerRef { get; set; }

    private void Awake()
    {
        Instance = this;
    }

    public override void Spawned()
    {
        if (Object.HasStateAuthority)
        {
            HostPlayerRef = Runner.LocalPlayer;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public bool IsHost(PlayerRef player)
    {
        return player == HostPlayerRef;
    }
}
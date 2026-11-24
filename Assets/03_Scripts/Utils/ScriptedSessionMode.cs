using UnityEngine;

/// <summary>
/// "연출 세션" 모드. 켜져 있으면 정식 게임 루프 대신 연출 쪽이 사망·재시도·카메라를 관리한다.
/// 지금은 튜토리얼(TutorialDirector)이 켜고 끈다.
/// <para>
/// 게임 코드(PlayerData·PlayerSpectator·ControllableWall·MarketUI 등)는 튜토리얼 클래스를 직접 참조하지 않고 이 값만 본다 —
/// 의존 방향을 "튜토리얼 → 게임" 한쪽으로 유지해, 나중에 게임 코드를 어셈블리(asmdef)로 나눌 때 튜토리얼이 위층으로 따라갈 수 있게.
/// </para>
/// 켜져 있을 때 달라지는 것:
/// - 사망 시 일반 관전 대신 연출의 실패 화면이 카메라를 쓴다(PlayerSpectator 비활성).
/// - 체크포인트 재시도(PlayerData.TryRespawnAtCheckpoint, ControllableWall.RestoreCheckpointState)를 허용한다.
/// - 연출 입력 잠금(<see cref="ScriptedInputLock"/>) 중이거나 재시도 직후 보호 시간에는 피해를 받지 않는다.
/// - 테스트용 단축키(마켓 B) 같은 정식 게임 전용 기능을 막는다.
/// </summary>
public static class ScriptedSessionMode
{
    private static object _owner;

    public static bool IsActive
    {
        get { return _owner != null; }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _owner = null;
    }

    public static void Enter(object owner)
    {
        if (owner != null)
        {
            _owner = owner;
        }
    }

    /// <summary>켠 쪽만 끌 수 있다(늦게 파괴된 이전 씬의 연출이 새 연출의 모드를 끄지 않게).</summary>
    public static void Exit(object owner)
    {
        if (owner != null && ReferenceEquals(_owner, owner))
        {
            _owner = null;
        }
    }
}

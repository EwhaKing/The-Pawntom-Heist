using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// GameplayInputBlocker
///
/// 담당:
/// - UI가 열려 있을 때 플레이어 이동/시점/상호작용 입력을 막고 커서를 풀어 두는 전역 차단
///
/// 소유자 방식:
/// - 여러 UI 가 동시에 열릴 수 있다(본부 단말기 + ESC 메뉴, 튜토리얼 실패 화면 + ESC 메뉴 등).
///   예전처럼 bool 하나로 두면 하나만 닫아도 입력·커서 잠금이 돌아와, 아직 열린 UI 뒤에서 캐릭터가 움직였다.
/// - 그래서 연 쪽이 자기 자신을 소유자로 <see cref="Acquire"/> 하고, 닫을 때 <see cref="Release"/> 한다.
///   마지막 소유자가 풀릴 때만 입력이 돌아오고 커서가 다시 잠긴다.
/// - 기존 호출부 호환을 위해 <see cref="SetBlocked"/> 는 공용 소유자 하나로 동작한다.
///
/// 사용 예:
/// - MainbaseControlUI가 열리면 Acquire(this), 닫히면 Release(this)
/// - PlayerController, PlayerInteraction, InputManager 등은 IsBlocked만 확인
/// </summary>
public static class GameplayInputBlocker
{
    private static readonly HashSet<object> Owners = new HashSet<object>();

    // SetBlocked(bool) 를 쓰는 기존 호출부가 함께 쓰는 소유자.
    private static readonly object LegacyOwner = new object();

    public static bool IsBlocked
    {
        get { return Owners.Count > 0; }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Owners.Clear();
    }

    /// <summary>로비 진입 시 이전 플레이의 차단 상태를 모두 지우되 커서는 UI 모드로 둡니다.</summary>
    public static void ResetForLobby()
    {
        Owners.Clear();
        ShowCursor();
    }

    /// <summary>owner 가 입력을 막고 커서를 쓴다. 같은 소유자가 여러 번 걸어도 한 번으로 친다.</summary>
    public static void Acquire(object owner)
    {
        if (owner == null)
        {
            return;
        }

        bool wasBlocked = IsBlocked;
        Owners.Add(owner);
        ShowCursor();

        if (!wasBlocked)
        {
            Debug.Log("[GameplayInputBlocker] 게임 입력 차단 / 마우스 UI 모드");
        }
    }

    /// <summary>owner 의 차단을 푼다. 마지막 소유자였을 때만 입력이 돌아오고 커서가 잠긴다.</summary>
    public static void Release(object owner)
    {
        if (owner == null || !Owners.Remove(owner) || IsBlocked)
        {
            return;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        Debug.Log("[GameplayInputBlocker] 게임 입력 복구 / 마우스 잠금");
    }

    /// <summary>
    /// 커서를 건드리지 않고 owner 만 지운다. 씬 전환·세션 종료처럼 커서를 정하는 쪽이 따로 있을 때 쓴다.
    /// </summary>
    public static void Forget(object owner)
    {
        if (owner != null)
        {
            Owners.Remove(owner);
        }
    }

    public static bool IsHeldBy(object owner)
    {
        return owner != null && Owners.Contains(owner);
    }

    /// <summary>
    /// 차단 중인데 다른 코드(팝업 정리·스폰 등)가 커서를 잠갔으면 다시 푼다. 차단 중이 아니면 아무것도 하지 않는다.
    /// </summary>
    public static void EnsureCursorFree()
    {
        if (IsBlocked && (Cursor.lockState != CursorLockMode.None || !Cursor.visible))
        {
            ShowCursor();
        }
    }

    /// <summary>
    /// 기존 호출부용. true/false 는 공용 소유자 하나를 걸고 푼다 — 다른 소유자가 걸어 둔 차단은 풀지 않는다.
    /// 새 코드는 <see cref="Acquire"/>/<see cref="Release"/> 를 쓴다.
    /// </summary>
    public static void SetBlocked(bool isBlocked)
    {
        if (isBlocked)
        {
            Acquire(LegacyOwner);
        }
        else
        {
            Release(LegacyOwner);
        }
    }

    private static void ShowCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}

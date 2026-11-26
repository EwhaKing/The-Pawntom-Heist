using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 연출 중 게임플레이 입력(이동·시점·버튼)을 막는다. 소유자 여러 명이 동시에 걸 수 있고(오프닝, 분전반 캐스팅, 정산·엔딩 등)
/// 마지막 소유자가 풀 때만 입력을 되살린다. 튜토리얼은 TutorialInputLock 으로 이것을 쓴다.
/// <para>
/// <see cref="GameplayInputBlocker"/>와 다르다 — 그쪽은 UI 조작용으로 커서를 풀지만, 이쪽은 커서를 건드리지 않는다.
/// </para>
/// </summary>
public static class ScriptedInputLock
{
    private static readonly HashSet<object> Owners = new HashSet<object>();

    // 앱 종료 중에는 매니저를 건드리지 않는다(이미 파괴된 싱글톤을 Instance 가 새로 만들지 않게).
    private static bool _quitting;

    public static bool IsLocked
    {
        get { return Owners.Count > 0; }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Owners.Clear();
        _quitting = false;
        Application.quitting -= MarkQuitting;
        Application.quitting += MarkQuitting;
    }

    private static void MarkQuitting()
    {
        _quitting = true;
    }

    public static void Acquire(object owner)
    {
        if (owner == null || !Owners.Add(owner))
        {
            return;
        }

        InputManager.Instance.DisableGameplayInput();
    }

    public static void Release(object owner)
    {
        if (owner == null || !Owners.Remove(owner) || Owners.Count > 0)
        {
            return;
        }

        // 정리(OnDisable) 중에 불릴 수 있다. 종료 중이거나 InputManager 가 이미 없으면 되살릴 입력도 없다.
        InputManager input = InputManager.Live;

        if (_quitting || input == null)
        {
            return;
        }

        // 게임 중일 때만 입력을 되살린다. 씬을 떠나는 중(로딩·로비·결과)에 마지막 잠금이 풀리면,
        // 그 상태에서 입력을 끈 GameManager/NetworkManager 의 결정을 덮어쓰지 않는다.
        GameManager game = GameManager.Instance;

        if (game == null || game.CurrentState == GameState.InGame)
        {
            input.EnableGameplayInput();
        }
    }

    /// <summary>
    /// 다른 코드(데모 진입 등)가 입력을 다시 켰을 수 있으므로, 잠겨 있으면 한 번 더 끈다.
    /// </summary>
    public static void Reassert()
    {
        if (IsLocked)
        {
            InputManager.Instance.DisableGameplayInput();
        }
    }

    /// <summary>
    /// 씬을 떠날 때. 입력은 되살리지 않는다 — 씬 전환 쪽(GameManager)이 입력 상태를 정한다.
    /// </summary>
    public static void ForgetOwner(object owner)
    {
        if (owner != null)
        {
            Owners.Remove(owner);
        }
    }
}

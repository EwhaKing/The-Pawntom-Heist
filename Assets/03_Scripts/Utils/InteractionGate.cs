using System;
using UnityEngine;

/// <summary>
/// 상호작용을 잠시 막아 두는 관문. 게임 코드는 누가 막는지 모른다 — 막고 싶은 쪽(예: 튜토리얼)이
/// <see cref="Query"/> 에 판정 함수를 걸어 두면, 상호작용 직전에 그 함수에 물어범
/// <para>
/// 판정 함수는 대상 컴포넌트를 받아, 허용이면 null, 막으면 화면에 띄울 이유 문구를 돌려줌
/// 아무도 걸지 않았으면(일반 게임) 항상 허용
/// </para>
/// </summary>
public static class InteractionGate
{
    /// 대상 → 막는 이유(허용이면 null) 마지막에 건 쪽 하나만 씀
    public static Func<Component, string> Query;

    /// <summary>대상이 지금 막혀 있는가. 막혀 있으면 <paramref name="reason"/> 에 안내 문구를 채운다.</summary>
    public static bool IsBlocked(Component target, out string reason)
    {
        reason = null;

        if (Query == null || target == null)
        {
            return false;
        }

        reason = Query(target);
        return reason != null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Query = null;
    }
}
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 게임 월드 일시정지. 대사·연출 화면만 움직이고 나머지(적 NPC·상호작용 대상 등)는 멈추게 할 때 쓴다.
///
/// 소유자 여러 명이 동시에 걸 수 있고, 마지막 소유자가 풀 때만 월드가 다시 움직인다.
///
/// <see cref="TutorialInputLock"/>과 다르다.
/// - TutorialInputLock: 플레이어 입력만 막는다. 분전반 캐스팅 중에도 걸리지만 그동안 적은 움직여야 한다.
/// - GameWorldPause: 입력을 막는 것에 더해 적과 월드 쪽 진행을 멈춘다.
///
/// 시간 자체(Time.timeScale)는 건드리지 않는다. 연출 UI와 Fusion 시뮬레이션이 같이 멈추면 안 되기 때문이다.
/// 각 시스템이 IsPaused를 보고 스스로 멈춘다.
/// </summary>
public static class GameWorldPause
{
    private static readonly HashSet<object> Owners = new HashSet<object>();

    public static bool IsPaused
    {
        get { return Owners.Count > 0; }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Owners.Clear();
    }

    public static void Acquire(object owner)
    {
        if (owner != null)
        {
            Owners.Add(owner);
        }
    }

    public static void Release(object owner)
    {
        if (owner != null)
        {
            Owners.Remove(owner);
        }
    }

    /// <summary>씬을 떠날 때. 소유자가 사라져도 정지가 남지 않게 지운다.</summary>
    public static void ForgetOwner(object owner)
    {
        Release(owner);
    }
}

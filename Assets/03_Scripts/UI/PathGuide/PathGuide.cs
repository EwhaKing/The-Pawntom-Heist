using UnityEngine;

/// <summary>
/// 바닥 길 안내 화살표의 진입점. 어디서든 <see cref="Show"/> 로 "이 플레이어를 저기로 안내해"라고 부탁하고,
/// 필요 없어지면 <see cref="Hide"/> 한다. 처음 부를 때 현재 씬에 알아서 생기고(씬이 바뀌면 새로 생긴다),
/// 화살표는 <b>내 화면에만</b> 보인다(네트워크로 보내지 않는다).
/// <para>
/// 여러 곳이 동시에 부탁하면 우선순위(<c>priority</c>)가 가장 높은 것 하나만 보인다. 같으면 나중에 부탁한 것.
/// 부탁한 쪽(requester)이 파괴된 Unity 오브젝트면 그 부탁은 저절로 사라진다.
/// </para>
/// <example>
/// <code>
/// PathGuide.Show(this, localPlayer, mainbaseBounds);   // 매 프레임 불러도 된다(길은 0.2초마다만 다시 찾는다)
/// PathGuide.Hide(this);
/// </code>
/// </example>
/// </summary>
public static class PathGuide
{
    /// <summary>
    /// <paramref name="player"/>(발 위치)를 목적지 영역 <paramref name="target"/> 으로 안내한다. 영역 안에 들어오거나 아주 가까우면 저절로 숨는다.
    /// </summary>
    public static void Show(object requester, Transform player, Bounds target, int priority = 0)
    {
        if (requester == null || player == null)
        {
            return;
        }

        PathGuideHost.GetOrCreate(player.gameObject.scene).SetRequest(requester, player, target, priority);
    }

    /// <summary>한 지점으로 안내한다(지점 둘레 1m 를 목적지로 본다).</summary>
    public static void Show(object requester, Transform player, Vector3 target, int priority = 0)
    {
        Show(requester, player, new Bounds(target, new Vector3(1f, 2f, 1f)), priority);
    }

    /// <summary><paramref name="requester"/> 의 부탁을 거둔다. 남은 부탁이 없으면 화살표가 사라진다.</summary>
    public static void Hide(object requester)
    {
        PathGuideHost host = PathGuideHost.Current;

        if (host != null && requester != null)
        {
            host.RemoveRequest(requester);
        }
    }

    /// <summary>지형이 크게 바뀌었을 때(층 이동, 벽이 새로 생김 등) 걷기 격자를 다시 만든다.</summary>
    public static void RebuildGrid()
    {
        PathGuideHost host = PathGuideHost.Current;

        if (host != null)
        {
            host.RebuildGrid();
        }
    }
}
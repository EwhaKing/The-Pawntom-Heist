using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 상호작용 가능 거리 판정. 본부 단말기(ControlMachine.IsPlayerInRange)와 같은 방식이다.
///
/// 대상의 경계(충돌체 우선, 없으면 렌더러)를 사방(수평)으로 margin 만큼 넓힌 상자 안에
/// 플레이어가 서 있어야 "가까이 갔다"고 본다. 높이는 보지 않는다.
///
/// 조준선만 맞으면 멀리서도 안내와 E 가 먹던 것을 막기 위해, 줍기·금고·출발 버튼 같은
/// 모든 상호작용 대상이 이 판정을 함께 쓴다.
/// </summary>
public static class InteractRange
{
    // 매 호출마다 목록을 새로 만들지 않는다(안내 판정이 0.1초마다 돈다).
    private static readonly List<Collider> ColliderBuffer = new List<Collider>(8);
    private static readonly List<Renderer> RendererBuffer = new List<Renderer>(8);

    /// <summary>
    /// 플레이어(수평 위치)가 대상의 상호작용 상자 안에 있는가.
    /// </summary>
    /// <param name="target">상호작용 대상. 자식의 충돌체까지 합쳐 경계를 구한다.</param>
    /// <param name="playerPosition">플레이어 위치(발 기준).</param>
    /// <param name="margin">대상 경계에서 사방으로 허용하는 거리(m).</param>
    public static bool IsInRange(Component target, Vector3 playerPosition, float margin)
    {
        if (target == null)
        {
            return false;
        }

        Bounds box = GetBounds(target);
        box.Expand(new Vector3(margin * 2f, 0f, margin * 2f));

        return playerPosition.x >= box.min.x && playerPosition.x <= box.max.x &&
               playerPosition.z >= box.min.z && playerPosition.z <= box.max.z;
    }

    /// <summary>
    /// 한 점(보통 카메라)에서 대상 경계까지의 최단 거리(3D). 점이 경계 안이면 0.
    /// 벽에 걸린 그림처럼 크고 높이 있는 대상도 "조준한 만큼 닿는지"를 재는 데 쓴다.
    /// </summary>
    public static float DistanceTo(Component target, Vector3 point)
    {
        if (target == null)
        {
            return float.MaxValue;
        }

        Bounds box = GetBounds(target);
        return Vector3.Distance(point, box.ClosestPoint(point));
    }

    /// <summary>
    /// 대상 경계. 켜져 있는 단단한 충돌체 → 켜져 있는 모든 충돌체(트리거 포함, 줍기 아이템이 트리거일 수 있다) →
    /// 렌더러 → 대상 위치 순으로 쓴다.
    /// </summary>
    private static Bounds GetBounds(Component target)
    {
        target.GetComponentsInChildren(true, ColliderBuffer);

        bool has = false;
        Bounds bounds = new Bounds(target.transform.position, Vector3.zero);

        for (int pass = 0; pass < 2 && !has; pass++)
        {
            for (int i = 0; i < ColliderBuffer.Count; i++)
            {
                Collider col = ColliderBuffer[i];

                if (col == null || !col.enabled || col.bounds.size == Vector3.zero)
                {
                    continue;
                }

                // 1차: 트리거 제외. 2차: 트리거 포함.
                if (pass == 0 && col.isTrigger)
                {
                    continue;
                }

                if (!has)
                {
                    bounds = col.bounds;
                    has = true;
                }
                else
                {
                    bounds.Encapsulate(col.bounds);
                }
            }
        }

        if (!has)
        {
            target.GetComponentsInChildren(true, RendererBuffer);

            for (int i = 0; i < RendererBuffer.Count; i++)
            {
                Renderer renderer = RendererBuffer[i];

                if (renderer == null || !renderer.enabled)
                {
                    continue;
                }

                if (!has)
                {
                    bounds = renderer.bounds;
                    has = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }
        }

        ColliderBuffer.Clear();
        RendererBuffer.Clear();

        return bounds;
    }
}

using UnityEngine;
using Fusion;

/// <summary>드롭과 사망 흔적이 플레이어/적/다른 아이템 위가 아닌 바닥에 놓이게 합니다.</summary>
public static class PlayerDeathPlacement
{
    public static Vector3 GroundPoint(Vector3 position, Transform owner, out Vector3 normal)
    {
        normal = Vector3.up;
        RaycastHit[] hits = Physics.RaycastAll(position + Vector3.up * 0.75f,
            Vector3.down, 30f, ~0, QueryTriggerInteraction.Ignore);
        float closest = float.MaxValue;
        Vector3 result = position;
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.transform.IsChildOf(owner) ||
                hit.collider.GetComponentInParent<NetworkObject>() != null ||
                Vector3.Dot(hit.normal, Vector3.up) < 0.5f || hit.distance >= closest) continue;
            closest = hit.distance;
            result = hit.point;
            normal = hit.normal;
        }
        return result;
    }
}

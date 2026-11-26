using UnityEngine;

/// <summary>레벨 데이터 항목 Id 규칙: 마커에 적은 Id, 비어 있으면 GameObject 이름.</summary>
public static class LevelLayoutIds
{
    public static string Resolve(string explicitId, Component owner)
    {
        if (!string.IsNullOrWhiteSpace(explicitId))
        {
            return explicitId.Trim();
        }

        return owner != null ? owner.gameObject.name : string.Empty;
    }

    /// <summary>수평 회전각(도). 마커를 돌린 방향이 스폰 방향이 된다.</summary>
    public static float Yaw(Transform transform)
    {
        return transform != null ? transform.eulerAngles.y : 0f;
    }
}

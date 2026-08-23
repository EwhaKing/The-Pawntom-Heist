using Fusion;
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// HeadquartersZone
/// 
/// 담당:
/// - 본부 공간 안에 들어온 플레이어 감지
/// - 출발 버튼을 눌렀을 때 현재 박스 안에 플레이어가 있는지 직접 검사
/// 
/// 참고:
/// - 본부 공간 오브젝트에 붙임
/// - Collider -> Is Trigger 상태
/// </summary>
public class MainbaseZone : MonoBehaviour
{
    [Header("Zone")]
    [SerializeField] private BoxCollider zoneCollider;

    [Header("Debug")]
    [SerializeField] private bool showDebugLog = true;


    /// <summary>
    /// 컴포넌트가 처음 준비될 때 BoxCollider를 자동으로 찾습니다.
    /// </summary>
    private void Awake()
    {
        if (zoneCollider == null)
        {
            zoneCollider = GetComponent<BoxCollider>();
            Debug.LogError("[HeadquartersZone] BoxCollider가 없습니다. HeadquartersZone 오브젝트에 Box Collider를 추가하세요.");
        }
    }

    /// <summary>
    /// 본부 안에 살아있는 플레이어가 최소 1명 이상 있는지 확인
    /// 
    /// 현재는 게임오버 시스템이 없다고 가정하에, InventoryManager가 있는 NetworkObject를 플레이어로 판단
    /// </summary>

    public bool HasAnyAlivePlayerInZone()
    {
        if (zoneCollider == null)
        {
            Debug.LogError("[HeadquartersZone] zoneCollider가 없습니다.");
            return false;
        }

        Vector3 worldCenter = transform.TransformPoint(zoneCollider.center);

        Vector3 worldHalfExtents = Vector3.Scale(zoneCollider.size * 0.5f, transform.lossyScale);

        Collider[] hits = Physics.OverlapBox(worldCenter, worldHalfExtents, transform.rotation);

        foreach (Collider hit in hits)
        {
            NetworkObject playerObject = hit.GetComponentInParent<NetworkObject>();

            if (playerObject == null)
                continue;

            InventoryManager inventory = playerObject.GetComponent<InventoryManager>();

            if (inventory == null)
                continue;
            
            // TODO:
            // 나중에 PlayerHealth가 생기면 여기서 사망 여부 검사
            //
            // PlayerHealth health = playerObject.GetComponent<PlayerHealth>();
            // if (health != null && health.IsDead)
            // {
            //     continue;
            // }

            if (showDebugLog)
            {
                Debug.Log($"[HeadquartersZone] 현재 본부 내부 플레이어 감지: {playerObject.name}");
            }

            return true;
        }

        if (showDebugLog)
        {
            Debug.Log("[HeadquartersZone] 현재 본부 내부에 플레이어가 없습니다.");
        }

        return false;
    }

    /// <summary>
    /// Scene 뷰에서 본부 감지 영역을 노란색 박스로 보여줌
    /// </summary>
    private void OnDrawGizmos()
    {
        BoxCollider box = zoneCollider;

        if (box == null)
        {
            box = GetComponent<BoxCollider>();
        }

        if (box == null)
        {
            return;
        }

        Gizmos.color = Color.yellow;

        Matrix4x4 oldMatrix = Gizmos.matrix;

        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(box.center, box.size);

        Gizmos.matrix = oldMatrix;
    }
}

using UnityEngine;

/// <summary>
/// DiscoveryZone
///
/// 담당:
/// - 실제 맵에서 플레이어가 특정 구역에 들어왔는지 감지
/// - 플레이어가 들어오면 연결된 DiscoverableArea를 발견 처리
///
/// 사용 위치:
/// - 실제 방/구역 안에 있는 Trigger 오브젝트에 붙임
///
/// 필요 컴포넌트:
/// - BoxCollider
/// - Is Trigger ON
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class DiscoveryZone : MonoBehaviour
{
    [Header("Target Area")]
    [Tooltip("플레이어가 이 Trigger에 들어왔을 때 발견 처리할 구역")]
    [SerializeField] private DiscoverableArea targetArea;

    [Header("Option")]
    [SerializeField] private bool discoverOnlyOnce = true;

    private bool hasDiscovered;

    private void Awake()
    {
        BoxCollider boxCollider = GetComponent<BoxCollider>();
        boxCollider.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (discoverOnlyOnce && hasDiscovered)
        {
            return;
        }

        // PlayerTarget은 네가 이미 이름 바꾼 플레이어 추적 스크립트
        PlayerTarget playerTarget = other.GetComponentInParent<PlayerTarget>();

        if (playerTarget == null)
        {
            return;
        }

        if (targetArea == null)
        {
            Debug.LogWarning($"[DiscoveryZone] Target Area가 연결되지 않았습니다: {gameObject.name}");
            return;
        }

        hasDiscovered = true;

        Debug.Log($"[DiscoveryZone] 플레이어가 발견 구역 진입: {gameObject.name}");

        targetArea.Discover();
    }
}

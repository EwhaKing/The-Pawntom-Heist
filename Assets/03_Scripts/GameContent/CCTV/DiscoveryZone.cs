using UnityEngine;

/// <summary>
/// DiscoveryZone
///
/// 담당:
/// - 실제 맵에서 플레이어가 특정 구역에 들어왔는지 감지
/// - 플레이어가 들어오면 연결된 DiscoverableArea를 발견 처리
///
/// 네트워크:
/// - 판정은 연결된 구역의 상태 권한(호스트)에서만 한다(<see cref="DiscoverableArea.CanDecideDiscovery"/>).
///   호스트는 모든 플레이어를 시뮬레이션하므로 원격 플레이어의 진입도 호스트 위치로 판정된다.
/// - 물리 트리거 이벤트를 쓰지 않는다. NetworkCharacterController 는 클라이언트 재시뮬레이션 중 위치가 앞뒤로 움직여
///   OnTriggerEnter 가 여러 번 울리거나 피어마다 다르게 울린다. 대신 매 프레임 플레이어 위치가 콜라이더 안인지 본다
///   (TutorialZone 과 같은 방식, 플레이어 최대 4명).
///
/// 사용 위치:
/// - 실제 방/구역 안에 있는 Trigger 오브젝트에 붙임 (NetworkObject 불필요)
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

    private BoxCollider _zoneCollider;
    private bool hasDiscovered;
    private bool _warnedMissingArea;

    private void Awake()
    {
        _zoneCollider = GetComponent<BoxCollider>();
        _zoneCollider.isTrigger = true;
    }

    private void Update()
    {
        if (discoverOnlyOnce && hasDiscovered)
        {
            return;
        }

        if (targetArea == null)
        {
            if (!_warnedMissingArea)
            {
                _warnedMissingArea = true;
                Debug.LogWarning($"[DiscoveryZone] Target Area가 연결되지 않았습니다: {gameObject.name}");
            }

            return;
        }

        if (!targetArea.CanDecideDiscovery)
        {
            return;
        }

        if (targetArea.IsDiscovered)
        {
            // 다른 경로(분전반·다른 영역)로 이미 발견됐다.
            hasDiscovered = true;
            return;
        }

        if (!AnyPlayerInside())
        {
            return;
        }

        hasDiscovered = true;

        Debug.Log($"[DiscoveryZone] 플레이어가 발견 구역 진입: {gameObject.name}");

        targetArea.Discover();
    }

    private bool AnyPlayerInside()
    {
        if (_zoneCollider == null || !_zoneCollider.enabled)
        {
            return false;
        }

        for (int i = 0; i < PlayerTarget.AllTargets.Count; i++)
        {
            PlayerTarget playerTarget = PlayerTarget.AllTargets[i];

            if (playerTarget == null)
            {
                continue;
            }

            // 발 위치는 바닥 경계에 걸칠 수 있어 발목·몸통 높이 두 점을 본다(예전 트리거는 캡슐 겹침이었다).
            Vector3 feet = playerTarget.transform.position;

            if (Contains(feet + Vector3.up * 0.1f) || Contains(feet + Vector3.up * 0.8f))
            {
                return true;
            }
        }

        return false;
    }

    private bool Contains(Vector3 point)
    {
        // 안쪽 점이면 ClosestPoint 가 그 점을 그대로 돌려준다(BoxCollider, 회전·스케일 반영).
        return (_zoneCollider.ClosestPoint(point) - point).sqrMagnitude < 0.0001f;
    }
}

using UnityEngine;

/// <summary>
/// DiscoverableArea
///
/// 담당:
/// - CCTV 지도에서 특정 구역이 발견되었는지 상태를 관리
/// - 위치탭 / 스프링클러탭 / 전등탭에서 공통으로 사용
///
/// 예:
/// - 플레이어가 방에 들어가면 발견됨
/// - 발견되면 ? 가림막이 사라짐
/// - 발견된 구역의 스프링클러/전등 조작이 가능해짐
///
/// 사용 위치:
/// - Area_01, Area_02 같은 빈 오브젝트에 붙임
/// </summary>
public class DiscoverableArea : MonoBehaviour
{
    [Header("Area Info")]
    [SerializeField] private string areaId = "Area_01";

    [Header("State")]
    [SerializeField] private bool isDiscovered;

    public string AreaId => areaId;
    public bool IsDiscovered => isDiscovered;

    /// <summary>
    /// 이 구역을 발견 상태로 변경
    /// </summary>
    public void Discover()
    {
        if (isDiscovered)
        {
            return;
        }

        isDiscovered = true;

        Debug.Log($"[DiscoverableArea] 구역 발견됨: {areaId}");
    }

    /// <summary>
    /// 테스트용으로 발견 상태를 초기화
    /// </summary>
    [ContextMenu("Reset Discovery")]
    private void ResetDiscovery()
    {
        isDiscovered = false;
        Debug.Log($"[DiscoverableArea] 발견 상태 초기화: {areaId}");
    }

    /// <summary>
    /// 테스트용으로 즉시 발견 처리
    /// </summary>
    [ContextMenu("Test Discover")]
    private void TestDiscover()
    {
        Discover();
    }
}

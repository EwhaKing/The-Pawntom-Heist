using UnityEngine;

/// <summary>
/// MapHiddenAreaUI
///
/// 담당:
/// - CCTV 지도에서 아직 발견되지 않은 구역을 네모칸으로 가림
/// - 연결된 DiscoverableArea가 발견되면 가림막을 숨김
///
/// 사용 위치:
/// - Layer_Position / Layer_Sprinkler / Layer_Light 안의 HiddenArea_01 같은 UI Image에 붙임
/// </summary>
public class MapHiddenAreaUI : MonoBehaviour
{
    [Header("Target Area")]
    [SerializeField] private DiscoverableArea targetArea;

    private void Update()
    {
        if (targetArea == null)
        {
            return;
        }

        gameObject.SetActive(!targetArea.IsDiscovered);
    }
}
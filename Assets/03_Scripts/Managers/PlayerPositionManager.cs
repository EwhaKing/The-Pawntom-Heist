using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// PlayerPositionManager
///
/// 담당:
/// - MainbaseControlUI의 위치 탭에서 플레이어들의 위치를 UI 마커로 표시
/// - 실제 플레이어 월드 좌표를 CCTV 맵 UI 좌표로 변환
/// - 플레이어 수에 맞춰 마커를 생성/삭제/이동
///
/// 사용 위치:
/// - MainbaseControlUI > MapContent > Layer_Position 오브젝트에 붙임
/// </summary>
public class PlayerPositionManager : MonoBehaviour
{
    [Header("Map Camera")]
    [Tooltip("CCTV 전체 맵을 찍는 카메라")]
    [SerializeField] private Camera mapCamera;

    [Header("Map UI")]
    [Tooltip("CCTV 맵이 표시되는 RawImage의 RectTransform")]
    [SerializeField] private RectTransform mapRect;

    [Tooltip("플레이어 마커들이 생성될 부모")]
    [SerializeField] private RectTransform markerParent;

    [Header("Marker Prefab")]
    [Tooltip("플레이어 위치를 표시할 푸른색 점 UI 프리팹")]
    [SerializeField] private RectTransform playerMarkerPrefab;

    [Header("Option")]
    [SerializeField] private bool hideMarkerOutsideMap = true;

    /// <summary>
    /// PlayerTarget과 해당 UI 마커를 연결해서 관리
    /// </summary>
    private readonly Dictionary<PlayerTarget, RectTransform> markers =
        new Dictionary<PlayerTarget, RectTransform>();

    private void Awake()
    {
        if (markerParent == null)
        {
            markerParent = GetComponent<RectTransform>();
        }
    }

    private void Update()
    {
        if (mapCamera == null || mapRect == null || markerParent == null || playerMarkerPrefab == null)
        {
            return;
        }

        CreateMissingMarkers();
        RemoveInvalidMarkers();
        UpdateMarkerPositions();
    }

    /// <summary>
    /// 새로 생성된 플레이어가 있으면 위치 마커를 생성
    /// </summary>
    private void CreateMissingMarkers()
    {
        for (int i = 0; i < PlayerTarget.AllTargets.Count; i++)
        {
            PlayerTarget target = PlayerTarget.AllTargets[i];

            if (target == null)
            {
                continue;
            }

            if (markers.ContainsKey(target))
            {
                continue;
            }

            RectTransform marker = Instantiate(playerMarkerPrefab, markerParent);
            marker.gameObject.SetActive(true);

            markers.Add(target, marker);

            Debug.Log($"[PlayerPositionManager] 플레이어 마커 생성: {target.gameObject.name}");
        }
    }

    /// <summary>
    /// 사라진 플레이어의 위치 마커를 제거
    /// </summary>
    private void RemoveInvalidMarkers()
    {
        List<PlayerTarget> removeList = new List<PlayerTarget>();

        foreach (KeyValuePair<PlayerTarget, RectTransform> pair in markers)
        {
            if (pair.Key != null)
            {
                continue;
            }

            if (pair.Value != null)
            {
                Destroy(pair.Value.gameObject);
            }

            removeList.Add(pair.Key);
        }

        for (int i = 0; i < removeList.Count; i++)
        {
            markers.Remove(removeList[i]);
        }
    }

    /// <summary>
    /// 플레이어 월드 좌표를 CCTV 맵 UI 좌표로 변환해서 마커 위치를 갱신
    /// </summary>
    private void UpdateMarkerPositions()
    {
        foreach (KeyValuePair<PlayerTarget, RectTransform> pair in markers)
        {
            PlayerTarget target = pair.Key;
            RectTransform marker = pair.Value;

            if (target == null || marker == null)
            {
                continue;
            }

            Vector3 worldPosition = target.MarkerTarget.position;

            // 월드 좌표를 카메라 Viewport 좌표로 변환
            // Viewport 좌표는 x, y가 0~1 사이면 카메라 화면 안에 있다는 뜻
            Vector3 viewportPosition = mapCamera.WorldToViewportPoint(worldPosition);

            bool isInside =
                viewportPosition.z > 0f &&
                viewportPosition.x >= 0f &&
                viewportPosition.x <= 1f &&
                viewportPosition.y >= 0f &&
                viewportPosition.y <= 1f;

            if (hideMarkerOutsideMap)
            {
                marker.gameObject.SetActive(isInside);
            }

            if (!isInside && hideMarkerOutsideMap)
            {
                continue;
            }

            // Viewport 좌표 0~1을 MapRawImage 기준 UI 좌표로 변환
            float uiX = (viewportPosition.x - 0.5f) * mapRect.rect.width;
            float uiY = (viewportPosition.y - 0.5f) * mapRect.rect.height;

            marker.anchoredPosition = new Vector2(uiX, uiY);
        }
    }
}

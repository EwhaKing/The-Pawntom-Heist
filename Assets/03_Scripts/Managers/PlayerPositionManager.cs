using System.Collections.Generic;
using Pawntom.Enemy.Authoring;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// PlayerPositionManager
///
/// 담당:
/// - MainbaseControlUI의 위치 탭에서 플레이어들의 위치를 UI 마커로 표시
/// - 적(K-9) 위치를 붉은 마커로 표시 (GAME_DESIGN CCTV 뷰: 평면도 위 아군/적 NPC 위치)
/// - 실제 월드 좌표를 CCTV 맵 UI 좌표로 변환
/// - 대상 수에 맞춰 마커를 생성/삭제/이동
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

    [Header("Enemy")]
    [Tooltip("적(K-9) 위치를 붉은 점으로 표시한다")]
    [SerializeField] private bool showEnemies = true;

    [Tooltip("적 마커 프리팹. 비우면 플레이어 마커를 복제해 아래 색으로 칠한다")]
    [SerializeField] private RectTransform enemyMarkerPrefab;

    [Tooltip("적 마커 색(UI_SPEC 의미 규칙: 위험 = 적색)")]
    [SerializeField] private Color enemyMarkerColor = new Color(1f, 0.18f, 0.12f, 1f);

    [Tooltip("미해금 구역 가림막(MapHiddenAreaUI) 아래에 있는 적은 숨긴다 — 해금 전 구역의 적은 보이지 않는다")]
    [SerializeField] private bool hideEnemiesUnderMask = true;

    [Header("Option")]
    [SerializeField] private bool hideMarkerOutsideMap = true;

    /// <summary>
    /// PlayerTarget과 해당 UI 마커를 연결해서 관리
    /// </summary>
    private readonly Dictionary<PlayerTarget, RectTransform> markers =
        new Dictionary<PlayerTarget, RectTransform>();

    private readonly Dictionary<EnemyAgent, RectTransform> enemyMarkers =
        new Dictionary<EnemyAgent, RectTransform>();

    // 매 프레임 새 목록을 만들지 않도록 재사용한다.
    private readonly List<PlayerTarget> removePlayers = new List<PlayerTarget>();
    private readonly List<EnemyAgent> removeEnemies = new List<EnemyAgent>();

    private MapHiddenAreaUI[] masks = new MapHiddenAreaUI[0];
    private readonly Vector3[] maskCorners = new Vector3[4];

    private void Awake()
    {
        if (markerParent == null)
        {
            markerParent = GetComponent<RectTransform>();
        }
    }

    private void OnEnable()
    {
        // 가림막은 평면도(MapContent) 아래 페이지들에 흩어져 있다. 탭이 바뀔 때마다 다시 모은다.
        Transform mapRoot = mapRect != null && mapRect.parent != null ? mapRect.parent : transform;
        masks = mapRoot.GetComponentsInChildren<MapHiddenAreaUI>(true);
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

        if (showEnemies)
        {
            SyncEnemyMarkers();
        }
    }

    /// <summary>
    /// 새로 생성된 플레이어가 있으면 위치 마커를 생성
    /// </summary>
    private void CreateMissingMarkers()
    {
        for (int i = 0; i < PlayerTarget.AllTargets.Count; i++)
        {
            PlayerTarget target = PlayerTarget.AllTargets[i];

            if (target == null || !target.IsVisibleOnMap)
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
        removePlayers.Clear();

        foreach (KeyValuePair<PlayerTarget, RectTransform> pair in markers)
        {
            if (pair.Key != null && pair.Key.IsVisibleOnMap)
            {
                continue;
            }

            if (pair.Value != null)
            {
                Destroy(pair.Value.gameObject);
            }

            removePlayers.Add(pair.Key);
        }

        for (int i = 0; i < removePlayers.Count; i++)
        {
            markers.Remove(removePlayers[i]);
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

            PlaceMarker(marker, target.MarkerTarget.position, false);
        }
    }

    // ───────────── 적 마커 ─────────────

    private void SyncEnemyMarkers()
    {
        IReadOnlyList<EnemyAgent> enemies = EnemyAgent.Active;

        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyAgent enemy = enemies[i];

            if (enemy == null || enemyMarkers.ContainsKey(enemy))
            {
                continue;
            }

            enemyMarkers.Add(enemy, CreateEnemyMarker());
        }

        removeEnemies.Clear();

        foreach (KeyValuePair<EnemyAgent, RectTransform> pair in enemyMarkers)
        {
            EnemyAgent enemy = pair.Key;
            RectTransform marker = pair.Value;

            if (enemy == null || !enemy.isActiveAndEnabled)
            {
                if (marker != null)
                {
                    Destroy(marker.gameObject);
                }

                removeEnemies.Add(enemy);
                continue;
            }

            if (marker != null)
            {
                PlaceMarker(marker, enemy.transform.position, hideEnemiesUnderMask);
            }
        }

        for (int i = 0; i < removeEnemies.Count; i++)
        {
            enemyMarkers.Remove(removeEnemies[i]);
        }
    }

    private RectTransform CreateEnemyMarker()
    {
        RectTransform prefab = enemyMarkerPrefab != null ? enemyMarkerPrefab : playerMarkerPrefab;
        RectTransform marker = Instantiate(prefab, markerParent);
        marker.name = "EnemyMarker";
        marker.gameObject.SetActive(true);

        if (enemyMarkerPrefab == null)
        {
            // 플레이어 마커(푸른 점)를 복제했으므로 붉게 칠한다. 생성 때 한 번만 한다.
            Graphic[] graphics = marker.GetComponentsInChildren<Graphic>(true);

            for (int i = 0; i < graphics.Length; i++)
            {
                graphics[i].color = enemyMarkerColor;
            }
        }

        return marker;
    }

    // ───────────── 공통 ─────────────

    /// <summary>
    /// 월드 좌표를 CCTV 맵 UI 좌표로 옮긴다. 맵 밖이거나(옵션) 가림막 아래면 숨긴다.
    /// </summary>
    private void PlaceMarker(RectTransform marker, Vector3 worldPosition, bool hideUnderMask)
    {
        // 월드 좌표를 카메라 Viewport 좌표로 변환
        // Viewport 좌표는 x, y가 0~1 사이면 카메라 화면 안에 있다는 뜻
        Vector3 viewportPosition = mapCamera.WorldToViewportPoint(worldPosition);

        bool isInside =
            viewportPosition.z > 0f &&
            viewportPosition.x >= 0f &&
            viewportPosition.x <= 1f &&
            viewportPosition.y >= 0f &&
            viewportPosition.y <= 1f;

        if (!isInside && hideMarkerOutsideMap)
        {
            if (marker.gameObject.activeSelf)
            {
                marker.gameObject.SetActive(false);
            }

            return;
        }

        // Viewport 좌표 0~1을 MapRawImage 기준 UI 좌표로 변환
        float uiX = (viewportPosition.x - 0.5f) * mapRect.rect.width;
        float uiY = (viewportPosition.y - 0.5f) * mapRect.rect.height;

        marker.anchoredPosition = new Vector2(uiX, uiY);

        bool visible = !hideUnderMask || !IsUnderMask(marker.position);

        if (marker.gameObject.activeSelf != visible)
        {
            marker.gameObject.SetActive(visible);
        }
    }

    /// <summary>켜져 있는 미해금 가림막 사각형 안에 있는가(UI 는 회전하지 않는다고 본다).</summary>
    private bool IsUnderMask(Vector3 markerWorldPosition)
    {
        for (int i = 0; i < masks.Length; i++)
        {
            MapHiddenAreaUI mask = masks[i];

            if (mask == null || !mask.gameObject.activeInHierarchy)
            {
                continue;
            }

            ((RectTransform)mask.transform).GetWorldCorners(maskCorners);

            // 0 = 왼쪽 아래, 2 = 오른쪽 위
            if (markerWorldPosition.x >= maskCorners[0].x && markerWorldPosition.x <= maskCorners[2].x &&
                markerWorldPosition.y >= maskCorners[0].y && markerWorldPosition.y <= maskCorners[2].y)
            {
                return true;
            }
        }

        return false;
    }
}

using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// CCTVMapZoomController
///
/// 담당:
/// - CCTV 지도 위에서 마우스 스크롤로 확대/축소 처리
/// - 스크롤 위: 확대
/// - 스크롤 아래: 축소
/// - 마우스 좌클릭 드래그로 지도 이동
/// - MapContent 전체를 조작해서 위치/격벽/스프링클러/전등 탭이 같은 확대/이동 상태를 공유하게 함
///
/// 사용 위치:
/// - MainbaseControlUI > ControlRoot > WindowPanel > MapFrame > MapViewport 오브젝트에 붙임
/// </summary>
public class CCTVZoomController : MonoBehaviour
{
    [Header("Viewport")]
    [Tooltip("마우스가 이 영역 안에 있을 때만 확대/드래그됩니다. 보통 MapViewport 자기 자신입니다.")]
    [SerializeField] private RectTransform viewportRect;

    [Header("Target")]
    [Tooltip("확대/축소/드래그 이동할 지도 전체 부모입니다. MapRawImage가 아니라 MapContent를 넣어야 합니다.")]
    [SerializeField] private RectTransform mapContent;

    [Header("Canvas")]
    [Tooltip("Canvas_HUD를 넣습니다. Screen Space - Overlay면 비워둬도 동작합니다.")]
    [SerializeField] private Canvas rootCanvas;

    [Header("Zoom Option")]
    [SerializeField] private float minZoom = 1f;
    [SerializeField] private float maxZoom = 2.5f;
    [SerializeField] private float zoomStep = 0.15f;

    [Header("Drag Option")]
    [SerializeField] private float dragSpeed = 1f;

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI zoomText;

    private float currentZoom = 1f;

    private bool isDragging;
    private Vector2 previousLocalMousePosition;

    private void Awake()
    {
        if (viewportRect == null)
        {
            viewportRect = GetComponent<RectTransform>();
        }

        if (rootCanvas == null)
        {
            rootCanvas = GetComponentInParent<Canvas>();
        }
    }

    private void Start()
    {
        ApplyZoom();
        ClampMapPosition();
    }

    private void Update()
    {
        if (mapContent == null || viewportRect == null)
        {
            return;
        }

        if (Mouse.current == null)
        {
            return;
        }

        Vector2 mouseScreenPosition = Mouse.current.position.ReadValue();

        bool isMouseInsideViewport = IsMouseInsideViewport(mouseScreenPosition);

        HandleZoom(isMouseInsideViewport);
        HandleDrag(mouseScreenPosition, isMouseInsideViewport);
    }

    /// <summary>
    /// 마우스 휠로 확대/축소
    /// </summary>
    private void HandleZoom(bool isMouseInsideViewport)
    {
        if (!isMouseInsideViewport)
        {
            return;
        }

        float scrollY = Mouse.current.scroll.ReadValue().y;

        if (scrollY > 0.01f)
        {
            currentZoom += zoomStep;
        }
        else if (scrollY < -0.01f)
        {
            currentZoom -= zoomStep;
        }
        else
        {
            return;
        }

        currentZoom = Mathf.Clamp(currentZoom, minZoom, maxZoom);

        ApplyZoom();
        ClampMapPosition();

        Debug.Log($"[CCTVMapZoomController] Zoom: {currentZoom}");
    }

    /// <summary>
    /// 마우스 좌클릭 드래그로 지도를 이동
    /// </summary>
    private void HandleDrag(Vector2 mouseScreenPosition, bool isMouseInsideViewport)
    {
        if (Mouse.current.leftButton.wasPressedThisFrame && isMouseInsideViewport)
        {
            isDragging = true;
            previousLocalMousePosition = GetLocalMousePosition(mouseScreenPosition);
        }

        if (Mouse.current.leftButton.wasReleasedThisFrame)
        {
            isDragging = false;
        }

        if (!isDragging || !Mouse.current.leftButton.isPressed)
        {
            return;
        }

        Vector2 currentLocalMousePosition = GetLocalMousePosition(mouseScreenPosition);
        Vector2 delta = currentLocalMousePosition - previousLocalMousePosition;

        mapContent.anchoredPosition += delta * dragSpeed;

        previousLocalMousePosition = currentLocalMousePosition;

        ClampMapPosition();
    }

    /// <summary>
    /// 마우스가 CCTV 지도 영역 안에 있는지 확인
    /// </summary>
    private bool IsMouseInsideViewport(Vector2 mouseScreenPosition)
    {
        Camera uiCamera = null;

        if (rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            uiCamera = rootCanvas.worldCamera;
        }

        return RectTransformUtility.RectangleContainsScreenPoint(
            viewportRect,
            mouseScreenPosition,
            uiCamera
        );
    }

    /// <summary>
    /// 화면 좌표를 Viewport 기준 로컬 좌표로 변환
    /// </summary>
    private Vector2 GetLocalMousePosition(Vector2 mouseScreenPosition)
    {
        Camera uiCamera = null;

        if (rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            uiCamera = rootCanvas.worldCamera;
        }

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            viewportRect,
            mouseScreenPosition,
            uiCamera,
            out Vector2 localPoint
        );

        return localPoint;
    }

    /// <summary>
    /// 현재 확대 비율을 MapContent에 적용
    /// </summary>
    private void ApplyZoom()
    {
        if (mapContent != null)
        {
            mapContent.localScale = new Vector3(currentZoom, currentZoom, 1f);
        }

        if (zoomText != null)
        {
            zoomText.text = $"{Mathf.RoundToInt(currentZoom * 100f)}%";
        }
    }

    /// <summary>
    /// 지도를 너무 바깥으로 끌 수 없게 제한
    /// </summary>
    private void ClampMapPosition()
    {
        if (mapContent == null || viewportRect == null)
        {
            return;
        }

        float viewportWidth = viewportRect.rect.width;
        float viewportHeight = viewportRect.rect.height;

        float contentWidth = mapContent.rect.width * currentZoom;
        float contentHeight = mapContent.rect.height * currentZoom;

        float maxX = Mathf.Max(0f, (contentWidth - viewportWidth) * 0.5f);
        float maxY = Mathf.Max(0f, (contentHeight - viewportHeight) * 0.5f);

        Vector2 position = mapContent.anchoredPosition;

        position.x = Mathf.Clamp(position.x, -maxX, maxX);
        position.y = Mathf.Clamp(position.y, -maxY, maxY);

        mapContent.anchoredPosition = position;
    }

    /// <summary>
    /// 테스트용 확대/위치 초기화
    /// </summary>
    [ContextMenu("Reset View")]
    private void ResetView()
    {
        currentZoom = 1f;

        if (mapContent != null)
        {
            mapContent.anchoredPosition = Vector2.zero;
        }

        ApplyZoom();
        ClampMapPosition();
    }
}

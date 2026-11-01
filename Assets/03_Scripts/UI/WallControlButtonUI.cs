using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// WallControlButtonUI
///
/// 담당:
/// - MainbaseControlUI의 지도 위에 표시되는 격벽 버튼
/// - 실제 ControllableWall과 연결됨
/// - 미해금 상태에서 클릭하면 해킹 미니게임 실행
/// - 해킹 성공 시 격벽 해금 + 열림
/// - 해금 상태에서 더블클릭하면 열림/닫힘 전환
///
/// 사용 위치:
/// - MainbaseControlUI > MapContent > Layer_Wall > WallButton_01 같은 UI 버튼에 붙임
/// </summary>
public class WallControlButtonUI : MonoBehaviour, IPointerClickHandler
{
    [Header("Target")]
    [SerializeField] private ControllableWall targetWall;

    [Header("UI")]
    [SerializeField] private Image wallImage;

    [Header("Hacking")]
    [SerializeField] private Hacking.HackingManager hackingManager;
    [SerializeField] private Hacking.SecurityLevel hackingLevel = Hacking.SecurityLevel.Normal;

    [Header("Double Click")]
    [SerializeField] private float doubleClickInterval = 0.35f;

    // 세 상태를 색(빨강·파랑·초록)과 모양(자물쇠 / 꽉 찬 바탕 / 얇은 막대)으로 이중 구분한다.
    // 해금 안 된 격벽은 열 수 없으므로 상태는 잠김·해금(닫힘)·열림 세 가지뿐이다(UI_SPEC 3.2).
    [Header("Colors")]
    [Tooltip("열림 = 초록 #2ED47A")]
    [SerializeField] private Color openColor = new Color(0.180f, 0.831f, 0.478f, 1f);

    [Tooltip("잠김(해금 안 됨) = 빨강 #E5342B")]
    [SerializeField] private Color hackingLockedColor = new Color(0.898f, 0.204f, 0.169f, 1f);

    [Tooltip("해금됐지만 닫혀 있을 때 = 파랑 #2E7DE0 (대본의 \"푸른색이 됐습니다\")")]
    [SerializeField] private Color unlockedColor = new Color(0.180f, 0.490f, 0.878f, 1f);

    [Header("Shape")]
    [Tooltip("열림일 때 버튼 바탕에 얹는 얇은 막대의 두께(버튼 짧은 변 대비 비율)")]
    [SerializeField, Range(0.1f, 1f)] private float openBarThickness = 0.3f;

    [Tooltip("열림일 때 바탕을 이 투명도로 흐리게 한다. 클릭 영역은 그대로 남는다")]
    [SerializeField, Range(0.05f, 1f)] private float openBackdropAlpha = 0.25f;

    [Tooltip("잠김일 때 버튼 위에 띄울 자물쇠 아이콘. 비우면 표시하지 않는다")]
    [SerializeField] private Sprite lockIcon;

    [Tooltip("자물쇠 아이콘 크기(UI 단위). 격벽 버튼이 가로로 긴 20 높이라 작게 잡는다")]
    [SerializeField, Min(8f)] private float lockIconSize = 24f;

    private Image _openBar;
    private Image _lockImage;

    private float lastClickTime;
    private bool isHacking;

    private void Awake()
    {
        if (wallImage == null)
        {
            wallImage = GetComponent<Image>();
        }

        if (hackingManager == null)
        {
            hackingManager = FindFirstObjectByType<Hacking.HackingManager>();
        }

        RefreshUI();
    }

    private void OnEnable()
    {
        RefreshUI();

        if (targetWall != null)
        {
            targetWall.OnStateChanged += RefreshUI;
        }
    }

    private void OnDisable()
    {
        if (targetWall != null)
        {
            targetWall.OnStateChanged -= RefreshUI;
        }
    }

    /// <summary>
    /// 버튼 클릭 처리
    /// </summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (targetWall == null)
        {
            Debug.LogWarning("[WallControlButtonUI] Target Wall이 연결되지 않았습니다.");
            return;
        }

        if (isHacking)
        {
            Debug.Log("[WallControlButtonUI] 이미 해킹 진행 중입니다.");
            return;
        }

        // 해금되지 않은 벽이면 클릭 시 해킹 미니게임 실행
        if (!targetWall.IsUnlocked)
        {
            StartHacking();
            return;
        }

        // 해금된 벽은 더블클릭으로 열고 닫음
        float currentTime = Time.unscaledTime;

        if (currentTime - lastClickTime <= doubleClickInterval)
        {
            targetWall.ToggleWall();
            RefreshUI();
            lastClickTime = 0f;
        }
        else
        {
            lastClickTime = currentTime;
            Debug.Log("[WallControlButtonUI] 해금된 격벽입니다. 한 번 더 클릭하면 열림/닫힘 전환");
        }
    }

    /// <summary>
    /// 해킹 미니게임 시작
    /// </summary>
    private void StartHacking()
    {
        if (hackingManager == null)
        {
            hackingManager = FindFirstObjectByType<Hacking.HackingManager>();
        }

        if (hackingManager == null)
        {
            Debug.LogWarning("[WallControlButtonUI] HackingManager를 찾지 못했습니다.");
            return;
        }

        isHacking = true;

        Debug.Log("[WallControlButtonUI] 미해금 격벽입니다. 해킹 미니게임 시작");

        hackingManager.OpenHackingPopup(hackingLevel, OnHackingFinished);
    }

    /// <summary>
    /// 해킹 미니게임 결과 처리
    /// </summary>
    private void OnHackingFinished(bool isSuccess)
    {
        isHacking = false;

        if (targetWall == null)
        {
            return;
        }

        if (isSuccess)
        {
            // GAME_DESIGN 격벽 상태 머신: 미니게임 성공 → Unlocked(푸른색, 닫힘).
            // 여는 것은 침투조가 현장에서 상호작용 키로 한다(5초 뒤 자동으로 닫힘).
            targetWall.Unlock();

            Debug.Log("[WallControlButtonUI] 해킹 성공. 격벽 해금(푸른색) — 현장에서 열 수 있음");
        }
        else
        {
            Debug.Log("[WallControlButtonUI] 해킹 실패. 격벽 유지");
        }

        RefreshUI();
    }

    /// <summary>
    /// 실제 격벽 상태에 맞춰 UI 색상 갱신
    /// </summary>
    public void RefreshUI()
    {
        if (wallImage == null)
        {
            return;
        }

        if (targetWall == null)
        {
            wallImage.color = Color.gray;
            SetOverlays(false, false);
            return;
        }

        if (!targetWall.IsUnlocked)
        {
            wallImage.color = hackingLockedColor;
            SetOverlays(false, true);
            return;
        }

        if (targetWall.IsOpen)
        {
            // 열림 = 초록. 바탕은 흐리게 두고(클릭 영역은 유지) 얇은 막대를 얹어 통로가 뚫린 모양으로 보이게 한다.
            Color backdrop = openColor;
            backdrop.a = openBackdropAlpha;
            wallImage.color = backdrop;

            if (_openBar != null || EnsureOpenBar())
            {
                _openBar.color = openColor;
            }

            SetOverlays(true, false);
            return;
        }

        // 해금된 격벽은 닫혀 있어도 파랑이다(잠김만 빨강, 열림만 초록).
        wallImage.color = unlockedColor;
        SetOverlays(false, false);
    }

    private void SetOverlays(bool showOpenBar, bool showLock)
    {
        if (_openBar != null)
        {
            _openBar.gameObject.SetActive(showOpenBar);
        }

        if (showLock && lockIcon != null)
        {
            EnsureLockImage();
        }

        if (_lockImage != null)
        {
            _lockImage.gameObject.SetActive(showLock);
        }
    }

    // 버튼 가운데에 길이 방향으로 뻗은 얇은 막대. 버튼 모양(가로/세로)에 따라 짧은 변 쪽을 얇게 한다.
    private bool EnsureOpenBar()
    {
        RectTransform parent = wallImage.rectTransform;
        Vector2 size = parent.rect.size;

        if (size.x <= 0f || size.y <= 0f)
        {
            size = parent.sizeDelta;
        }

        float half = openBarThickness * 0.5f;
        bool vertical = size.x <= size.y;

        GameObject bar = new GameObject("OpenBar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        bar.layer = wallImage.gameObject.layer;

        RectTransform rect = (RectTransform)bar.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = vertical ? new Vector2(0.5f - half, 0f) : new Vector2(0f, 0.5f - half);
        rect.anchorMax = vertical ? new Vector2(0.5f + half, 1f) : new Vector2(1f, 0.5f + half);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        _openBar = bar.GetComponent<Image>();
        _openBar.raycastTarget = false; // 더블클릭은 버튼 본체가 받는다

        return true;
    }

    private void EnsureLockImage()
    {
        if (_lockImage != null)
        {
            return;
        }

        GameObject icon = new GameObject("LockIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        icon.layer = wallImage.gameObject.layer;

        RectTransform rect = (RectTransform)icon.transform;
        rect.SetParent(wallImage.rectTransform, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(lockIconSize, lockIconSize);

        _lockImage = icon.GetComponent<Image>();
        _lockImage.sprite = lockIcon;
        _lockImage.preserveAspect = true;
        _lockImage.raycastTarget = false;
    }
}

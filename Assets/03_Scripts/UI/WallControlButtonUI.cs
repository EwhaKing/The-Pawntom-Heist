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
    [Tooltip("열림 = 민트 #10FFB8")]
    [SerializeField] private Color openColor = new Color(0.063f, 1f, 0.722f, 1f);

    [Tooltip("잠김(해금 안 됨) = 코랄 #FF4E56")]
    [SerializeField] private Color hackingLockedColor = new Color(1f, 0.306f, 0.337f, 1f);

    [Tooltip("해금됐지만 닫혀 있을 때 = 파랑 #2F8CFF (대본의 \"푸른색이 됐습니다\")")]
    [SerializeField] private Color unlockedColor = new Color(0.184f, 0.549f, 1f, 1f);

    [Header("Shape")]
    [Tooltip("열림일 때 양 끝에 남기는 문틀 조각의 길이(버튼 긴 변 대비 비율). 가운데는 뚫려 보인다")]
    [SerializeField, Range(0.1f, 0.45f)] private float openEndFraction = 0.25f;

    [Tooltip("열림일 때 가운데 빈 자리의 바탕 투명도. 클릭 영역은 그대로 남는다")]
    [SerializeField, Range(0f, 0.5f)] private float openGapAlpha = 0.08f;

    [Tooltip("버튼을 모서리 둥근 알약 모양 + 윗면 광택으로 그린다. 끄면 예전 각진 사각형")]
    [SerializeField] private bool roundedStyle = true;

    [Tooltip("잠김일 때 버튼 위에 띄울 자물쇠 아이콘. 비우면 표시하지 않는다")]
    [SerializeField] private Sprite lockIcon;

    [Tooltip("자물쇠 아이콘 크기(UI 단위). 격벽 버튼이 가로로 긴 20 높이라 작게 잡는다")]
    [SerializeField, Min(8f)] private float lockIconSize = 24f;

    /// <summary>미해금 격벽 버튼을 눌러 해킹을 시작하려 할 때(튜토리얼 안내가 어느 문인지 알아야 한다).</summary>
    public static event System.Action<ControllableWall> HackRequested;

    private Image _openBar;   // 열림: 한쪽 끝 문틀 조각
    private Image _openBar2;  // 열림: 반대쪽 끝 문틀 조각
    private Image _lockImage;
    private Image _sheen;

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

        ApplyRoundedStyle();
        RefreshUI();
    }

    // 단색 사각형 대신 둥근 알약 + 윗면 광택. 열림 상태의 양 끝 조각도 같은 둥근 스프라이트를 쓴다.
    private void ApplyRoundedStyle()
    {
        if (!roundedStyle || wallImage == null)
        {
            return;
        }

        wallImage.sprite = RoundedShapes.RoundedSprite;
        wallImage.type = Image.Type.Sliced;

        if (_sheen != null)
        {
            return;
        }

        GameObject sheen = new GameObject("Sheen", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        sheen.layer = wallImage.gameObject.layer;

        RectTransform rect = (RectTransform)sheen.transform;
        rect.SetParent(wallImage.rectTransform, false);
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.offsetMin = new Vector2(4f, 0f);
        rect.offsetMax = new Vector2(-4f, -2f);

        _sheen = sheen.GetComponent<Image>();
        _sheen.sprite = RoundedShapes.RoundedSprite;
        _sheen.type = Image.Type.Sliced;
        _sheen.color = new Color(1f, 1f, 1f, 0.28f);
        _sheen.raycastTarget = false;
    }

    private static readonly System.Collections.Generic.List<WallControlButtonUI> ActiveButtons =
        new System.Collections.Generic.List<WallControlButtonUI>(8);

    /// <summary>지금 켜져 있는(보이는 탭의) 격벽 버튼들. 튜토리얼 안내 틀이 어느 버튼을 가리킬지 고를 때 읽는다.</summary>
    public static System.Collections.Generic.IReadOnlyList<WallControlButtonUI> Active => ActiveButtons;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetActive()
    {
        ActiveButtons.Clear();
    }

    /// <summary>이 버튼이 조작하는 실제 격벽.</summary>
    public ControllableWall TargetWall => targetWall;

    private void OnEnable()
    {
        RefreshUI();

        if (!ActiveButtons.Contains(this))
        {
            ActiveButtons.Add(this);
        }

        if (targetWall != null)
        {
            targetWall.OnStateChanged += RefreshUI;
        }
    }

    private void OnDisable()
    {
        ActiveButtons.Remove(this);

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

        HackRequested?.Invoke(targetWall);

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
            // 열림 = 초록. 가운데를 비워(바탕은 거의 투명, 클릭 영역은 유지) 양 끝 문틀 조각만 남겨
            // 닫힌 파란 꽉 찬 막대와 한눈에 구분되게 한다.
            Color backdrop = openColor;
            backdrop.a = openGapAlpha;
            wallImage.color = backdrop;

            if (_openBar != null || EnsureOpenBar())
            {
                _openBar.color = openColor;
                _openBar2.color = openColor;
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
        // 열림은 가운데가 뚫린 모양이라 광택 줄이 어울리지 않는다.
        if (_sheen != null)
        {
            _sheen.gameObject.SetActive(!showOpenBar);
        }

        if (_openBar != null)
        {
            _openBar.gameObject.SetActive(showOpenBar);
            _openBar2.gameObject.SetActive(showOpenBar);
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

    // 열린 문 모양: 긴 변의 양 끝에만 조각을 남기고 가운데는 비운다. 버튼 모양(가로/세로)에 맞춰 긴 변 방향으로 놓는다.
    private bool EnsureOpenBar()
    {
        RectTransform parent = wallImage.rectTransform;
        Vector2 size = parent.rect.size;

        if (size.x <= 0f || size.y <= 0f)
        {
            size = parent.sizeDelta;
        }

        bool vertical = size.x <= size.y;
        float f = openEndFraction;

        _openBar = CreateEndPiece("OpenEndA", parent, vertical, 0f, f);
        _openBar2 = CreateEndPiece("OpenEndB", parent, vertical, 1f - f, 1f);

        return true;
    }

    private Image CreateEndPiece(string pieceName, RectTransform parent, bool vertical, float from, float to)
    {
        GameObject piece = new GameObject(pieceName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        piece.layer = wallImage.gameObject.layer;

        RectTransform rect = (RectTransform)piece.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = vertical ? new Vector2(0f, from) : new Vector2(from, 0f);
        rect.anchorMax = vertical ? new Vector2(1f, to) : new Vector2(to, 1f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = piece.GetComponent<Image>();

        if (roundedStyle)
        {
            image.sprite = RoundedShapes.RoundedSprite;
            image.type = Image.Type.Sliced;
        }

        image.raycastTarget = false; // 더블클릭은 버튼 본체가 받는다
        return image;
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

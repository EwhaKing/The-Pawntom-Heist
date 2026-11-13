using UnityEngine;

/// <summary>
/// MainbaseControlUI
///
/// 담당:
/// - Mainbase 조작 기계 UI 열기/닫기
/// - CCTV 화면 표시
/// - UI 안의 특정 버튼을 누르면 해킹 미니게임 실행
///
/// 사용 위치:
/// - HUDCanvas > MainbaseControlUI 오브젝트에 붙임
/// </summary>
public class MainbaseControlUI : MonoBehaviour
{
    /// <summary>본부 터미널 탭. Position 이 CCTV(팀원·적 위치) 탭이다.</summary>
    public enum Tab
    {
        Position,
        Wall,
        Sprinkler,
        Light
    }

    /// <summary>터미널 UI가 열렸을 때. 튜토리얼 안내 등 외부 관찰용.</summary>
    public static event System.Action Opened;

    /// <summary>터미널 UI가 닫혔을 때.</summary>
    public static event System.Action Closed;

    /// <summary>탭이 바뀌었을 때.</summary>
    public static event System.Action<Tab> TabShown;

    public bool IsOpen => root != null && root.activeSelf;

    public Tab CurrentTab { get; private set; }

    /// <summary>탭 버튼의 위치(튜토리얼 안내 틀용). 버튼을 못 찾았으면 null.</summary>
    public RectTransform GetTabRect(Tab tab)
    {
        UnityEngine.UI.Button button = tab switch
        {
            Tab.Position => positionTabButton,
            Tab.Wall => wallTabButton,
            Tab.Sprinkler => sprinklerTabButton,
            _ => lightTabButton
        };

        return button != null ? (RectTransform)button.transform : null;
    }

    [Header("Root")]
    [SerializeField] private GameObject root;

    [Header("Map Layers")]
    [SerializeField] private GameObject layerPosition;
    [SerializeField] private GameObject layerWall;
    [SerializeField] private GameObject layerSprinkler;
    [SerializeField] private GameObject layerLight;

    [Header("Guide")]
    [SerializeField] private TMPro.TextMeshProUGUI guideText;

    [Header("Hacking")]
    [SerializeField] private Hacking.HackingManager hackingManager;
    [SerializeField] private Hacking.SecurityLevel hackingLevel = Hacking.SecurityLevel.Normal;

    [Header("Style")]
    [Tooltip("켜면 창·탭·글자 색을 TerminalPalette(미니게임 UI 팔레트)로 칠하고 테두리 장식을 단다")]
    [SerializeField] private bool applyTerminalStyle = true;

    [Header("Tab Buttons (비워두면 onClick 연결을 보고 자동 탐색)")]
    [SerializeField] private UnityEngine.UI.Button positionTabButton;
    [SerializeField] private UnityEngine.UI.Button wallTabButton;
    [SerializeField] private UnityEngine.UI.Button sprinklerTabButton;
    [SerializeField] private UnityEngine.UI.Button lightTabButton;

    [Tooltip("현재 탭이 아닌 탭 버튼에 입히는 색 (원래 색에 곱해짐)")]
    [SerializeField] private Color inactiveTabTint = new Color(0.45f, 0.45f, 0.45f, 1f);

    private readonly System.Collections.Generic.Dictionary<UnityEngine.UI.Graphic, Color> tabOriginalColors =
        new System.Collections.Generic.Dictionary<UnityEngine.UI.Graphic, Color>();

    private void Awake()
    {
        if (root == null)
        {
            root = gameObject;
        }

        if (applyTerminalStyle)
        {
            ApplyTerminalStyle();
        }

        FindTabButtons();

        if (hackingManager == null)
        {
            hackingManager = FindFirstObjectByType<Hacking.HackingManager>();
        }

        Close();
    }

    private void Update()
    {
        if (root != null && root.activeSelf && Input.GetKeyDown(KeyCode.Escape))
        {
            Close();
        }
    }

    /// <summary>
    /// Mainbase 조작 UI를 엶
    /// ControlMachine에서 호출
    /// </summary>
    public void Open()
    {
        if (PlayerData.IsLocalPlayerDead) return;
        if (root != null)
        {
            root.SetActive(true);
        }
    
        ShowPositionTab();

        GameplayInputBlocker.SetBlocked(true);

        Debug.Log("[MainbaseControlUI] UI 열림");

        Opened?.Invoke();
    }

    /// <summary>
    /// Mainbase 조작 UI를 닫음
    /// </summary>
    public void Close()
    {
        // Awake 에서도 Close 를 부르므로, 실제로 열려 있다가 닫힐 때만 Closed 를 알린다.
        bool wasOpen = IsOpen;

        if (root != null)
        {
            root.SetActive(false);
        }

        GameplayInputBlocker.SetBlocked(false);

        Debug.Log("[MainbaseControlUI] UI 닫힘");

        if (wasOpen)
        {
            Closed?.Invoke();
        }
    }

    /// <summary>
    /// CCTV UI 안의 특정 조작 버튼을 눌렀을 때 호출
    /// 예: 격벽 조작 버튼, CCTV 해금 버튼 등
    /// </summary>
    public void OnClickHackButton()
    {
        if (hackingManager == null)
        {
            hackingManager = FindFirstObjectByType<Hacking.HackingManager>();
        }

        if (hackingManager == null)
        {
            Debug.LogWarning("[MainbaseControlUI] HackingManager가 없습니다.");
            return;
        }

        Debug.Log("[MainbaseControlUI] 해킹 미니게임 시작");

        hackingManager.OpenHackingPopup(hackingLevel, OnHackFinished);
    }

    /// <summary>
    /// 해킹 미니게임 결과 처리
    /// </summary>
    private void OnHackFinished(bool isSuccess)
    {
        if (isSuccess)
        {
            Debug.Log("[MainbaseControlUI] 해킹 성공. 기능 작동!");
        }
        else
        {
            Debug.Log("[MainbaseControlUI] 해킹 실패. 기능 작동 안 함");
        }
    }

    public void ShowPositionTab()
    {
        SetLayer(layerPosition);
        NotifyTab(Tab.Position);

        if (guideText != null)
        {
            guideText.text = "팀원과 적의 위치를 확인합니다.";
        }
    }

    public void ShowWallTab()
    {
        SetLayer(layerWall);
        NotifyTab(Tab.Wall);

        if (guideText != null)
        {
            guideText.text = "격벽을 선택하세요. 붉은 격벽은 해킹 후 조작할 수 있습니다.";
        }
    }

    public void ShowSprinklerTab()
    {
        SetLayer(layerSprinkler);
        NotifyTab(Tab.Sprinkler);

        if (guideText != null)
        {
            guideText.text = "스프링클러를 작동시킬 구역을 선택하세요.";
        }
    }

    public void ShowLightTab()
    {
        SetLayer(layerLight);
        NotifyTab(Tab.Light);

        if (guideText != null)
        {
            guideText.text = "전등을 조작할 구역을 선택하세요.";
        }
    }

    // ───────────── 단말기 스타일(미니게임 UI 팔레트) ─────────────

    private const string DecorName = "TerminalDecor";

    private Transform FindChildDeep(string childName)
    {
        Transform start = root != null ? root.transform : transform;

        foreach (Transform t in start.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == childName)
            {
                return t;
            }
        }

        return null;
    }

    private void TintImage(string childName, Color color)
    {
        Transform t = FindChildDeep(childName);
        UnityEngine.UI.Image image = t != null ? t.GetComponent<UnityEngine.UI.Image>() : null;

        if (image != null)
        {
            image.color = color;
        }
    }

    private void TintText(string childName, Color color, bool bold = false)
    {
        Transform t = FindChildDeep(childName);
        TMPro.TMP_Text text = t != null ? t.GetComponent<TMPro.TMP_Text>() : null;

        if (text != null)
        {
            text.color = color;

            if (bold)
            {
                text.fontStyle |= TMPro.FontStyles.Bold;
            }
        }
    }

    /// <summary>
    /// 씬의 단순 흰/회색 UI 를 해킹 미니게임 UI 에셋 팔레트로 바꾼다. 배치·크기는 건드리지 않고 색과 장식만 얹는다.
    /// 장식(테두리·모서리 괄호·헤더 선)은 한 번만 만든다.
    /// </summary>
    private void ApplyTerminalStyle()
    {
        TintImage("DimBackground", new Color(TerminalPalette.Black.r, TerminalPalette.Black.g, TerminalPalette.Black.b, 0.86f));
        TintImage("WindowPanel", TerminalPalette.Dark);
        TintImage("WindowHeader", TerminalPalette.Black);
        TintImage("MapFrame", TerminalPalette.Charcoal);
        TintImage("MapViewport", TerminalPalette.Dark);

        TintText("TitleText", TerminalPalette.Mint, true);
        TintText("ZoomText", TerminalPalette.Light);
        TintText("GuideText", TerminalPalette.Text);

        // 닫기 버튼은 경고색, 탭은 민트(활성) — 비활성 탭은 RefreshTabButtons 가 어둡게 한다.
        TintImage("CloseButton", TerminalPalette.Coral);
        Transform close = FindChildDeep("CloseButton");

        if (close != null)
        {
            foreach (TMPro.TMP_Text text in close.GetComponentsInChildren<TMPro.TMP_Text>(true))
            {
                text.color = TerminalPalette.Black;
            }
        }

        foreach (string tabName in new[] { "Tab_Position", "Tab_Wall", "Tab_Sprinkler", "Tab_Light" })
        {
            TintImage(tabName, TerminalPalette.Mint);
            Transform tab = FindChildDeep(tabName);

            if (tab == null)
            {
                continue;
            }

            foreach (TMPro.TMP_Text text in tab.GetComponentsInChildren<TMPro.TMP_Text>(true))
            {
                text.color = TerminalPalette.Black;
                text.fontStyle |= TMPro.FontStyles.Bold;
            }
        }

        Transform panel = FindChildDeep("WindowPanel");
        Transform header = FindChildDeep("WindowHeader");
        Transform mapFrame = FindChildDeep("MapFrame");

        if (panel != null && panel.Find(DecorName) == null)
        {
            AddOutline(panel, TerminalPalette.Mid, 3f);
            AddCornerBrackets(panel as RectTransform, TerminalPalette.Mint, 26f, 3f, 8f);
        }

        if (header != null && header.Find(DecorName) == null)
        {
            // 헤더 아래 민트 선.
            RectTransform line = NewDecor(header as RectTransform, TerminalPalette.Mint);
            line.anchorMin = new Vector2(0f, 0f);
            line.anchorMax = new Vector2(1f, 0f);
            line.pivot = new Vector2(0.5f, 0f);
            line.offsetMin = new Vector2(0f, 0f);
            line.offsetMax = new Vector2(0f, 2f);
        }

        if (mapFrame != null && mapFrame.Find(DecorName) == null)
        {
            AddOutline(mapFrame, TerminalPalette.Light, 2f);
            AddCornerBrackets(mapFrame as RectTransform, TerminalPalette.Mint, 20f, 2f, 0f);
        }
    }

    private static void AddOutline(Transform target, Color color, float distance)
    {
        UnityEngine.UI.Graphic graphic = target.GetComponent<UnityEngine.UI.Graphic>();

        if (graphic == null || target.GetComponent<UnityEngine.UI.Outline>() != null)
        {
            return;
        }

        UnityEngine.UI.Outline outline = target.gameObject.AddComponent<UnityEngine.UI.Outline>();
        outline.effectColor = color;
        outline.effectDistance = new Vector2(distance, -distance);
    }

    private static RectTransform NewDecor(RectTransform parent, Color color)
    {
        GameObject go = new GameObject(DecorName, typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
        go.layer = parent.gameObject.layer;

        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);

        UnityEngine.UI.Image image = go.GetComponent<UnityEngine.UI.Image>();
        image.color = color;
        image.raycastTarget = false;

        return rect;
    }

    /// <summary>네 모서리에 「 」 모양 괄호를 단다(미니게임 키 UI 의 모서리 표시와 같은 모양).</summary>
    private static void AddCornerBrackets(RectTransform parent, Color color, float length, float thickness, float inset)
    {
        if (parent == null)
        {
            return;
        }

        for (int cx = 0; cx <= 1; cx++)
        {
            for (int cy = 0; cy <= 1; cy++)
            {
                float sx = cx == 0 ? 1f : -1f; // 안쪽으로 향하는 방향
                float sy = cy == 0 ? 1f : -1f;
                Vector2 anchor = new Vector2(cx, cy);

                // 가로 변
                RectTransform h = NewDecor(parent, color);
                h.anchorMin = h.anchorMax = anchor;
                h.pivot = new Vector2(cx == 0 ? 0f : 1f, cy == 0 ? 0f : 1f);
                h.sizeDelta = new Vector2(length, thickness);
                h.anchoredPosition = new Vector2(sx * inset, sy * inset);

                // 세로 변
                RectTransform v = NewDecor(parent, color);
                v.anchorMin = v.anchorMax = anchor;
                v.pivot = h.pivot;
                v.sizeDelta = new Vector2(thickness, length);
                v.anchoredPosition = new Vector2(sx * inset, sy * inset);
            }
        }
    }

    private void NotifyTab(Tab tab)
    {
        CurrentTab = tab;
        RefreshTabButtons();
        TabShown?.Invoke(tab);
    }

    /// <summary>탭 버튼이 비어 있으면 자식 Button 의 onClick 대상 메서드 이름으로 찾는다.</summary>
    private void FindTabButtons()
    {
        if (positionTabButton != null && wallTabButton != null
            && sprinklerTabButton != null && lightTabButton != null)
        {
            return;
        }

        foreach (var button in GetComponentsInChildren<UnityEngine.UI.Button>(true))
        {
            var onClick = button.onClick;
            for (int i = 0; i < onClick.GetPersistentEventCount(); i++)
            {
                if (onClick.GetPersistentTarget(i) != this) continue;

                switch (onClick.GetPersistentMethodName(i))
                {
                    case nameof(ShowPositionTab):
                        if (positionTabButton == null) positionTabButton = button;
                        break;
                    case nameof(ShowWallTab):
                        if (wallTabButton == null) wallTabButton = button;
                        break;
                    case nameof(ShowSprinklerTab):
                        if (sprinklerTabButton == null) sprinklerTabButton = button;
                        break;
                    case nameof(ShowLightTab):
                        if (lightTabButton == null) lightTabButton = button;
                        break;
                }
            }
        }
    }

    private void RefreshTabButtons()
    {
        ApplyTabTint(positionTabButton, CurrentTab == Tab.Position);
        ApplyTabTint(wallTabButton, CurrentTab == Tab.Wall);
        ApplyTabTint(sprinklerTabButton, CurrentTab == Tab.Sprinkler);
        ApplyTabTint(lightTabButton, CurrentTab == Tab.Light);
    }

    private void ApplyTabTint(UnityEngine.UI.Button button, bool isCurrent)
    {
        if (button == null) return;

        foreach (var graphic in button.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
        {
            if (!tabOriginalColors.TryGetValue(graphic, out var original))
            {
                original = graphic.color;
                tabOriginalColors[graphic] = original;
            }

            graphic.color = isCurrent ? original : original * inactiveTabTint;
        }
    }

    private void SetLayer(GameObject targetLayer)
    {
        if (layerPosition != null)
        {
            layerPosition.SetActive(layerPosition == targetLayer);
        }

        if (layerWall != null)
        {
            layerWall.SetActive(layerWall == targetLayer);
        }

        if (layerSprinkler != null)
        {
            layerSprinkler.SetActive(layerSprinkler == targetLayer);
        }

        if (layerLight != null)
        {
            layerLight.SetActive(layerLight == targetLayer);
        }
    }
}

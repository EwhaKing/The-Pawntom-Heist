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

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

    private void Awake()
    {
        if (root == null)
        {
            root = gameObject;
        }

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
        TabShown?.Invoke(tab);
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
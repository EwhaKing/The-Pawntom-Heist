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

    [Header("Colors")]
    [SerializeField] private Color closedColor = new Color(1f, 0.15f, 0.1f, 1f);
    [SerializeField] private Color openColor = new Color(0.1f, 0.55f, 1f, 1f);
    [SerializeField] private Color hackingLockedColor = new Color(1f, 0.15f, 0.1f, 1f);

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
            targetWall.Unlock();

            // 성공하면 즉시 열림 상태로 변경
            targetWall.OpenWall();

            Debug.Log("[WallControlButtonUI] 해킹 성공. 격벽 해금 및 열림");
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
            return;
        }

        if (!targetWall.IsUnlocked)
        {
            wallImage.color = hackingLockedColor;
            return;
        }

        wallImage.color = targetWall.IsOpen ? openColor : closedColor;
    }
}

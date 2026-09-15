using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// SprinklerControlButtonUI
///
/// 담당:
/// - CCTV 스프링클러 탭의 스프링클러 아이콘 버튼 관리
/// - 침투자가 해당 스프링클러 구역에 접촉하기 전까지 버튼을 숨김
/// - 버튼 클릭 시 해킹 미니게임 실행
/// - 해킹 성공 시 SprinklerZone.ActivateSprinkler 호출
/// - 작동 중에는 보라색 구역 표시 UI를 켬
/// - 이미 사용한 스프링클러는 회색으로 표시하고 재사용 불가 처리
///
/// 사용 위치:
/// - MainbaseControlUI > MapContent > Layer_Sprinkler > SprinklerButton_01에 붙임
/// </summary>
public class SprinklerControlButtonUI : MonoBehaviour, IPointerClickHandler
{
    [Header("Target")]
    [SerializeField] private SprinklerZone targetZone;

    [Header("Discovery")]
    [SerializeField] private DiscoverableArea targetArea;

    [Header("UI")]
    [SerializeField] private Image iconImage;
    [SerializeField] private CanvasGroup canvasGroup;

    [Tooltip("작동 중 CCTV 지도에 표시할 보라색 반투명 구역 UI")]
    [SerializeField] private GameObject activeAreaOverlay;

    [Header("Hacking")]
    [SerializeField] private Hacking.HackingManager hackingManager;
    [SerializeField] private Hacking.SecurityLevel hackingLevel = Hacking.SecurityLevel.Normal;

    [Header("Colors")]
    [SerializeField] private Color readyColor = new Color(0.45f, 0.15f, 1f, 1f);
    [SerializeField] private Color usedColor = Color.gray;
    [SerializeField] private Color runningColor = new Color(0.7f, 0.45f, 1f, 1f);

    private bool isHacking;

    private void Awake()
    {
        if (iconImage == null)
        {
            iconImage = GetComponent<Image>();
        }

        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        if (hackingManager == null)
        {
            hackingManager = FindFirstObjectByType<Hacking.HackingManager>();
        }

        RefreshUI();
    }

    private void Update()
    {
        RefreshUI();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (targetZone == null)
        {
            Debug.LogWarning("[SprinklerControlButtonUI] Target Zone이 연결되지 않았습니다.");
            return;
        }

        if (targetArea == null)
        {
            Debug.LogWarning("[SprinklerControlButtonUI] Target Area가 연결되지 않았습니다.");
            return;
        }

        // 구역이 아직 발견되지 않았다면 클릭해도 해킹 불가
        if (!targetArea.IsDiscovered)
        {
            Debug.Log("[SprinklerControlButtonUI] 아직 발견되지 않은 구역입니다.");
            return;
        }

        if (targetZone.IsUsed)
        {
            Debug.Log("[SprinklerControlButtonUI] 이미 사용한 스프링클러입니다.");
            return;
        }

        if (isHacking)
        {
            Debug.Log("[SprinklerControlButtonUI] 이미 해킹 진행 중입니다.");
            return;
        }

        StartHacking();
    }

    private void StartHacking()
    {
        if (hackingManager == null)
        {
            hackingManager = FindFirstObjectByType<Hacking.HackingManager>();
        }

        if (hackingManager == null)
        {
            Debug.LogWarning("[SprinklerControlButtonUI] HackingManager를 찾지 못했습니다.");
            return;
        }

        isHacking = true;

        Debug.Log("[SprinklerControlButtonUI] 스프링클러 해킹 시작");

        hackingManager.OpenHackingPopup(hackingLevel, OnHackingFinished);
    }

    private void OnHackingFinished(bool isSuccess)
    {
        isHacking = false;

        if (targetZone == null)
        {
            return;
        }

        if (isSuccess)
        {
            Debug.Log("[SprinklerControlButtonUI] 해킹 성공. 스프링클러 작동");

            targetZone.ActivateSprinkler();
        }
        else
        {
            Debug.Log("[SprinklerControlButtonUI] 해킹 실패. 스프링클러 미작동");
        }

        RefreshUI();
    }

    private void RefreshUI()
    {
        if (targetZone == null || targetArea == null)
        {
            SetVisible(false);

            if (activeAreaOverlay != null)
            {
                activeAreaOverlay.SetActive(false);
            }

            return;
        }

        // 구역 발견 전에는 스프링클러 버튼 숨김
        if (!targetArea.IsDiscovered)
        {
            SetVisible(false);

            if (activeAreaOverlay != null)
            {
                activeAreaOverlay.SetActive(false);
            }

            return;
        }

        // 구역 발견 후에는 버튼 표시
        SetVisible(true);

        if (iconImage != null)
        {
            if (targetZone.IsRunning)
            {
                iconImage.color = runningColor;
            }
            else if (targetZone.IsUsed)
            {
                iconImage.color = usedColor;
            }
            else
            {
                iconImage.color = readyColor;
            }
        }

        // 스프링클러 작동 중일 때만 보라색 구역 표시
        if (activeAreaOverlay != null)
        {
            activeAreaOverlay.SetActive(targetZone.IsRunning);
        }
    }
    
    private void SetVisible(bool isVisible)
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = isVisible ? 1f : 0f;
            canvasGroup.interactable = isVisible;
            canvasGroup.blocksRaycasts = isVisible;
        }
        else if (iconImage != null)
        {
            Color color = iconImage.color;
            color.a = isVisible ? 1f : 0f;
            iconImage.color = color;
            iconImage.raycastTarget = isVisible;
        }
    }
}

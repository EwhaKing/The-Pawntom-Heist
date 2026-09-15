using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// LightControlButtonUI
///
/// 담당:
/// - CCTV 전등 탭의 전등 버튼 관리
/// - 구역이 발견되기 전에는 버튼 숨김
/// - 처음 클릭 시 해킹 미니게임 실행
/// - 해킹 성공 시 전등 해금 + 전등 ON/OFF 전환
/// - 해금 후에는 클릭할 때마다 전등 ON/OFF 전환
///
/// 사용 위치:
/// - MainbaseControlUI > MapContent > Page_Light > LightButton_01에 붙임
/// </summary>
public class LightControlButtonUI : MonoBehaviour, IPointerClickHandler
{
    [Header("Target")]
    [SerializeField] private ControllableLight targetLight;

    [Header("Discovery")]
    [SerializeField] private DiscoverableArea targetArea;

    [Header("UI")]
    [SerializeField] private Image iconImage;
    [SerializeField] private CanvasGroup canvasGroup;

    [Tooltip("전등이 꺼졌을 때 CCTV 지도 위에 표시할 어두운 구역 UI입니다. 없어도 됩니다.")]
    [SerializeField] private GameObject darkAreaOverlay;

    [Header("Hacking")]
    [SerializeField] private Hacking.HackingManager hackingManager;
    [SerializeField] private Hacking.SecurityLevel hackingLevel = Hacking.SecurityLevel.Normal;

    [Header("Colors")]
    [SerializeField] private Color lockedColor = new Color(1f, 0.85f, 0.1f, 1f);
    [SerializeField] private Color lightOnColor = new Color(1f, 0.95f, 0.2f, 1f);
    [SerializeField] private Color lightOffColor = new Color(0.35f, 0.35f, 0.35f, 1f);
    [SerializeField] private Color hackingColor = new Color(0.75f, 0.45f, 1f, 1f);

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

    /// <summary>
    /// 전등 버튼 클릭 처리
    /// </summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (targetLight == null)
        {
            Debug.LogWarning("[LightControlButtonUI] Target Light가 연결되지 않았습니다.");
            return;
        }

        if (targetArea == null)
        {
            Debug.LogWarning("[LightControlButtonUI] Target Area가 연결되지 않았습니다.");
            return;
        }

        if (!targetArea.IsDiscovered)
        {
            Debug.Log("[LightControlButtonUI] 아직 발견되지 않은 구역입니다.");
            return;
        }

        if (isHacking)
        {
            Debug.Log("[LightControlButtonUI] 이미 해킹 진행 중입니다.");
            return;
        }

        // 처음 조작하는 전등이면 해킹부터 진행
        if (!targetLight.IsUnlocked)
        {
            StartHacking();
            return;
        }

        // 이미 해금된 전등은 클릭으로 ON/OFF 전환
        targetLight.ToggleLight();
        RefreshUI();
    }

    /// <summary>
    /// 전등 해킹 미니게임 시작
    /// </summary>
    private void StartHacking()
    {
        if (hackingManager == null)
        {
            hackingManager = FindFirstObjectByType<Hacking.HackingManager>();
        }

        if (hackingManager == null)
        {
            Debug.LogWarning("[LightControlButtonUI] HackingManager를 찾지 못했습니다.");
            return;
        }

        isHacking = true;

        Debug.Log("[LightControlButtonUI] 전등 해킹 시작");

        hackingManager.OpenHackingPopup(hackingLevel, OnHackingFinished);
    }

    /// <summary>
    /// 해킹 미니게임 결과 처리
    /// </summary>
    private void OnHackingFinished(bool isSuccess)
    {
        isHacking = false;

        if (targetLight == null)
        {
            return;
        }

        if (isSuccess)
        {
            Debug.Log("[LightControlButtonUI] 해킹 성공. 전등 해금 및 상태 전환");

            targetLight.Unlock();

            // 첫 해킹 성공 시 바로 전등 상태를 바꿈
            // 보통 전등이 켜져 있는 상태에서 시작하므로, 성공하면 꺼짐
            targetLight.ToggleLight();
        }
        else
        {
            Debug.Log("[LightControlButtonUI] 해킹 실패. 전등 상태 유지");
        }

        RefreshUI();
    }

    /// <summary>
    /// 전등 상태에 따라 CCTV 버튼 UI를 갱신합니다.
    /// </summary>
    private void RefreshUI()
    {
        if (targetLight == null || targetArea == null)
        {
            SetVisible(false);

            if (darkAreaOverlay != null)
            {
                darkAreaOverlay.SetActive(false);
            }

            return;
        }

        if (!targetArea.IsDiscovered)
        {
            SetVisible(false);

            if (darkAreaOverlay != null)
            {
                darkAreaOverlay.SetActive(false);
            }

            return;
        }

        SetVisible(true);

        if (iconImage != null)
        {
            if (isHacking)
            {
                iconImage.color = hackingColor;
            }
            else if (!targetLight.IsUnlocked)
            {
                iconImage.color = lockedColor;
            }
            else
            {
                iconImage.color = targetLight.IsOn ? lightOnColor : lightOffColor;
            }
        }

        if (darkAreaOverlay != null)
        {
            darkAreaOverlay.SetActive(targetLight.IsUnlocked && !targetLight.IsOn);
        }
    }

    /// <summary>
    /// 버튼 표시/클릭 가능 상태 변경
    /// </summary>
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
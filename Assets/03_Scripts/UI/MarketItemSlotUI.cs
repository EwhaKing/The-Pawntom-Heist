using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// MarketItemSlotUI
///
/// 담당:
/// - 암시장 아이템 버튼 하나를 표시
/// - 아이템 이름 / 가격 / 선택 상태 / 품절 상태 표시
/// - 클릭되면 BlackMarketUI에 선택 요청 전달
///
/// 사용 위치:
/// - ShopItemSlot_0 ~ ShopItemSlot_5 같은 Button 오브젝트에 붙임
/// </summary>
public class MarketItemSlotUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Button button;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI priceText;
    [SerializeField] private GameObject selectedOutline;
    [SerializeField] private GameObject soldOutText;

    [Header("Colors")]
    [SerializeField] private Color normalColor = new Color(0.75f, 0.75f, 0.75f, 1f);
    [SerializeField] private Color selectedColor = new Color(0.65f, 1f, 0.72f, 1f);
    [SerializeField] private Color soldOutColor = new Color(0.45f, 0.45f, 0.45f, 1f);

    private MarketUI owner;
    private int slotIndex;
    private MarketItemData itemData;
    private bool isSelected;
    private bool isSoldOut;

    public MarketItemData ItemData => itemData;
    public bool IsSelected => isSelected;
    public bool IsSoldOut => isSoldOut;

    private void Awake()
    {
        if (button == null)
        {
            button = GetComponent<Button>();
        }

        if (backgroundImage == null)
        {
            backgroundImage = GetComponent<Image>();
        }
    }

    /// <summary>
    /// 슬롯을 초기화합니다.
    /// </summary>
    public void Initialize(MarketUI ownerUI, int index)
    {
        owner = ownerUI;
        slotIndex = index;

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnClickSlot);
        }
    }

    /// <summary>
    /// 이 슬롯에 표시할 아이템을 설정합니다.
    /// </summary>
    public void SetItem(MarketItemData data)
    {
        itemData = data;
        isSelected = false;
        isSoldOut = false;

        RefreshUI();
    }

    /// <summary>
    /// 선택 상태를 설정합니다.
    /// </summary>
    public void SetSelected(bool selected)
    {
        isSelected = selected;
        RefreshUI();
    }

    /// <summary>
    /// 구매 완료 후 품절 상태로 변경
    /// </summary>
    public void SetSoldOut()
    {
        isSoldOut = true;
        isSelected = false;
        RefreshUI();
    }

    /// <summary>
    /// 슬롯 클릭 처리.\
    /// </summary>
    private void OnClickSlot()
    {
        if (owner == null)
        {
            Debug.LogWarning("[MarketItemSlotUI] owner가 없습니다.");
            return;
        }

        if (itemData == null)
        {
            Debug.LogWarning("[MarketItemSlotUI] itemData가 없습니다.");
            return;
        }

        if (isSoldOut)
        {
            Debug.Log("[MarketItemSlotUI] 이미 품절된 아이템입니다.");
            return;
        }

        owner.ToggleSelectSlot(slotIndex);
    }

    /// <summary>
    /// 현재 상태에 맞게 UI를 갱신
    /// </summary>
    private void RefreshUI()
    {
        if (nameText != null)
        {
            nameText.text = itemData != null ? itemData.itemName : "Empty";
        }

        if (priceText != null)
        {
            priceText.text = itemData != null ? $"{itemData.price} $" : "";
        }

        if (selectedOutline != null)
        {
            selectedOutline.SetActive(isSelected && !isSoldOut);
        }

        if (soldOutText != null)
        {
            soldOutText.SetActive(isSoldOut);
        }

        if (button != null)
        {
            button.interactable = !isSoldOut && itemData != null;
        }

        if (backgroundImage != null)
        {
            if (isSoldOut)
            {
                backgroundImage.color = soldOutColor;
            }
            else if (isSelected)
            {
                backgroundImage.color = selectedColor;
            }
            else
            {
                backgroundImage.color = normalColor;
            }
        }
    }
}
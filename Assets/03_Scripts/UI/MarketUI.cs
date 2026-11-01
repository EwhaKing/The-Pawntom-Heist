using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// MarketUI
///
/// 담당:
/// - 암시장 UI 열기/닫기
/// - 현재 팀 돈 표시
/// - 판매 아이템 목록 생성
/// - 아이템 선택/해제
/// - 총 구매 금액 계산
/// - 구매 처리
/// - 새로고침 처리
///
/// 현재 버전:
/// - 네트워크 동기화 없음
/// - 테스트용 로컬 돈 사용
/// - 실제 아이템 스폰 대신 Debug.Log만 출력
///
/// 나중에 교체할 부분:
/// - currentMoney → Networked 팀 공용 돈
/// - PurchaseSelectedItems() 내부 → 실제 본부 보관함에 아이템 Spawn
/// </summary>
public class MarketUI : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject root;

    [Header("Money")]
    [SerializeField] private int currentMoney = 168;
    [SerializeField] private TextMeshProUGUI moneyText;

    [Header("Items")]
    [SerializeField] private List<MarketItemData> itemPool = new List<MarketItemData>();
    [SerializeField] private MarketItemSlotUI[] itemSlots;

    [Header("Buttons")]
    [SerializeField] private Button refreshButton;
    [SerializeField] private Button buyButton;
    [SerializeField] private Button closeButton;

    [Header("Text")]
    [SerializeField] private TextMeshProUGUI totalText;

    private readonly HashSet<int> selectedSlotIndexes = new HashSet<int>();

    private void Awake()
    {
        if (root == null)
        {
            root = gameObject;
        }

        InitializeSlots();
        BindButtons();

        Close();
    }

    /// <summary>
    /// 테스트용 단축키
    /// 지금은 B키로 암시장 UI를 열고 닫음
    /// </summary>
    private void Update()
    {
        if (PlayerData.IsLocalPlayerDead) return;
        if (Input.GetKeyDown(KeyCode.B))
        {
            if (root != null && root.activeSelf)
            {
                Close();
            }
            else
            {
                OpenForTest();
            }
        }

        if (root != null && root.activeSelf && Input.GetKeyDown(KeyCode.Escape))
        {
            Close();
        }
    }

    /// <summary>
    /// 슬롯 초기화
    /// </summary>
    private void InitializeSlots()
    {
        if (itemSlots == null)
        {
            return;
        }

        for (int i = 0; i < itemSlots.Length; i++)
        {
            if (itemSlots[i] == null)
            {
                continue;
            }

            itemSlots[i].Initialize(this, i);
        }
    }

    /// <summary>
    /// 버튼 이벤트 연결
    /// </summary>
    private void BindButtons()
    {
        if (refreshButton != null)
        {
            refreshButton.onClick.RemoveAllListeners();
            refreshButton.onClick.AddListener(RefreshShopItems);
        }

        if (buyButton != null)
        {
            buyButton.onClick.RemoveAllListeners();
            buyButton.onClick.AddListener(PurchaseSelectedItems);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Close);
        }
    }

    /// <summary>
    /// 테스트용으로 암시장 UI 열기
    /// </summary>
    public void OpenForTest()
    {
        Open();
        RefreshShopItems();

        Debug.Log("[MarketUI] 테스트용 암시장 UI 열림");
    }

    /// <summary>
    /// 암시장 UI 열기
    /// </summary>
    public void Open()
    {
        if (root != null)
        {
            root.SetActive(true);
        }

        selectedSlotIndexes.Clear();
        RefreshAllSlotSelection();
        RefreshTexts();

        // 플레이어 입력 차단 시스템을 쓰고 있다면 유지
        GameplayInputBlocker.SetBlocked(true);
    }

    /// <summary>
    /// 암시장 UI 닫기
    /// </summary>
    public void Close()
    {
        if (root != null)
        {
            root.SetActive(false);
        }

        selectedSlotIndexes.Clear();

        GameplayInputBlocker.SetBlocked(false);
    }

    /// <summary>
    /// 상점 아이템 목록을 새로 뽑음
    /// 품절 상태도 초기화
    /// </summary>
    public void RefreshShopItems()
    {
        selectedSlotIndexes.Clear();

        if (itemSlots == null || itemSlots.Length == 0)
        {
            Debug.LogWarning("[MarketUI] itemSlots가 없습니다.");
            return;
        }

        if (itemPool == null || itemPool.Count == 0)
        {
            Debug.LogWarning("[MarketUI] itemPool이 비어 있습니다.");
            return;
        }

        List<MarketItemData> shuffledItems = new List<MarketItemData>(itemPool);
        Shuffle(shuffledItems);

        for (int i = 0; i < itemSlots.Length; i++)
        {
            if (itemSlots[i] == null)
            {
                continue;
            }

            MarketItemData data = shuffledItems[i % shuffledItems.Count];
            itemSlots[i].SetItem(data);
        }

        RefreshTexts();

        Debug.Log("[MarketUI] 상점 아이템 새로고침");
    }

    /// <summary>
    /// 슬롯 선택/해제
    /// </summary>
    public void ToggleSelectSlot(int slotIndex)
    {
        if (itemSlots == null)
        {
            return;
        }

        if (slotIndex < 0 || slotIndex >= itemSlots.Length)
        {
            return;
        }

        MarketItemSlotUI slot = itemSlots[slotIndex];

        if (slot == null || slot.ItemData == null || slot.IsSoldOut)
        {
            return;
        }

        if (selectedSlotIndexes.Contains(slotIndex))
        {
            selectedSlotIndexes.Remove(slotIndex);
            slot.SetSelected(false);
        }
        else
        {
            selectedSlotIndexes.Add(slotIndex);
            slot.SetSelected(true);
        }

        RefreshTexts();
    }

    /// <summary>
    /// 선택된 아이템들을 구매
    /// </summary>
    private void PurchaseSelectedItems()
    {
        int totalPrice = GetTotalPrice();

        if (selectedSlotIndexes.Count == 0)
        {
            Debug.Log("[MarketUI] 선택된 아이템이 없습니다.");
            return;
        }

        if (totalPrice > currentMoney)
        {
            Debug.Log($"[MarketUI] 돈이 부족합니다. Total={totalPrice}, Money={currentMoney}");
            return;
        }

        currentMoney -= totalPrice;

        List<int> purchasedIndexes = new List<int>(selectedSlotIndexes);

        for (int i = 0; i < purchasedIndexes.Count; i++)
        {
            int slotIndex = purchasedIndexes[i];

            if (slotIndex < 0 || slotIndex >= itemSlots.Length)
            {
                continue;
            }

            MarketItemSlotUI slot = itemSlots[slotIndex];

            if (slot == null || slot.ItemData == null)
            {
                continue;
            }

            Debug.Log($"[MarketUI] 구매 완료: {slot.ItemData.itemName}");

            // 나중에 여기서 실제 본부 보관함에 아이템을 Spawn
            // SpawnPurchasedItem(slot.ItemData);

            slot.SetSoldOut();
        }

        selectedSlotIndexes.Clear();
        RefreshTexts();

        Debug.Log($"[MarketUI] 구매 완료. 남은 돈={currentMoney}");
    }

    /// <summary>
    /// 선택된 아이템 총액 계산
    /// </summary>
    private int GetTotalPrice()
    {
        int total = 0;

        foreach (int slotIndex in selectedSlotIndexes)
        {
            if (itemSlots == null)
            {
                continue;
            }

            if (slotIndex < 0 || slotIndex >= itemSlots.Length)
            {
                continue;
            }

            MarketItemSlotUI slot = itemSlots[slotIndex];

            if (slot == null || slot.ItemData == null || slot.IsSoldOut)
            {
                continue;
            }

            total += slot.ItemData.price;
        }

        return total;
    }

    /// <summary>
    /// 모든 슬롯 선택 상태 새로고침
    /// </summary>
    private void RefreshAllSlotSelection()
    {
        if (itemSlots == null)
        {
            return;
        }

        for (int i = 0; i < itemSlots.Length; i++)
        {
            if (itemSlots[i] == null)
            {
                continue;
            }

            itemSlots[i].SetSelected(selectedSlotIndexes.Contains(i));
        }
    }

    /// <summary>
    /// 돈 / 총액 / 구매 버튼 상태 갱신
    /// </summary>
    private void RefreshTexts()
    {
        int totalPrice = GetTotalPrice();

        if (moneyText != null)
        {
            moneyText.text = $"{currentMoney} $";
        }

        if (totalText != null)
        {
            totalText.text = $"Total : {totalPrice} $";
        }

        if (buyButton != null)
        {
            buyButton.interactable =
                selectedSlotIndexes.Count > 0 &&
                totalPrice > 0 &&
                totalPrice <= currentMoney;
        }
    }

    /// <summary>
    /// 리스트를 랜덤 순서로 섞음
    /// </summary>
    private void Shuffle<T>(List<T> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int randomIndex = Random.Range(i, list.Count);

            T temp = list[i];
            list[i] = list[randomIndex];
            list[randomIndex] = temp;
        }
    }
}

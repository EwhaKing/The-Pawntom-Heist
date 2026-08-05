using ExitGames.Client.Photon.StructWrapping;
using UnityEngine;

/// <summary>
/// InventoryUI
///
/// 담당:
/// - 로컬 플레이어의 InventoryManager를 UI에 연결
/// - 인벤토리 슬롯 표시
/// - 아이템 아이콘 표시
/// - 현재 선택된 슬롯 강조
///
/// 특징:
/// - UI는 네트워크 상태를 직접 변경하지 않음
/// - InventoryManager의 NetworkArray 값을 읽어서 화면에 표시만 함
/// </summary>
public class InventoryUI : MonoBehaviour
{
    [Header("Slots")]
    [SerializeField] private InventorySlotUI[] slotUIs;

    private InventoryManager targetInventory;

    /// <summary>
    /// UI 시작 시 슬롯 번호와 초기 상태를 설정
    /// </summary>
    private void Start()
    {
        InitializeSlots();
    }

    /// <summary>
    /// 슬롯 UI 초기화
    /// </summary>
    private void InitializeSlots()
    {
        if (slotUIs == null)
            return;

        int count = Mathf.Min(slotUIs.Length, InventoryManager.MaxSlotCount);

        for (int i = 0; i < count; i++)
        {
            if (slotUIs[i] == null)
                continue;
            
            slotUIs[i].SetSlotNumber(i + 1);
            slotUIs[i].ClearItem();
            slotUIs[i].SetSelected(false);
        }
    }

    /// <summary>
    /// 표시할 인벤토리 대상을 설정
    /// 로컬 플레이어가 Spawn된 뒤 InventoryManager에서 호출
    /// </summary>
    public void SetTargetInventory(InventoryManager inventory)
    {
        targetInventory = inventory;
        
        Debug.Log("[InventoryUI] 표시 대상 인벤토리 설정 완료");

        Refresh();
    }

    /// <summary>
    /// 매 프레임 인벤토리 UI를 갱신
    ///
    /// 나중에 최적화가 필요하면 네트워크 값 변경 이벤트 방식으로 바꿀 예정
    /// </summary>
    private void Update()
    {
        Refresh();
    }

    /// <summary>
    /// 인벤토리 슬롯 UI 갱신
    /// </summary>
    private void Refresh()
    {
        if (targetInventory == null)
            return;

        if (slotUIs == null || slotUIs.Length == 0)
            return;

        int count = Mathf.Min(slotUIs.Length, InventoryManager.MaxSlotCount);

        for (int i = 0; i < count; i++)
        {
            if (slotUIs[i] == null)
                continue;
            
            int itemId = targetInventory.Slots[i];

            if (itemId == InventoryManager.EmptySlot)
            {
                slotUIs[i].ClearItem();
            }
            else
            {
                ItemData itemData = ItemDatabase.Instance != null ? ItemDatabase.Instance.GetItemData(itemId) : null;
                
                if (itemData != null)
                {
                    slotUIs[i].SetItem(itemData.itemIcon);
                }
                else
                {
                    slotUIs[i].ClearItem();
                }
            }

            bool isSelected = targetInventory.SelectedSlotIndex == i;
            slotUIs[i].SetSelected(isSelected);
        }
    }
}

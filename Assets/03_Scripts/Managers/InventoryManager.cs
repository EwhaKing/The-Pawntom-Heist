using Fusion;
using Fusion.Editor;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// InventoryManager
/// 
/// 담당:
/// - 각 Player의 아이템 목록 관리
/// - NetworkArray 기반 인벤토리 슬롯 관리
/// - 아이템 추가 / 정렬
/// - 사용 / 버리기 가능 여부 확인, 특정 아이템 보유 여부 확인
/// 
/// 참고:
/// - Player 오브젝트에 붙는 것 기준
/// - 
/// </summary>
public class InventoryManager : NetworkBehaviour
{
    public const int MaxSlotCount = 5;
    // 비어있는 슬롯은 0으로 처리
    public const int EmptySlot = 0;

    [Networked, Capacity(MaxSlotCount)]
    public NetworkArray<int> Slots => default;

    /// <summary>
    /// 특정 itemId를 인벤토리에 가지고 있는지 확인
    /// ex. Catleaf 탈출 조건이 맞는지 확인 
    /// </summary>
    public bool HasItem(int itemId)
    {
        for (int i = 0; i < MaxSlotCount; i++)
        {
            if (Slots[i] == itemId)
                return true;
        }

        return false;
    }

    /// <summary>
    /// 인벤토리에 아이템 추가
    /// 아이템 획득 성공시 StateAuthority에서 호출
    /// </summary>
    public bool TryAddItem(int itemId)
    {
        if (!Object.HasStateAuthority)
            return false;
        
        // 빈 슬롯에 저장
        for (int i = 0; i < MaxSlotCount; i++)
        {
            if (Slots[i] == EmptySlot)
            {
                Slots.Set(i, itemId);

                ItemData data = ItemDatabase.Instance.GetItemData(itemId);
                string itemName = data != null ? data.itemName : itemId.ToString();

                Debug.Log($"[InventoryManager] {itemName}을 훔쳤다!");
                return true;
            }
        }

        // 빈 슬롯이 없을 때
        Debug.Log("[InventoryManager] 인벤토리 가득 찼습니다.");
        return false;
    }

    /// <summary>
    /// 아이템 사용
    /// 소비 아이템이면 사용 후 슬롯에서 제거
    /// </summary>
    public bool TryUseItem(int slotIndex)
    {
        if (!Object.HasStateAuthority)
            return false;
        
        if (slotIndex < 0 || slotIndex >= MaxSlotCount)
            return false;

        int itemId = Slots[slotIndex];

        // 빈 슬롯 사용 불가
        if (itemId == EmptySlot)
            return false;
        
        ItemData data = ItemDatabase.Instance.GetItemData(itemId);

        // ItemData가 없거나 사용 불가 아이템은 사용 불가
        if (data == null || !data.canUse)
        {
            Debug.Log("[InventoryManager] 사용할 수 없는 아이템입니다.");
            return false;
        }

        Debug.Log($"[InventoryManager] {data.itemName} 사용");

        // 사용 후 제거
        if (data.itemType == ItemType.Consumable)
        {
            Slots.Set(slotIndex, EmptySlot);
        }

        return true;
    }

    /// <summary>
    /// 아이템 버리기
    /// canDrop이 false인 아이템은 버릴 수 없음
    /// </summary>
    public bool TryDropItem(int slotIndex)
    {
        if (!Object.HasStateAuthority)
            return false;
        
        if (slotIndex < 0 || slotIndex >= MaxSlotCount)
            return false;

        int itemId = Slots[slotIndex];

        if (itemId == EmptySlot)
            return false;
        
        ItemData data = ItemDatabase.Instance.GetItemData(itemId);

        Slots.Set(slotIndex, EmptySlot);

        Debug.Log($"[InventoryManager] ItemId={itemId} 버림");
        return true;
    }
}
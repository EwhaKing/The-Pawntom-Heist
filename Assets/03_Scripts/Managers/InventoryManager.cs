using Fusion;
//using Fusion.Editor;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// InventoryManager
/// 
/// 담당:
/// - 각 Player의 아이템 목록 관리
/// - NetworkArray 기반 인벤토리 슬롯 관리
/// - 아이템 보유 여부 확인
/// - 아이템 추가 / 정렬
/// - 사용 / 버리기 가능 여부 확인, 특정 아이템 보유 여부 확인
/// 
/// 참고:
/// - Player 오브젝트에 붙는 것 기준
/// </summary>
public class InventoryManager : NetworkBehaviour
{
    public const int MaxSlotCount = 4;
    // 비어있는 슬롯은 0으로 처리
    public const int EmptySlot = 0;

    /// <summary>
    /// 현재 씬에 존재하는 모든 플레이어 InventoryManager 목록
    /// 팀 전체에 아이템 보유 여부를 확인할 때 사용
    /// </summary>
    public static readonly List<InventoryManager> AllInventories = new List<InventoryManager>();

    [Networked, Capacity(MaxSlotCount)]
    public NetworkArray<int> Slots => default;

    [Networked] public int SelectedSlotIndex { get; set; }

    /// <summary>
    /// Fusion에서 Player NetworkObject가 Spawn된 후 호출
    /// 전체 인벤토리 목록에 자신을 등록
    /// 
    /// 추가사항:
    /// 로컬 플레이어의 인벤토리만 UI 연결
    /// </summary>
    public override void Spawned()
    {
        if(!AllInventories.Contains(this))
            AllInventories.Add(this);

        if(Object.HasStateAuthority)
            SelectedSlotIndex = -1;

        
        if (Object.HasInputAuthority)
        {
            InventoryUI inventoryUI = FindFirstObjectByType<InventoryUI>();
            if (inventoryUI != null)
            {
                inventoryUI.SetTargetInventory(this);
            }
            else
            {
                Debug.LogWarning("[InventoryManager] 씬에서 InventoryUI를 찾지 못했습니다.");
            }
        }
    }

    /// <summary>
    /// Fusion에서 Player NetworkObject가 Despawn될 때 호출
    /// 전체 인벤토리 목록에서 자신을 제거
    /// </summary>
    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (AllInventories.Contains(this))
            AllInventories.Remove(this);
    }

    /// <summary>
    /// Fusion 네트워크 Tick마다 호출
    /// 숫자키 1~4 입력이 들어오면 현재 선택 슬롯 변경
    /// </summary>
    public override void FixedUpdateNetwork()
    {
        if (!GetInput(out NetworkInputData input))
        {
            return;
        }

        // -1이면 이번 Tick에 슬롯 선택 입력이 없다는 뜻
        if (input.SelectedSlotIndex >= 0)
        {
            ToggleSlot(input.SelectedSlotIndex);
            return;
        }

        if (input.SlotScrollDirection != 0)
        {
            MoveSelectedSlot(input.SlotScrollDirection);
        }
    }

    /// <summary>
    /// 슬롯을 선택하거나, 이미 선택된 슬롯을 선택 해제
    /// </summary>
    public void ToggleSlot(int slotIndex)
    {
        if (!Object.HasStateAuthority)
        {
            return;
        }

        if (slotIndex < 0 || slotIndex >= MaxSlotCount)
        {
            Debug.LogWarning($"[InventoryManager] 잘못된 슬롯 번호입니다: {slotIndex}");
            return;
        }

        if (SelectedSlotIndex == slotIndex)
        {
            SelectedSlotIndex = -1;
            Debug.Log("[InventoryManager] 슬롯 선택 해제. 맨손 상태");
            return;
        }

        SelectedSlotIndex = slotIndex;

        Debug.Log($"[InventoryManager] 선택 슬롯 변경: {SelectedSlotIndex + 1}번 슬롯");
    }

    /// <summary>
    /// 현재 선택 슬롯을 좌우로 이동
    /// 
    /// 아무 슬롯도 선택하지 않은 상태에서 움직이면
    /// 아래 방향은 1번 슬롯, 위 방향은 4번 슬롯 선택
    /// </summary>
    private void MoveSelectedSlot(int direction)
    {
        if (!Object.HasStateAuthority)
            return;
        
        if (SelectedSlotIndex < 0)
        {
            if (direction > 0)
            {
                SelectedSlotIndex = 0;
            }
            else
            {
                SelectedSlotIndex = MaxSlotCount - 1;
            }

            Debug.Log($"[InventoryManager] 휠 입력으로 슬롯 선택: {SelectedSlotIndex + 1}번 슬롯");
            return;
        }

        int nextIndex = SelectedSlotIndex + direction;

        if (nextIndex >= MaxSlotCount)
            nextIndex = 0;

        if (nextIndex < 0)
            nextIndex = MaxSlotCount - 1;

        SelectedSlotIndex = nextIndex;

        Debug.Log($"[InventoryManager] 마우스 휠 선택 슬롯 변경: {SelectedSlotIndex + 1}번 슬롯");
    }

    /// <summary>
    /// 현재 손에 들고 있는 아이템 ID 반환
    /// 
    /// 반환값:
    /// - EmptySlot(0): 맨손 또는 빈 슬롯 선택
    /// - 1 이상: 들고 있는 아이템 ID
    /// 
    /// 참고:
    /// - SelectedSlotIndex가 -1이면 아무 슬롯도 선택하지 않은 상태이므로 맨손 처리
    /// </summary>
    public int GetHeldItemId()
    {
        if (SelectedSlotIndex < 0 || SelectedSlotIndex >= MaxSlotCount)
        {
            return EmptySlot;
        }

        return Slots[SelectedSlotIndex];
    }

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
    /// 팀원 중 하나라도 itemId 가지고 있는지 확인
    /// </summary>
    public static bool HasAnyPlayerItem(int itemId)
    {
        foreach (InventoryManager inventory in AllInventories)
        {
            if (inventory != null && inventory.HasItem(itemId))
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
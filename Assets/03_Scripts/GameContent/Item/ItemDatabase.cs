using UnityEngine;
/// <summary>
/// ItemDatabase
///
/// 담당:
/// - itemId를 기준으로 ItemData 검색
/// - Inventory UI, Shop UI, InventoryManager 등이 아이템 정보를 조회하는 중간 역할
/// - 전체 아이템 데이터 목록 관리
///
/// 참고:
/// - 데이터 조회용 매니저
/// - 아이템 상태 변경하지 않음
/// - 네트워크 동기화 대상 x
/// </summary>

public class ItemDatabase : MonoBehaviour
{
    public static ItemDatabase Instance;

    public ItemData[] items;

    private void Awake()
    {
        Instance = this;
    }

    public ItemData GetItemData(int itemId)
    {
        // 등록된 아이템 데이터 목록 검사
        foreach (ItemData item in items)
        {
            // null 체크 후 ID가 일치하면 해당 데이터 반환
            if(item != null && item.itemId == itemId)
            {
                return item;
            }
        }

        Debug.LogWarning($"[ItemDatabase] itemId={itemId}에 해당하는 ItemData를 찾지 못했습니다.");
        return null;
    }
}

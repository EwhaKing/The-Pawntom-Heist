using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 인벤토리매니저입니다.
/// TODO : 아이템 정렬, 아이템 사용 함수, 버리기 규칙 등 들어갈 예정
/// </summary>
public class InventoryManager : PawntomSingleton<InventoryManager>
{
    public List<ItemObject> items = new List<ItemObject>();

    public void AddItem(ItemObject item)
    {
        items.Add(item);
        Debug.Log(item.itemName + " 을 훔쳤다!");
    }

    public bool HasItem(string itemName)
    {
        foreach (ItemObject item in items)
        {
            if (item.itemName == itemName)
            {
                return true;
            }
        }
        return false;
    }
}
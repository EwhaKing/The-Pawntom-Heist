using System.Collections.Generic;
using UnityEngine;

public class Inventory : MonoBehaviour
{
    public static Inventory Instance;

    public List<ItemObject> items = new List<ItemObject>();

    private void Awake()
    {
        Instance = this;
    }

    public void AddItem(ItemObject item)
    {
        items.Add(item);
        Debug.Log(item.itemName + " 을 훔쳤다!");
    }
}

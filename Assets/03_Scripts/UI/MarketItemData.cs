using UnityEngine;

/// <summary>
/// MarketItemData
///
/// 담당:
/// - 암시장에서 판매할 아이템 데이터
/// - 지금은 UI 테스트용으로 이름/가격만 사용하고, 나중에 실제 아이템 스폰이 필요하면 itemId나 prefab을 연결
/// </summary>
[CreateAssetMenu(
    fileName = "MarketItemData",
    menuName = "Game/Market Item Data"
)]
public class MarketItemData : ScriptableObject
{
    [Header("Basic Info")]
    public int itemId;
    public string itemName = "Item";
    public int price = 10;

    [Header("Optional")]
    public Sprite icon;
    public GameObject itemPrefab;
}
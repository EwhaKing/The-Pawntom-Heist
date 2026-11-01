using UnityEngine;

/// <summary>
/// ItemData
///
/// 담당:
/// - Item 의 고정 데이터 정의
/// - 아이템 ID, 이름, 아이콘(UI), 타입 관리
/// - 사용 가능 / 버리기 기능 / 구매 가능 여부 설정
/// - 상점 가격, 판매 가격 설정
/// - UI / Inventory / Shop / Spawn 시스템에서 공통으로 참조하는 아이템 정보 제공
///
/// 참고:
/// - ScriptableObject 기반 데이터
/// - 런타임 중 플레이어가 가진 아이템 상태 저장 x
/// </summary>
public enum ItemType
{
    KeyItem,
    stealable,
    Consumable,
    Tool,
    ShopItem
}

[CreateAssetMenu(menuName = "Game/Item Data")]
public class ItemData : ScriptableObject
{
    [Header("Basic")]
    public int itemId;
    public string itemName;
    public Sprite itemIcon;
    public ItemType itemType;

    [Header("Rules")]
    public bool canUse;
    public bool canDrop = true;
    public bool canBuy = true;

    [Header("Shop")]
    public int buyPrice;
    public int sellPrice;

    [Header("Weight")]
    [Tooltip("소지 무게. 가방에 든 아이템 무게의 합이 클수록 이동 속도가 줄어든다(페널티는 이동 속도 감소 하나뿐). 수치는 임시값이다.")]
    [Min(0f)] public float weight = 1f;

    [Header("World")]
    public GameObject modelPrefab;
}

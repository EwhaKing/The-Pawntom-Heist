
using Fusion;
using UnityEngine;

/// <summary>
/// VaultKeyInventoryBridge
///
/// 담당:
/// - 현재 선택한 인벤토리 아이템이 열쇠인지 확인
/// - 금고에서 요청한 열쇠 소비
///
/// 사용 위치:
/// - Player 프리팹 루트
///
/// 필요 컴포넌트:
/// - InventoryManager
/// </summary>
[RequireComponent(typeof(InventoryManager))]
public class VaultKeyInventoryBridge : MonoBehaviour
{
    [Header("Vault Key")]
    [Tooltip("ItemDatabase에 등록된 금고 열쇠 ItemId")]
    [SerializeField] private int vaultKeyItemId = 10;

    private InventoryManager inventoryManager;

    private void Awake()
    {
        inventoryManager = GetComponent<InventoryManager>();
    }

    /// <summary>
    /// 현재 선택된 슬롯에 금고 열쇠가 있는지 확인
    /// 선택한 슬롯이 없거나 다른 아이템이면 false
    /// </summary>
    public bool HasSelectedKey
    {
        get
        {
            if (inventoryManager == null)
                return false;

            if (inventoryManager.Object == null ||
                !inventoryManager.Object.IsValid)
                return false;

            return inventoryManager.GetHeldItemId() ==
                   vaultKeyItemId;
        }
    }

    /// <summary>
    /// 금고의 StateAuthority에서 호출
    ///
    /// 현재 손에 든 열쇠를 실제로 소비
    /// 선택되지 않은 슬롯에 열쇠가 있어도 소비하지 않음
    /// </summary>
    public bool TryConsumeKeyOnAuthority()
    {
        if (inventoryManager == null)
        {
            Debug.LogWarning(
                "[VaultKeyInventoryBridge] InventoryManager 없음"
            );
            return false;
        }

        if (inventoryManager.Object == null ||
            !inventoryManager.Object.HasStateAuthority)
        {
            Debug.LogWarning(
                "[VaultKeyInventoryBridge] Inventory 권한 없음"
            );
            return false;
        }

        if (!HasSelectedKey)
        {
            Debug.Log(
                "[VaultKeyInventoryBridge] 현재 선택된 아이템이 열쇠가 아닙니다."
            );
            return false;
        }

        bool consumed =
            inventoryManager.TryConsumeSelectedItem(vaultKeyItemId);

        if (consumed)
        {
            Debug.Log(
                "[VaultKeyInventoryBridge] 실제 인벤토리 열쇠 소비 성공"
            );
        }

        return consumed;
    }
}

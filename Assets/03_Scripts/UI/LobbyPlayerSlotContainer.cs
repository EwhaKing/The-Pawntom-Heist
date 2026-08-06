using UnityEngine;

/// <summary>
/// 로비 UI에서 플레이어 4명의 슬롯 생성을 담당.
/// </summary>
public class LobbyPlayerSlotContainer : MonoBehaviour
{
    private const int SlotCount = 4;
    [SerializeField] private LobbyPlayerSlotUI slotPrefab;
    private LobbyPlayerSlotUI[] _slots;

    [SerializeField] private CharacterSelectPopup characterSelectPopup;

    private void Awake()
    {
        CreateSlots();
    }

    private void CreateSlots()
    {
        _slots = new LobbyPlayerSlotUI[SlotCount];

        for (int i = 0; i < SlotCount; i++)
        {
            LobbyPlayerSlotUI slot = Instantiate(slotPrefab, transform);

            slot.Initialize(characterSelectPopup);
            slot.SetSlotIndex(i);
            slot.SetEmpty();

            _slots[i] = slot;
        }
    }

    public LobbyPlayerSlotUI GetSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= _slots.Length)
        {
            Debug.LogError(
                $"[LobbyPlayerSlotContainer] 잘못된 슬롯 번호: {slotIndex}");

            return null;
        }

        return _slots[slotIndex];
    }
}

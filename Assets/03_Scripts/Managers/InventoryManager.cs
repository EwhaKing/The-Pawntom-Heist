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
    private PlayerData _playerData;
    private bool IsOwnerDead => _playerData != null && _playerData.IsDead;

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
    /// 빈 슬롯이 없어 아이템 추가가 거절됐을 때(StateAuthority 쪽에서) 호출됩니다.
    /// "가방 가득 참" 경고·튜토리얼 안내 같은 표시용 구독자를 위한 것입니다.
    /// </summary>
    public static event System.Action<InventoryManager> AddRejectedFull;

    /// <summary>
    /// Fusion에서 Player NetworkObject가 Spawn된 후 호출
    /// 전체 인벤토리 목록에 자신을 등록
    /// 
    /// 추가사항:
    /// 로컬 플레이어의 인벤토리만 UI 연결
    /// </summary>
    public override void Spawned()
    {
        _playerData = GetComponent<PlayerData>();
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

    [Header("Drop")]
    [Tooltip("Q 로 버린 아이템이 놓이는 거리(플레이어 앞, m)")]
    [SerializeField, Min(0.2f)] private float dropDistance = 0.8f;

    /// <summary>
    /// Fusion 네트워크 Tick마다 호출
    /// 숫자키 1~4 / 휠 입력으로 선택 슬롯 변경, Q 로 선택 아이템 버리기
    ///
    /// 선택 슬롯은 입력 권한 쪽(내 클라이언트)에서도 예측해서 바꾼다 —
    /// 호스트 응답을 기다리면 핫바가 한 박자 늦게 움직여 끊겨 보인다. 어긋나면 Fusion 이 호스트 값으로 되돌린다.
    /// </summary>
    public override void FixedUpdateNetwork()
    {
        if (IsOwnerDead || !GetInput(out NetworkInputData input))
        {
            return;
        }

        // -1이면 이번 Tick에 슬롯 선택 입력이 없다는 뜻
        if (input.SelectedSlotIndex >= 0)
        {
            ApplyToggle(input.SelectedSlotIndex);
        }
        else if (input.SlotScrollDirection != 0)
        {
            ApplyScroll(input.SlotScrollDirection);
        }

        // 버리기는 월드에 아이템을 만들므로 호스트에서 한 번만 한다(예측·재시뮬레이션 제외).
        if (input.DropItem && Object.HasStateAuthority && Runner.IsForward)
        {
            DropSelectedItem();
        }
    }

    private void ApplyToggle(int slotIndex)
    {
        SelectedSlotIndex = Pawntom.Player.Core.HotbarSelection.Toggle(SelectedSlotIndex, slotIndex, MaxSlotCount);
    }

    /// <summary>휠로 여러 칸 넘김. 선택이 없으면 첫 칸은 방향에 따라 1번(아래) 또는 4번(위)에서 시작한다.</summary>
    private void ApplyScroll(int steps)
    {
        SelectedSlotIndex = Pawntom.Player.Core.HotbarSelection.Scroll(SelectedSlotIndex, steps, MaxSlotCount);
    }

    /// <summary>
    /// 선택 슬롯의 아이템을 플레이어 앞 바닥에 월드 아이템(다시 주울 수 있음)으로 내려놓고 슬롯을 비운다. 호스트 전용.
    /// 앞이 벽이면 벽 앞에 둔다. 버릴 수 없는 아이템(canDrop=false)·빈 슬롯은 무시한다.
    /// </summary>
    private void DropSelectedItem()
    {
        int slot = SelectedSlotIndex;

        if (slot < 0 || slot >= MaxSlotCount || Slots[slot] == EmptySlot)
        {
            return;
        }

        int itemId = Slots[slot];
        ItemData data = ItemDatabase.Instance != null ? ItemDatabase.Instance.GetItemData(itemId) : null;

        if (data == null || !data.canDrop)
        {
            Debug.Log("[InventoryManager] 버릴 수 없는 아이템입니다.");
            return;
        }

        NetworkObject prefab = data.modelPrefab != null ? data.modelPrefab.GetComponent<NetworkObject>() : null;

        if (prefab == null)
        {
            Debug.LogWarning($"[InventoryManager] {data.itemName} 의 월드 프리팹(modelPrefab)에 NetworkObject 가 없어 버릴 수 없습니다.");
            return;
        }

        Vector3 position = FindDropPosition(prefab.transform);
        Quaternion rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f) * prefab.transform.rotation;
        NetworkObject spawned = Runner.Spawn(prefab, position, rotation);

        if (spawned == null)
        {
            return;
        }

        NetworkItem item = spawned.GetComponent<NetworkItem>();

        if (item != null)
        {
            item.Init(itemId);
        }

        Slots.Set(slot, EmptySlot);
        Debug.Log($"[InventoryManager] {data.itemName} 버림");
    }

    /// <summary>플레이어 앞 바닥 위치. 프리팹 콜라이더 바닥이 땅에 닿도록 높이를 올린다.</summary>
    private Vector3 FindDropPosition(Transform prefabRoot)
    {
        Vector3 forward = transform.forward;
        forward.y = 0f;
        forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;

        Vector3 chest = transform.position + Vector3.up * 0.6f;
        float distance = dropDistance;

        // 앞이 벽이면 벽 앞에서 멈춘다(내 몸 콜라이더는 CharacterController 라 걸러진다).
        if (Physics.Raycast(chest, forward, out RaycastHit wall, dropDistance + 0.3f, ~0, QueryTriggerInteraction.Ignore) &&
            !(wall.collider is CharacterController))
        {
            distance = Mathf.Max(0.2f, wall.distance - 0.3f);
        }

        Vector3 above = chest + forward * distance;
        float groundY = transform.position.y;

        if (Physics.Raycast(above, Vector3.down, out RaycastHit ground, 3f, ~0, QueryTriggerInteraction.Ignore))
        {
            groundY = ground.point.y;
        }

        return new Vector3(above.x, groundY + PrefabBottomOffset(prefabRoot), above.z);
    }

    private static readonly Dictionary<Transform, float> BottomOffsetCache = new Dictionary<Transform, float>();

    /// <summary>
    /// 프리팹 루트에서 콜라이더 가장 아래까지의 높이(월드 단위, 루트 회전 그대로 기준). 루트를 땅 위 이만큼에 두면 바닥이 땅에 닿는다.
    /// 프리팹 애셋의 콜라이더 모양으로 계산하고 결과를 캐시한다.
    /// </summary>
    private static float PrefabBottomOffset(Transform prefabRoot)
    {
        if (BottomOffsetCache.TryGetValue(prefabRoot, out float cached))
        {
            return cached;
        }

        float lowest = float.MaxValue;
        Matrix4x4 rootInverse = Matrix4x4.TRS(prefabRoot.position, prefabRoot.rotation, Vector3.one).inverse;

        foreach (Collider collider in prefabRoot.GetComponentsInChildren<Collider>(true))
        {
            if (collider.isTrigger)
            {
                continue;
            }

            Bounds local;

            if (collider is BoxCollider box) local = new Bounds(box.center, box.size);
            else if (collider is SphereCollider sphere) local = new Bounds(sphere.center, Vector3.one * sphere.radius * 2f);
            else if (collider is CapsuleCollider capsule) local = new Bounds(capsule.center, Vector3.one * Mathf.Max(capsule.radius * 2f, capsule.height));
            else if (collider is MeshCollider mesh && mesh.sharedMesh != null) local = mesh.sharedMesh.bounds;
            else continue;

            Matrix4x4 toRoot = rootInverse * collider.transform.localToWorldMatrix;

            for (int c = 0; c < 8; c++)
            {
                Vector3 corner = local.center + Vector3.Scale(local.extents,
                    new Vector3((c & 1) == 0 ? -1f : 1f, (c & 2) == 0 ? -1f : 1f, (c & 4) == 0 ? -1f : 1f));
                lowest = Mathf.Min(lowest, toRoot.MultiplyPoint3x4(corner).y);
            }
        }

        float offset = lowest < float.MaxValue ? -lowest : 0f;
        BottomOffsetCache[prefabRoot] = offset;
        return offset;
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
            if (inventory != null && inventory.Object != null && inventory.Object.IsValid &&
                !inventory.IsOwnerDead && inventory.HasItem(itemId))
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
        if (!Object.HasStateAuthority || IsOwnerDead)
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
        AddRejectedFull?.Invoke(this);
        return false;
    }

    /// <summary>
    /// 아이템 사용
    /// 소비 아이템이면 사용 후 슬롯에서 제거
    /// </summary>
    public bool TryUseItem(int slotIndex)
    {
        if (!Object.HasStateAuthority || IsOwnerDead)
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
    /// 현재 선택한 슬롯의 아이템이 requiredItemId와 같으면 해당 아이템 1개를 소비
    /// 반드시 StateAuthority에서 실행
    /// </summary>
    public bool TryConsumeSelectedItem(int requiredItemId)
    {
        if (Object == null || !Object.HasStateAuthority)
            return false;

        if (requiredItemId <= EmptySlot)
            return false;

        if (SelectedSlotIndex < 0 ||
            SelectedSlotIndex >= MaxSlotCount)
        {
            Debug.Log("[InventoryManager] 맨손 상태입니다.");
            return false;
        }

        int heldItemId = Slots[SelectedSlotIndex];

        if (heldItemId != requiredItemId)
        {
            Debug.Log(
                $"[InventoryManager] 선택한 아이템이 열쇠가 아닙니다. " +
                $"Held={heldItemId}, Required={requiredItemId}"
            );
            return false;
        }

        // 선택한 슬롯만 비움
        // 나머지 아이템을 앞으로 당기지 않음
        Slots.Set(SelectedSlotIndex, EmptySlot);

        Debug.Log(
            $"[InventoryManager] 열쇠 소비 완료: 슬롯 {SelectedSlotIndex + 1}"
        );

        return true;
    }

    /// <summary>
    /// 아이템 버리기
    /// canDrop이 false인 아이템은 버릴 수 없음
    /// </summary>
    public bool TryDropItem(int slotIndex)
    {
        if (!Object.HasStateAuthority || IsOwnerDead)
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

    /// <summary>사망 전용: 열쇠와 캣닢을 포함해 canDrop과 무관하게 전부 현장에 남깁니다.</summary>
    public bool TryDropAllOnDeath(Vector3 deathPosition)
    {
        if (!Object.HasStateAuthority || !IsOwnerDead) return false;

        bool completed = true;
        for (int i = 0; i < MaxSlotCount; i++)
        {
            int itemId = Slots[i];
            if (itemId == EmptySlot) continue;

            ItemData data = ItemDatabase.Instance != null ? ItemDatabase.Instance.GetItemData(itemId) : null;
            NetworkObject prefab = data != null && data.modelPrefab != null
                ? data.modelPrefab.GetComponent<NetworkObject>() : null;
            if (prefab == null || prefab.GetComponent<NetworkItem>() == null)
            {
                Debug.LogError($"[InventoryManager] 사망 드롭 프리팹 누락: ItemId={itemId}. 슬롯을 보존합니다.");
                completed = false;
                continue;
            }

            float angle = i * Mathf.PI * 2f / MaxSlotCount;
            Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 0.7f;
            Vector3 position = PlayerDeathPlacement.GroundPoint(deathPosition + offset, transform, out _);
            Quaternion rotation = Quaternion.Euler(0f, i * 90f, 0f);
            try
            {
                NetworkObject dropped = Runner.Spawn(prefab, position, rotation,
                    onBeforeSpawned: (runner, spawned) =>
                        spawned.GetComponent<NetworkItem>().InitializeDeathDrop(itemId, position, rotation));
                if (dropped == null)
                {
                    completed = false;
                    continue;
                }
                Slots.Set(i, EmptySlot);
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
                completed = false;
            }
        }
        SelectedSlotIndex = -1;
        return completed;
    }
}

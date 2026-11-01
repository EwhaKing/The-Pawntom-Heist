using Fusion;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// NetworkItem
///
/// 담당:
/// - 월드에 존재하는 네트워크 아이템 정보 관리
/// - 해당 아이템의 itemId 저장
/// - 아이템이 이미 획득되었는지 여부 관리
/// - SpawnManager가 생성한 아이템의 초기 데이터 설정
///
/// 참고:
/// - NetworkObject가 붙은 월드 아이템 프리팹 컴포넌트에 추가해야 함 (중요)
/// - Runner.Spawn으로 생성되는 아이템 오브젝트에 사용
/// - itemId를 통해 ItemDatabase의 ItemData와 연결됨
/// 
/// </summary>
public class NetworkItem : NetworkBehaviour
{
    [Networked] public int ItemId { get; set; }
    [Networked] public NetworkBool IsPickedUp { get; set; }
    [Networked] public NetworkBool IsDeathDrop { get; private set; }
    [Networked] private Vector3 DropPosition { get; set; }
    [Networked] private Quaternion DropRotation { get; set; }
    // 스폰 위치·회전. NetworkTransform 이 없는 아이템은 Runner.Spawn 의 위치·회전이 클라이언트에 전달되지 않아
    // (프리팹에 저장된 자리에 나타난다) 호스트가 정한 자세를 직접 복제한다.
    [Networked] private NetworkBool HasSpawnPose { get; set; }
    [Networked] private Vector3 SpawnPosition { get; set; }
    [Networked] private Quaternion SpawnRotation { get; set; }
    private Vector3 _prefabScale;

    /// <summary>드롭된 아이템의 가장 긴 변(m). 드롭 자리 계산(InventoryManager)도 같은 값으로 크기를 맞춘다.</summary>
    public const float DropItemSize = 0.45f;

    private void Awake() => _prefabScale = transform.localScale;

    /// <summary>
    /// 호스트의 onBeforeSpawned 에서 호출. 아이템 ID 와 스폰 자세를 정해 모든 피어가 같은 자리에서 시작하게 한다.
    /// </summary>
    public void InitializeSpawn(int itemId, Vector3 position, Quaternion rotation)
    {
        ItemId = itemId;
        IsPickedUp = false;
        SpawnPosition = position;
        SpawnRotation = rotation;
        HasSpawnPose = true;
    }

    /// <summary>호스트의 onBeforeSpawned에서 호출. NetworkTransform 없는 아이템도 위치를 복제합니다.</summary>
    public void InitializeDeathDrop(int itemId, Vector3 position, Quaternion rotation)
    {
        ItemId = itemId;
        IsPickedUp = false;
        IsDeathDrop = true;
        DropPosition = position;
        DropRotation = rotation;
    }

    public override void Spawned()
    {
        transform.localScale = _prefabScale;

        if (!IsDeathDrop)
        {
            // 호스트가 정한 스폰 자세를 모든 피어에 적용한다(씬에 미리 놓인 아이템은 HasSpawnPose 가 꺼져 있어 건드리지 않는다).
            if (HasSpawnPose)
            {
                transform.SetPositionAndRotation(SpawnPosition, SpawnRotation);
            }

            return;
        }

        transform.SetPositionAndRotation(DropPosition, DropRotation);
        // Collider.bounds 는 Physics.SyncTransforms 전에는 갱신되지 않는다. 위치·크기를 바꾼 뒤에는 반드시 동기화하고 읽는다.
        // (안 하면 크기를 줄이기 전의 낡은 경계로 높이를 보정해, 12배 스케일 프리팹인 캣닢이 12m 공중에 뜬다)
        Physics.SyncTransforms();

        // 기존 아이템 프리팹은 이전 맵 스케일을 사용합니다. 드롭은 현재 캐릭터 크기에 맞춥니다.
        if (TryGetBounds(out Bounds bounds))
        {
            float size = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            if (size > 0.001f) transform.localScale *= DropItemSize / size;
            Physics.SyncTransforms();

            if (TryGetBounds(out bounds))
                transform.position += Vector3.up * (DropPosition.y - bounds.min.y + 0.02f);
        }
    }

    private bool TryGetBounds(out Bounds bounds)
    {
        bounds = default;
        bool found = false;
        foreach (Collider collider in GetComponentsInChildren<Collider>())
        {
            if (!collider.enabled || collider.isTrigger) continue;
            if (!found) bounds = collider.bounds;
            else bounds.Encapsulate(collider.bounds);
            found = true;
        }
        return found;
    }

    // <summary>
    // 아이템 초기화
    // ItemSpawnManager에서 Runner.Spawn 직후 호출
    // </summary>
    public void Init(int itemId)
    {
        if(!Object.HasStateAuthority)
            return;

        // 월드안에서 아이템이 어떤 아이템인지 ID 저장
        ItemId = itemId;

        // 처음 생성된 아이템은 PickUp(획득)되지 않은 상태
        IsPickedUp = false;

        Debug.Log($"[NetworkItem] Init ItemId={ItemId}");
    }
}

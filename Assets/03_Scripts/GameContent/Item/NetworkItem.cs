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
    private Vector3 _prefabScale;

    private void Awake() => _prefabScale = transform.localScale;

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
        if (!IsDeathDrop) return;

        transform.SetPositionAndRotation(DropPosition, DropRotation);
        // 기존 아이템 프리팹은 이전 맵 스케일을 사용합니다. 드롭은 현재 캐릭터 크기에 맞춥니다.
        if (TryGetBounds(out Bounds bounds))
        {
            float size = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            if (size > 0.001f) transform.localScale *= 0.45f / size;
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

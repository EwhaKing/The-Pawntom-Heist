using Fusion;
using UnityEngine;

/// <summary>
/// ItemSpawnManager
///
/// 담당:
/// - 맵 위 아이템 스폰 관리
/// - 지정된 SpawnPoint 목록 관리
/// - 스폰 가능한 itemId 목록 관리
/// - 확률 기반 랜덤 아이템 스폰
/// - Runner.Spawn을 통한 NetworkItem 생성
/// 
/// 참고:
/// - 아이템 전용 스폰 매니저
/// - Server/Host 또는 StateAuthority 에서만 아이템 스폰 실행
/// - 생성된 아이템에는 NetworkItem을 통해 itemId를 전달
/// 
/// 담당x:
/// - 아이템 획득 입력 처리
/// - 아이템 사용 / 버리기
/// </summary>

public class ItemSpawnManager : NetworkBehaviour
{
    [Header("공통 월드 아이템 프리팹")]
    [SerializeField] private NetworkPrefabRef worldItemPrefab;

    [Header("스폰 포인트")]
    [SerializeField] private Transform[] spawnPoints;

    [Header("스폰 가능 아이템 ID")]
    [SerializeField] private int[] possibleItemIds;

    [Header("스폰 확률")]
    [Range(0.0f, 1.0f)]
    [SerializeField] private float spawnChance = 0.5f;

    [Header("스폰 위치 보정")]
    [SerializeField] private float yOffset = 0.5f;

    /// <summary>
    /// Fusion에서 NetworkObject가 Spawn된 뒤 호출
    /// 호스트가 아닌 클라이언트가 중복으로 아이템을 생성하지 않도록 방지
    /// </summary>
    public override void Spawned()
    {
        if (!Object.HasStateAuthority)
            return;
        
        SpawnItems();
    }

    /// <summary>
    /// 
    /// </summary>
    private void SpawnItems()
    {
        // 스폰 포인트가 없으면 아이템 생성 불가
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogError("[ItemSpawnManager] Spawn Points가 비어있습니다.");
            return;
        }

        // 생성 가능한 아이템 ID 목록이 없으면 아이템 생성 불가
        if (possibleItemIds == null || possibleItemIds.Length == 0)
        {
            Debug.LogError("[ItemSpawnManager] Possible Item Ids가 비어있습니다.");
            return;
        }

        // 등록된 스폰 포인트 검사
        foreach (Transform spawnPoint in spawnPoints)
        {
            // 배열에 비어있는 칸이 있으면 건너뛰기
            if (spawnPoint == null)
            {
                continue;
            }

            // spawnChance 확률 실패시 아이템 생성 안 됨
            if (Random.value > spawnChance)
            {
                Debug.Log($"[ItemSpawnManager] {spawnPoint.name} : 아이템 생성 안 됨");
                continue;
            }

            // PossibleItemIds 중 하나를 랜덤으로 선택
            int randomIndex = Random.Range(0, possibleItemIds.Length);
            int selectedItemId = possibleItemIds[randomIndex];

            // 스폰 위치
            Vector3 spawnPosition = spawnPoint.position + Vector3.up * yOffset;

            // 네트워크 아이템 생성
            NetworkObject spawnedObject = Runner.Spawn(
                worldItemPrefab,
                spawnPosition,
                spawnPoint.rotation,
                null
            );

            // 생성된 오브젝트 NetworkItem 컴포넌트 체크
            NetworkItem item = spawnedObject.GetComponent<NetworkItem>();

            if (item == null)
            {
                Debug.LogError("[ItemSpawnManager] 생성된 프리팹에 NetworkItem이 없습니다.");
                return;
            }

            // itemId 전달
            item.Init(selectedItemId);
            Debug.Log($"[ItemSpawnManager] Spawn ItemId={selectedItemId}, Position={spawnPosition}");
        }
    }
}

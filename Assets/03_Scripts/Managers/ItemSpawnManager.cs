using System.Collections.Generic;
using Fusion;
using Pawntom.Stage;
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
/// 두 경로:
/// - 레벨 데이터 씬(Map_Scene): 호스트 시드 계획(StageNetworkState.Plan)의 열쇠(KeySpotIndices, 열쇠 itemId)와
///   전리품(Items)을 스폰한다. 매니저가 여럿이어도 계획 스폰은 한 번(StageNetworkState.TryClaimItemSpawn).
///   계획이 아직이면 StageNetworkState.PlanReady 를 기다린다.
/// - 레벨 데이터가 없는 씬(Tutorial_Scene): 인스펙터 spawnPoints 에 확률을 굴린다(기존 경로).
///
/// 참고:
/// - 아이템 전용 스폰 매니저
/// - Server/Host 또는 StateAuthority 에서만 아이템 스폰 실행
/// - 생성된 아이템에는 NetworkItem을 통해 itemId를 전달
/// - 판정 난수는 StageRandom 만 쓴다(UnityEngine.Random 금지 — fusion-network 규칙)
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

    [Header("레벨 데이터 씬")]
    [Tooltip("계획된 열쇠 후보 지점에 스폰할 열쇠 itemId(ItemData 열쇠). 레벨 데이터가 없는 씬에서는 쓰지 않는다")]
    [SerializeField] private int _keyItemId = 10;

    [Header("전리품 최소 보장")]
    [Tooltip("켜면 같은 씬에서 이 옵션이 켜진 ItemSpawnManager 전체를 한 묶음으로 보고, 확률 굴림이 전부 실패해도 최소 한 곳은 반드시 아이템을 만든다.\n" +
             "스폰 지점마다 따로 확률을 굴리면(예: 4곳 × 50%) 전리품이 하나도 안 뜰 수 있기 때문이다(6.25%).")]
    [SerializeField] private bool guaranteeAtLeastOneInScene = true;

    [Tooltip("묶음 전체에서 최소 몇 곳은 반드시 아이템을 만들지(위 옵션이 켜져 있을 때). 기본 1. 묶음 안 매니저들의 값 중 가장 큰 값을 쓴다(튜토리얼 전리품 방은 2).\n" +
             "확률에서 빠진 지점 중에서 모자란 만큼 무작위로 골라 만든다(그 지점을 가진 매니저의 프리팹·아이템 ID 로).")]
    [SerializeField, Min(1)] private int guaranteedMinimumInScene = 1;

    // ── 묶음 굴림 상태 ──
    // 매니저들이 한 씬에서 차례로 Spawned 되므로, "마지막 차례인데 아직 아무도 못 만들었다"를 세어 그때 강제로 하나 만든다.
    // 같은 세션(Runner)·같은 씬 안에서만 이어지고, 다르면 새 묶음으로 센다.
    private static NetworkRunner _groupRunner;
    private static int _groupSceneHandle = -1;
    private static int _groupExpected;
    private static int _groupDecided;
    private static int _groupSpawned;
    private static int _groupMinimum = 1;

    // 튜토리얼 전리품 방 최소 개수(무게 감속 안내용). 씬 값이 2로 바뀌어도 결과는 같다.
    private const int TutorialLootRoomMinimum = 2;

    // 기존 경로의 판정 난수(호스트 로컬). 묶음마다 새 시드로 만든다 — 기존 경로는 호스트만 굴리고 결과는 NetworkObject 로 전달된다.
    private static StageRandom _legacyRandom;

    // 확률에서 빠진 지점들(어느 매니저의 지점인지 함께). 최소 개수를 못 채우면 여기서 골라 만든다.
    private static readonly System.Collections.Generic.List<(ItemSpawnManager owner, Transform point)> _groupSkipped =
        new System.Collections.Generic.List<(ItemSpawnManager owner, Transform point)>(8);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetGroup()
    {
        _groupRunner = null;
        _groupSceneHandle = -1;
        _groupExpected = _groupDecided = _groupSpawned = 0;
        _groupMinimum = 1;
        _groupSkipped.Clear();
        _legacyRandom = null;
    }

    private static StageRandom LegacyRandom
    {
        get
        {
            if (_legacyRandom == null)
            {
                _legacyRandom = new StageRandom(System.Guid.NewGuid().GetHashCode());
            }

            return _legacyRandom;
        }
    }

    // 계획 대기 중인지(PlanReady 구독 중).
    private bool _waitingForPlan;
    private readonly List<StageItemSpawn> _planSpawns = new List<StageItemSpawn>(32);

    /// <summary>
    /// Fusion에서 NetworkObject가 Spawn된 뒤 호출
    /// 호스트가 아닌 클라이언트가 중복으로 아이템을 생성하지 않도록 방지
    /// </summary>
    public override void Spawned()
    {
        if (!Object.HasStateAuthority)
            return;

        StageSpawnMode mode = StageNetworkState.ResolveSpawnMode(out StageNetworkState stage);

        switch (mode)
        {
            case StageSpawnMode.UsePlan:
                SpawnFromPlan(stage);
                return;

            case StageSpawnMode.Wait:
                // 스테이지 상태가 아직 Spawned 되지 않았다(씬 오브젝트끼리 Spawned 순서는 보장되지 않는다).
                _waitingForPlan = true;
                StageNetworkState.PlanReady += HandlePlanReady;
                return;

            case StageSpawnMode.LegacyMissingState:
                Debug.LogError("[ItemSpawnManager] 레벨 데이터는 있는데 StageNetworkState(NetworkObject)가 씬에 없어 인스펙터 스폰 지점을 씁니다.");
                SpawnItems();
                return;

            default:
                SpawnItems();
                return;
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        StopWaitingForPlan();
    }

    private void OnDestroy()
    {
        StopWaitingForPlan();
    }

    private void StopWaitingForPlan()
    {
        if (!_waitingForPlan)
        {
            return;
        }

        _waitingForPlan = false;
        StageNetworkState.PlanReady -= HandlePlanReady;
    }

    private void HandlePlanReady(StageNetworkState stage)
    {
        StopWaitingForPlan();

        if (Object == null || !Object.IsValid || !Object.HasStateAuthority || stage == null)
        {
            return;
        }

        SpawnFromPlan(stage);
    }

    /// <summary>
    /// 레벨 데이터 씬: 호스트 시드 계획의 열쇠·전리품을 스폰한다. 같은 계획에 대해 씬 전체에서 1회.
    /// </summary>
    private void SpawnFromPlan(StageNetworkState stage)
    {
        if (!stage.TryClaimItemSpawn())
        {
            // 다른 ItemSpawnManager 가 이미 이 계획을 스폰했다.
            return;
        }

        if (!worldItemPrefab.IsValid)
        {
            Debug.LogError("[ItemSpawnManager] World Item Prefab 이 비어 있어 계획 아이템을 스폰하지 못합니다.");
            return;
        }

        StageSpawnList.BuildItemSpawns(stage.Layout, stage.Plan, _keyItemId, _planSpawns);

        int keys = 0;
        int spawned = 0;

        for (int i = 0; i < _planSpawns.Count; i++)
        {
            StageItemSpawn spawn = _planSpawns[i];
            Vector3 position = LevelLayoutData.ToUnity(spawn.Position);

            if (SpawnItem(position, Quaternion.identity, spawn.ItemId, spawn.SpotId))
            {
                spawned++;

                if (spawn.IsKey)
                {
                    keys++;
                }
            }
        }

        Debug.Log($"[ItemSpawnManager] 계획 아이템 스폰 (seed {stage.Plan.Seed}) : 열쇠 {keys} · 전리품 {spawned - keys} / 계획 {_planSpawns.Count}");
    }

    // 같은 세션·같은 씬이면 이어서, 다르면 새 묶음으로 센다. 첫 매니저가 지점을 기록하기 전에 불러야 한다.
    private void EnsureGroup()
    {
        int sceneHandle = gameObject.scene.handle;

        if (ReferenceEquals(_groupRunner, Runner) && _groupSceneHandle == sceneHandle)
        {
            return;
        }

        _groupRunner = Runner;
        _groupSceneHandle = sceneHandle;
        _groupDecided = _groupSpawned = 0;
        _groupExpected = 0;
        _groupMinimum = 1;
        _groupSkipped.Clear();
        _legacyRandom = null;

        ItemSpawnManager[] managers = FindObjectsByType<ItemSpawnManager>(FindObjectsSortMode.None);

        for (int i = 0; i < managers.Length; i++)
        {
            if (managers[i] != null && managers[i].isActiveAndEnabled &&
                managers[i].guaranteeAtLeastOneInScene && managers[i].gameObject.scene.handle == sceneHandle)
            {
                _groupExpected++;

                if (managers[i].guaranteedMinimumInScene > _groupMinimum)
                {
                    _groupMinimum = managers[i].guaranteedMinimumInScene;
                }
            }
        }
    }

    /// <summary>
    /// 이 매니저의 차례를 묶음에 알린다. 마지막 차례이고 묶음 전체에서 만든 곳이 최소 개수에 모자라면,
    /// 확률에서 빠진 지점 중 무작위로 골라 모자란 만큼 만든다.
    /// </summary>
    private void ReportAndForce(int spawnedHere)
    {
        if (!guaranteeAtLeastOneInScene)
        {
            return;
        }

        EnsureGroup();
        _groupDecided++;
        _groupSpawned += spawnedHere;

        if (_groupDecided < _groupExpected)
        {
            return;
        }

        // 묶음 최소 개수 = 묶음 안 매니저들의 guaranteedMinimumInScene 중 최댓값.
        // 튜토리얼 전리품 방은 무게 감속 안내(TUTORIAL_SCRIPT 5-3~5-5)를 위해 최소 2개가 필요하다.
        // Tutorial_Scene 매니저 값은 아직 1이라, 이전 동작(최소 2)을 SceneNames 상수로 유지한다.
        int minimum = Mathf.Max(1, _groupMinimum);
        if (gameObject.scene.name == SceneNames.DemoTutorialMap)
        {
            minimum = Mathf.Max(minimum, TutorialLootRoomMinimum);
        }
        int need = minimum - _groupSpawned;

        while (need > 0 && _groupSkipped.Count > 0)
        {
            int pick = LegacyRandom.NextInt(_groupSkipped.Count);
            (ItemSpawnManager owner, Transform point) forced = _groupSkipped[pick];
            _groupSkipped.RemoveAt(pick);

            if (forced.owner == null || forced.point == null)
            {
                continue;
            }

            Debug.Log($"[ItemSpawnManager] 전리품이 최소 개수에 모자라 {forced.point.name} 에 강제로 생성합니다.");

            if (forced.owner.SpawnAt(forced.point))
            {
                _groupSpawned++;
                need--;
            }
        }
    }

    /// <summary>
    /// 한 스폰 지점에 아이템을 만든다(기존 경로). 성공하면 true.
    /// </summary>
    private bool SpawnAt(Transform spawnPoint)
    {
        // PossibleItemIds 중 하나를 랜덤으로 선택
        int randomIndex = LegacyRandom.NextInt(possibleItemIds.Length);
        int selectedItemId = possibleItemIds[randomIndex];

        return SpawnItem(spawnPoint.position, spawnPoint.rotation, selectedItemId, spawnPoint.name);
    }

    /// <summary>
    /// 지점 위치에 아이템 하나를 만든다(호스트). 성공하면 true.
    /// </summary>
    private bool SpawnItem(Vector3 pointPosition, Quaternion spawnRotation, int selectedItemId, string label)
    {
        // 스폰 위치. 높이는 PickupDisplay 가 바닥 면에 맞춰 다시 잡는다.
        Vector3 spawnPosition = pointPosition + Vector3.up * yOffset;

        // 네트워크 아이템 생성. 위치·회전·itemId 를 스폰 전에 넣어 클라이언트도 같은 자리에서 시작하게 한다
        // (NetworkTransform 이 없으면 Spawn 의 위치·회전 인자는 클라이언트에 전달되지 않는다).
        NetworkObject spawnedObject = Runner.Spawn(
            worldItemPrefab,
            spawnPosition,
            spawnRotation,
            null,
            onBeforeSpawned: (networkRunner, networkObject) =>
            {
                NetworkItem spawnedItem = networkObject.GetComponent<NetworkItem>();

                if (spawnedItem != null)
                {
                    spawnedItem.InitializeSpawn(selectedItemId, spawnPosition, spawnRotation);
                }
            }
        );

        // 생성된 오브젝트 NetworkItem 컴포넌트 체크
        if (spawnedObject == null || spawnedObject.GetComponent<NetworkItem>() == null)
        {
            Debug.LogError("[ItemSpawnManager] 생성된 프리팹에 NetworkItem이 없습니다.");
            return false;
        }

        Debug.Log($"[ItemSpawnManager] Spawn ItemId={selectedItemId}, Point={label}, Position={spawnPosition}");
        return true;
    }

    /// <summary>
    /// 기존 경로: 인스펙터 spawnPoints 마다 확률을 굴린다.
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

        int spawnedHere = 0;

        if (guaranteeAtLeastOneInScene)
        {
            EnsureGroup();
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
            if (!(LegacyRandom.NextFloat01() < spawnChance))
            {
                Debug.Log($"[ItemSpawnManager] {spawnPoint.name} : 아이템 생성 안 됨");
                if (guaranteeAtLeastOneInScene)
                {
                    _groupSkipped.Add((this, spawnPoint));
                }

                continue;
            }

            if (!SpawnAt(spawnPoint))
            {
                return;
            }

            spawnedHere++;
        }

        // 묶음 전체에서 만든 곳이 최소 개수에 모자라고 이 매니저가 마지막 차례면, 확률에서 빠진 지점을 골라 채운다.
        ReportAndForce(spawnedHere);
    }
}

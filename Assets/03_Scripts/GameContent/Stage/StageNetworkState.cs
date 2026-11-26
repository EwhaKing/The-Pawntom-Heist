using System;
using System.Collections.Generic;
using Fusion;
using Pawntom.Stage;
using UnityEngine;

/// <summary>
/// 한 판(Map_Scene)의 무작위 배치를 호스트 시드 하나로 정해 모든 피어에 공유한다(씬에 1개, NetworkObject 필요).
/// <para>
/// 권한: 상태 권한(호스트)이 <see cref="Spawned"/> 에서 시드를 정해 [Networked] <see cref="Seed"/> 에 1회 쓴다.
/// 이후 판정은 모두 <see cref="StageRules.CreatePlan"/>(StageRandom)이다 — 같은 레벨 데이터 + 같은 시드 = 모든 피어에서 같은 계획.
/// 모든 피어가 계획을 계산·캐시하고, 본부 도킹(<see cref="HeadquartersDocking"/>)을 각자 같은 위치로 적용한다.
/// 스폰(플레이어·K-9·아이템·브루투스)은 호스트만 한다(SpawnManager·ItemSpawnManager 가 <see cref="PlanReady"/> 를 기다린다).
/// </para>
/// <para>
/// 늦게 들어온 참가자: [Networked] 시드만 받아 같은 계획을 다시 만들고 도킹을 적용한다. 나머지 배치는 호스트가 스폰한
/// NetworkObject 로 받는다. 시드가 Spawned 시점에 아직 없으면 <see cref="Render"/> 에서 처음 보인 순간 적용한다.
/// </para>
/// <para>
/// 호스트 순서 보장: Fusion 은 씬 NetworkObject 를 등록(Spawned)한 뒤 OnSceneLoadDone 을 부르지만, 그 순서에 기대지 않는다.
/// SpawnManager 는 <see cref="StageSpawnGate"/> 로 판정해 계획이 없으면 스폰을 미뤘다가 <see cref="PlanReady"/> 에서
/// 플레이어 → 적 순서로 처리한다. <see cref="PlanReady"/> 는 도킹 적용이 끝난 뒤에 발행된다.
/// </para>
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Pawntom/Level/Stage Network State")]
public sealed class StageNetworkState : NetworkBehaviour
{
    [Header("본부 (비우면 자동)")]
    [Tooltip("본부 루트. 비우면 씬의 MainbaseManager 가 붙은 오브젝트")]
    [SerializeField] private Transform _mainbase;

    [Tooltip("본부 안 플레이어 스폰 지점들의 부모. 비우면 본부 루트 아래 'PlayerSpawns'")]
    [SerializeField] private Transform _playerSpawnRoot;

    [Tooltip("본부가 씬에 붙어 저장된 기준 도킹. 비우면 Blocker 가 꺼진 도킹")]
    [SerializeField] private DockingPointMarker _referenceDock;

    /// <summary>본부 아래 플레이어 스폰 부모 이름(씬 규칙).</summary>
    public const string PlayerSpawnRootName = "PlayerSpawns";

    /// <summary>호스트가 정한 시드. <see cref="HasSeed"/> 가 true 일 때만 의미가 있다.</summary>
    [Networked] public int Seed { get; private set; }

    [Networked] public NetworkBool HasSeed { get; private set; }

    /// <summary>현재 씬의 상태. 스폰 전·씬 밖이면 null.</summary>
    public static StageNetworkState Current { get; private set; }

    /// <summary>이 피어에서 계획이 준비되고 본부 도킹 적용이 끝난 순간 1회(상태 인스턴스마다).</summary>
    public static event Action<StageNetworkState> PlanReady;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Current = null;
        PlanReady = null;
    }

    private readonly List<Transform> _playerSpawnPoints = new List<Transform>(4);
    private readonly StageOnceFlag _itemSpawnClaim = new StageOnceFlag();
    private HeadquartersDocking _docking;

    /// <summary>이 피어가 계산한 계획. 준비 전이면 null.</summary>
    public StagePlan Plan { get; private set; }

    /// <summary>계획을 만든 레이아웃(공유 캐시 — 고치지 않는다).</summary>
    public StageLayout Layout { get; private set; }

    public bool IsPlanReady
    {
        get { return Plan != null; }
    }

    /// <summary>본부 안 플레이어 스폰 지점(본부가 옮겨진 뒤의 위치). 계획 준비 후 유효.</summary>
    public IReadOnlyList<Transform> PlayerSpawnPoints
    {
        get { return _playerSpawnPoints; }
    }

    /// <summary>
    /// 현재 씬의 레벨 데이터. 없으면 null(레벨 데이터가 없는 씬 = 기존 경로).
    /// </summary>
    public static StageLayout CurrentLayout
    {
        get { return StageLayoutProvider.Current != null ? StageLayoutProvider.Current.Layout : null; }
    }

    /// <summary>
    /// 호스트 스폰 경로 판정. <paramref name="state"/> 는 UsePlan 일 때 계획이 준비된 상태다.
    /// 씬 탐색이 들어 있으므로 맵 로드·스폰 시점에만 부른다(Update 계열 금지).
    /// </summary>
    public static StageSpawnMode ResolveSpawnMode(out StageNetworkState state)
    {
        state = Current;

        if (state == null && CurrentLayout != null)
        {
            // 아직 Spawned 되지 않은 씬 오브젝트(NetworkObject 가 있어야 언젠가 Spawned 된다).
            StageNetworkState found = FindFirstObjectByType<StageNetworkState>(FindObjectsInactive.Include);
            state = found != null && found.GetComponent<NetworkObject>() != null ? found : null;
        }

        return StageSpawnGate.Decide(CurrentLayout != null, state != null, state != null && state.IsPlanReady);
    }

    /// <summary>호스트: 계획 아이템 스폰을 한 번만 하도록 차지한다(ItemSpawnManager 가 여럿이어도 1회).</summary>
    public bool TryClaimItemSpawn()
    {
        return _itemSpawnClaim.TryClaim();
    }

    public override void Spawned()
    {
        if (Current != null && Current != this)
        {
            Debug.LogWarning("[StageNetworkState] 씬에 스테이지 상태가 둘 이상입니다. 마지막으로 스폰된 것을 씁니다.", this);
        }

        Current = this;

        if (Object.HasStateAuthority && !HasSeed)
        {
            // 호스트 결정만 시스템 기반 엔트로피를 쓴다. 이후 판정은 모두 이 시드의 StageRandom.
            Seed = CreateHostSeed();
            HasSeed = true;
            Debug.Log($"[StageNetworkState] 호스트 시드 = {Seed}", this);
        }

        TryBuildPlan();
    }

    public override void Render()
    {
        // 시드가 Spawned 이후에 도착한 피어: 처음 보인 순간 한 번만 적용한다. 준비 뒤에는 비교 하나뿐이다.
        if (Plan == null && HasSeed)
        {
            TryBuildPlan();
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (Current == this)
        {
            Current = null;
        }

        Plan = null;
        Layout = null;
        _playerSpawnPoints.Clear();
    }

    private void TryBuildPlan()
    {
        if (Plan != null || !HasSeed)
        {
            return;
        }

        StageLayout layout = CurrentLayout;

        if (layout == null)
        {
            // 레벨 데이터가 없는 씬에 이 오브젝트가 놓였다: 아무것도 하지 않는다(스폰은 기존 경로).
            return;
        }

        Layout = layout;
        Plan = StageRules.CreatePlan(layout, Seed);

        if (Object.HasStateAuthority)
        {
            if (Plan.KeysTruncated)
            {
                Debug.LogWarning($"[StageNetworkState] 필요 열쇠 {layout.RequiredKeyCount}개 > 열쇠 후보 {layout.KeySpots.Count}곳 — 후보 수만큼만 배치합니다.", this);
            }

            if (Plan.PatrolRoutesTruncated)
            {
                Debug.LogWarning($"[StageNetworkState] K-9 {layout.K9Count}마리 > 순찰 경로 {layout.PatrolRoutes.Count}개 — 경로 수만큼만 스폰합니다.", this);
            }
        }

        Transform mainbase = ResolveMainbase();

        if (_docking == null)
        {
            _docking = new HeadquartersDocking(mainbase, _referenceDock);
        }

        if (Plan.DockingIndex >= 0)
        {
            _docking.Apply(layout, Plan.DockingIndex);
        }
        else
        {
            Debug.LogWarning("[StageNetworkState] 레벨 데이터에 도킹 후보가 없어 본부를 옮기지 않습니다.", this);
        }

        Transform spawnRoot = _playerSpawnRoot != null
            ? _playerSpawnRoot
            : (mainbase != null ? mainbase.Find(PlayerSpawnRootName) : null);
        HeadquartersDocking.CollectSpawnPoints(spawnRoot, _playerSpawnPoints);

        if (_playerSpawnPoints.Count == 0)
        {
            Debug.LogWarning("[StageNetworkState] 본부 안 플레이어 스폰 지점을 찾지 못했습니다. SpawnManager 의 인스펙터 지점을 씁니다.", this);
        }

        Debug.Log($"[StageNetworkState] 계획 준비 — seed {Seed}, 도킹 #{Plan.DockingIndex}, 열쇠 {Plan.KeySpotIndices.Count}, 아이템 {Plan.Items.Count}, K-9 경로 {Plan.PatrolRouteIndices.Count}", this);
        PlanReady?.Invoke(this);
    }

    private Transform ResolveMainbase()
    {
        if (_mainbase != null)
        {
            return _mainbase;
        }

        MainbaseManager manager = FindFirstObjectByType<MainbaseManager>(FindObjectsInactive.Include);
        return manager != null ? manager.transform : null;
    }

    private static int CreateHostSeed()
    {
        return Guid.NewGuid().GetHashCode();
    }
}

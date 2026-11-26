using System;
using System.Collections.Generic;
using Pawntom.Stage;
using UnityEngine;

/// <summary>
/// 단계 × 층 하나의 배치 데이터(GAME_DESIGN §3.8 "레벨 데이터로 분리해야 하는 항목"). 씬에는 편집용 마커만 둔다.
/// <para>
/// 에셋 이름 규칙: <c>LevelLayoutData_&lt;식별자&gt;</c> (예: <c>LevelLayoutData_Stage1_1F</c>).
/// 배치 목록은 <c>Pawntom/Level/Bake Layout From Scene</c> 으로 씬 마커에서 굽는다. 단계·층·열쇠 수·K-9 수는 직접 정한다.
/// 순찰 경로(웨이포인트·loop)는 씬의 PatrolRoute 에서 구워지고, 그중 K-9 수만큼을 호스트가 시드로 고른다.
/// 런타임 판정은 <see cref="ToCore"/> 로 얻은 순수 C# 모델과 <see cref="StageRules"/> 로 한다.
/// </para>
/// </summary>
[CreateAssetMenu(menuName = "Pawntom/Level Layout Data", fileName = "LevelLayoutData_Stage1_1F")]
public sealed class LevelLayoutData : ScriptableObject
{
    [Serializable]
    public sealed class DockingEntry
    {
        public string id;
        public Vector3 position;
        public float yaw;
    }

    [Serializable]
    public sealed class ItemSpotEntry
    {
        public string id;
        public Vector3 position;
        [Tooltip("이 지점에 나올 수 있는 ItemData.itemID")]
        public List<int> allowedItemIds = new List<int>();
        [Range(0f, 1f)] public float spawnChance = 1f;
    }

    [Serializable]
    public sealed class KeySpotEntry
    {
        public string id;
        public Vector3 position;
    }

    [Serializable]
    public sealed class PatrolRouteData
    {
        public string id;
        public List<Vector3> waypoints = new List<Vector3>();
        public bool loop = true;
    }

    [Serializable]
    public sealed class K9SpawnEntry
    {
        public string id;
        public Vector3 position;
        public float yaw;
        [Tooltip("순찰 경로 id. 비우면 제자리 대기")]
        public string routeId;
    }

    [Serializable]
    public sealed class BrutusSpawnEntry
    {
        public string id;
        public Vector3 position;
        public float yaw;
        [Min(0f)] public float patrolRadius = 20f;
    }

    [Serializable]
    public sealed class DoorData
    {
        public string id;
        public Vector3 position;
        public float yaw;
        public DoorSecurity security = DoorSecurity.Normal;
        public DoorInitialState initialState = DoorInitialState.Locked;
    }

    [Serializable]
    public sealed class VentData
    {
        public string id;
        public Vector3 position;
    }

    [Serializable]
    public sealed class PowerPanelData
    {
        public string id;
        public Vector3 position;
        public string unlockZoneId;
    }

    [Serializable]
    public sealed class ZoneData
    {
        public string id;
    }

    [Serializable]
    public sealed class VaultData
    {
        public string id;
        public Vector3 position;
        public float yaw;
    }

    [Header("단계 · 층")]
    [Tooltip("GAME_DESIGN §3.8 단계 1~4")]
    [SerializeField, Min(1)] private int _stageNumber = 1;

    [Tooltip("이 데이터가 표현하는 층(1~단계)")]
    [SerializeField, Min(1)] private int _floorNumber = 1;

    [Tooltip("이 층에 배치할 열쇠 수. 1단계 1층 = 1")]
    [SerializeField, Min(0)] private int _requiredKeyCount = 1;

    [Tooltip("이 층에 내보낼 K-9 수. 순찰 경로 풀에서 이만큼 시드로 골라 한 마리씩 배정한다")]
    [SerializeField, Min(0)] private int _k9Count = StageRules.DefaultK9Count;

    [Header("배치 (씬에서 굽는다)")]
    [SerializeField] private List<DockingEntry> _docking = new List<DockingEntry>();
    [SerializeField] private List<ItemSpotEntry> _itemSpots = new List<ItemSpotEntry>();
    [SerializeField] private List<KeySpotEntry> _keySpots = new List<KeySpotEntry>();
    [SerializeField] private List<PatrolRouteData> _patrolRoutes = new List<PatrolRouteData>();
    [SerializeField] private List<K9SpawnEntry> _k9Spawns = new List<K9SpawnEntry>();
    [SerializeField] private List<BrutusSpawnEntry> _brutusSpawns = new List<BrutusSpawnEntry>();
    [SerializeField] private List<DoorData> _doors = new List<DoorData>();
    [SerializeField] private List<VentData> _vents = new List<VentData>();
    [SerializeField] private List<PowerPanelData> _powerPanels = new List<PowerPanelData>();
    [SerializeField] private List<ZoneData> _zones = new List<ZoneData>();
    [SerializeField] private List<VaultData> _vaults = new List<VaultData>();

    public int StageNumber
    {
        get { return _stageNumber; }
    }

    public int FloorNumber
    {
        get { return _floorNumber; }
    }

    public int RequiredKeyCount
    {
        get { return _requiredKeyCount; }
    }

    public int K9Count
    {
        get { return _k9Count; }
    }

    /// <summary>
    /// 순수 C# 모델로 변환한다. 호출할 때마다 새로 만든다 — 런타임에는 <see cref="StageLayoutProvider.Layout"/> 의 캐시를 쓴다.
    /// </summary>
    public StageLayout ToCore()
    {
        var layout = new StageLayout
        {
            StageNumber = _stageNumber,
            FloorNumber = _floorNumber,
            RequiredKeyCount = _requiredKeyCount,
            K9Count = _k9Count,
        };

        for (int i = 0; i < _docking.Count; i++)
        {
            DockingEntry e = _docking[i];
            layout.DockingCandidates.Add(new DockingCandidate(e.id, ToStage(e.position), e.yaw));
        }

        for (int i = 0; i < _itemSpots.Count; i++)
        {
            ItemSpotEntry e = _itemSpots[i];
            var allowed = e.allowedItemIds != null ? new List<int>(e.allowedItemIds) : new List<int>();
            layout.ItemSpots.Add(new ItemSpot(e.id, ToStage(e.position), allowed, e.spawnChance));
        }

        for (int i = 0; i < _keySpots.Count; i++)
        {
            KeySpotEntry e = _keySpots[i];
            layout.KeySpots.Add(new KeySpot(e.id, ToStage(e.position)));
        }

        for (int i = 0; i < _patrolRoutes.Count; i++)
        {
            PatrolRouteData e = _patrolRoutes[i];
            var route = new PatrolRouteEntry(e.id, e.loop);

            if (e.waypoints != null)
            {
                for (int w = 0; w < e.waypoints.Count; w++)
                {
                    route.Waypoints.Add(ToStage(e.waypoints[w]));
                }
            }

            layout.PatrolRoutes.Add(route);
        }

        for (int i = 0; i < _k9Spawns.Count; i++)
        {
            K9SpawnEntry e = _k9Spawns[i];
            layout.K9Spawns.Add(new K9Spawn(e.id, ToStage(e.position), e.yaw, e.routeId));
        }

        for (int i = 0; i < _brutusSpawns.Count; i++)
        {
            BrutusSpawnEntry e = _brutusSpawns[i];
            layout.BrutusSpawns.Add(new BrutusSpawn(e.id, ToStage(e.position), e.yaw, e.patrolRadius));
        }

        for (int i = 0; i < _doors.Count; i++)
        {
            DoorData e = _doors[i];
            layout.Doors.Add(new DoorEntry(e.id, ToStage(e.position), e.yaw, e.security, e.initialState));
        }

        for (int i = 0; i < _vents.Count; i++)
        {
            VentData e = _vents[i];
            layout.Vents.Add(new VentEntry(e.id, ToStage(e.position)));
        }

        for (int i = 0; i < _powerPanels.Count; i++)
        {
            PowerPanelData e = _powerPanels[i];
            layout.PowerPanels.Add(new PowerPanelEntry(e.id, ToStage(e.position), e.unlockZoneId));
        }

        for (int i = 0; i < _zones.Count; i++)
        {
            layout.Zones.Add(new ZoneEntry(_zones[i].id));
        }

        for (int i = 0; i < _vaults.Count; i++)
        {
            VaultData e = _vaults[i];
            layout.Vaults.Add(new VaultEntry(e.id, ToStage(e.position), e.yaw));
        }

        return layout;
    }

    /// <summary>
    /// 순수 C# 모델의 배치 목록으로 덮어쓴다(굽기 도구용). 단계·층·열쇠 수도 모델 값을 따른다.
    /// Undo 기록·SetDirty 는 호출하는 에디터 코드가 한다.
    /// </summary>
    public void OverwriteFrom(StageLayout layout)
    {
        if (layout == null)
        {
            throw new ArgumentNullException(nameof(layout));
        }

        _stageNumber = layout.StageNumber;
        _floorNumber = layout.FloorNumber;
        _requiredKeyCount = layout.RequiredKeyCount;
        _k9Count = layout.K9Count;

        _docking.Clear();
        foreach (DockingCandidate c in layout.DockingCandidates)
        {
            _docking.Add(new DockingEntry { id = c.Id, position = ToUnity(c.Position), yaw = c.Yaw });
        }

        _itemSpots.Clear();
        foreach (ItemSpot s in layout.ItemSpots)
        {
            _itemSpots.Add(new ItemSpotEntry
            {
                id = s.Id,
                position = ToUnity(s.Position),
                allowedItemIds = new List<int>(s.AllowedItemIds),
                spawnChance = s.SpawnChance,
            });
        }

        _keySpots.Clear();
        foreach (KeySpot k in layout.KeySpots)
        {
            _keySpots.Add(new KeySpotEntry { id = k.Id, position = ToUnity(k.Position) });
        }

        _patrolRoutes.Clear();
        foreach (PatrolRouteEntry r in layout.PatrolRoutes)
        {
            var data = new PatrolRouteData { id = r.Id, loop = r.Loop };
            foreach (StageVector w in r.Waypoints)
            {
                data.waypoints.Add(ToUnity(w));
            }

            _patrolRoutes.Add(data);
        }

        _k9Spawns.Clear();
        foreach (K9Spawn k in layout.K9Spawns)
        {
            _k9Spawns.Add(new K9SpawnEntry { id = k.Id, position = ToUnity(k.Position), yaw = k.Yaw, routeId = k.RouteId });
        }

        _brutusSpawns.Clear();
        foreach (BrutusSpawn b in layout.BrutusSpawns)
        {
            _brutusSpawns.Add(new BrutusSpawnEntry { id = b.Id, position = ToUnity(b.Position), yaw = b.Yaw, patrolRadius = b.PatrolRadius });
        }

        _doors.Clear();
        foreach (DoorEntry d in layout.Doors)
        {
            _doors.Add(new DoorData { id = d.Id, position = ToUnity(d.Position), yaw = d.Yaw, security = d.Security, initialState = d.InitialState });
        }

        _vents.Clear();
        foreach (VentEntry v in layout.Vents)
        {
            _vents.Add(new VentData { id = v.Id, position = ToUnity(v.Position) });
        }

        _powerPanels.Clear();
        foreach (PowerPanelEntry p in layout.PowerPanels)
        {
            _powerPanels.Add(new PowerPanelData { id = p.Id, position = ToUnity(p.Position), unlockZoneId = p.UnlockZoneId });
        }

        _zones.Clear();
        foreach (ZoneEntry z in layout.Zones)
        {
            _zones.Add(new ZoneData { id = z.Id });
        }

        _vaults.Clear();
        foreach (VaultEntry v in layout.Vaults)
        {
            _vaults.Add(new VaultData { id = v.Id, position = ToUnity(v.Position), yaw = v.Yaw });
        }
    }

    public static StageVector ToStage(Vector3 v)
    {
        return new StageVector(v.x, v.y, v.z);
    }

    public static Vector3 ToUnity(StageVector v)
    {
        return new Vector3(v.X, v.Y, v.Z);
    }

    private void Reset()
    {
        _requiredKeyCount = StageRules.RequiredKeysForStage(_stageNumber);
    }
}

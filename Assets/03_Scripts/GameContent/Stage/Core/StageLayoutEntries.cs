using System.Collections.Generic;

namespace Pawntom.Stage
{
    /// <summary>격벽 보안 등급. 금고방 문은 Vault.</summary>
    public enum DoorSecurity
    {
        Normal = 0,
        Vault = 1,
    }

    /// <summary>스테이지 시작 시 격벽 상태.</summary>
    public enum DoorInitialState
    {
        Locked = 0,
        Unlocked = 1,
    }

    /// <summary>모든 배치 항목의 공통 식별자. 같은 분류 안에서 유일해야 한다.</summary>
    public interface IStageLayoutEntry
    {
        string Id { get; }
    }

    /// <summary>본부 도킹 후보 지점 (GAME_DESIGN §3.8: 1층에 3곳, 시작 시 1곳 무작위).</summary>
    public sealed class DockingCandidate : IStageLayoutEntry
    {
        public string Id { get; set; }
        public StageVector Position { get; set; }
        public float Yaw { get; set; }

        public DockingCandidate(string id, StageVector position, float yaw)
        {
            Id = id;
            Position = position;
            Yaw = yaw;
        }
    }

    /// <summary>아이템(전리품·가젯) 스폰 지점. 확률 판정 후 허용 목록에서 1개를 고른다.</summary>
    public sealed class ItemSpot : IStageLayoutEntry
    {
        public string Id { get; set; }
        public StageVector Position { get; set; }

        /// <summary>이 지점에 나올 수 있는 ItemData.itemID 목록.</summary>
        public List<int> AllowedItemIds { get; }

        /// <summary>스폰 확률 0~1.</summary>
        public float SpawnChance { get; set; }

        public ItemSpot(string id, StageVector position, List<int> allowedItemIds, float spawnChance)
        {
            Id = id;
            Position = position;
            AllowedItemIds = allowedItemIds ?? new List<int>();
            SpawnChance = spawnChance;
        }
    }

    /// <summary>열쇠 배치 후보 지점 (GAME_DESIGN §3.8: 후보 풀에서 단계 수만큼 무작위).</summary>
    public sealed class KeySpot : IStageLayoutEntry
    {
        public string Id { get; set; }
        public StageVector Position { get; set; }

        public KeySpot(string id, StageVector position)
        {
            Id = id;
            Position = position;
        }
    }

    /// <summary>K-9 순찰 경로.</summary>
    public sealed class PatrolRouteEntry : IStageLayoutEntry
    {
        public string Id { get; set; }
        public List<StageVector> Waypoints { get; }
        public bool Loop { get; set; }

        public PatrolRouteEntry(string id, bool loop)
        {
            Id = id;
            Loop = loop;
            Waypoints = new List<StageVector>();
        }
    }

    /// <summary>K-9 스폰. <see cref="RouteId"/> 가 비어 있으면 제자리 대기.</summary>
    public sealed class K9Spawn : IStageLayoutEntry
    {
        public string Id { get; set; }
        public StageVector Position { get; set; }
        public float Yaw { get; set; }
        public string RouteId { get; set; }

        public K9Spawn(string id, StageVector position, float yaw, string routeId)
        {
            Id = id;
            Position = position;
            Yaw = yaw;
            RouteId = routeId;
        }
    }

    /// <summary>브루투스 스폰 지점 (캣닢 경보 시). 광역 순찰 반경(m)을 함께 둔다(GAME_DESIGN §3.8 레벨 데이터 항목).</summary>
    public sealed class BrutusSpawn : IStageLayoutEntry
    {
        public string Id { get; set; }
        public StageVector Position { get; set; }
        public float Yaw { get; set; }
        public float PatrolRadius { get; set; }

        public BrutusSpawn(string id, StageVector position, float yaw, float patrolRadius)
        {
            Id = id;
            Position = position;
            Yaw = yaw;
            PatrolRadius = patrolRadius;
        }
    }

    /// <summary>격벽(문).</summary>
    public sealed class DoorEntry : IStageLayoutEntry
    {
        public string Id { get; set; }
        public StageVector Position { get; set; }
        public float Yaw { get; set; }
        public DoorSecurity Security { get; set; }
        public DoorInitialState InitialState { get; set; }

        public DoorEntry(string id, StageVector position, float yaw, DoorSecurity security, DoorInitialState initialState)
        {
            Id = id;
            Position = position;
            Yaw = yaw;
            Security = security;
            InitialState = initialState;
        }
    }

    /// <summary>환풍구 통로.</summary>
    public sealed class VentEntry : IStageLayoutEntry
    {
        public string Id { get; set; }
        public StageVector Position { get; set; }

        public VentEntry(string id, StageVector position)
        {
            Id = id;
            Position = position;
        }
    }

    /// <summary>분전반(보안 노드). 캐스팅 완료 시 <see cref="UnlockZoneId"/> 구역을 해금한다.</summary>
    public sealed class PowerPanelEntry : IStageLayoutEntry
    {
        public string Id { get; set; }
        public StageVector Position { get; set; }
        public string UnlockZoneId { get; set; }

        public PowerPanelEntry(string id, StageVector position, string unlockZoneId)
        {
            Id = id;
            Position = position;
            UnlockZoneId = unlockZoneId;
        }
    }

    /// <summary>구역(해금 단위).</summary>
    public sealed class ZoneEntry : IStageLayoutEntry
    {
        public string Id { get; set; }

        public ZoneEntry(string id)
        {
            Id = id;
        }
    }

    /// <summary>캣닢 금고.</summary>
    public sealed class VaultEntry : IStageLayoutEntry
    {
        public string Id { get; set; }
        public StageVector Position { get; set; }
        public float Yaw { get; set; }

        public VaultEntry(string id, StageVector position, float yaw)
        {
            Id = id;
            Position = position;
            Yaw = yaw;
        }
    }
}

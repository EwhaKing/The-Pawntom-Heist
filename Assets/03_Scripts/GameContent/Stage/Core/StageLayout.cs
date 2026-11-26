using System.Collections.Generic;

namespace Pawntom.Stage
{
    /// <summary>
    /// 단계 × 층 하나의 배치 데이터(엔진 비의존). 씬에 하드코딩하지 않는 항목(GAME_DESIGN §3.8)을 담는다.
    /// <para>
    /// 목록의 순서가 곧 인덱스다. <see cref="StagePlan"/> 은 인덱스로 결과를 돌려주므로,
    /// 모든 피어가 같은 데이터(같은 에셋)를 읽어야 같은 결과를 얻는다. 굽기 도구는 <see cref="SortById"/> 로 순서를 고정한다.
    /// </para>
    /// </summary>
    public sealed class StageLayout
    {
        /// <summary>단계 번호(1~4).</summary>
        public int StageNumber { get; set; } = 1;

        /// <summary>층 번호(1~단계).</summary>
        public int FloorNumber { get; set; } = 1;

        /// <summary>이 레이아웃에 배치할 열쇠 수. 1단계 1층이면 1개(§3.8).</summary>
        public int RequiredKeyCount { get; set; } = 1;

        /// <summary>
        /// 이 레이아웃에 내보낼 K-9 수. 순찰 경로 풀(약 13개)에서 이만큼 시드로 골라 한 마리씩 배정한다.
        /// </summary>
        public int K9Count { get; set; } = StageRules.DefaultK9Count;

        public List<DockingCandidate> DockingCandidates { get; } = new List<DockingCandidate>(3);
        public List<ItemSpot> ItemSpots { get; } = new List<ItemSpot>();
        public List<KeySpot> KeySpots { get; } = new List<KeySpot>();
        public List<PatrolRouteEntry> PatrolRoutes { get; } = new List<PatrolRouteEntry>();
        public List<K9Spawn> K9Spawns { get; } = new List<K9Spawn>();
        public List<BrutusSpawn> BrutusSpawns { get; } = new List<BrutusSpawn>();
        public List<DoorEntry> Doors { get; } = new List<DoorEntry>();
        public List<VentEntry> Vents { get; } = new List<VentEntry>();
        public List<PowerPanelEntry> PowerPanels { get; } = new List<PowerPanelEntry>();
        public List<ZoneEntry> Zones { get; } = new List<ZoneEntry>();
        public List<VaultEntry> Vaults { get; } = new List<VaultEntry>();

        /// <summary>씬에서 읽어 오는 배치 목록을 모두 비운다. 단계·층·열쇠 수는 그대로 둔다.</summary>
        public void ClearEntries()
        {
            DockingCandidates.Clear();
            ItemSpots.Clear();
            KeySpots.Clear();
            PatrolRoutes.Clear();
            K9Spawns.Clear();
            BrutusSpawns.Clear();
            Doors.Clear();
            Vents.Clear();
            PowerPanels.Clear();
            Zones.Clear();
            Vaults.Clear();
        }

        /// <summary>
        /// 모든 목록을 Id 순(서수 비교)으로 정렬한다. 씬 탐색 순서는 보장되지 않으므로
        /// 굽기 결과(=인덱스)를 다시 구워도 같게 하려고 쓴다.
        /// </summary>
        public void SortById()
        {
            Sort(DockingCandidates);
            Sort(ItemSpots);
            Sort(KeySpots);
            Sort(PatrolRoutes);
            Sort(K9Spawns);
            Sort(BrutusSpawns);
            Sort(Doors);
            Sort(Vents);
            Sort(PowerPanels);
            Sort(Zones);
            Sort(Vaults);
        }

        private static void Sort<T>(List<T> list) where T : IStageLayoutEntry
        {
            list.Sort((a, b) => string.CompareOrdinal(a.Id ?? string.Empty, b.Id ?? string.Empty));
        }
    }
}

using System.Collections.Generic;

namespace Pawntom.Stage.Tests
{
    /// <summary>테스트용 레이아웃 생성기. 기본값은 Validate 를 오류 없이 통과한다.</summary>
    internal static class StageLayoutFixtures
    {
        /// <summary>
        /// 1단계 1층 기준(GAME_DESIGN §3.8): 도킹 후보 3곳, 열쇠 1개, 열쇠 후보 <paramref name="keyCandidates"/>곳.
        /// </summary>
        public static StageLayout ValidStage1(int keyCandidates = 4, int itemSpots = 6)
        {
            var layout = new StageLayout
            {
                StageNumber = 1,
                FloorNumber = 1,
                RequiredKeyCount = 1,
                K9Count = 1, // 경로 1개뿐이므로 경고가 나지 않게
            };

            layout.DockingCandidates.Add(new DockingCandidate("Dock_1", new StageVector(0f, 0f, 40f), 180f));
            layout.DockingCandidates.Add(new DockingCandidate("Dock_2", new StageVector(60f, 0f, 0f), 270f));
            layout.DockingCandidates.Add(new DockingCandidate("Dock_3", new StageVector(0f, 0f, -90f), 0f));

            for (int i = 0; i < keyCandidates; i++)
            {
                layout.KeySpots.Add(new KeySpot("Key_" + i, new StageVector(100f + i * 5f, 0f, 0f)));
            }

            for (int i = 0; i < itemSpots; i++)
            {
                layout.ItemSpots.Add(new ItemSpot("Item_" + i, new StageVector(-100f - i * 5f, 0f, 0f), new List<int> { 2, 3 }, 1f));
            }

            var route = new PatrolRouteEntry("Route_A", true);
            route.Waypoints.Add(new StageVector(0f, 0f, 0f));
            route.Waypoints.Add(new StageVector(10f, 0f, 0f));
            layout.PatrolRoutes.Add(route);

            layout.K9Spawns.Add(new K9Spawn("K9_1", new StageVector(0f, 0f, 0f), 0f, "Route_A"));
            layout.BrutusSpawns.Add(new BrutusSpawn("Brutus_1", new StageVector(5f, 0f, 5f), 0f, 20f));
            layout.Doors.Add(new DoorEntry("Door_1", new StageVector(3f, 0f, 3f), 90f, DoorSecurity.Normal, DoorInitialState.Locked));
            layout.Doors.Add(new DoorEntry("Door_Vault", new StageVector(4f, 0f, 3f), 0f, DoorSecurity.Vault, DoorInitialState.Locked));
            layout.Vents.Add(new VentEntry("Vent_1", new StageVector(1f, 2f, 1f)));
            layout.Zones.Add(new ZoneEntry("Zone_East"));
            layout.PowerPanels.Add(new PowerPanelEntry("Panel_1", new StageVector(2f, 1f, 2f), "Zone_East"));
            layout.Vaults.Add(new VaultEntry("Vault_1", new StageVector(0f, 0f, -60f), 0f));

            return layout;
        }

        public static bool HasIssue(IReadOnlyList<StageLayoutIssue> issues, StageLayoutIssueCode code)
        {
            for (int i = 0; i < issues.Count; i++)
            {
                if (issues[i].Code == code)
                {
                    return true;
                }
            }

            return false;
        }
    }
}

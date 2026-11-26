using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Pawntom.Stage.Tests
{
    /// <summary>
    /// 시드 → 스폰 목록(열쇠·전리품·K-9 경로). 호스트만 스폰하지만, 같은 시드면 어느 피어가 계산해도
    /// 같은 목록이 나와야 늦게 들어온 피어·재계산이 어긋나지 않는다(GAME_DESIGN §3.8).
    /// </summary>
    public sealed class StageSpawnListTests
    {
        private const int KeyItemId = 10;

        private static StageLayout LayoutWithRoutes(int routeCount, int k9Count)
        {
            StageLayout layout = StageLayoutFixtures.ValidStage1(keyCandidates: 5, itemSpots: 8);
            layout.PatrolRoutes.Clear();

            for (int i = 0; i < routeCount; i++)
            {
                var route = new PatrolRouteEntry("Route_" + (i + 1).ToString("00"), true);
                route.Waypoints.Add(new StageVector(i, 0f, 0f));
                route.Waypoints.Add(new StageVector(i, 0f, 10f));
                layout.PatrolRoutes.Add(route);
            }

            layout.K9Count = k9Count;
            return layout;
        }

        [Test]
        public void BuildItemSpawns_SameSeed_SameList()
        {
            StageLayout layout = LayoutWithRoutes(4, 2);
            var a = new List<StageItemSpawn>();
            var b = new List<StageItemSpawn>();

            StageSpawnList.BuildItemSpawns(layout, StageRules.CreatePlan(layout, 777), KeyItemId, a);
            StageSpawnList.BuildItemSpawns(layout, StageRules.CreatePlan(layout, 777), KeyItemId, b);

            CollectionAssert.AreEqual(a, b);
        }

        [Test]
        public void BuildItemSpawns_KeysComeFirstWithKeyItemId()
        {
            StageLayout layout = LayoutWithRoutes(4, 2);
            layout.RequiredKeyCount = 2;
            StagePlan plan = StageRules.CreatePlan(layout, 3);
            var list = new List<StageItemSpawn>();

            StageSpawnList.BuildItemSpawns(layout, plan, KeyItemId, list);

            // GAME_DESIGN §3.8: 단계 N 에 열쇠 N 개. 열쇠 itemId 10(브리프).
            var keys = new List<StageItemSpawn> { list[0], list[1] };
            Assert.That(keys, Has.All.Matches<StageItemSpawn>(s => s.IsKey && s.ItemId == KeyItemId));
        }

        [Test]
        public void BuildItemSpawns_KeyPositionsComeFromPlannedKeySpots()
        {
            StageLayout layout = LayoutWithRoutes(4, 2);
            StagePlan plan = StageRules.CreatePlan(layout, 12);
            var list = new List<StageItemSpawn>();

            StageSpawnList.BuildItemSpawns(layout, plan, KeyItemId, list);

            Assert.AreEqual(layout.KeySpots[plan.KeySpotIndices[0]].Position, list[0].Position);
        }

        [Test]
        public void BuildItemSpawns_CountIsKeysPlusRolledItems()
        {
            StageLayout layout = LayoutWithRoutes(4, 2);
            StagePlan plan = StageRules.CreatePlan(layout, 99);
            var list = new List<StageItemSpawn>();

            StageSpawnList.BuildItemSpawns(layout, plan, KeyItemId, list);

            Assert.AreEqual(plan.KeySpotIndices.Count + plan.Items.Count, list.Count);
        }

        [Test]
        public void BuildItemSpawns_LootUsesPlannedItemIdAndSpot()
        {
            StageLayout layout = LayoutWithRoutes(4, 2);
            StagePlan plan = StageRules.CreatePlan(layout, 5);
            var list = new List<StageItemSpawn>();

            StageSpawnList.BuildItemSpawns(layout, plan, KeyItemId, list);

            StageItemSpawn loot = list[plan.KeySpotIndices.Count];
            ItemPlacement placement = plan.Items[0];
            Assert.AreEqual(
                (layout.ItemSpots[placement.SpotIndex].Position, placement.ItemId, false),
                (loot.Position, loot.ItemId, loot.IsKey));
        }

        [Test]
        public void BuildItemSpawns_OutOfRangeIndices_AreSkipped()
        {
            // 레벨 데이터가 다시 구워져 목록이 줄었는데 오래된 계획을 들고 있는 경우에도 예외 없이 건너뛴다.
            StageLayout layout = LayoutWithRoutes(4, 2);
            var plan = new StagePlan(1, 0, new List<int> { 0, 42 }, false,
                new List<ItemPlacement> { new ItemPlacement(99, "gone", 3), new ItemPlacement(0, "Item_0", 2) },
                new List<int>(), false);
            var list = new List<StageItemSpawn>();

            StageSpawnList.BuildItemSpawns(layout, plan, KeyItemId, list);

            Assert.AreEqual(2, list.Count);
        }

        [Test]
        public void BuildItemSpawns_ClearsPreviousResults()
        {
            StageLayout layout = LayoutWithRoutes(4, 2);
            var list = new List<StageItemSpawn> { new StageItemSpawn("old", new StageVector(), 1, false) };

            StageSpawnList.BuildItemSpawns(layout, new StagePlan(1, 0, null, false, null, null, false), KeyItemId, list);

            Assert.AreEqual(0, list.Count);
        }

        [Test]
        public void BuildItemSpawns_NullLayout_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                StageSpawnList.BuildItemSpawns(null, new StagePlan(1, 0, null, false, null, null, false), KeyItemId, new List<StageItemSpawn>()));
        }

        [Test]
        public void BuildPatrolRouteIds_OneIdPerK9InPlanOrder()
        {
            StageLayout layout = LayoutWithRoutes(12, 6);
            StagePlan plan = StageRules.CreatePlan(layout, 2026);
            var ids = new List<string>();

            StageSpawnList.BuildPatrolRouteIds(layout, plan, ids);

            var expected = new List<string>();
            foreach (int index in plan.PatrolRouteIndices)
            {
                expected.Add(layout.PatrolRoutes[index].Id);
            }

            CollectionAssert.AreEqual(expected, ids);
        }

        [Test]
        public void BuildPatrolRouteIds_DistinctRoutes()
        {
            StageLayout layout = LayoutWithRoutes(12, 6);
            var ids = new List<string>();

            StageSpawnList.BuildPatrolRouteIds(layout, StageRules.CreatePlan(layout, 8), ids);

            CollectionAssert.AllItemsAreUnique(ids);
        }

        [Test]
        public void BuildPatrolRouteIds_MoreK9ThanRoutes_TruncatedToRouteCount()
        {
            StageLayout layout = LayoutWithRoutes(3, 6);
            StagePlan plan = StageRules.CreatePlan(layout, 8);
            var ids = new List<string>();

            StageSpawnList.BuildPatrolRouteIds(layout, plan, ids);

            Assert.AreEqual((true, 3), (plan.PatrolRoutesTruncated, ids.Count));
        }
    }
}

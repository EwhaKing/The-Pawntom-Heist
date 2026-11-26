using System.Collections.Generic;
using NUnit.Framework;

namespace Pawntom.Stage.Tests
{
    /// <summary>
    /// 호스트가 시드 하나로 도킹·열쇠·아이템 배치를 정하는 규칙 (GAME_DESIGN §3.8, §3.6).
    /// </summary>
    public sealed class StageRulesTests
    {
        // ── 도킹 ─────────────────────────────────────────────

        [Test]
        public void PickDocking_SameSeed_ReturnsSameIndex()
        {
            int a = StageRules.PickDocking(new StageRandom(42), 3);
            int b = StageRules.PickDocking(new StageRandom(42), 3);

            Assert.AreEqual(a, b);
        }

        [Test]
        public void PickDocking_ManySeeds_SelectsEveryCandidateAtLeastOnce()
        {
            // GAME_DESIGN §3.8: 도킹 지점 3곳 중 1곳 무작위
            var hits = new int[3];

            for (int seed = 0; seed < 100; seed++)
            {
                hits[StageRules.PickDocking(new StageRandom(seed), hits.Length)]++;
            }

            Assert.That(hits, Has.All.GreaterThan(0));
        }

        [Test]
        public void PickDocking_NoCandidates_ReturnsMinusOne()
        {
            Assert.AreEqual(-1, StageRules.PickDocking(new StageRandom(1), 0));
        }

        // ── 열쇠 ─────────────────────────────────────────────

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(4)]
        public void PickKeySpots_StageN_PicksNDistinctIndices(int required)
        {
            // GAME_DESIGN §3.8: N단계는 열쇠 N개
            bool allDistinctAndSized = true;

            for (int seed = 0; seed < 50; seed++)
            {
                var results = new List<int>();
                StageRules.PickKeySpots(new StageRandom(seed), 6, required, results);
                var unique = new HashSet<int>(results);

                if (results.Count != required || unique.Count != required)
                {
                    allDistinctAndSized = false;
                }
            }

            Assert.IsTrue(allDistinctAndSized);
        }

        [Test]
        public void PickKeySpots_AllIndices_AreWithinCandidateRange()
        {
            bool inRange = true;

            for (int seed = 0; seed < 50; seed++)
            {
                var results = new List<int>();
                StageRules.PickKeySpots(new StageRandom(seed), 5, 3, results);

                for (int i = 0; i < results.Count; i++)
                {
                    if (results[i] < 0 || results[i] >= 5)
                    {
                        inRange = false;
                    }
                }
            }

            Assert.IsTrue(inRange);
        }

        [Test]
        public void PickKeySpots_SameSeed_ReturnsSameIndices()
        {
            var a = new List<int>();
            var b = new List<int>();
            StageRules.PickKeySpots(new StageRandom(777), 8, 3, a);
            StageRules.PickKeySpots(new StageRandom(777), 8, 3, b);

            CollectionAssert.AreEqual(a, b);
        }

        [Test]
        public void PickKeySpots_ManySeeds_SelectsEveryCandidateAtLeastOnce()
        {
            var hits = new int[5];

            for (int seed = 0; seed < 200; seed++)
            {
                var results = new List<int>();
                StageRules.PickKeySpots(new StageRandom(seed), hits.Length, 1, results);
                hits[results[0]]++;
            }

            Assert.That(hits, Has.All.GreaterThan(0));
        }

        [Test]
        public void PickKeySpots_MoreRequiredThanCandidates_TruncatesToCandidateCount()
        {
            var results = new List<int>();
            StageRules.PickKeySpots(new StageRandom(3), 2, 4, results);

            Assert.AreEqual(2, results.Count);
        }

        [Test]
        public void PickKeySpots_MoreRequiredThanCandidates_ReportsTruncated()
        {
            var results = new List<int>();

            Assert.IsTrue(StageRules.PickKeySpots(new StageRandom(3), 2, 4, results));
        }

        [Test]
        public void PickKeySpots_EnoughCandidates_ReportsNotTruncated()
        {
            var results = new List<int>();

            Assert.IsFalse(StageRules.PickKeySpots(new StageRandom(3), 4, 2, results));
        }

        [Test]
        public void PickKeySpots_ResultsListHasOldValues_IsClearedFirst()
        {
            var results = new List<int> { 9, 9, 9 };
            StageRules.PickKeySpots(new StageRandom(3), 4, 1, results);

            Assert.AreEqual(1, results.Count);
        }

        // ── 순찰 경로 ────────────────────────────────────────

        [Test]
        public void PickPatrolRoutes_SameSeed_ReturnsSameIndices()
        {
            var a = new List<int>();
            var b = new List<int>();
            StageRules.PickPatrolRoutes(new StageRandom(404), 13, 6, a);
            StageRules.PickPatrolRoutes(new StageRandom(404), 13, 6, b);

            CollectionAssert.AreEqual(a, b);
        }

        [Test]
        public void PickPatrolRoutes_SixOfThirteen_PicksSixDistinctIndicesInRange()
        {
            // 오케스트레이터 지시: 경로 약 13개 중 K-9 수(기본 6)만큼
            bool ok = true;

            for (int seed = 0; seed < 50; seed++)
            {
                var results = new List<int>();
                StageRules.PickPatrolRoutes(new StageRandom(seed), 13, 6, results);
                var unique = new HashSet<int>(results);

                if (results.Count != 6 || unique.Count != 6)
                {
                    ok = false;
                }

                foreach (int index in results)
                {
                    if (index < 0 || index >= 13)
                    {
                        ok = false;
                    }
                }
            }

            Assert.IsTrue(ok);
        }

        [Test]
        public void PickPatrolRoutes_ManySeeds_SelectsEveryRouteAtLeastOnce()
        {
            var hits = new int[13];

            for (int seed = 0; seed < 200; seed++)
            {
                var results = new List<int>();
                StageRules.PickPatrolRoutes(new StageRandom(seed), hits.Length, 6, results);
                foreach (int index in results)
                {
                    hits[index]++;
                }
            }

            Assert.That(hits, Has.All.GreaterThan(0));
        }

        [Test]
        public void PickPatrolRoutes_MoreK9ThanRoutes_TruncatesToRouteCount()
        {
            var results = new List<int>();
            StageRules.PickPatrolRoutes(new StageRandom(2), 4, 6, results);

            Assert.AreEqual(4, results.Count);
        }

        [Test]
        public void PickPatrolRoutes_MoreK9ThanRoutes_ReportsTruncated()
        {
            var results = new List<int>();

            Assert.IsTrue(StageRules.PickPatrolRoutes(new StageRandom(2), 4, 6, results));
        }

        [Test]
        public void PickPatrolRoutes_EnoughRoutes_ReportsNotTruncated()
        {
            var results = new List<int>();

            Assert.IsFalse(StageRules.PickPatrolRoutes(new StageRandom(2), 13, 6, results));
        }

        [Test]
        public void CreatePlan_SameSeed_ProducesSamePatrolRoutes()
        {
            StageLayout layout = StageLayoutWithRoutes(13, 6);

            CollectionAssert.AreEqual(StageRules.CreatePlan(layout, 88).PatrolRouteIndices, StageRules.CreatePlan(layout, 88).PatrolRouteIndices);
        }

        [Test]
        public void CreatePlan_K9Count_PicksThatManyRoutes()
        {
            Assert.AreEqual(6, StageRules.CreatePlan(StageLayoutWithRoutes(13, 6), 88).PatrolRouteIndices.Count);
        }

        [Test]
        public void CreatePlan_K9CountExceedsRoutes_FlagsRoutesTruncated()
        {
            Assert.IsTrue(StageRules.CreatePlan(StageLayoutWithRoutes(3, 6), 1).PatrolRoutesTruncated);
        }

        [Test]
        public void CreatePlan_AddingRouteSelection_DoesNotChangeEarlierPicks()
        {
            // 경로 선택은 난수 소비 순서의 맨 끝이다 — 경로 수가 바뀌어도 도킹·열쇠·아이템 결과는 그대로여야 한다.
            StageLayout few = StageLayoutWithRoutes(2, 1);
            StageLayout many = StageLayoutWithRoutes(13, 6);

            Assert.AreEqual(StageRules.CreatePlan(few, 9).DockingIndex, StageRules.CreatePlan(many, 9).DockingIndex);
        }

        [Test]
        public void StageLayout_NewInstance_DefaultsToSixK9()
        {
            Assert.AreEqual(6, new StageLayout().K9Count);
        }

        private static StageLayout StageLayoutWithRoutes(int routeCount, int k9Count)
        {
            StageLayout layout = StageLayoutFixtures.ValidStage1();
            layout.PatrolRoutes.Clear();
            layout.K9Spawns.Clear();
            layout.K9Count = k9Count;

            for (int i = 0; i < routeCount; i++)
            {
                var route = new PatrolRouteEntry("Route_" + i.ToString("00"), true);
                route.Waypoints.Add(new StageVector(i, 0f, 0f));
                route.Waypoints.Add(new StageVector(i, 0f, 10f));
                layout.PatrolRoutes.Add(route);
            }

            return layout;
        }

        // ── 아이템 ───────────────────────────────────────────

        [Test]
        public void RollItems_SameSeed_ReturnsSamePlacements()
        {
            StageLayout layout = StageLayoutFixtures.ValidStage1(itemSpots: 10);
            for (int i = 0; i < layout.ItemSpots.Count; i++)
            {
                layout.ItemSpots[i].SpawnChance = 0.5f;
            }

            var a = new List<ItemPlacement>();
            var b = new List<ItemPlacement>();
            StageRules.RollItems(new StageRandom(5), layout.ItemSpots, layout.KeySpots, a);
            StageRules.RollItems(new StageRandom(5), layout.ItemSpots, layout.KeySpots, b);

            CollectionAssert.AreEqual(a, b);
        }

        [Test]
        public void RollItems_ChanceOne_PlacesEverySpot()
        {
            StageLayout layout = StageLayoutFixtures.ValidStage1(itemSpots: 6);
            var results = new List<ItemPlacement>();
            StageRules.RollItems(new StageRandom(5), layout.ItemSpots, layout.KeySpots, results);

            Assert.AreEqual(6, results.Count);
        }

        [Test]
        public void RollItems_ChanceZero_PlacesNothing()
        {
            StageLayout layout = StageLayoutFixtures.ValidStage1(itemSpots: 6);
            for (int i = 0; i < layout.ItemSpots.Count; i++)
            {
                layout.ItemSpots[i].SpawnChance = 0f;
            }

            var results = new List<ItemPlacement>();
            StageRules.RollItems(new StageRandom(5), layout.ItemSpots, layout.KeySpots, results);

            Assert.AreEqual(0, results.Count);
        }

        [Test]
        public void RollItems_PickedItemId_IsFromAllowedList()
        {
            StageLayout layout = StageLayoutFixtures.ValidStage1(itemSpots: 20);
            var results = new List<ItemPlacement>();
            StageRules.RollItems(new StageRandom(11), layout.ItemSpots, layout.KeySpots, results);
            bool allAllowed = true;

            for (int i = 0; i < results.Count; i++)
            {
                if (!layout.ItemSpots[results[i].SpotIndex].AllowedItemIds.Contains(results[i].ItemId))
                {
                    allAllowed = false;
                }
            }

            Assert.IsTrue(allAllowed);
        }

        [Test]
        public void RollItems_SpotOverlappingKeyCandidate_IsExcluded()
        {
            StageLayout layout = StageLayoutFixtures.ValidStage1(keyCandidates: 2, itemSpots: 0);
            // 열쇠 후보 Key_0 과 같은 자리에 아이템 지점을 둔다.
            layout.ItemSpots.Add(new ItemSpot("Overlap", layout.KeySpots[0].Position, new List<int> { 2 }, 1f));
            layout.ItemSpots.Add(new ItemSpot("Free", new StageVector(-500f, 0f, 0f), new List<int> { 2 }, 1f));

            var results = new List<ItemPlacement>();
            StageRules.RollItems(new StageRandom(1), layout.ItemSpots, layout.KeySpots, results);

            Assert.That(results.ConvertAll(p => p.SpotId), Is.EqualTo(new List<string> { "Free" }));
        }

        [Test]
        public void RollItems_SpotWithoutAllowedItems_IsSkipped()
        {
            var spots = new List<ItemSpot>
            {
                new ItemSpot("Empty", new StageVector(0f, 0f, 0f), new List<int>(), 1f),
            };
            var results = new List<ItemPlacement>();
            StageRules.RollItems(new StageRandom(1), spots, null, results);

            Assert.AreEqual(0, results.Count);
        }

        // ── 시드 하나 → 전체 계획 ────────────────────────────

        [Test]
        public void CreatePlan_SameSeed_ProducesSameDocking()
        {
            StageLayout layout = StageLayoutFixtures.ValidStage1();

            Assert.AreEqual(StageRules.CreatePlan(layout, 2026).DockingIndex, StageRules.CreatePlan(layout, 2026).DockingIndex);
        }

        [Test]
        public void CreatePlan_SameSeed_ProducesSameKeys()
        {
            StageLayout layout = StageLayoutFixtures.ValidStage1();
            layout.RequiredKeyCount = 2;

            CollectionAssert.AreEqual(StageRules.CreatePlan(layout, 2026).KeySpotIndices, StageRules.CreatePlan(layout, 2026).KeySpotIndices);
        }

        [Test]
        public void CreatePlan_SameSeed_ProducesSameItems()
        {
            StageLayout layout = StageLayoutFixtures.ValidStage1(itemSpots: 12);
            for (int i = 0; i < layout.ItemSpots.Count; i++)
            {
                layout.ItemSpots[i].SpawnChance = 0.5f;
            }

            CollectionAssert.AreEqual(StageRules.CreatePlan(layout, 31).Items, StageRules.CreatePlan(layout, 31).Items);
        }

        [Test]
        public void CreatePlan_ManySeeds_SelectsEveryDockingAtLeastOnce()
        {
            StageLayout layout = StageLayoutFixtures.ValidStage1();
            var hits = new int[layout.DockingCandidates.Count];

            for (int seed = 0; seed < 100; seed++)
            {
                hits[StageRules.CreatePlan(layout, seed).DockingIndex]++;
            }

            Assert.That(hits, Has.All.GreaterThan(0));
        }

        [Test]
        public void CreatePlan_RequiredKeysExceedCandidates_FlagsKeysTruncated()
        {
            StageLayout layout = StageLayoutFixtures.ValidStage1(keyCandidates: 1);
            layout.RequiredKeyCount = 3;

            Assert.IsTrue(StageRules.CreatePlan(layout, 1).KeysTruncated);
        }

        [Test]
        public void CreatePlan_RecordsSeed()
        {
            Assert.AreEqual(-17, StageRules.CreatePlan(StageLayoutFixtures.ValidStage1(), -17).Seed);
        }

        [TestCase(1, 1)]
        [TestCase(2, 2)]
        [TestCase(4, 4)]
        [TestCase(0, 1)]
        public void RequiredKeysForStage_StageN_ReturnsN(int stage, int expected)
        {
            // GAME_DESIGN §3.8 단계 표: 1~4단계 필요 열쇠 1~4개
            Assert.AreEqual(expected, StageRules.RequiredKeysForStage(stage));
        }

        // ── 정렬 ─────────────────────────────────────────────

        [Test]
        public void SortById_UnorderedDocking_OrdersByOrdinalId()
        {
            var layout = new StageLayout();
            layout.DockingCandidates.Add(new DockingCandidate("B", default(StageVector), 0f));
            layout.DockingCandidates.Add(new DockingCandidate("A", default(StageVector), 0f));
            layout.SortById();

            Assert.AreEqual("A", layout.DockingCandidates[0].Id);
        }
    }
}

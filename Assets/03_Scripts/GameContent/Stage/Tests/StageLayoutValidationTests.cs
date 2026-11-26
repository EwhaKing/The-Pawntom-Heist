using System.Collections.Generic;
using NUnit.Framework;

namespace Pawntom.Stage.Tests
{
    /// <summary>
    /// 레벨 데이터 검증: 굽기 도구가 콘솔에 띄우는 오류 목록.
    /// 배치가 설계(GAME_DESIGN §3.8)를 만족하지 못하면 플레이 전에 드러나야 한다.
    /// </summary>
    public sealed class StageLayoutValidationTests
    {
        private static IReadOnlyList<StageLayoutIssue> Validate(StageLayout layout)
        {
            return StageRules.Validate(layout);
        }

        [Test]
        public void Validate_ValidStage1Layout_ReturnsNoIssues()
        {
            Assert.AreEqual(0, Validate(StageLayoutFixtures.ValidStage1()).Count);
        }

        [Test]
        public void Validate_NullLayout_ReportsMissingLayout()
        {
            Assert.IsTrue(StageLayoutFixtures.HasIssue(Validate(null), StageLayoutIssueCode.MissingLayout));
        }

        [TestCase(0)]
        [TestCase(2)]
        public void Validate_FewerThanThreeDockingCandidates_ReportsTooFewDocking(int count)
        {
            // GAME_DESIGN §3.8: 1층에 도킹 지점 3곳
            StageLayout layout = StageLayoutFixtures.ValidStage1();
            layout.DockingCandidates.RemoveRange(count, layout.DockingCandidates.Count - count);

            Assert.IsTrue(StageLayoutFixtures.HasIssue(Validate(layout), StageLayoutIssueCode.TooFewDockingCandidates));
        }

        [Test]
        public void Validate_RequiredKeysExceedCandidates_ReportsKeysExceed()
        {
            StageLayout layout = StageLayoutFixtures.ValidStage1(keyCandidates: 1);
            layout.RequiredKeyCount = 2;

            Assert.IsTrue(StageLayoutFixtures.HasIssue(Validate(layout), StageLayoutIssueCode.RequiredKeysExceedCandidates));
        }

        [Test]
        public void Validate_NonPositiveRequiredKeys_ReportsInvalidKeyCount()
        {
            StageLayout layout = StageLayoutFixtures.ValidStage1();
            layout.RequiredKeyCount = 0;

            Assert.IsTrue(StageLayoutFixtures.HasIssue(Validate(layout), StageLayoutIssueCode.InvalidRequiredKeyCount));
        }

        [Test]
        public void Validate_RouteWithOneWaypoint_ReportsTooFewWaypoints()
        {
            StageLayout layout = StageLayoutFixtures.ValidStage1();
            layout.PatrolRoutes[0].Waypoints.RemoveAt(1);

            Assert.IsTrue(StageLayoutFixtures.HasIssue(Validate(layout), StageLayoutIssueCode.TooFewWaypoints));
        }

        [Test]
        public void Validate_DuplicateDockingId_ReportsDuplicateId()
        {
            StageLayout layout = StageLayoutFixtures.ValidStage1();
            layout.DockingCandidates[1].Id = layout.DockingCandidates[0].Id;

            Assert.IsTrue(StageLayoutFixtures.HasIssue(Validate(layout), StageLayoutIssueCode.DuplicateId));
        }

        [Test]
        public void Validate_DuplicateDoorId_ReportsDuplicateId()
        {
            StageLayout layout = StageLayoutFixtures.ValidStage1();
            layout.Doors[1].Id = layout.Doors[0].Id;

            Assert.IsTrue(StageLayoutFixtures.HasIssue(Validate(layout), StageLayoutIssueCode.DuplicateId));
        }

        [Test]
        public void Validate_SameIdInDifferentCategories_IsAllowed()
        {
            StageLayout layout = StageLayoutFixtures.ValidStage1();
            layout.Vents[0].Id = layout.Doors[0].Id;

            Assert.IsFalse(StageLayoutFixtures.HasIssue(Validate(layout), StageLayoutIssueCode.DuplicateId));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void Validate_BlankKeySpotId_ReportsMissingId(string id)
        {
            StageLayout layout = StageLayoutFixtures.ValidStage1();
            layout.KeySpots[0].Id = id;

            Assert.IsTrue(StageLayoutFixtures.HasIssue(Validate(layout), StageLayoutIssueCode.MissingId));
        }

        [TestCase(-0.1f)]
        [TestCase(1.5f)]
        [TestCase(float.NaN)]
        public void Validate_SpawnChanceOutsideUnitRange_ReportsInvalidChance(float chance)
        {
            StageLayout layout = StageLayoutFixtures.ValidStage1();
            layout.ItemSpots[0].SpawnChance = chance;

            Assert.IsTrue(StageLayoutFixtures.HasIssue(Validate(layout), StageLayoutIssueCode.InvalidSpawnChance));
        }

        [Test]
        public void Validate_ItemSpotWithoutAllowedItems_ReportsNoAllowedItems()
        {
            StageLayout layout = StageLayoutFixtures.ValidStage1();
            layout.ItemSpots[0].AllowedItemIds.Clear();

            Assert.IsTrue(StageLayoutFixtures.HasIssue(Validate(layout), StageLayoutIssueCode.NoAllowedItems));
        }

        [Test]
        public void Validate_K9WithUnknownRoute_ReportsUnknownRoute()
        {
            StageLayout layout = StageLayoutFixtures.ValidStage1();
            layout.K9Spawns[0].RouteId = "Nowhere";

            Assert.IsTrue(StageLayoutFixtures.HasIssue(Validate(layout), StageLayoutIssueCode.UnknownRouteReference));
        }

        [Test]
        public void Validate_K9WithoutRoute_IsAllowed()
        {
            // EnemySpawnPoint: 경로를 비우면 제자리 대기 — 배치 오류가 아니다.
            StageLayout layout = StageLayoutFixtures.ValidStage1();
            layout.K9Spawns[0].RouteId = string.Empty;

            Assert.AreEqual(0, Validate(layout).Count);
        }

        [Test]
        public void Validate_PanelWithUnknownZone_ReportsUnknownZone()
        {
            StageLayout layout = StageLayoutFixtures.ValidStage1();
            layout.PowerPanels[0].UnlockZoneId = "Zone_Moon";

            Assert.IsTrue(StageLayoutFixtures.HasIssue(Validate(layout), StageLayoutIssueCode.UnknownZoneReference));
        }

        [Test]
        public void Validate_K9CountExceedsRoutes_ReportsK9Exceed()
        {
            StageLayout layout = StageLayoutFixtures.ValidStage1();
            layout.K9Count = layout.PatrolRoutes.Count + 1;

            Assert.IsTrue(StageLayoutFixtures.HasIssue(Validate(layout), StageLayoutIssueCode.K9CountExceedsRoutes));
        }

        [Test]
        public void Validate_K9CountExceedsRoutes_IsWarningNotError()
        {
            StageLayout layout = StageLayoutFixtures.ValidStage1();
            layout.K9Count = layout.PatrolRoutes.Count + 1;
            List<StageLayoutIssue> issues = StageRules.Validate(layout);

            Assert.AreEqual(StageLayoutIssueSeverity.Warning, issues.Find(i => i.Code == StageLayoutIssueCode.K9CountExceedsRoutes).Severity);
        }

        [Test]
        public void Validate_NegativeK9Count_ReportsInvalidK9Count()
        {
            StageLayout layout = StageLayoutFixtures.ValidStage1();
            layout.K9Count = -1;

            Assert.IsTrue(StageLayoutFixtures.HasIssue(Validate(layout), StageLayoutIssueCode.InvalidK9Count));
        }

        [Test]
        public void Validate_DuplicateId_IsError()
        {
            StageLayout layout = StageLayoutFixtures.ValidStage1();
            layout.Doors[1].Id = layout.Doors[0].Id;
            List<StageLayoutIssue> issues = StageRules.Validate(layout);

            Assert.AreEqual(StageLayoutIssueSeverity.Error, issues.Find(i => i.Code == StageLayoutIssueCode.DuplicateId).Severity);
        }

        [Test]
        public void Validate_StageZero_ReportsInvalidStage()
        {
            StageLayout layout = StageLayoutFixtures.ValidStage1();
            layout.StageNumber = 0;

            Assert.IsTrue(StageLayoutFixtures.HasIssue(Validate(layout), StageLayoutIssueCode.InvalidStageNumber));
        }

        [Test]
        public void Validate_FloorAboveStage_ReportsInvalidFloor()
        {
            // GAME_DESIGN §3.8: N단계 접근 가능 층은 1~N층
            StageLayout layout = StageLayoutFixtures.ValidStage1();
            layout.FloorNumber = 2;

            Assert.IsTrue(StageLayoutFixtures.HasIssue(Validate(layout), StageLayoutIssueCode.InvalidFloorNumber));
        }
    }
}

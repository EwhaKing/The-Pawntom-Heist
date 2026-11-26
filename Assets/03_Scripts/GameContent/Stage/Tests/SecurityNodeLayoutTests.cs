using System.Collections.Generic;
using NUnit.Framework;

namespace Pawntom.Stage.Tests
{
    /// <summary>
    /// 분전반이 레벨 데이터에 자기와 해금 구역을 기록하는 규칙(GAME_DESIGN §3.4 링크 트리거 → 구역 해금, §3.8 레벨 데이터 분리).
    /// </summary>
    public sealed class SecurityNodeLayoutTests
    {
        private static readonly StageVector Origin = new StageVector(1f, 0f, 2f);

        [Test]
        public void Append_PanelWithZones_AddsOnePowerPanel()
        {
            var layout = new StageLayout();

            SecurityNodeLayout.Append(layout, "Panel_A", Origin, null, new List<string> { "Zone_East" });

            Assert.AreEqual(1, layout.PowerPanels.Count);
        }

        [Test]
        public void Append_NoExplicitUnlockZone_UsesFirstLinkedZone()
        {
            var layout = new StageLayout();

            SecurityNodeLayout.Append(layout, "Panel_A", Origin, "", new List<string> { "Zone_East", "Zone_West" });

            Assert.AreEqual("Zone_East", layout.PowerPanels[0].UnlockZoneId);
        }

        [Test]
        public void Append_ExplicitUnlockZone_UsesIt()
        {
            var layout = new StageLayout();

            SecurityNodeLayout.Append(layout, "Panel_A", Origin, "Zone_West", new List<string> { "Zone_East", "Zone_West" });

            Assert.AreEqual("Zone_West", layout.PowerPanels[0].UnlockZoneId);
        }

        [Test]
        public void Append_LinkedZones_AddedAsZoneEntries()
        {
            var layout = new StageLayout();

            SecurityNodeLayout.Append(layout, "Panel_A", Origin, null, new List<string> { "Zone_East", "Zone_West" });

            Assert.AreEqual(2, layout.Zones.Count);
        }

        [Test]
        public void Append_TwoPanelsSameZone_ZoneAddedOnce()
        {
            // 같은 구역을 두 분전반이 해금해도 Zone Id 중복 오류가 나지 않게 한다(StageRules.CheckIds).
            var layout = new StageLayout();

            SecurityNodeLayout.Append(layout, "Panel_A", Origin, null, new List<string> { "Zone_East" });
            SecurityNodeLayout.Append(layout, "Panel_B", Origin, null, new List<string> { "Zone_East" });

            Assert.AreEqual(1, layout.Zones.Count);
        }

        [Test]
        public void Append_BlankZoneIds_Skipped()
        {
            var layout = new StageLayout();

            SecurityNodeLayout.Append(layout, "Panel_A", Origin, null, new List<string> { "", "  ", null, "Zone_East" });

            Assert.AreEqual(1, layout.Zones.Count);
        }

        [Test]
        public void Append_ValidPanel_PassesZoneReferenceValidation()
        {
            var layout = new StageLayout();

            SecurityNodeLayout.Append(layout, "Panel_A", Origin, null, new List<string> { "Zone_East" });

            Assert.IsFalse(StageLayoutFixtures.HasIssue(StageRules.Validate(layout), StageLayoutIssueCode.UnknownZoneReference));
        }
    }
}

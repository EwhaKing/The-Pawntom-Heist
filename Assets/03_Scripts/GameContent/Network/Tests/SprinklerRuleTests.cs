#if UNITY_INCLUDE_TESTS
using NUnit.Framework;

namespace Pawntom.Network.Tests
{
    /// <summary>GAME_DESIGN §3.4 스프링클러: 1회용, 발견된 구역에서만.</summary>
    public class SprinklerRuleTests
    {
        [Test]
        public void Evaluate_Unused_Accepted()
        {
            Assert.AreEqual(RequestVerdict.Accepted, SprinklerRule.Evaluate(false, true, HostRequestRulesTests.Ok()));
        }

        [Test]
        public void Evaluate_AlreadyUsed_AlreadyApplied()
        {
            Assert.AreEqual(RequestVerdict.AlreadyApplied, SprinklerRule.Evaluate(true, true, HostRequestRulesTests.Ok()));
        }

        [Test]
        public void Evaluate_AreaUndiscovered_AreaUndiscovered()
        {
            Assert.AreEqual(RequestVerdict.AreaUndiscovered, SprinklerRule.Evaluate(false, false, HostRequestRulesTests.Ok()));
        }

        [Test]
        public void Evaluate_AwayFromTerminal_NotAtTerminal()
        {
            RequesterFacts away = HostRequestRulesTests.Ok();
            away.InPlace = false;

            Assert.AreEqual(RequestVerdict.NotAtTerminal, SprinklerRule.Evaluate(false, true, away));
        }
    }
}
#endif

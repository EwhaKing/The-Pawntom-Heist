#if UNITY_INCLUDE_TESTS
using NUnit.Framework;

namespace Pawntom.Network.Tests
{
    /// <summary>GAME_DESIGN §3.4 전등: 미니게임 성공 → 소등 → 5초 후 자동 점등. 해금 뒤에는 단말기에서 켜고 끈다.</summary>
    public class LightCommandRuleTests
    {
        // unlocked, on, areaDiscovered → expected
        [TestCase(LightCommand.UnlockAndTurnOff, false, true, false, RequestVerdict.AreaUndiscovered)]
        [TestCase(LightCommand.TurnOn, true, false, false, RequestVerdict.AreaUndiscovered)]
        [TestCase(LightCommand.UnlockAndTurnOff, false, true, true, RequestVerdict.Accepted)]
        [TestCase(LightCommand.UnlockAndTurnOff, true, false, true, RequestVerdict.AlreadyApplied)]
        [TestCase(LightCommand.UnlockAndTurnOff, true, true, true, RequestVerdict.Accepted)]
        [TestCase(LightCommand.Unlock, false, true, true, RequestVerdict.Accepted)]
        [TestCase(LightCommand.Unlock, true, true, true, RequestVerdict.AlreadyApplied)]
        [TestCase(LightCommand.TurnOff, false, true, true, RequestVerdict.Locked)]
        [TestCase(LightCommand.TurnOn, false, false, true, RequestVerdict.Locked)]
        [TestCase(LightCommand.TurnOff, true, false, true, RequestVerdict.AlreadyApplied)]
        [TestCase(LightCommand.TurnOn, true, true, true, RequestVerdict.AlreadyApplied)]
        [TestCase(LightCommand.TurnOff, true, true, true, RequestVerdict.Accepted)]
        [TestCase(LightCommand.TurnOn, true, false, true, RequestVerdict.Accepted)]
        public void CheckTransition_State_Verdict(LightCommand command, bool unlocked, bool on, bool discovered, RequestVerdict expected)
        {
            Assert.AreEqual(expected, LightCommandRule.CheckTransition(command, unlocked, on, discovered));
        }

        [Test]
        public void Evaluate_AwayFromTerminal_NotAtTerminal()
        {
            RequesterFacts away = HostRequestRulesTests.Ok();
            away.InPlace = false;

            Assert.AreEqual(RequestVerdict.NotAtTerminal, LightCommandRule.Evaluate(LightCommand.TurnOff, true, true, true, away));
        }

        [Test]
        public void Evaluate_AtTerminal_Accepted()
        {
            Assert.AreEqual(RequestVerdict.Accepted,
                LightCommandRule.Evaluate(LightCommand.TurnOff, true, true, true, HostRequestRulesTests.Ok()));
        }
    }
}
#endif

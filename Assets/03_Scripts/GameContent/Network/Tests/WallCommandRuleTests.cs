#if UNITY_INCLUDE_TESTS
using NUnit.Framework;

namespace Pawntom.Network.Tests
{
    /// <summary>GAME_DESIGN §3.4 격벽 상태 머신(Locked → Unlocked → Open → 5초 후 Unlocked) 요청 판정.</summary>
    public class WallCommandRuleTests
    {
        // unlocked, open, fieldAllowed → expected
        [TestCase(WallCommand.Unlock, false, false, true, RequestVerdict.Accepted)]
        [TestCase(WallCommand.Unlock, true, false, true, RequestVerdict.AlreadyApplied)]
        [TestCase(WallCommand.FieldOpen, false, false, true, RequestVerdict.Locked)]
        [TestCase(WallCommand.FieldOpen, true, true, true, RequestVerdict.AlreadyApplied)]
        [TestCase(WallCommand.FieldOpen, true, false, true, RequestVerdict.Accepted)]
        [TestCase(WallCommand.FieldOpen, true, false, false, RequestVerdict.Disabled)]
        [TestCase(WallCommand.RemoteOpen, false, false, true, RequestVerdict.Locked)]
        [TestCase(WallCommand.RemoteOpen, true, false, true, RequestVerdict.Accepted)]
        [TestCase(WallCommand.RemoteOpen, true, true, true, RequestVerdict.AlreadyApplied)]
        [TestCase(WallCommand.RemoteClose, true, false, true, RequestVerdict.AlreadyApplied)]
        [TestCase(WallCommand.RemoteClose, true, true, true, RequestVerdict.Accepted)]
        [TestCase(WallCommand.RemoteClose, false, true, true, RequestVerdict.Accepted)]
        public void CheckTransition_State_Verdict(WallCommand command, bool unlocked, bool open, bool fieldAllowed, RequestVerdict expected)
        {
            Assert.AreEqual(expected, WallCommandRule.CheckTransition(command, unlocked, open, fieldAllowed));
        }

        [Test]
        public void Evaluate_SecondFieldOpenAfterFirstApplied_AlreadyApplied()
        {
            // 같은 요청이 두 번 오면 두 번째는 반영된 상태(열림)로 판정된다.
            Assert.AreEqual(RequestVerdict.AlreadyApplied,
                WallCommandRule.Evaluate(WallCommand.FieldOpen, true, true, true, HostRequestRulesTests.Ok()));
        }

        [Test]
        public void Evaluate_FieldOpenAwayFromDoor_OutOfRange()
        {
            RequesterFacts away = HostRequestRulesTests.Ok();
            away.InPlace = false;

            Assert.AreEqual(RequestVerdict.OutOfRange, WallCommandRule.Evaluate(WallCommand.FieldOpen, true, false, true, away));
        }

        [TestCase(WallCommand.Unlock, false, false)]
        [TestCase(WallCommand.RemoteOpen, true, false)]
        [TestCase(WallCommand.RemoteClose, true, true)]
        public void Evaluate_RemoteAwayFromTerminal_NotAtTerminal(WallCommand command, bool unlocked, bool open)
        {
            RequesterFacts away = HostRequestRulesTests.Ok();
            away.InPlace = false;

            Assert.AreEqual(RequestVerdict.NotAtTerminal, WallCommandRule.Evaluate(command, unlocked, open, true, away));
        }

        [TestCase(WallCommand.Unlock, false)]
        [TestCase(WallCommand.FieldOpen, true)]
        [TestCase(WallCommand.RemoteOpen, false)]
        [TestCase(WallCommand.RemoteClose, false)]
        public void IsField_Command_OnlyFieldOpen(WallCommand command, bool expected)
        {
            Assert.AreEqual(expected, WallCommandRule.IsField(command));
        }
    }
}
#endif

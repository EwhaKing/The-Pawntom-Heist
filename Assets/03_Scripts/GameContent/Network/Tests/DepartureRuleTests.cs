#if UNITY_INCLUDE_TESTS
using NUnit.Framework;

namespace Pawntom.Network.Tests
{
    /// <summary>
    /// 출발(레벨 클리어) 판정. RuleManager 클리어 조건: 본부 안에 산 플레이어 1명 이상 + 캣닢 보유 + 출발 버튼.
    /// </summary>
    public class DepartureRuleTests
    {
        private static DepartureFacts Ready()
        {
            return new DepartureFacts
            {
                AlreadyCleared = false,
                ConfigValid = true,
                RequesterValid = true,
                RequesterDead = false,
                CoolingDown = false,
                AnyAlivePlayerInHeadquarters = true,
                HasExitItem = true,
            };
        }

        [Test]
        public void Evaluate_AllSatisfied_Accepted()
        {
            Assert.AreEqual(DepartureVerdict.Accepted, DepartureRule.Evaluate(Ready()));
        }

        [Test]
        public void Evaluate_AlreadyClearedAndNoExit_AlreadyCleared()
        {
            DepartureFacts f = Ready();
            f.AlreadyCleared = true;
            f.HasExitItem = false;

            Assert.AreEqual(DepartureVerdict.AlreadyCleared, DepartureRule.Evaluate(f));
        }

        [Test]
        public void Evaluate_NobodyInHeadquartersAndNoExit_NoAlivePlayerInHeadquarters()
        {
            DepartureFacts f = Ready();
            f.AnyAlivePlayerInHeadquarters = false;
            f.HasExitItem = false;

            Assert.AreEqual(DepartureVerdict.NoAlivePlayerInHeadquarters, DepartureRule.Evaluate(f));
        }

        [Test]
        public void Evaluate_NoExitItem_NoExitItem()
        {
            DepartureFacts f = Ready();
            f.HasExitItem = false;

            Assert.AreEqual(DepartureVerdict.NoExitItem, DepartureRule.Evaluate(f));
        }

        [Test]
        public void Evaluate_RequesterInvalid_RequesterInvalid()
        {
            DepartureFacts f = Ready();
            f.RequesterValid = false;

            Assert.AreEqual(DepartureVerdict.RequesterInvalid, DepartureRule.Evaluate(f));
        }

        [Test]
        public void Evaluate_RequesterDead_RequesterDead()
        {
            DepartureFacts f = Ready();
            f.RequesterDead = true;

            Assert.AreEqual(DepartureVerdict.RequesterDead, DepartureRule.Evaluate(f));
        }

        [Test]
        public void Evaluate_CoolingDown_CoolingDown()
        {
            DepartureFacts f = Ready();
            f.CoolingDown = true;

            Assert.AreEqual(DepartureVerdict.CoolingDown, DepartureRule.Evaluate(f));
        }

        [Test]
        public void Evaluate_Misconfigured_Misconfigured()
        {
            DepartureFacts f = Ready();
            f.ConfigValid = false;

            Assert.AreEqual(DepartureVerdict.Misconfigured, DepartureRule.Evaluate(f));
        }
    }
}
#endif

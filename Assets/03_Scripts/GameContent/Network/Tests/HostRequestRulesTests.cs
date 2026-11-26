#if UNITY_INCLUDE_TESTS
using NUnit.Framework;

namespace Pawntom.Network.Tests
{
    /// <summary>
    /// 상태 권한이 요청자를 다시 판정하는 공통 순서: 요청자 → 사망 → 상태 전이 → 위치 → 쿨다운.
    /// </summary>
    public class HostRequestRulesTests
    {
        internal static RequesterFacts Ok()
        {
            return new RequesterFacts { Valid = true, Dead = false, InPlace = true, CoolingDown = false };
        }

        [Test]
        public void Combine_RequesterInvalidEvenIfDead_RequesterInvalid()
        {
            RequesterFacts r = Ok();
            r.Valid = false;
            r.Dead = true;

            Assert.AreEqual(RequestVerdict.RequesterInvalid,
                HostRequestRule.Combine(r, RequestVerdict.Accepted, RequestVerdict.OutOfRange));
        }

        [Test]
        public void Combine_RequesterDead_RequesterDead()
        {
            RequesterFacts r = Ok();
            r.Dead = true;

            Assert.AreEqual(RequestVerdict.RequesterDead,
                HostRequestRule.Combine(r, RequestVerdict.Accepted, RequestVerdict.OutOfRange));
        }

        [Test]
        public void Combine_TransitionInvalidAndOutOfPlace_ReportsTransitionFirst()
        {
            RequesterFacts r = Ok();
            r.InPlace = false;

            Assert.AreEqual(RequestVerdict.AlreadyApplied,
                HostRequestRule.Combine(r, RequestVerdict.AlreadyApplied, RequestVerdict.NotAtTerminal));
        }

        [TestCase(RequestVerdict.NotAtTerminal)]
        [TestCase(RequestVerdict.OutOfRange)]
        public void Combine_OutOfPlace_ReportsCommandPlaceFailure(RequestVerdict placeFailure)
        {
            RequesterFacts r = Ok();
            r.InPlace = false;

            Assert.AreEqual(placeFailure, HostRequestRule.Combine(r, RequestVerdict.Accepted, placeFailure));
        }

        [Test]
        public void Combine_CoolingDown_CoolingDown()
        {
            RequesterFacts r = Ok();
            r.CoolingDown = true;

            Assert.AreEqual(RequestVerdict.CoolingDown,
                HostRequestRule.Combine(r, RequestVerdict.Accepted, RequestVerdict.OutOfRange));
        }

        [Test]
        public void Combine_AllSatisfied_Accepted()
        {
            Assert.AreEqual(RequestVerdict.Accepted,
                HostRequestRule.Combine(Ok(), RequestVerdict.Accepted, RequestVerdict.OutOfRange));
        }
    }
}
#endif

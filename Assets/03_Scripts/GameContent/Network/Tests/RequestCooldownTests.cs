#if UNITY_INCLUDE_TESTS
using NUnit.Framework;

namespace Pawntom.Network.Tests
{
    public class RequestCooldownTests
    {
        [Test]
        public void IsCoolingDown_SameRequesterWithinWindow_True()
        {
            var cooldown = new RequestCooldown(10);
            cooldown.Record(1, 100);

            Assert.IsTrue(cooldown.IsCoolingDown(1, 105));
        }

        [Test]
        public void IsCoolingDown_OtherRequester_False()
        {
            var cooldown = new RequestCooldown(10);
            cooldown.Record(1, 100);

            Assert.IsFalse(cooldown.IsCoolingDown(2, 105));
        }

        [TestCase(109, true)]
        [TestCase(110, false)]
        [TestCase(200, false)]
        public void IsCoolingDown_AtTick_MatchesWindow(int tick, bool expected)
        {
            var cooldown = new RequestCooldown(10);
            cooldown.Record(1, 100);

            Assert.AreEqual(expected, cooldown.IsCoolingDown(1, tick));
        }

        [TestCase(100)]
        [TestCase(90)]
        public void IsCoolingDown_SameOrEarlierTick_True(int tick)
        {
            // 같은 틱 재전송·순서가 뒤바뀐 요청은 막는다.
            var cooldown = new RequestCooldown(10);
            cooldown.Record(1, 100);

            Assert.IsTrue(cooldown.IsCoolingDown(1, tick));
        }

        [Test]
        public void IsCoolingDown_ZeroTicks_False()
        {
            var cooldown = new RequestCooldown(0);
            cooldown.Record(1, 100);

            Assert.IsFalse(cooldown.IsCoolingDown(1, 100));
        }

        [Test]
        public void IsCoolingDown_AfterClear_False()
        {
            var cooldown = new RequestCooldown(10);
            cooldown.Record(1, 100);
            cooldown.Clear();

            Assert.IsFalse(cooldown.IsCoolingDown(1, 101));
        }

        [Test]
        public void IsCoolingDown_NeverRecorded_False()
        {
            var cooldown = new RequestCooldown(10);

            Assert.IsFalse(cooldown.IsCoolingDown(1, 0));
        }
    }
}
#endif

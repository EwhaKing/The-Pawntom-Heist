#if UNITY_INCLUDE_TESTS
using NUnit.Framework;

namespace Pawntom.Network.Tests
{
    /// <summary>레벨 클리어처럼 [Networked] 값이 참이 된 순간 피어마다 부작용을 정확히 1회 내는 래치.</summary>
    public class OneShotLatchTests
    {
        [Test]
        public void Observe_FirstTrue_Fires()
        {
            var latch = new OneShotLatch();
            latch.Observe(false);

            Assert.IsTrue(latch.Observe(true));
        }

        [Test]
        public void Observe_TrueRepeated_FiresOnce()
        {
            var latch = new OneShotLatch();
            latch.Observe(true);

            Assert.IsFalse(latch.Observe(true));
        }

        [Test]
        public void Observe_False_DoesNotFire()
        {
            var latch = new OneShotLatch();

            Assert.IsFalse(latch.Observe(false));
        }

        [Test]
        public void Observe_LateJoinerFirstSeesTrue_Fires()
        {
            // 늦은 참가자는 이전 값을 모른다 — 첫 관찰이 참이어도 1회 울린다.
            Assert.IsTrue(new OneShotLatch().Observe(true));
        }

        [Test]
        public void Observe_TrueAgainAfterFalse_FiresAgain()
        {
            var latch = new OneShotLatch();
            latch.Observe(true);
            latch.Observe(false);

            Assert.IsTrue(latch.Observe(true));
        }

        [Test]
        public void Observe_AfterReset_FiresAgain()
        {
            var latch = new OneShotLatch();
            latch.Observe(true);
            latch.Reset();

            Assert.IsTrue(latch.Observe(true));
        }
    }
}
#endif

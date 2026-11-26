using NUnit.Framework;

namespace Pawntom.Settings.Tests
{
    public sealed class StageHudMathTests
    {
        [TestCase(0f, 0f)]
        [TestCase(0.25f, 90f)]
        [TestCase(0.5f, 180f)]
        [TestCase(1f, 360f)]
        [TestCase(1.7f, 360f)]
        [TestCase(-0.3f, 0f)]
        public void ArcDegrees_ProgressMapsToClockwiseDegrees(float progress, float expected)
        {
            Assert.AreEqual(expected, StageHudMath.ArcDegrees(progress), 0.001f);
        }

        [TestCase(0f, 0)]
        [TestCase(0.333f, 33)]
        [TestCase(0.999f, 99)]
        [TestCase(1f, 100)]
        [TestCase(2f, 100)]
        public void Percent_FloorsAndClamps(float progress, int expected)
        {
            Assert.AreEqual(expected, StageHudMath.Percent(progress));
        }

        [Test]
        public void ArcDegrees_NaN_IsZero()
        {
            Assert.AreEqual(0f, StageHudMath.ArcDegrees(float.NaN), 0.001f);
        }

        [Test]
        public void AlarmPulse_AtCycleStart_IsMinimum()
        {
            Assert.AreEqual(0.62f, StageHudMath.AlarmPulse(0f, 2.4f, 0.62f), 0.001f);
        }

        [Test]
        public void AlarmPulse_AtHalfCycle_IsOne()
        {
            Assert.AreEqual(1f, StageHudMath.AlarmPulse(1.2f, 2.4f, 0.62f), 0.001f);
        }

        [Test]
        public void AlarmPulse_AtFullCycle_ReturnsToMinimum()
        {
            Assert.AreEqual(0.62f, StageHudMath.AlarmPulse(2.4f, 2.4f, 0.62f), 0.001f);
        }

        [Test]
        public void AlarmPulse_ZeroCycle_IsOne()
        {
            Assert.AreEqual(1f, StageHudMath.AlarmPulse(5f, 0f, 0.5f), 0.001f);
        }

        [TestCase(0f, 0f)]
        [TestCase(0.5f, 0.5f)]
        [TestCase(1f, 1f)]
        [TestCase(3f, 1f)]
        public void SmoothFade_EndpointsAndMidpoint(float t, float expected)
        {
            Assert.AreEqual(expected, StageHudMath.SmoothFade(t), 0.001f);
        }

        [TestCase(0f, 1f)]
        [TestCase(0.6f, 0.25f)]
        [TestCase(1.2f, 0f)]
        [TestCase(5f, 0f)]
        [TestCase(-1f, 0f)]
        public void BurstDecay_FallsFromOneToZero(float elapsed, float expected)
        {
            Assert.AreEqual(expected, StageHudMath.BurstDecay(elapsed, 1.2f), 0.001f);
        }

        [Test]
        public void TimedMessage_BeforeShow_IsHidden()
        {
            Assert.AreEqual(0f, new TimedMessage().Alpha(1f, 0.2f), 0.001f);
        }

        [Test]
        public void TimedMessage_MidHold_IsOpaque()
        {
            var message = new TimedMessage();
            message.Show("a", 10f, 2f);
            Assert.AreEqual(1f, message.Alpha(11f, 0.2f), 0.001f);
        }

        [Test]
        public void TimedMessage_JustShown_FadesIn()
        {
            var message = new TimedMessage();
            message.Show("a", 10f, 2f);
            Assert.AreEqual(0.5f, message.Alpha(10.1f, 0.2f), 0.001f);
        }

        [Test]
        public void TimedMessage_NearExpiry_FadesOut()
        {
            var message = new TimedMessage();
            message.Show("a", 10f, 2f);
            Assert.AreEqual(0.25f, message.Alpha(11.95f, 0.2f), 0.001f);
        }

        [Test]
        public void TimedMessage_AfterExpiry_IsHidden()
        {
            var message = new TimedMessage();
            message.Show("a", 10f, 2f);
            Assert.IsFalse(message.IsVisible(12.5f));
        }

        [Test]
        public void TimedMessage_Clear_Hides()
        {
            var message = new TimedMessage();
            message.Show("a", 10f, 2f);
            message.Clear();
            Assert.IsFalse(message.IsVisible(10.5f));
        }
    }
}

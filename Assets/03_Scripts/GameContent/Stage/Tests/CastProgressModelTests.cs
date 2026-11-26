using NUnit.Framework;

namespace Pawntom.Stage.Tests
{
    /// <summary>
    /// 분전반 길게 누르기 캐스팅(GAME_DESIGN §3.4 링크 트리거: F 3초 캐스팅, 이동 불가).
    /// </summary>
    public sealed class CastProgressModelTests
    {
        // GAME_DESIGN §3.4: 분전반 캐스팅 3초
        private const float CastSeconds = 3f;

        private static CastProgressModel StartedModel()
        {
            var model = new CastProgressModel(CastSeconds);
            model.Tick(0f, true, false, true);
            return model;
        }

        [Test]
        public void Tick_FirstHoldInRange_ReturnsStarted()
        {
            var model = new CastProgressModel(CastSeconds);

            Assert.AreEqual(CastStep.Started, model.Tick(0.1f, true, false, true));
        }

        [Test]
        public void Tick_NotHolding_StaysIdle()
        {
            var model = new CastProgressModel(CastSeconds);

            Assert.AreEqual(CastStep.Idle, model.Tick(0.1f, false, false, true));
        }

        [Test]
        public void Tick_HoldWhileMoving_DoesNotStart()
        {
            var model = new CastProgressModel(CastSeconds);

            Assert.AreEqual(CastStep.Idle, model.Tick(0.1f, true, true, true));
        }

        [Test]
        public void Tick_HoldOutOfRange_DoesNotStart()
        {
            var model = new CastProgressModel(CastSeconds);

            Assert.AreEqual(CastStep.Idle, model.Tick(0.1f, true, false, false));
        }

        [Test]
        public void Tick_HoldingHalfDuration_ProgressIsHalf()
        {
            CastProgressModel model = StartedModel();

            model.Tick(1.5f, true, false, true);

            Assert.AreEqual(0.5f, model.Progress, 0.0001f);
        }

        [Test]
        public void Tick_HoldingBeforeDuration_ReturnsProgressed()
        {
            CastProgressModel model = StartedModel();

            Assert.AreEqual(CastStep.Progressed, model.Tick(1f, true, false, true));
        }

        [Test]
        public void Tick_HoldingFullDuration_ReturnsCompleted()
        {
            CastProgressModel model = StartedModel();
            model.Tick(1.5f, true, false, true);

            Assert.AreEqual(CastStep.Completed, model.Tick(1.5f, true, false, true));
        }

        [Test]
        public void Tick_AfterCompleted_NeverCompletesAgain()
        {
            CastProgressModel model = StartedModel();
            model.Tick(CastSeconds, true, false, true);

            Assert.AreEqual(CastStep.Idle, model.Tick(CastSeconds, true, false, true));
        }

        [Test]
        public void Tick_AfterCompleted_IsCompletedStaysTrue()
        {
            CastProgressModel model = StartedModel();
            model.Tick(CastSeconds, true, false, true);
            model.Tick(1f, false, false, true);

            Assert.IsTrue(model.IsCompleted);
        }

        [TestCase(false, false, true, TestName = "Tick_ReleasedDuringCast_ReturnsCanceled")]
        [TestCase(true, true, true, TestName = "Tick_MovedDuringCast_ReturnsCanceled")]
        [TestCase(true, false, false, TestName = "Tick_LeftRangeDuringCast_ReturnsCanceled")]
        public void Tick_InterruptedDuringCast_ReturnsCanceled(bool holding, bool moved, bool inRange)
        {
            CastProgressModel model = StartedModel();
            model.Tick(1f, true, false, true);

            Assert.AreEqual(CastStep.Canceled, model.Tick(0.1f, holding, moved, inRange));
        }

        [Test]
        public void Tick_Canceled_ResetsProgressToZero()
        {
            CastProgressModel model = StartedModel();
            model.Tick(2f, true, false, true);
            model.Tick(0.1f, false, false, true);

            Assert.AreEqual(0f, model.Progress, 0.0001f);
        }

        [Test]
        public void Tick_CanceledByMoveWhileStillHolding_DoesNotRestartUntilReleased()
        {
            CastProgressModel model = StartedModel();
            model.Tick(1f, true, true, true);

            Assert.AreEqual(CastStep.Idle, model.Tick(0.1f, true, false, true));
        }

        [Test]
        public void Tick_CanceledThenReleasedAndHeldAgain_Restarts()
        {
            CastProgressModel model = StartedModel();
            model.Tick(1f, true, true, true);
            model.Tick(0.1f, false, false, true);

            Assert.AreEqual(CastStep.Started, model.Tick(0.1f, true, false, true));
        }

        [Test]
        public void Tick_NegativeDelta_DoesNotReduceProgress()
        {
            CastProgressModel model = StartedModel();
            model.Tick(1.5f, true, false, true);
            model.Tick(-1f, true, false, true);

            Assert.AreEqual(0.5f, model.Progress, 0.0001f);
        }

        [Test]
        public void Cancel_WhileCasting_ReturnsTrue()
        {
            CastProgressModel model = StartedModel();

            Assert.IsTrue(model.Cancel());
        }

        [Test]
        public void Cancel_WhenIdle_ReturnsFalse()
        {
            var model = new CastProgressModel(CastSeconds);

            Assert.IsFalse(model.Cancel());
        }

        [Test]
        public void Reset_AfterCompleted_AllowsNewCast()
        {
            CastProgressModel model = StartedModel();
            model.Tick(CastSeconds, true, false, true);
            model.Reset();

            Assert.AreEqual(CastStep.Started, model.Tick(0f, true, false, true));
        }

        [Test]
        public void Constructor_NonPositiveDuration_UsesMinimumDuration()
        {
            var model = new CastProgressModel(0f);

            Assert.Greater(model.Duration, 0f);
        }
    }
}

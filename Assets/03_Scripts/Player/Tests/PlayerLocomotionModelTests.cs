using NUnit.Framework;
using Pawntom.PlayerAnimation;

namespace Pawntom.PlayerAnimation.Tests
{
    public sealed class PlayerLocomotionModelTests
    {
        // PlayerController 기본값: walkSpeed 30, sprintSpeed 60
        private const float Walk = 30f;
        private const float Sprint = 60f;

        private static PlayerLocomotionModel Instant()
        {
            return new PlayerLocomotionModel(Walk, Sprint, 0f, 0.12f, 0.5f);
        }

        [TestCase(0f, 0f)]
        [TestCase(15f, 0.5f)]
        [TestCase(30f, 1f)]
        [TestCase(45f, 1.5f)]
        [TestCase(60f, 2f)]
        [TestCase(120f, 2f)]
        public void TargetLocomotion_HorizontalSpeed_MapsToIdleWalkRun(float speed, float expected)
        {
            Assert.AreEqual(expected, Instant().TargetLocomotion(speed), 0.0001f);
        }

        [Test]
        public void Tick_WithSmoothing_MovesPartwayTowardTarget()
        {
            var model = new PlayerLocomotionModel(Walk, Sprint, 10f, 0.12f, 0.5f);

            model.Tick(Walk, 0f, true, 0.016f);

            Assert.That(model.Locomotion, Is.GreaterThan(0f).And.LessThan(1f));
        }

        [Test]
        public void Tick_UngroundedShorterThanGrace_StaysGrounded()
        {
            // 계단 한 칸처럼 아주 잠깐 발이 떨어지면 점프 동작을 내지 않는다.
            var model = Instant();

            model.Tick(Walk, -1f, false, 0.05f);

            Assert.IsFalse(model.Airborne);
        }

        [Test]
        public void Tick_UngroundedLongerThanGrace_BecomesAirborne()
        {
            var model = Instant();

            model.Tick(Walk, -1f, false, 0.08f);
            model.Tick(Walk, -1f, false, 0.08f);

            Assert.IsTrue(model.Airborne);
        }

        [Test]
        public void Tick_RisingFasterThanThreshold_BecomesAirborneImmediately()
        {
            var model = Instant();

            model.Tick(0f, 3f, false, 0.016f);

            Assert.IsTrue(model.Airborne);
        }

        [Test]
        public void Tick_Landing_ClearsAirborne()
        {
            var model = Instant();
            model.Tick(0f, 3f, false, 0.016f);

            model.Tick(0f, 0f, true, 0.016f);

            Assert.IsFalse(model.Airborne);
        }

        [Test]
        public void Constructor_SprintNotAboveWalk_UsesDoubleWalkAsRun()
        {
            var model = new PlayerLocomotionModel(Walk, Walk, 0f, 0.12f, 0.5f);

            Assert.AreEqual(2f, model.TargetLocomotion(Walk * 2f), 0.0001f);
        }
    }
}

using NUnit.Framework;
using Pawntom.Player.Core;
using UnityEngine;

namespace Pawntom.Player.Tests
{
    public sealed class FirstPersonArmMotionTests
    {
        private static ArmMotionInput Frame(float locomotion = 0f, bool grounded = true, float vertical = 0f)
        {
            return new ArmMotionInput { DeltaTime = 1f / 60f, Locomotion = locomotion, Grounded = grounded, VerticalSpeed = vertical };
        }

        private static float MaxAbsY(FirstPersonArmMotion motion, ArmMotionInput input, int frames)
        {
            float max = 0f;
            for (int i = 0; i < frames; i++)
            {
                motion.Tick(input);
                max = Mathf.Max(max, Mathf.Abs(motion.PositionOffset.y));
            }
            return max;
        }

        [TestCase(0f, 0f)]
        [TestCase(0.09f, 1f)]
        [TestCase(0.11f, 1f)]
        [TestCase(0.33f, 0f)]
        [TestCase(-0.1f, 0f)]
        public void ReachCurve_Timeline_OutHoldBack(float t, float expected)
        {
            // 기본값: 뻗기 0.09초, 머무름 0.04초, 복귀 0.2초
            Assert.AreEqual(expected, FirstPersonArmMotion.ReachCurve(t, 0.09f, 0.04f, 0.2f), 0.001f);
        }

        [Test]
        public void ReachCurve_GoingOut_RisesFastEarly()
        {
            // 톡 치는 느낌: 뻗는 시간의 절반이면 이미 80% 넘게 나가 있다(ease-out)
            Assert.Greater(FirstPersonArmMotion.ReachCurve(0.045f, 0.09f, 0.04f, 0.2f), 0.8f);
        }

        [Test]
        public void SwitchCurve_Middle_IsDeepest()
        {
            Assert.AreEqual(1f, FirstPersonArmMotion.SwitchCurve(0.15f, 0.3f), 0.001f);
        }

        [Test]
        public void Tick_Idle_StaysNearRest()
        {
            var motion = new FirstPersonArmMotion(new FirstPersonArmSettings());

            Assert.Less(MaxAbsY(motion, Frame(), 180), 0.01f);
        }

        [Test]
        public void Tick_Running_BobsMoreThanWalking()
        {
            var walking = new FirstPersonArmMotion(new FirstPersonArmSettings());
            var running = new FirstPersonArmMotion(new FirstPersonArmSettings());
            float walkMax = MaxAbsY(walking, Frame(1f), 180);
            float runMax = MaxAbsY(running, Frame(2f), 180);

            Assert.Greater(runMax, walkMax);
        }

        [Test]
        public void Tick_Landing_DipsArmDown()
        {
            var motion = new FirstPersonArmMotion(new FirstPersonArmSettings());
            for (int i = 0; i < 30; i++) motion.Tick(Frame(0f, false, -5f));
            motion.Tick(Frame(0f, true, 0f));

            float lowest = 0f;
            for (int i = 0; i < 10; i++)
            {
                motion.Tick(Frame());
                lowest = Mathf.Min(lowest, motion.PositionOffset.y);
            }

            // 공중 지연(위로 뜸)을 빼고도 아래로 꺼진다
            Assert.Less(lowest, 0f);
        }

        [Test]
        public void Reach_AtPeak_PushesArmForward()
        {
            var settings = new FirstPersonArmSettings();
            var motion = new FirstPersonArmMotion(settings);
            motion.Reach(FirstPersonArmMotion.ReachKind.Touch);
            for (int i = 0; i < 6; i++) motion.Tick(Frame()); // 0.1초

            Assert.Greater(motion.PositionOffset.z, settings.reachOffset.z * 0.8f);
        }

        [Test]
        public void Reach_AfterDuration_Finishes()
        {
            var motion = new FirstPersonArmMotion(new FirstPersonArmSettings());
            motion.Reach(FirstPersonArmMotion.ReachKind.Place);
            for (int i = 0; i < 30; i++) motion.Tick(Frame()); // 0.5초

            Assert.IsFalse(motion.IsReaching);
        }
    }
}

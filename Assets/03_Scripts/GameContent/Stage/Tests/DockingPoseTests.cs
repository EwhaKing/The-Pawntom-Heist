using NUnit.Framework;

namespace Pawntom.Stage.Tests
{
    /// <summary>
    /// 본부 도킹 이동 계산(GAME_DESIGN §3.8). 본부는 기준 도킹에 붙은 채로 씬에 저장돼 있고,
    /// 선택된 도킹으로 "기준 도킹 포즈의 역 × 본부 포즈" 를 그대로 옮긴다. 모든 피어가 같은 값을 얻어야 한다.
    /// </summary>
    public sealed class DockingPoseTests
    {
        private const float Tolerance = 1e-3f;

        private static void AssertVector(StageVector expected, StageVector actual)
        {
            Assert.AreEqual(expected.X, actual.X, Tolerance, "X");
            Assert.AreEqual(expected.Y, actual.Y, Tolerance, "Y");
            Assert.AreEqual(expected.Z, actual.Z, Tolerance, "Z");
        }

        [Test]
        public void RotateYaw_90Degrees_MatchesUnityConvention()
        {
            // Unity: Quaternion.Euler(0, 90, 0) * forward(0,0,1) = right(1,0,0)
            AssertVector(new StageVector(1f, 0f, 0f), DockingPose.RotateYaw(new StageVector(0f, 0f, 1f), 90f));
            AssertVector(new StageVector(0f, 0f, -1f), DockingPose.RotateYaw(new StageVector(1f, 0f, 0f), 90f));
        }

        [Test]
        public void Relocate_TargetIsReference_KeepsBodyPose()
        {
            var dock = new StagePose(new StageVector(10f, 0f, 40f), 180f);
            var body = new StagePose(new StageVector(12f, 1f, 50f), 30f);

            StagePose moved = DockingPose.Relocate(dock, dock, body);

            AssertVector(body.Position, moved.Position);
            Assert.AreEqual(30f, moved.Yaw, Tolerance);
        }

        [Test]
        public void Relocate_PureTranslation_MovesBodyBySameOffset()
        {
            var reference = new StagePose(new StageVector(0f, 0f, 0f), 0f);
            var target = new StagePose(new StageVector(5f, 2f, -3f), 0f);
            var body = new StagePose(new StageVector(1f, 0f, 4f), 0f);

            StagePose moved = DockingPose.Relocate(reference, target, body);

            AssertVector(new StageVector(6f, 2f, 1f), moved.Position);
            Assert.AreEqual(0f, moved.Yaw, Tolerance);
        }

        [Test]
        public void Relocate_RotatedTarget_KeepsBodyInFrontOfDock()
        {
            // 기준 도킹은 +Z 를 보고, 본부는 그 앞 10m. 대상 도킹이 +X(yaw 90)를 보면 본부는 대상 앞 +X 10m.
            var reference = new StagePose(new StageVector(0f, 0f, 0f), 0f);
            var target = new StagePose(new StageVector(100f, 0f, 0f), 90f);
            var body = new StagePose(new StageVector(0f, 0f, 10f), 0f);

            StagePose moved = DockingPose.Relocate(reference, target, body);

            AssertVector(new StageVector(110f, 0f, 0f), moved.Position);
            Assert.AreEqual(90f, moved.Yaw, Tolerance);
        }

        [Test]
        public void Relocate_RotatedReference_UsesInverseOfReference()
        {
            // 씬 상황과 같은 꼴: 기준 도킹 yaw 180(본부 쪽을 -Z 로 봄), 본부는 기준 -Z 쪽 8m.
            var reference = new StagePose(new StageVector(0f, 0f, 40f), 180f);
            var target = new StagePose(new StageVector(60f, 0f, 0f), 270f);
            var body = new StagePose(new StageVector(0f, 0f, 48f), 0f);

            StagePose moved = DockingPose.Relocate(reference, target, body);

            // 본부는 기준 도킹 "뒤"(로컬 -Z) 8m. 대상(yaw 270)의 로컬 -Z 는 월드 +X → (68, 0, 0)
            AssertVector(new StageVector(68f, 0f, 0f), moved.Position);
            Assert.AreEqual(90f, moved.Yaw, Tolerance);
        }

        [Test]
        public void DeltaYaw_IsNormalizedTo0To360()
        {
            Assert.AreEqual(270f, DockingPose.DeltaYaw(90f, 0f), Tolerance);
            Assert.AreEqual(90f, DockingPose.DeltaYaw(180f, 270f), Tolerance);
            Assert.AreEqual(0f, DockingPose.DeltaYaw(45f, 405f), Tolerance);
        }

        [Test]
        public void Relocate_SameInputs_SameResult()
        {
            var reference = new StagePose(new StageVector(3f, 0f, 40f), 180f);
            var target = new StagePose(new StageVector(-70f, 0f, 12f), 90f);
            var body = new StagePose(new StageVector(4f, 0f, 47f), 0f);

            StagePose a = DockingPose.Relocate(reference, target, body);
            StagePose b = DockingPose.Relocate(reference, target, body);

            Assert.AreEqual(a.Position, b.Position);
            Assert.AreEqual(a.Yaw, b.Yaw);
        }
    }
}

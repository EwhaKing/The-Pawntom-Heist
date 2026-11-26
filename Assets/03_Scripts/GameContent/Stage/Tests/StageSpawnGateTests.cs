using NUnit.Framework;

namespace Pawntom.Stage.Tests
{
    /// <summary>
    /// 호스트 스폰 순서 판정: 레벨 데이터 씬에서는 시드(→ 본부 도킹)가 정해진 뒤에만 플레이어·적·아이템을 스폰한다.
    /// 레벨 데이터가 없는 씬(Tutorial_Scene·Enemy_TestScene)은 기존 경로 그대로.
    /// </summary>
    public sealed class StageSpawnGateTests
    {
        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void Decide_NoLayout_UsesLegacyPath(bool hasStateObject, bool planReady)
        {
            Assert.AreEqual(StageSpawnMode.Legacy, StageSpawnGate.Decide(false, hasStateObject, planReady));
        }

        [Test]
        public void Decide_LayoutAndPlanReady_UsesPlan()
        {
            Assert.AreEqual(StageSpawnMode.UsePlan, StageSpawnGate.Decide(true, true, true));
        }

        [Test]
        public void Decide_LayoutButPlanNotReady_Waits()
        {
            // 스테이지 상태 오브젝트가 아직 Spawned 되지 않았다 → 본부가 옮겨지기 전에 플레이어를 만들지 않는다.
            Assert.AreEqual(StageSpawnMode.Wait, StageSpawnGate.Decide(true, true, false));
        }

        [Test]
        public void Decide_LayoutButNoStateObject_FallsBackWithError()
        {
            // 씬 작업이 빠졌을 때 영원히 기다려 아무도 스폰되지 않는 것을 막는다.
            Assert.AreEqual(StageSpawnMode.LegacyMissingState, StageSpawnGate.Decide(true, false, false));
        }

        [Test]
        public void PendingQueue_PlayersBeforeEnemies_RegardlessOfRequestOrder()
        {
            var pending = new StagePendingSpawns();
            pending.RequestEnemies();
            pending.RequestPlayers();

            Assert.AreEqual(StagePendingStep.Players, pending.Next());
        }

        [Test]
        public void PendingQueue_DrainsEachStepOnce()
        {
            var pending = new StagePendingSpawns();
            pending.RequestPlayers();
            pending.RequestPlayers();
            pending.RequestEnemies();

            var order = new[] { pending.Next(), pending.Next(), pending.Next() };

            CollectionAssert.AreEqual(new[] { StagePendingStep.Players, StagePendingStep.Enemies, StagePendingStep.None }, order);
        }

        [Test]
        public void PendingQueue_Empty_ReturnsNone()
        {
            Assert.AreEqual(StagePendingStep.None, new StagePendingSpawns().Next());
        }

        [Test]
        public void PendingQueue_Clear_DropsRequests()
        {
            var pending = new StagePendingSpawns();
            pending.RequestPlayers();
            pending.Clear();

            Assert.IsFalse(pending.HasAny);
        }

        [Test]
        public void OnceFlag_SecondClaim_IsRejected()
        {
            // 같은 계획의 아이템·브루투스 스폰 요청이 두 번 와도 한 번만 처리한다.
            var once = new StageOnceFlag();
            once.TryClaim();

            Assert.IsFalse(once.TryClaim());
        }

        [Test]
        public void OnceFlag_FirstClaim_Succeeds()
        {
            Assert.IsTrue(new StageOnceFlag().TryClaim());
        }
    }
}

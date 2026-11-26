using NUnit.Framework;

namespace Pawntom.Stage.Tests
{
    /// <summary>
    /// 금고 최종 해킹 흐름(GAME_DESIGN §3.8 부분 개방: 모든 잠금 해제 → 미니게임 → 성공 시 최종 개방,
    /// §3.5 실패 시 3초 쿨타임). 개봉 RPC 가 거부되면 열림으로 처리하지 않는다(fusion-network.md 과거 결함).
    /// </summary>
    public sealed class VaultHackSessionTests
    {
        // GAME_DESIGN §3.5: 실패 시 3초 쿨타임
        private const float FailCooldown = 3f;
        private const float OpenTimeout = 2f;

        private static VaultHackSession Session()
        {
            return new VaultHackSession(FailCooldown, OpenTimeout);
        }

        [Test]
        public void Evaluate_OpenedVault_ReturnsAlreadyOpened()
        {
            Assert.AreEqual(VaultHackGate.AlreadyOpened, Session().Evaluate(0f, true, true, false));
        }

        [Test]
        public void Evaluate_KeysMissing_ReturnsLocked()
        {
            Assert.AreEqual(VaultHackGate.Locked, Session().Evaluate(0f, false, false, false));
        }

        [Test]
        public void Evaluate_AllKeysUsed_ReturnsReady()
        {
            Assert.AreEqual(VaultHackGate.Ready, Session().Evaluate(0f, false, true, false));
        }

        [Test]
        public void Evaluate_OtherHackPopupOpen_ReturnsHackInProgress()
        {
            Assert.AreEqual(VaultHackGate.HackInProgress, Session().Evaluate(0f, false, true, true));
        }

        [Test]
        public void TryBegin_Ready_ReturnsTrue()
        {
            Assert.IsTrue(Session().TryBegin(0f, false, true, false));
        }

        [Test]
        public void TryBegin_Locked_ReturnsFalse()
        {
            Assert.IsFalse(Session().TryBegin(0f, false, false, false));
        }

        [Test]
        public void TryBegin_WhileOwnHackRunning_ReturnsFalse()
        {
            VaultHackSession session = Session();
            session.TryBegin(0f, false, true, false);

            Assert.IsFalse(session.TryBegin(0.1f, false, true, false));
        }

        [Test]
        public void Finish_Failed_StartsCooldown()
        {
            VaultHackSession session = Session();
            session.TryBegin(0f, false, true, false);
            session.Finish(false, false, 1f);

            Assert.AreEqual(VaultHackGate.CoolingDown, session.Evaluate(1f + FailCooldown - 0.1f, false, true, false));
        }

        [Test]
        public void Finish_Failed_ReadyAfterCooldown()
        {
            VaultHackSession session = Session();
            session.TryBegin(0f, false, true, false);
            session.Finish(false, false, 1f);

            Assert.AreEqual(VaultHackGate.Ready, session.Evaluate(1f + FailCooldown, false, true, false));
        }

        [Test]
        public void Finish_CanceledByDeath_NoCooldown()
        {
            VaultHackSession session = Session();
            session.TryBegin(0f, false, true, false);
            session.Finish(false, true, 1f);

            Assert.AreEqual(VaultHackGate.Ready, session.Evaluate(1.1f, false, true, false));
        }

        [Test]
        public void Finish_Success_WaitsForAuthorityOpen()
        {
            VaultHackSession session = Session();
            session.TryBegin(0f, false, true, false);
            session.Finish(true, false, 1f);

            Assert.AreEqual(VaultHackGate.AwaitingOpen, session.Evaluate(1.1f, false, true, false));
        }

        [Test]
        public void Poll_SuccessThenAuthorityOpened_ReturnsOpened()
        {
            VaultHackSession session = Session();
            session.TryBegin(0f, false, true, false);
            session.Finish(true, false, 1f);

            Assert.AreEqual(VaultHackPoll.Opened, session.Poll(1.2f, true));
        }

        [Test]
        public void Poll_SuccessButNotOpenedBeforeTimeout_ReturnsNone()
        {
            VaultHackSession session = Session();
            session.TryBegin(0f, false, true, false);
            session.Finish(true, false, 1f);

            Assert.AreEqual(VaultHackPoll.None, session.Poll(1f + OpenTimeout - 0.1f, false));
        }

        [Test]
        public void Poll_SuccessButAuthorityRejected_ReturnsRejectedAfterTimeout()
        {
            // 금고 RPC 가 거부되면 IsOpened 가 바뀌지 않는다 → 열림으로 처리하지 않고 거절로 알린다.
            VaultHackSession session = Session();
            session.TryBegin(0f, false, true, false);
            session.Finish(true, false, 1f);

            Assert.AreEqual(VaultHackPoll.Rejected, session.Poll(1f + OpenTimeout, false));
        }

        [Test]
        public void Poll_AfterRejected_AllowsRetry()
        {
            VaultHackSession session = Session();
            session.TryBegin(0f, false, true, false);
            session.Finish(true, false, 1f);
            session.Poll(1f + OpenTimeout, false);

            Assert.AreEqual(VaultHackGate.Ready, session.Evaluate(1f + OpenTimeout, false, true, false));
        }

        [Test]
        public void Poll_ReportsOpenedOnlyOnce()
        {
            VaultHackSession session = Session();
            session.TryBegin(0f, false, true, false);
            session.Finish(true, false, 1f);
            session.Poll(1.2f, true);

            Assert.AreEqual(VaultHackPoll.None, session.Poll(1.3f, true));
        }

        [Test]
        public void CancelBegin_PopupFailedToOpen_ReturnsToReady()
        {
            VaultHackSession session = Session();
            session.TryBegin(0f, false, true, false);
            session.CancelBegin();

            Assert.AreEqual(VaultHackGate.Ready, session.Evaluate(0f, false, true, false));
        }

        [Test]
        public void CooldownRemaining_AfterFailure_CountsDown()
        {
            VaultHackSession session = Session();
            session.TryBegin(0f, false, true, false);
            session.Finish(false, false, 1f);

            Assert.AreEqual(2f, session.CooldownRemaining(2f), 0.0001f);
        }
    }
}

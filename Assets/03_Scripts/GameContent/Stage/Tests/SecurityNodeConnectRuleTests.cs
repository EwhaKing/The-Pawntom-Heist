using NUnit.Framework;

namespace Pawntom.Stage.Tests
{
    /// <summary>
    /// 분전반 접속 요청을 상태 권한이 다시 검증하는 규칙(.claude/rules/fusion-network.md: RPC 처리기는 재검증).
    /// </summary>
    public sealed class SecurityNodeConnectRuleTests
    {
        // GAME_DESIGN §3.4: 3초 캐스팅
        private const float CastSeconds = 3f;
        private const float TimeTolerance = 0.3f;
        private const float MaxDisplacement = 0.6f;

        private static SecurityNodeConnectRule Rule()
        {
            return new SecurityNodeConnectRule(CastSeconds, TimeTolerance, MaxDisplacement);
        }

        private static SecurityNodeConnectRequest ValidRequest()
        {
            return new SecurityNodeConnectRequest
            {
                AlreadyConnected = false,
                PlayerValid = true,
                PlayerDead = false,
                InRange = true,
                HasCastRecord = true,
                ElapsedSeconds = CastSeconds,
                Displacement = 0f,
            };
        }

        [Test]
        public void Evaluate_ValidRequest_Accepted()
        {
            Assert.AreEqual(SecurityNodeVerdict.Accepted, Rule().Evaluate(ValidRequest()));
        }

        [Test]
        public void Evaluate_AlreadyConnected_RejectsAsDuplicate()
        {
            SecurityNodeConnectRequest request = ValidRequest();
            request.AlreadyConnected = true;

            Assert.AreEqual(SecurityNodeVerdict.AlreadyConnected, Rule().Evaluate(request));
        }

        [Test]
        public void Evaluate_SenderDoesNotOwnPlayer_RejectsInvalidPlayer()
        {
            SecurityNodeConnectRequest request = ValidRequest();
            request.PlayerValid = false;

            Assert.AreEqual(SecurityNodeVerdict.InvalidPlayer, Rule().Evaluate(request));
        }

        [Test]
        public void Evaluate_DeadPlayer_RejectsPlayerDead()
        {
            SecurityNodeConnectRequest request = ValidRequest();
            request.PlayerDead = true;

            Assert.AreEqual(SecurityNodeVerdict.PlayerDead, Rule().Evaluate(request));
        }

        [Test]
        public void Evaluate_PlayerOutOfRange_RejectsOutOfRange()
        {
            SecurityNodeConnectRequest request = ValidRequest();
            request.InRange = false;

            Assert.AreEqual(SecurityNodeVerdict.OutOfRange, Rule().Evaluate(request));
        }

        [Test]
        public void Evaluate_NoCastStartedOnAuthority_RejectsNoCast()
        {
            SecurityNodeConnectRequest request = ValidRequest();
            request.HasCastRecord = false;

            Assert.AreEqual(SecurityNodeVerdict.NoCastStarted, Rule().Evaluate(request));
        }

        [Test]
        public void Evaluate_CastShorterThanDurationMinusTolerance_RejectsTooShort()
        {
            SecurityNodeConnectRequest request = ValidRequest();
            request.ElapsedSeconds = CastSeconds - TimeTolerance - 0.05f;

            Assert.AreEqual(SecurityNodeVerdict.CastTooShort, Rule().Evaluate(request));
        }

        [Test]
        public void Evaluate_CastWithinNetworkTolerance_Accepted()
        {
            // 시작·완료 RPC 지연 차이를 허용한다.
            SecurityNodeConnectRequest request = ValidRequest();
            request.ElapsedSeconds = CastSeconds - TimeTolerance + 0.05f;

            Assert.AreEqual(SecurityNodeVerdict.Accepted, Rule().Evaluate(request));
        }

        [Test]
        public void Evaluate_MovedDuringCast_RejectsMoved()
        {
            // GAME_DESIGN §3.4: 캐스팅 중 이동 불가
            SecurityNodeConnectRequest request = ValidRequest();
            request.Displacement = MaxDisplacement + 0.1f;

            Assert.AreEqual(SecurityNodeVerdict.MovedDuringCast, Rule().Evaluate(request));
        }

        [Test]
        public void CanBegin_ConnectedNode_RejectsAsDuplicate()
        {
            Assert.AreEqual(SecurityNodeVerdict.AlreadyConnected, Rule().CanBegin(true, true, false, true));
        }

        [Test]
        public void CanBegin_ValidPlayerInRange_Accepted()
        {
            Assert.AreEqual(SecurityNodeVerdict.Accepted, Rule().CanBegin(false, true, false, true));
        }
    }
}

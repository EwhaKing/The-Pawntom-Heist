using NUnit.Framework;

namespace Pawntom.Lobby.Tests
{
    public class LobbyRulesTests
    {
        private const int CatCount = 4;

        private static LobbyParticipant Host(bool hasCat) =>
            new LobbyParticipant(isHost: true, hasSelectedCat: hasCat, isReady: false);

        private static LobbyParticipant Guest(bool hasCat, bool ready) =>
            new LobbyParticipant(isHost: false, hasSelectedCat: hasCat, isReady: ready);

        // ---- IsValidCat ----

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void IsValidCat_InRange_True(int value)
        {
            Assert.IsTrue(LobbyRules.IsValidCat(value, CatCount));
        }

        [TestCase(-1)]
        [TestCase(4)]
        [TestCase(int.MaxValue)]
        [TestCase(int.MinValue)]
        public void IsValidCat_OutOfRange_False(int value)
        {
            Assert.IsFalse(LobbyRules.IsValidCat(value, CatCount));
        }

        [Test]
        public void IsValidCat_NoCatsDefined_False()
        {
            Assert.IsFalse(LobbyRules.IsValidCat(0, 0));
        }

        // ---- CanToggleReady ----

        [Test]
        public void CanToggleReady_GuestWithCat_True()
        {
            Assert.IsTrue(LobbyRules.CanToggleReady(isHost: false, hasSelectedCat: true));
        }

        [Test]
        public void CanToggleReady_GuestWithoutCat_False()
        {
            Assert.IsFalse(LobbyRules.CanToggleReady(isHost: false, hasSelectedCat: false));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void CanToggleReady_Host_AlwaysFalse(bool hasCat)
        {
            Assert.IsFalse(LobbyRules.CanToggleReady(isHost: true, hasSelectedCat: hasCat));
        }

        // ---- CanStartGame ----

        [Test]
        public void CanStartGame_HostAloneWithCat_True()
        {
            var list = new[] { Host(true) };
            Assert.IsTrue(LobbyRules.CanStartGame(list, list.Length));
        }

        [Test]
        public void CanStartGame_HostAloneWithoutCat_False()
        {
            var list = new[] { Host(false) };
            Assert.IsFalse(LobbyRules.CanStartGame(list, list.Length));
        }

        [Test]
        public void CanStartGame_AllSelectedAndGuestsReady_True()
        {
            var list = new[] { Host(true), Guest(true, true), Guest(true, true), Guest(true, true) };
            Assert.IsTrue(LobbyRules.CanStartGame(list, list.Length));
        }

        [Test]
        public void CanStartGame_GuestNotReady_False()
        {
            var list = new[] { Host(true), Guest(true, true), Guest(true, false) };
            Assert.IsFalse(LobbyRules.CanStartGame(list, list.Length));
        }

        [Test]
        public void CanStartGame_GuestWithoutCat_False()
        {
            // Ready와 미선택이 함께 올 수는 없지만(선택 전 Ready 거부), 판정은 각각 따로 막는다.
            var list = new[] { Host(true), Guest(false, true) };
            Assert.IsFalse(LobbyRules.CanStartGame(list, list.Length));
        }

        [Test]
        public void CanStartGame_HostWithoutCat_False()
        {
            var list = new[] { Host(false), Guest(true, true) };
            Assert.IsFalse(LobbyRules.CanStartGame(list, list.Length));
        }

        [Test]
        public void CanStartGame_HostNotReady_IgnoredBecauseHostNeedsNoReady()
        {
            // Host()는 isReady=false로 만든다.
            var list = new[] { Guest(true, true), Host(true) };
            Assert.IsTrue(LobbyRules.CanStartGame(list, list.Length));
        }

        [Test]
        public void CanStartGame_NoHostInList_False()
        {
            var list = new[] { Guest(true, true) };
            Assert.IsFalse(LobbyRules.CanStartGame(list, list.Length));
        }

        [Test]
        public void CanStartGame_ZeroCount_False()
        {
            Assert.IsFalse(LobbyRules.CanStartGame(new LobbyParticipant[4], 0));
        }

        [Test]
        public void CanStartGame_NullBuffer_False()
        {
            Assert.IsFalse(LobbyRules.CanStartGame(null, 4));
        }

        [Test]
        public void CanStartGame_OnlyFirstCountEntriesAreRead()
        {
            // 사전 할당 버퍼 재사용: count 뒤의 오래된 항목은 무시한다.
            var buffer = new[] { Host(true), Guest(true, true), Guest(false, false), Guest(false, false) };
            Assert.IsTrue(LobbyRules.CanStartGame(buffer, 2));
        }

        [Test]
        public void CanStartGame_CountLargerThanBuffer_ClampedToLength()
        {
            var list = new[] { Host(true) };
            Assert.IsTrue(LobbyRules.CanStartGame(list, 10));
        }

        [Test]
        public void CanStartGame_NegativeCount_False()
        {
            var list = new[] { Host(true) };
            Assert.IsFalse(LobbyRules.CanStartGame(list, -1));
        }

        // ---- DefaultPlayerName ----

        [TestCase(0, "Player 1")]
        [TestCase(1, "Player 2")]
        [TestCase(2, "Player 3")]
        [TestCase(3, "Player 4")]
        public void DefaultPlayerName_SlotPlusOne(int slot, string expected)
        {
            Assert.AreEqual(expected, LobbyRules.DefaultPlayerName(slot));
        }

        [TestCase(0)]
        [TestCase(3)]
        public void DefaultPlayerName_FitsNetworkString16(int slot)
        {
            // LobbyPlayerData.PlayerName은 NetworkString<_16>이다.
            Assert.LessOrEqual(LobbyRules.DefaultPlayerName(slot).Length, 16);
        }
    }
}

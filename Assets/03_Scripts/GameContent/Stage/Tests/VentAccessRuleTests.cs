using NUnit.Framework;

namespace Pawntom.Stage.Tests
{
    /// <summary>
    /// 소형 환풍구 통과 규칙(GAME_DESIGN §3.2 카오스 삼색: 소형 환풍구 등 특수 통로 통과).
    /// 사용자 결정: 카오스 삼색(CatType.CalicoCat = 3)만 통과, 나머지는 막힌다.
    /// </summary>
    public sealed class VentAccessRuleTests
    {
        // Player/CatType.cs 순서: BlackCat 0, TabbyCat 1, OrangeCat 2, CalicoCat 3
        private const int BlackCat = 0;
        private const int TabbyCat = 1;
        private const int OrangeCat = 2;
        private const int CalicoCat = 3;

        [Test]
        public void CanPass_CalicoOnlyRule_CalicoPasses()
        {
            var rule = new VentAccessRule(CalicoCat);

            Assert.IsTrue(rule.CanPass(CalicoCat));
        }

        [TestCase(BlackCat)]
        [TestCase(TabbyCat)]
        [TestCase(OrangeCat)]
        public void CanPass_CalicoOnlyRule_OtherCatsBlocked(int catType)
        {
            var rule = new VentAccessRule(CalicoCat);

            Assert.IsFalse(rule.CanPass(catType));
        }

        [TestCase(-1)]
        [TestCase(32)]
        [TestCase(int.MaxValue)]
        public void CanPass_OutOfRangeValue_Blocked(int catType)
        {
            var rule = new VentAccessRule(CalicoCat);

            Assert.IsFalse(rule.CanPass(catType));
        }

        [Test]
        public void ShouldIgnoreCollision_NoPlayerData_KeepsCollision()
        {
            var rule = new VentAccessRule(CalicoCat);

            Assert.IsFalse(rule.ShouldIgnoreCollision(false, CalicoCat));
        }

        [Test]
        public void ShouldIgnoreCollision_CalicoPlayer_IgnoresCollision()
        {
            var rule = new VentAccessRule(CalicoCat);

            Assert.IsTrue(rule.ShouldIgnoreCollision(true, CalicoCat));
        }

        [Test]
        public void ShouldIgnoreCollision_CharacterChangedAwayFromCalico_KeepsCollision()
        {
            // 캐릭터가 바뀌면 같은 규칙으로 다시 판정한다(상태 없음).
            var rule = new VentAccessRule(CalicoCat);
            rule.ShouldIgnoreCollision(true, CalicoCat);

            Assert.IsFalse(rule.ShouldIgnoreCollision(true, OrangeCat));
        }

        [Test]
        public void CanPass_InvalidAllowedValue_BlocksEveryone()
        {
            var rule = new VentAccessRule(-5);

            Assert.IsFalse(rule.CanPass(CalicoCat));
        }
    }
}

using NUnit.Framework;
using Pawntom.Enemy.Core;

namespace Pawntom.Enemy.Tests
{
    public sealed class EnemyStunTests
    {
        [Test]
        public void New_IsNotStunned()
        {
            var stun = new EnemyStun();

            Assert.IsFalse(stun.IsStunned);
            Assert.IsFalse(stun.Tick(1f));
        }

        [Test]
        public void Begin_StunsForTheGivenSeconds()
        {
            var stun = new EnemyStun();
            stun.Begin(2f);

            Assert.IsTrue(stun.IsStunned);
            Assert.IsTrue(stun.Tick(1.9f));
            Assert.IsTrue(stun.IsStunned);
        }

        [Test]
        public void Tick_ReturnsFalseOnTheTickItEnds()
        {
            var stun = new EnemyStun();
            stun.Begin(2f);
            stun.Tick(1f);

            Assert.IsFalse(stun.Tick(1f));
            Assert.IsFalse(stun.IsStunned);
        }

        [Test]
        public void Begin_WhileStunned_KeepsTheLongerOne()
        {
            var stun = new EnemyStun();
            stun.Begin(2f);
            stun.Tick(1f);
            stun.Begin(0.5f);

            Assert.AreEqual(1f, stun.RemainingSeconds, 0.0001f);

            stun.Begin(2f);
            Assert.AreEqual(2f, stun.RemainingSeconds, 0.0001f);
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void Begin_InvalidSeconds_IsIgnored(float seconds)
        {
            var stun = new EnemyStun();
            stun.Begin(seconds);

            Assert.IsFalse(stun.IsStunned);
        }

        [Test]
        public void Tick_NegativeDelta_DoesNotExtend()
        {
            var stun = new EnemyStun();
            stun.Begin(2f);
            stun.Tick(-5f);

            Assert.AreEqual(2f, stun.RemainingSeconds, 0.0001f);
        }

        [Test]
        public void Clear_EndsStunAtOnce()
        {
            var stun = new EnemyStun();
            stun.Begin(2f);
            stun.Clear();

            Assert.IsFalse(stun.IsStunned);
        }

        [Test]
        public void AttackSettings_DefaultStunIsTwoSeconds()
        {
            Assert.AreEqual(2f, new EnemyAttackSettings().StunSecondsAfterAttack, 0.0001f);
        }
    }
}

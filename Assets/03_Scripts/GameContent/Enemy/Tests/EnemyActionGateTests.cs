using NUnit.Framework;
using Pawntom.Enemy.Core;

namespace Pawntom.Enemy.Tests
{
    public sealed class EnemyActionGateTests
    {
        [TestCase(EnemyState.Chase, false, true)]
        [TestCase(EnemyState.Chase, true, false)]
        [TestCase(EnemyState.Patrol, false, false)]
        [TestCase(EnemyState.Investigate, false, false)]
        public void CanBite_OnlyWhenChasingAndNotStunned(EnemyState state, bool stunned, bool expected)
        {
            Assert.AreEqual(expected, EnemyActionGate.CanBite(state, stunned));
        }

        [TestCase(false, false, false)]
        [TestCase(true, false, true)]
        [TestCase(false, true, true)]
        [TestCase(true, true, true)]
        public void ShouldHoldMotion_WhenPausedOrStunned(bool paused, bool stunned, bool expected)
        {
            Assert.AreEqual(expected, EnemyActionGate.ShouldHoldMotion(paused, stunned));
        }
    }
}

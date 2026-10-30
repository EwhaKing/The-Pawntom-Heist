using System.Collections.Generic;
using NUnit.Framework;
using Pawntom.Enemy.Core;
using UnityEngine;

namespace Pawntom.Enemy.Tests
{
    public sealed class EnemyContactDamageTests
    {
        // EnemyAttackSettings 기본값: 피해 20, 거리 1.5, 간격 1초
        private static List<EnemyAttacker> One(Vector3 position)
        {
            return new List<EnemyAttacker> { new EnemyAttacker(position, 1.5f, 20f, 1f) };
        }

        [Test]
        public void Tick_AttackerWithinRange_DealsDamage()
        {
            var damage = new EnemyContactDamage(1f);

            Assert.AreEqual(20f, damage.Tick(0.016f, Vector3.zero, One(new Vector3(1.2f, 0f, 0f))), 0.0001f);
        }

        [Test]
        public void Tick_AttackerOutOfRange_DealsNothing()
        {
            var damage = new EnemyContactDamage(1f);

            Assert.AreEqual(0f, damage.Tick(0.016f, Vector3.zero, One(new Vector3(2f, 0f, 0f))), 0.0001f);
        }

        [Test]
        public void Tick_DuringCooldown_DealsNothing()
        {
            var damage = new EnemyContactDamage(1f);
            var attackers = One(new Vector3(1f, 0f, 0f));
            damage.Tick(0.016f, Vector3.zero, attackers);

            Assert.AreEqual(0f, damage.Tick(0.5f, Vector3.zero, attackers), 0.0001f);
        }

        [Test]
        public void Tick_AfterCooldown_DealsAgain()
        {
            var damage = new EnemyContactDamage(1f);
            var attackers = One(new Vector3(1f, 0f, 0f));
            damage.Tick(0.016f, Vector3.zero, attackers);
            damage.Tick(0.6f, Vector3.zero, attackers);

            Assert.AreEqual(20f, damage.Tick(0.5f, Vector3.zero, attackers), 0.0001f);
        }

        [Test]
        public void Tick_PlayerHighAbove_DealsNothing()
        {
            // 상자 위로 피한 플레이어(1.2m 위)는 물리지 않는다.
            var damage = new EnemyContactDamage(1f);

            Assert.AreEqual(0f, damage.Tick(0.016f, new Vector3(0f, 1.2f, 0f), One(new Vector3(0.5f, 0f, 0f))), 0.0001f);
        }

        [Test]
        public void Tick_TwoAttackersAtOnce_DealsOneHit()
        {
            var damage = new EnemyContactDamage(1f);
            var attackers = new List<EnemyAttacker>
            {
                new EnemyAttacker(new Vector3(1f, 0f, 0f), 1.5f, 20f, 1f),
                new EnemyAttacker(new Vector3(-1f, 0f, 0f), 1.5f, 20f, 1f)
            };

            Assert.AreEqual(20f, damage.Tick(0.016f, Vector3.zero, attackers), 0.0001f);
        }
    }
}

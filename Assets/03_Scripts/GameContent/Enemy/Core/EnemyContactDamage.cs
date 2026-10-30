using System.Collections.Generic;
using UnityEngine;

namespace Pawntom.Enemy.Core
{
    /// <summary>공격 중인 적 한 마리. 위치와 그 개체의 공격 수치.</summary>
    public struct EnemyAttacker
    {
        public Vector3 Position;
        public float Range;
        public float Damage;
        public float CooldownSeconds;

        public EnemyAttacker(Vector3 position, float range, float damage, float cooldownSeconds)
        {
            Position = position;
            Range = range;
            Damage = damage;
            CooldownSeconds = cooldownSeconds;
        }
    }

    /// <summary>
    /// 플레이어 한 명이 적에게 닿아 받는 피해 판정. 플레이어마다 하나씩 둔다(공격 간격이 플레이어 기준이다).
    /// <para>
    /// 닿은 순간 바로 한 번 물고, 그 뒤 <see cref="EnemyAttacker.CooldownSeconds"/> 동안은 다시 물지 않는다.
    /// 여러 마리가 동시에 닿아도 한 번에 한 마리 몫만 깎는다 — 겹친 개 무리에 순식간에 녹지 않도록.
    /// </para>
    /// </summary>
    public sealed class EnemyContactDamage
    {
        private readonly float _verticalTolerance;
        private float _cooldownRemaining;

        /// <param name="verticalTolerance">위아래로 이만큼 떨어져 있으면 닿지 않은 것으로 본다(상자 위로 피했을 때).</param>
        public EnemyContactDamage(float verticalTolerance)
        {
            _verticalTolerance = Mathf.Max(0f, verticalTolerance);
        }

        public float CooldownRemaining
        {
            get { return _cooldownRemaining; }
        }

        /// <returns>이번 틱에 깎을 체력. 물지 않았으면 0.</returns>
        public float Tick(float deltaTime, Vector3 player, IReadOnlyList<EnemyAttacker> attackers)
        {
            if (_cooldownRemaining > 0f)
            {
                _cooldownRemaining = Mathf.Max(0f, _cooldownRemaining - Mathf.Max(0f, deltaTime));

                if (_cooldownRemaining > 0f)
                {
                    return 0f;
                }
            }

            if (attackers == null)
            {
                return 0f;
            }

            int nearest = -1;
            float nearestSqr = float.MaxValue;

            for (int i = 0; i < attackers.Count; i++)
            {
                EnemyAttacker attacker = attackers[i];

                if (attacker.Damage <= 0f || Mathf.Abs(attacker.Position.y - player.y) > _verticalTolerance)
                {
                    continue;
                }

                float dx = attacker.Position.x - player.x;
                float dz = attacker.Position.z - player.z;
                float sqr = dx * dx + dz * dz;

                if (sqr <= attacker.Range * attacker.Range && sqr < nearestSqr)
                {
                    nearest = i;
                    nearestSqr = sqr;
                }
            }

            if (nearest < 0)
            {
                return 0f;
            }

            _cooldownRemaining = attackers[nearest].CooldownSeconds;
            return attackers[nearest].Damage;
        }
    }
}

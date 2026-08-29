namespace Pawntom.Enemy.Core
{
    /// <summary>K-9 기본. 하울링으로 동료를 부른 뒤 추격한다. 현행 동작을 그대로 옮긴 것이다.</summary>
    /// <remarks>
    /// 상태를 갖지 않는다 — 개체마다 새로 만들 필요가 없다.
    /// </remarks>
    public sealed class HowlingEngagementPolicy : IEnemyEngagementPolicy
    {
        /// <summary>접촉은 하울링 이력과 무관하게 언제나 경계다.</summary>
        public EnemyState OnContact(EnemyState current)
        {
            return EnemyState.Alert;
        }

        /// <summary>
        /// 시야 포착은 기본이 경계다.
        /// <para>
        /// 단 하나의 예외 — <b>조사 중</b>에 방금 짖었다면 동료는 이미 불렀으므로 바로 추격한다.
        /// 순찰 중에는 하울링 이력을 <b>보지 않는다</b>. 이 구분이 사라지면
        /// 순찰하던 K-9 이 짖지도 않고 달려든다.
        /// </para>
        /// </summary>
        public EnemyState OnSighted(EnemyState current, bool howledRecently)
        {
            if (current == EnemyState.Investigate && howledRecently)
            {
                return EnemyState.Chase;
            }

            return EnemyState.Alert;
        }
    }

    /// <summary>브루투스. 하울링을 거치지 않고 곧바로 달려든다.</summary>
    /// <remarks>
    /// 상태를 갖지 않는다 — 개체마다 새로 만들 필요가 없다.
    /// </remarks>
    public sealed class DirectChaseEngagementPolicy : IEnemyEngagementPolicy
    {
        /// <summary>접촉하면 곧바로 추격한다.</summary>
        public EnemyState OnContact(EnemyState current)
        {
            return EnemyState.Chase;
        }

        /// <summary>눈에 보이면 하울링 이력과 무관하게 곧바로 추격한다.</summary>
        public EnemyState OnSighted(EnemyState current, bool howledRecently)
        {
            return EnemyState.Chase;
        }
    }
}

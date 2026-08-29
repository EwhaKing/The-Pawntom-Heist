using Pawntom.Enemy.Core;
using UnityEngine;

namespace Pawntom.Enemy.Adapters
{
    /// <summary>
    /// 하울링을 거치지 않고 곧바로 달려드는 교전 정책. 브루투스에 붙인다.
    /// <para>
    /// 이 컴포넌트가 붙어 있다는 사실 하나가 유닛 종류를 가른다 —
    /// 코드 어디에도 "브루투스인가"를 묻는 분기는 없다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Pawntom/Enemy/Direct Chase Engagement")]
    public sealed class DirectChaseEngagementBehaviour : EnemyEngagementPolicyBehaviour
    {
        // 상태가 없는 정책이라 하나만 만들어 계속 쓴다. getter 에서 new 하면 매 조회마다 GC 할당이 생긴다.
        private readonly DirectChaseEngagementPolicy _policy = new DirectChaseEngagementPolicy();

        /// <inheritdoc/>
        public override IEnemyEngagementPolicy Policy
        {
            get { return _policy; }
        }
    }
}

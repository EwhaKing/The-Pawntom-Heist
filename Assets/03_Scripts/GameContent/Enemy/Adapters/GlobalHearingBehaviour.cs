using Pawntom.Enemy.Core;
using UnityEngine;

namespace Pawntom.Enemy.Adapters
{
    /// <summary>
    /// 거리와 반경을 무시하고 모든 소집을 듣는 청취 정책. 브루투스에 붙인다.
    /// <para>
    /// 이 부품이 붙어 있다는 사실 하나가 유닛 종류를 가른다 —
    /// 채널 어디에도 "브루투스인가"를 묻는 분기는 없다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Pawntom/Enemy/Global Hearing")]
    public sealed class GlobalHearingBehaviour : EnemyHearingPolicyBehaviour
    {
        // 상태가 없는 정책이라 하나만 만들어 계속 쓴다. getter 에서 new 하면 매 조회마다 GC 할당이 생긴다.
        private readonly GlobalHearingPolicy _policy = new GlobalHearingPolicy();

        /// <inheritdoc/>
        public override IEnemyHearingPolicy Policy
        {
            get { return _policy; }
        }
    }
}

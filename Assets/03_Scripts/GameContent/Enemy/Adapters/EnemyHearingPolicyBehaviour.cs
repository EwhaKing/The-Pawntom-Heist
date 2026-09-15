using Pawntom.Enemy.Core;
using UnityEngine;

namespace Pawntom.Enemy.Adapters
{
    /// <summary>
    /// 씬에 붙여서 쓰는 청취 정책의 밑판.
    /// 붙이지 않으면 기존 동작(하울러가 제시한 반경 제한)이 쓰인다.
    /// <para>
    /// <b>확장 지점(OCP)</b> — 새 유닛의 청력은 이 클래스를 상속한 파일 하나를 만들어
    /// 오브젝트에 붙이기만 하면 된다. <c>EnemyAlertChannelBehaviour</c> 는 바뀌지 않는다.
    /// </para>
    /// </summary>
    public abstract class EnemyHearingPolicyBehaviour : MonoBehaviour
    {
        /// <summary>채널에 주입할 정책. 조립 시점에 한 번 읽힌다.</summary>
        public abstract IEnemyHearingPolicy Policy { get; }
    }
}

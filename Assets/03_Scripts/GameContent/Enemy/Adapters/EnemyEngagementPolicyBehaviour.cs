using Pawntom.Enemy.Core;
using UnityEngine;

namespace Pawntom.Enemy.Adapters
{
    /// <summary>
    /// 씬에 붙여서 쓰는 교전 정책의 밑판.
    /// 붙이지 않으면 K-9 기본(하울링) 정책이 쓰인다.
    /// <para>
    /// <b>확장 지점(OCP)</b> — 새 유닛의 교전 규칙은 이 클래스를 상속한 파일 하나를 만들어
    /// 오브젝트에 붙이기만 하면 된다. <c>EnemyAgent</c> 도 <c>EnemyBrain</c> 도 바뀌지 않는다.
    /// </para>
    /// </summary>
    public abstract class EnemyEngagementPolicyBehaviour : MonoBehaviour
    {
        /// <summary>두뇌에 주입할 정책. 조립 시점에 한 번 읽힌다.</summary>
        public abstract IEnemyEngagementPolicy Policy { get; }
    }
}

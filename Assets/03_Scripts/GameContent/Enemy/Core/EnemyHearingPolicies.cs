using UnityEngine;

namespace Pawntom.Enemy.Core
{
    /// <summary>기본. 하울러가 제시한 반경 안에서만 듣는다. 현행 동작을 그대로 옮긴 것이다.</summary>
    /// <remarks>
    /// 상태를 갖지 않는다 — 개체마다 새로 만들 필요가 없다.
    /// </remarks>
    public sealed class RangedHearingPolicy : IEnemyHearingPolicy
    {
        /// <summary>
        /// 제곱 거리로 비교한다. 제곱근을 뽑지 않는 것은 하울링마다 도는 경로이기 때문이다.
        /// <para>
        /// <b>경계값은 들린다</b> — 거리가 정확히 <paramref name="radius"/> 면 <c>true</c> 다.
        /// 옮겨 오기 전 코드가 "반경보다 <b>클 때만</b> 건너뛴다"였으므로 부등호는 <c>&lt;=</c> 여야 한다.
        /// <c>&lt;</c> 로 쓰면 경계에 선 개체가 소집을 놓친다.
        /// </para>
        /// </summary>
        public bool CanHear(Vector3 listener, Vector3 origin, float radius)
        {
            return (listener - origin).sqrMagnitude <= radius * radius;
        }
    }

    /// <summary>브루투스. 거리와 반경을 무시하고 언제나 듣는다.</summary>
    /// <remarks>
    /// 상태를 갖지 않는다 — 개체마다 새로 만들 필요가 없다.
    /// </remarks>
    public sealed class GlobalHearingPolicy : IEnemyHearingPolicy
    {
        /// <summary>
        /// 인자를 보지 않고 언제나 <c>true</c> 다.
        /// 반경이 0 이거나 음수여도, 거리가 아무리 멀어도 들린다.
        /// </summary>
        public bool CanHear(Vector3 listener, Vector3 origin, float radius)
        {
            return true;
        }
    }
}

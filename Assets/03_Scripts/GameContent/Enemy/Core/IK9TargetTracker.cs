using UnityEngine;

namespace Pawntom.Enemy.Core
{
    /// <summary>
    /// 추격 대상의 현재 좌표를 알려 준다. 시야에 잡히는지는 묻지 않는다.
    /// <para>
    /// 두뇌는 대상이 무엇으로 표현되는지 알지 못한다(DIP).
    /// 씬의 실제 대상을 어떻게 찾는지는 어댑터 쪽 구현의 책임이다.
    /// </para>
    /// </summary>
    public interface IK9TargetTracker
    {
        /// <summary>
        /// 마지막으로 알던 좌표에 가장 가까운 대상의 현재 좌표를 돌려준다.
        /// <para>
        /// 대상이 하나도 없으면 false 를 돌려주고 <paramref name="currentPosition"/> 을 건드리지 않는다.
        /// 매 틱 호출되므로 구현은 힙 할당을 만들지 않아야 한다.
        /// </para>
        /// </summary>
        bool TryTrack(Vector3 lastKnownPosition, out Vector3 currentPosition);
    }
}

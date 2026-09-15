using UnityEngine;

namespace Pawntom.Enemy.Core
{
    /// <summary>
    /// 소집 신호가 이 개체에게 들리는지 판단한다.
    /// <para>
    /// 청력은 듣는 쪽의 속성이다 — 같은 하울링이라도 개체마다 다르게 들릴 수 있다.
    /// 새 청력은 이 인터페이스를 구현해 주입하면 되고 채널은 바뀌지 않는다(OCP).
    /// </para>
    /// </summary>
    public interface IEnemyHearingPolicy
    {
        /// <summary>이 개체에게 소집이 들리는가.</summary>
        /// <param name="listener">듣는 개체의 월드 좌표.</param>
        /// <param name="origin">하울링이 난 좌표.</param>
        /// <param name="radius">하울러가 제시한 소집 반경(m).</param>
        bool CanHear(Vector3 listener, Vector3 origin, float radius);
    }
}

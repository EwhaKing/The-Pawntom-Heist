using UnityEngine;

namespace Pawntom.Enemy.Core
{
    /// <summary>
    /// 하울링 송수신 통로. 누가 듣는지는 두뇌가 알지 못한다.
    /// </summary>
    public interface IK9AlertChannel
    {
        /// <summary>반경 안의 동료에게 소집 좌표를 알린다.</summary>
        void Broadcast(Vector3 origin, float radius);

        /// <summary>대기 중인 소집이 있으면 꺼내 소비한다.</summary>
        bool TryConsumeSummon(out Vector3 target);
    }
}

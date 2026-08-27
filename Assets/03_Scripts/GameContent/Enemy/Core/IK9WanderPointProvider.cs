using UnityEngine;

namespace Pawntom.Enemy.Core
{
    /// <summary>
    /// 기준점 주변에서 실제로 갈 수 있는 무작위 지점을 하나 고른다.
    /// <para>
    /// 무작위성과 이동 가능 판정을 여기로 몰아 두뇌를 결정적으로 유지한다(DIP·SRP).
    /// 덕분에 두뇌는 에디터 모드 테스트에서 같은 입력에 늘 같은 결과를 낸다.
    /// </para>
    /// </summary>
    public interface IK9WanderPointProvider
    {
        /// <summary>
        /// <paramref name="anchor"/> 에서 <paramref name="radius"/> 안의 갈 수 있는 지점을 고른다.
        /// 고르지 못하면 false 를 돌려주고 <paramref name="point"/> 를 건드리지 않는다.
        /// </summary>
        bool TryGetPoint(Vector3 anchor, float radius, out Vector3 point);
    }
}

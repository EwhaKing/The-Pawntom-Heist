using Pawntom.Enemy.Core;
using UnityEngine;
using UnityEngine.AI;

namespace Pawntom.Enemy.Adapters
{
    /// <summary>
    /// 기준점 주변 원판에서 무작위 지점을 뽑고, 내비 표면 위로 보정해 돌려준다.
    /// <para>
    /// 무작위성과 이동 가능 판정을 이 어댑터가 전부 떠안는다.
    /// 덕분에 두뇌는 결정적으로 남고 에디터 모드 테스트가 흔들리지 않는다(DIP).
    /// </para>
    /// </summary>
    public sealed class NavMeshWanderPointProvider : IK9WanderPointProvider
    {
        /// <summary>
        /// 표본을 몇 번까지 다시 뽑을지. 기획 수치가 아니라 구현 상수라 인스펙터에 노출하지 않는다.
        /// </summary>
        private const int SampleAttempts = 4;

        private readonly float _sampleDistance;

        /// <param name="sampleDistance">뽑은 지점이 갈 수 없는 곳일 때 근처에서 갈 수 있는 곳을 찾는 허용 거리(m).</param>
        public NavMeshWanderPointProvider(float sampleDistance)
        {
            _sampleDistance = sampleDistance;
        }

        /// <inheritdoc/>
        public bool TryGetPoint(Vector3 anchor, float radius, out Vector3 point)
        {
            for (int attempt = 0; attempt < SampleAttempts; attempt++)
            {
                // 원판 위 무작위 지점을 기준점의 수평면(XZ)에 얹는다. 높이는 기준점을 그대로 쓴다.
                Vector2 offset = Random.insideUnitCircle * radius;
                Vector3 candidate = new Vector3(anchor.x + offset.x, anchor.y, anchor.z + offset.y);

                NavMeshHit hit;
                if (NavMesh.SamplePosition(candidate, out hit, _sampleDistance, NavMesh.AllAreas))
                {
                    point = hit.position;
                    return true;
                }
            }

            point = default(Vector3);
            return false;
        }
    }
}

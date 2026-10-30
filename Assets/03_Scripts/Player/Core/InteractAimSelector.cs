using System.Collections.Generic;
using UnityEngine;

namespace Pawntom.Player.Core
{
    /// <summary>
    /// 조준선이 실제로 겨눈 대상 고르기. 콜라이더가 아니라 <b>눈에 보이는 모양(렌더러 경계)</b>으로 판정한다.
    /// <para>
    /// 아이템 콜라이더가 보이는 모양보다 훨씬 크면(예: 그림 프리팹의 14m 상자) "가장 가까운 콜라이더"를 고르는 방식은
    /// 옆 아이템을 겨눠도 큰 콜라이더 쪽을 집는다. 그래서 후보의 보이는 경계를 광선이 지나는지 보고,
    /// 여럿이면 광선에서 중심이 가장 가까운(조준선 한가운데에 가장 가까운) 것을 고른다.
    /// </para>
    /// </summary>
    public static class InteractAimSelector
    {
        /// <param name="ray">카메라 정면 광선(방향은 정규화).</param>
        /// <param name="maxDistance">상호작용 최대 거리.</param>
        /// <param name="visuals">후보들의 보이는 경계(월드).</param>
        /// <param name="padding">경계를 사방으로 넓히는 여유(m). 작은 물건을 살짝 빗겨 겨눠도 집히게 한다.</param>
        /// <param name="blockDistance">광선이 처음 막히는 거리(벽 등). 이보다 뒤에 있는 후보는 고르지 않는다.</param>
        /// <returns>고른 후보 번호. 겨눈 것이 없으면 -1.</returns>
        public static int Choose(Ray ray, float maxDistance, IReadOnlyList<Bounds> visuals, float padding, float blockDistance)
        {
            if (visuals == null)
            {
                return -1;
            }

            Vector3 direction = ray.direction.normalized;
            float limit = Mathf.Min(maxDistance, blockDistance);
            int best = -1;
            float bestPerpendicular = float.MaxValue;
            float bestEntry = float.MaxValue;

            for (int i = 0; i < visuals.Count; i++)
            {
                Bounds padded = visuals[i];
                padded.Expand(padding * 2f);

                float entry;

                if (!padded.IntersectRay(new Ray(ray.origin, direction), out entry))
                {
                    continue;
                }

                // 광선 시작점이 경계 안이면 entry 는 0 이다.
                if (entry > limit)
                {
                    continue;
                }

                Vector3 toCenter = visuals[i].center - ray.origin;
                float along = Vector3.Dot(toCenter, direction);
                float perpendicular = (toCenter - direction * along).magnitude;

                // 조준선 중심에 더 가까운 쪽. 거의 같으면(1cm) 더 가까운 쪽.
                bool better = perpendicular < bestPerpendicular - 0.01f ||
                              (Mathf.Abs(perpendicular - bestPerpendicular) <= 0.01f && entry < bestEntry);

                if (better)
                {
                    best = i;
                    bestPerpendicular = perpendicular;
                    bestEntry = entry;
                }
            }

            return best;
        }
    }
}

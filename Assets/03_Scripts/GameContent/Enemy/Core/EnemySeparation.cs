using System.Collections.Generic;
using UnityEngine;

namespace Pawntom.Enemy.Core
{
    /// <summary>
    /// 같은 지점을 노리는 적끼리 겹치지 않게 목적지를 비켜 준다.
    /// <para>
    /// NavMesh 회피는 "움직이는 동안" 서로 비켜 가게 할 뿐, 두 마리의 목적지가 같은 점(같은 순찰 웨이포인트, 같은 소집 좌표,
    /// 같은 추격 대상)이면 도착해서 그대로 겹친다. 목적지 근처에 이미 다른 개체가 있으면, 내 쪽으로 그만큼 당겨 그 앞에서 멈추게 한다.
    /// </para>
    /// 할당하지 않는다.
    /// </summary>
    public static class EnemySeparation
    {
        /// <param name="self">나의 현재 위치</param>
        /// <param name="destination">원래 목적지</param>
        /// <param name="others">다른 개체들의 현재 위치(나 제외)</param>
        /// <param name="minSeparation">개체 중심 사이에 둘 최소 거리. 0 이하면 손대지 않는다</param>
        public static Vector3 AdjustDestination(Vector3 self, Vector3 destination, IReadOnlyList<Vector3> others, float minSeparation)
        {
            if (others == null || minSeparation <= 0f)
            {
                return destination;
            }

            float minSq = minSeparation * minSeparation;

            for (int i = 0; i < others.Count; i++)
            {
                Vector3 other = others[i];
                Vector3 offset = destination - other;
                offset.y = 0f;

                if (offset.sqrMagnitude >= minSq)
                {
                    continue;
                }

                // 다른 개체 쪽에서 나를 향하는 방향으로 minSeparation 만큼 떨어진 지점을 새 목적지로 삼는다.
                Vector3 away = self - other;
                away.y = 0f;

                if (away.sqrMagnitude < 0.0001f)
                {
                    // 이미 같은 자리에 있다 — 원래 목적지 반대쪽으로라도 비킨다.
                    away = -offset;
                    away.y = 0f;

                    if (away.sqrMagnitude < 0.0001f)
                    {
                        away = Vector3.right;
                    }
                }

                Vector3 adjusted = other + away.normalized * minSeparation;
                adjusted.y = destination.y;
                return adjusted;
            }

            return destination;
        }
    }
}

using UnityEngine;

namespace Pawntom.Enemy.Core
{
    /// <summary>
    /// 시야 기하 계산. 감지 어댑터에서 떼어내 테스트 가능하게 둔다.
    /// </summary>
    public static class K9SightGeometry
    {
        // 방향 벡터를 수평에 눕혔을 때 사실상 0 인지 판정하는 부동소수점 방어값.
        // 감지·이동 어댑터가 쓰는 값과 같다.
        private const float FlatEpsilon = 0.0001f;

        /// <summary>
        /// 대상이 월드 수평면 기준 시야 부채꼴 안에 있는가. 높이 차는 각도에 반영하지 않는다.
        /// <para>
        /// 기즈모(<c>DrawSightCone</c>)와 정확히 같은 판정은 아니다 — 기즈모는 눈의 로컬 XZ 평면,
        /// 즉 눈의 피치만큼 기울어진 평면에 부채꼴을 그린다. 이 판정은 그것을 월드 수평면으로
        /// 근사한 것이다. 피치 13.816°·반각 37.9° 에서 두 경계의 차이는 0.8° 안쪽이라
        /// 배치 작업에서 눈에 띄지 않는다. 3D 각도 판정과의 차이(같은 조건에서 7° 이상)에 비하면
        /// 무시할 수 있는 오차이므로, 예측하기 쉬운 월드 수평면 쪽을 택한다.
        /// </para>
        /// <para>
        /// <paramref name="forward"/> 를 수평에 눕혔을 때 사실상 0 이면(수직으로 위·아래를 보는 눈)
        /// 판정할 평면이 없으므로 3D 각도로 되돌아간다.
        /// </para>
        /// <para>힙 할당이 없다. 매 틱 대상 수만큼 호출된다.</para>
        /// </summary>
        /// <param name="forward">눈이 바라보는 방향. 정규화되어 있지 않아도 된다.</param>
        /// <param name="toTarget">눈에서 대상으로 향하는 벡터.</param>
        /// <param name="halfAngleDegrees">시야각 전체 폭의 절반(도).</param>
        public static bool IsWithinHorizontalCone(Vector3 forward, Vector3 toTarget, float halfAngleDegrees)
        {
            Vector3 flatForward = forward;
            flatForward.y = 0f;

            // 눈이 거의 수직으로 위나 아래를 본다. 눕힐 평면이 없으므로 3D 각도로 판정한다.
            if (flatForward.sqrMagnitude < FlatEpsilon)
            {
                return Vector3.Angle(forward, toTarget) <= halfAngleDegrees;
            }

            Vector3 flatToTarget = toTarget;
            flatToTarget.y = 0f;

            // 대상이 눈의 바로 위나 아래다. 수평 각도가 정의되지 않으므로 부채꼴 안으로 본다 —
            // 이 경우를 걸러 내는 것은 각도가 아니라 거리 판정의 몫이다(TASK-007 3 비범위).
            if (flatToTarget.sqrMagnitude < FlatEpsilon)
            {
                return true;
            }

            return Vector3.Angle(flatForward, flatToTarget) <= halfAngleDegrees;
        }
    }
}

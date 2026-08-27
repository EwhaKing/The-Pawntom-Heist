using UnityEngine;

namespace Pawntom.Enemy.Core
{
    /// <summary>
    /// 두뇌가 바깥에 내리는 이동 명령.
    /// 구현이 내비게이션 기반이든 단순 보간이든 두뇌는 알지 못한다(DIP).
    /// </summary>
    public interface IK9Motor
    {
        /// <summary>목적지로 이동을 시작하거나 목적지를 갱신한다.</summary>
        void MoveTo(Vector3 destination, float speed);

        /// <summary>이동을 멈춘다.</summary>
        void Stop();

        /// <summary>
        /// 이동의 성격을 지정한다. 속도는 <see cref="MoveTo"/> 가 따로 받는다 —
        /// 여기서는 "어떻게 움직이는가"만 정한다.
        /// <para>상태 진입에서 한 번 부르는 것을 전제한다. 매 틱 부르지 않는다.</para>
        /// </summary>
        /// <param name="acceleration">목표 속도까지 붙는 가속도(m/s²). 낮으면 방향 전환 후 재가속이 느리다.</param>
        /// <param name="turnSpeedDegrees">이동 중 몸이 도는 속도(도/초). 경계 중 조준 회전과는 별개다.</param>
        /// <param name="brakeNearDestination">
        /// 목적지 근처에서 감속할지. 웨이포인트·조사 지점에는 곱게 서야 하므로 true,
        /// 추격에는 false 다 — 추격의 목적지는 대상 본인이라 늘 목적지 근처이고, 제동을 켜면 내내 브레이크가 걸린다.
        /// </param>
        void SetMotionProfile(float acceleration, float turnSpeedDegrees, bool brakeNearDestination);

        /// <summary>
        /// 목표 지점 쪽으로 몸의 방향을 돌린다. 위치는 바꾸지 않는다.
        /// <para>
        /// 회전 속도가 유한하므로 한 번의 호출로 다 돌지 않을 수 있다 — 매 틱 부르는 것을 전제한다.
        /// 목표가 자기 위치와 사실상 같으면 아무것도 하지 않는다.
        /// </para>
        /// </summary>
        void Face(Vector3 target, float degreesPerSecond, float deltaTime);

        /// <summary>마지막 목적지에 도달했는가.</summary>
        bool HasArrived { get; }

        /// <summary>현재 월드 좌표.</summary>
        Vector3 Position { get; }
    }
}

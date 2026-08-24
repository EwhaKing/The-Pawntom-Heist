using System.Collections.Generic;
using UnityEngine;

namespace Pawntom.Enemy.Core
{
    /// <summary>
    /// 순찰 커서가 이번 틱에 요구하는 행동.
    /// <para>
    /// 커서는 이동 담당을 모른다. "무엇을 해야 하는가" 만 돌려주고 실행은 두뇌가 한다 —
    /// 그래야 커서가 엔진도 이동 구현도 없이 단독으로 검증된다(DIP).
    /// </para>
    /// </summary>
    public enum K9PatrolAction
    {
        /// <summary>이번 틱에는 아무 명령도 내지 않는다.</summary>
        None = 0,

        /// <summary>목적지로 이동한다. 이때만 destination 이 유효하다.</summary>
        MoveTo = 1,

        /// <summary>제자리에 선다. 정지 래치가 열려 있을 때 한 번만 나간다.</summary>
        Stop = 2
    }

    /// <summary>
    /// 순찰 경로 위를 도는 커서. <b>"몇 번 지점을 향하고 있는가"</b> 와
    /// <b>"이번 틱에 무엇을 해야 하는가"</b> 만 안다.
    /// <para>
    /// 상태 머신 본체가 웨이포인트 순회까지 겸하면서 필드 7개를 떠안고 있었다.
    /// 그 덩어리를 통째로 옮긴 자리다(TASK-009 4.2).
    /// </para>
    /// <para>
    /// 두뇌가 필드로 하나만 들고 재사용한다. 틱마다 새로 만들지 않으므로 힙 할당이 없다.
    /// </para>
    /// </summary>
    public sealed class K9PatrolCursor
    {
        // 순찰 경로 — 씬에서 개체마다 다르게 주입된다. 코드에 좌표를 박지 않는다.
        private IReadOnlyList<Vector3> _waypoints;
        private bool _loop;
        private int _waypointIndex;
        private float _waypointWaitTimer;

        // 같은 지점으로 이동 명령을 두 번 내지 않기 위한 래치.
        private bool _moveCommandIssued;

        // 순환하지 않는 경로에서 마지막 지점까지 다 돈 상태.
        private bool _routeFinished;

        // 정지 명령이 이미 나갔는지. 제자리 대기 중에 매 틱 정지 명령이 나가는 것을 막는다.
        private bool _stopped;

        /// <summary>현재 향하고 있는 웨이포인트 번호. 경로가 없으면 0.</summary>
        public int WaypointIndex
        {
            get { return _waypointIndex; }
        }

        /// <summary>
        /// 순찰 경로를 교체한다. 웨이포인트가 0개면 순찰 상태에서 제자리 대기한다.
        /// <para>
        /// <b>번호를 0 으로 되돌린다.</b> 새 경로를 받았으면 처음부터 도는 것이 맞다 —
        /// <see cref="Reset"/> 과 다른 점이 여기다.
        /// </para>
        /// </summary>
        public void SetRoute(IReadOnlyList<Vector3> waypoints, bool loop)
        {
            _waypoints = waypoints;
            _loop = loop;
            _waypointIndex = 0;
            _waypointWaitTimer = 0f;
            _moveCommandIssued = false;
            _routeFinished = false;
            _stopped = false;
        }

        /// <summary>
        /// 순찰로 복귀할 때 진행 플래그만 되돌린다.
        /// <para>
        /// <b>번호는 유지한다.</b> 조사·추격을 마치고 돌아온 개는 가던 웨이포인트부터 이어 간다 —
        /// 매번 1번 지점으로 되돌아가면 경로 앞머리만 반복해서 밟게 된다.
        /// 이 비대칭은 의도된 것이다(TASK-009 5.3-1).
        /// </para>
        /// </summary>
        public void Reset()
        {
            _waypointWaitTimer = 0f;
            _moveCommandIssued = false;
            _routeFinished = false;
            _stopped = false;
        }

        /// <summary>
        /// 두뇌가 순찰 밖에서 직접 정지시켰을 때 정지 래치를 맞춰 준다.
        /// <para>
        /// 커서가 모르는 사이에 개가 이미 서 있으면, 커서는 중복 정지 명령을 낼지 말지 판단할 수 없다.
        /// 경계 진입처럼 두뇌가 직접 세운 자리에서 이 메서드를 부른다.
        /// </para>
        /// </summary>
        public void NotifyStopped()
        {
            _stopped = true;
        }

        /// <summary>
        /// 이번 틱에 무엇을 할지 고른다. <b>한 번의 호출에서 나가는 행동은 최대 하나다.</b>
        /// </summary>
        /// <param name="deltaTime">흐른 시간(초).</param>
        /// <param name="hasArrived">이동 담당이 목적지에 닿았는가. 커서는 이 값을 직접 읽지 않는다.</param>
        /// <param name="waitSeconds">웨이포인트 도달 후 다음으로 넘어가기까지의 대기 시간(초).</param>
        /// <param name="destination">
        /// <see cref="K9PatrolAction.MoveTo"/> 일 때만 유효한 목적지.
        /// </param>
        public K9PatrolAction Advance(
            float deltaTime, bool hasArrived, float waitSeconds, out Vector3 destination)
        {
            destination = default(Vector3);

            int count = _waypoints == null ? 0 : _waypoints.Count;

            // 경로가 없거나(0개) 순회가 끝났으면 제자리 대기한다.
            if (count == 0 || _routeFinished)
            {
                return StopOnce();
            }

            // 경로가 짧은 것으로 교체됐을 수 있다. 번호가 밖으로 나가면 처음으로 되돌린다.
            if (_waypointIndex >= count)
            {
                _waypointIndex = 0;
                _moveCommandIssued = false;
            }

            if (!_moveCommandIssued)
            {
                return IssueWaypointMove(out destination);
            }

            if (!hasArrived)
            {
                return K9PatrolAction.None;
            }

            _waypointWaitTimer += deltaTime;
            if (_waypointWaitTimer < waitSeconds)
            {
                return K9PatrolAction.None;
            }

            _waypointWaitTimer = 0f;

            int next = _waypointIndex + 1;
            if (next >= count)
            {
                if (!_loop)
                {
                    _routeFinished = true;
                    return StopOnce();
                }

                next = 0;
            }

            _waypointIndex = next;
            return IssueWaypointMove(out destination);
        }

        private K9PatrolAction IssueWaypointMove(out Vector3 destination)
        {
            destination = _waypoints[_waypointIndex];
            _moveCommandIssued = true;
            _stopped = false;
            return K9PatrolAction.MoveTo;
        }

        /// <summary>정지 명령은 래치가 열려 있을 때 한 번만 나간다.</summary>
        private K9PatrolAction StopOnce()
        {
            if (_stopped)
            {
                return K9PatrolAction.None;
            }

            _stopped = true;
            return K9PatrolAction.Stop;
        }
    }
}

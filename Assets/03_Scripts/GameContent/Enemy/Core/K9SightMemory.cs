using UnityEngine;

namespace Pawntom.Enemy.Core
{
    /// <summary>
    /// 시야의 잔상. <b>"지금 보이는가"</b> 와 <b>"방금 전까지 보였는가"</b> 를 분리한다.
    /// <para>
    /// 지각 스냅샷은 매 틱 비워지므로, 시야에서 벗어나는 순간 아무것도 못 본 개가 된다.
    /// 이 클래스가 그 사이를 메워 짧은 기억을 남긴다(TASK-007 2.3 원인 D).
    /// </para>
    /// <para>
    /// 두뇌가 필드로 하나만 들고 재사용한다. 틱마다 새로 만들지 않으므로 힙 할당이 없다.
    /// </para>
    /// </summary>
    public sealed class K9SightMemory
    {
        private bool _everSeen;
        private float _secondsSinceSeen;
        private Vector3 _lastSeenPosition;

        /// <summary>한 번이라도 본 적이 있는가.</summary>
        public bool EverSeen
        {
            get { return _everSeen; }
        }

        /// <summary>마지막으로 본 이후 흐른 시간(초). 본 적이 없으면 값의 의미가 없다.</summary>
        public float SecondsSinceSeen
        {
            get { return _secondsSinceSeen; }
        }

        /// <summary>마지막으로 본 좌표. 본 적이 없으면 default.</summary>
        public Vector3 LastSeenPosition
        {
            get { return _lastSeenPosition; }
        }

        /// <summary>
        /// 매 틱 한 번 호출한다.
        /// 보이면 시각을 0 으로 되돌리고 좌표를 갱신하며, 보이지 않으면 경과 시간만 누적한다.
        /// </summary>
        public void Tick(float deltaTime, bool hasSight, Vector3 sightPosition)
        {
            if (hasSight)
            {
                _everSeen = true;
                _secondsSinceSeen = 0f;
                _lastSeenPosition = sightPosition;
                return;
            }

            // 본 적이 없으면 셀 것도 없다. 0 이하의 시간은 누적하지 않는다.
            if (!_everSeen || deltaTime <= 0f)
            {
                return;
            }

            _secondsSinceSeen += deltaTime;
        }

        /// <summary>
        /// 본 적이 있고, 마지막으로 본 뒤 <paramref name="withinSeconds"/> 이하가 흘렀는가.
        /// <para>
        /// <paramref name="withinSeconds"/> 가 0 이면 "이번 틱에 실제로 보였을 때만" true 다 —
        /// 기억을 끈 설정에서 종전과 같은 동작을 유지하기 위한 경계다.
        /// </para>
        /// </summary>
        public bool IsWarm(float withinSeconds)
        {
            if (!_everSeen)
            {
                return false;
            }

            return _secondsSinceSeen <= withinSeconds;
        }

        /// <summary>전부 잊는다. 순찰로 복귀할 때 호출한다 — 순찰 복귀는 완전한 포기다.</summary>
        public void Forget()
        {
            _everSeen = false;
            _secondsSinceSeen = 0f;
            _lastSeenPosition = default(Vector3);
        }
    }
}

using System.Collections.Generic;

namespace Pawntom.Network
{
    /// <summary>
    /// 요청자별 짧은 쿨다운(틱 기준). 상태 권한 쪽에서만 쓴다 — 네트워크 상태가 아니다.
    /// 요청자 키는 PlayerRef 의 정수 값(PlayerRef.RawEncoded 등)을 쓴다.
    /// </summary>
    public sealed class RequestCooldown
    {
        private readonly Dictionary<int, int> _lastTick = new Dictionary<int, int>(4);
        private readonly int _cooldownTicks;

        public RequestCooldown(int cooldownTicks)
        {
            _cooldownTicks = cooldownTicks < 0 ? 0 : cooldownTicks;
        }

        /// <summary>
        /// 마지막 기록 틱에서 쿨다운 틱이 지나지 않았으면 true. 기록보다 같거나 이른 틱(재전송·순서 뒤바뀜)도 true.
        /// </summary>
        public bool IsCoolingDown(int requester, int tick)
        {
            if (_cooldownTicks == 0)
            {
                return false;
            }

            if (!_lastTick.TryGetValue(requester, out int last))
            {
                return false;
            }

            return tick - last < _cooldownTicks;
        }

        public void Record(int requester, int tick)
        {
            _lastTick[requester] = tick;
        }

        public void Clear()
        {
            _lastTick.Clear();
        }
    }
}

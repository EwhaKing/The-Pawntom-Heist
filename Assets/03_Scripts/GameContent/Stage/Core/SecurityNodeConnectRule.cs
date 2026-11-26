namespace Pawntom.Stage
{
    /// <summary>분전반 접속 요청 판정 결과.</summary>
    public enum SecurityNodeVerdict
    {
        Accepted = 0,
        AlreadyConnected = 1,
        InvalidPlayer = 2,
        PlayerDead = 3,
        OutOfRange = 4,
        NoCastStarted = 5,
        CastTooShort = 6,
        MovedDuringCast = 7,
    }

    /// <summary>상태 권한이 본 요청 시점의 사실. 클라이언트가 보낸 값이 아니라 권한 쪽 월드 상태로 채운다.</summary>
    public struct SecurityNodeConnectRequest
    {
        public bool AlreadyConnected;

        /// <summary>RPC 를 보낸 피어가 그 플레이어의 입력 권한자다.</summary>
        public bool PlayerValid;

        public bool PlayerDead;

        public bool InRange;

        /// <summary>권한 쪽에 이 플레이어의 캐스팅 시작 기록이 있다.</summary>
        public bool HasCastRecord;

        /// <summary>권한 쪽 틱으로 잰 캐스팅 시작부터 지금까지의 시간(초).</summary>
        public float ElapsedSeconds;

        /// <summary>캐스팅 시작 위치에서 지금까지 움직인 거리(m).</summary>
        public float Displacement;
    }

    /// <summary>
    /// 분전반(보안 노드) 접속 요청을 상태 권한에서 다시 검증하는 규칙(엔진 비의존).
    /// GAME_DESIGN §3.4 "3초 캐스팅, 이동 불가" + fusion-network.md "RPC 처리기는 거리·소유·중복을 재검증".
    /// </summary>
    public sealed class SecurityNodeConnectRule
    {
        private readonly float _castSeconds;
        private readonly float _timeTolerance;
        private readonly float _maxDisplacement;

        /// <param name="castSeconds">요구 캐스팅 시간.</param>
        /// <param name="timeTolerance">시작·완료 RPC 의 지연 차이 허용(초).</param>
        /// <param name="maxDisplacement">캐스팅 중 허용하는 위치 변화(m). 밀림·보정 오차.</param>
        public SecurityNodeConnectRule(float castSeconds, float timeTolerance, float maxDisplacement)
        {
            _castSeconds = castSeconds > 0f ? castSeconds : 0f;
            _timeTolerance = timeTolerance > 0f ? timeTolerance : 0f;
            _maxDisplacement = maxDisplacement > 0f ? maxDisplacement : 0f;
        }

        /// <summary>캐스팅 시작 요청 판정(시간·이동은 아직 보지 않는다).</summary>
        public SecurityNodeVerdict CanBegin(bool alreadyConnected, bool playerValid, bool playerDead, bool inRange)
        {
            if (alreadyConnected)
            {
                return SecurityNodeVerdict.AlreadyConnected;
            }

            if (!playerValid)
            {
                return SecurityNodeVerdict.InvalidPlayer;
            }

            if (playerDead)
            {
                return SecurityNodeVerdict.PlayerDead;
            }

            return inRange ? SecurityNodeVerdict.Accepted : SecurityNodeVerdict.OutOfRange;
        }

        /// <summary>캐스팅 완료(접속) 요청 판정.</summary>
        public SecurityNodeVerdict Evaluate(SecurityNodeConnectRequest request)
        {
            SecurityNodeVerdict begin = CanBegin(request.AlreadyConnected, request.PlayerValid, request.PlayerDead, request.InRange);

            if (begin != SecurityNodeVerdict.Accepted)
            {
                return begin;
            }

            if (!request.HasCastRecord)
            {
                return SecurityNodeVerdict.NoCastStarted;
            }

            if (request.ElapsedSeconds < _castSeconds - _timeTolerance)
            {
                return SecurityNodeVerdict.CastTooShort;
            }

            return request.Displacement > _maxDisplacement
                ? SecurityNodeVerdict.MovedDuringCast
                : SecurityNodeVerdict.Accepted;
        }
    }
}

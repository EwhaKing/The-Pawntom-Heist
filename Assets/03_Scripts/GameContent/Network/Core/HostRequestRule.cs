namespace Pawntom.Network
{
    /// <summary>
    /// 상태 권한(호스트)이 클라이언트 요청을 다시 판정한 결과. 거부 사유는 요청자에게 그대로 통지된다(RPC 인자로 int 변환).
    /// 값을 바꾸지 않는다 — 통지 RPC 가 숫자로 주고받는다.
    /// </summary>
    public enum RequestVerdict : byte
    {
        Accepted = 0,

        /// <summary>요청자(PlayerRef)의 플레이어 오브젝트를 찾지 못했다.</summary>
        RequesterInvalid = 1,

        /// <summary>요청자가 죽어 있다.</summary>
        RequesterDead = 2,

        /// <summary>현장 조작: 대상 근처가 아니다.</summary>
        OutOfRange = 3,

        /// <summary>본부 원격 조작: 단말기 앞(또는 본부 안)이 아니다.</summary>
        NotAtTerminal = 4,

        /// <summary>같은 요청자의 직전 요청과 너무 가깝다.</summary>
        CoolingDown = 5,

        /// <summary>해금 전이라 할 수 없다.</summary>
        Locked = 6,

        /// <summary>이미 그 상태다(중복 요청 포함).</summary>
        AlreadyApplied = 7,

        /// <summary>이 대상에서 꺼진 기능이다(예: 현장 개방 off).</summary>
        Disabled = 8,

        /// <summary>대상이 속한 구역이 아직 발견되지 않았다.</summary>
        AreaUndiscovered = 9,
    }

    /// <summary>요청자에 대해 상태 권한이 직접 확인한 사실. 클라이언트가 보낸 값이 아니다.</summary>
    public struct RequesterFacts
    {
        /// <summary>요청자 PlayerRef 에 대응하는 유효한 플레이어 오브젝트가 있다.</summary>
        public bool Valid;

        public bool Dead;

        /// <summary>명령이 요구하는 자리(문 근처 / 단말기 앞)에 있다.</summary>
        public bool InPlace;

        public bool CoolingDown;
    }

    /// <summary>
    /// 공통 판정 순서: 요청자 유효 → 사망 → 대상 상태 전이 → 위치 → 쿨다운.
    /// 상태 전이를 위치보다 먼저 보는 이유: 중복 요청에는 "이미 그 상태"가 더 정확한 사유다.
    /// 쿨다운을 마지막에 보는 이유: 쿨다운은 수락될 요청의 연타만 막는다.
    /// </summary>
    public static class HostRequestRule
    {
        public static RequestVerdict Combine(in RequesterFacts requester, RequestVerdict transition, RequestVerdict placeFailure)
        {
            if (!requester.Valid)
            {
                return RequestVerdict.RequesterInvalid;
            }

            if (requester.Dead)
            {
                return RequestVerdict.RequesterDead;
            }

            if (transition != RequestVerdict.Accepted)
            {
                return transition;
            }

            if (!requester.InPlace)
            {
                return placeFailure;
            }

            if (requester.CoolingDown)
            {
                return RequestVerdict.CoolingDown;
            }

            return RequestVerdict.Accepted;
        }
    }
}

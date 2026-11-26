namespace Pawntom.Network
{
    /// <summary>출발 요청 판정 결과. 값을 바꾸지 않는다 — 거부 통지 RPC 가 숫자로 주고받는다.</summary>
    public enum DepartureVerdict : byte
    {
        Accepted = 0,
        AlreadyCleared = 1,
        Misconfigured = 2,
        RequesterInvalid = 3,
        RequesterDead = 4,
        CoolingDown = 5,
        NoAlivePlayerInHeadquarters = 6,
        NoExitItem = 7,
    }

    /// <summary>상태 권한이 직접 확인한 출발 조건.</summary>
    public struct DepartureFacts
    {
        public bool AlreadyCleared;

        /// <summary>본부 구역·탈출 아이템 Id 같은 씬 설정이 올바르다.</summary>
        public bool ConfigValid;

        public bool RequesterValid;
        public bool RequesterDead;
        public bool CoolingDown;
        public bool AnyAlivePlayerInHeadquarters;
        public bool HasExitItem;
    }

    /// <summary>
    /// RuleManager 클리어 조건: 본부 안에 산 플레이어 1명 이상 + 팀원 중 1명 이상 캣닢 보유 + 출발 버튼.
    /// 순서: 이미 클리어 → 설정 오류 → 요청자 → 쿨다운 → 본부 인원 → 캣닢(기존 판정 순서 유지).
    /// </summary>
    public static class DepartureRule
    {
        public static DepartureVerdict Evaluate(in DepartureFacts facts)
        {
            if (facts.AlreadyCleared)
            {
                return DepartureVerdict.AlreadyCleared;
            }

            if (!facts.ConfigValid)
            {
                return DepartureVerdict.Misconfigured;
            }

            if (!facts.RequesterValid)
            {
                return DepartureVerdict.RequesterInvalid;
            }

            if (facts.RequesterDead)
            {
                return DepartureVerdict.RequesterDead;
            }

            if (facts.CoolingDown)
            {
                return DepartureVerdict.CoolingDown;
            }

            if (!facts.AnyAlivePlayerInHeadquarters)
            {
                return DepartureVerdict.NoAlivePlayerInHeadquarters;
            }

            if (!facts.HasExitItem)
            {
                return DepartureVerdict.NoExitItem;
            }

            return DepartureVerdict.Accepted;
        }
    }
}

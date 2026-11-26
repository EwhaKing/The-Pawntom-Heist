namespace Pawntom.Network
{
    /// <summary>GAME_DESIGN §3.4 스프링클러 가동 요청 판정: 1회용, 발견된 구역, 본부 단말기에서.</summary>
    public static class SprinklerRule
    {
        public static RequestVerdict Evaluate(bool used, bool areaDiscovered, in RequesterFacts requester)
        {
            RequestVerdict transition = !areaDiscovered
                ? RequestVerdict.AreaUndiscovered
                : (used ? RequestVerdict.AlreadyApplied : RequestVerdict.Accepted);

            return HostRequestRule.Combine(requester, transition, RequestVerdict.NotAtTerminal);
        }
    }
}

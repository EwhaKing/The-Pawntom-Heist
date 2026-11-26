namespace Pawntom.Network
{
    /// <summary>전등 요청 종류. 값을 바꾸지 않는다 — RPC 가 숫자로 주고받는다.</summary>
    public enum LightCommand : byte
    {
        Unlock = 0,

        /// <summary>미니게임 성공: 해금과 소등을 한 요청으로(두 RPC 로 나누면 순서·부분 반영 문제가 생긴다).</summary>
        UnlockAndTurnOff = 1,

        TurnOff = 2,
        TurnOn = 3,
    }

    /// <summary>GAME_DESIGN §3.4 전등 요청 판정. 모든 명령은 본부 단말기에서 한다.</summary>
    public static class LightCommandRule
    {
        public static RequestVerdict CheckTransition(LightCommand command, bool unlocked, bool on, bool areaDiscovered)
        {
            if (!areaDiscovered)
            {
                return RequestVerdict.AreaUndiscovered;
            }

            switch (command)
            {
                case LightCommand.Unlock:
                    return unlocked ? RequestVerdict.AlreadyApplied : RequestVerdict.Accepted;

                case LightCommand.UnlockAndTurnOff:
                    return unlocked && !on ? RequestVerdict.AlreadyApplied : RequestVerdict.Accepted;

                case LightCommand.TurnOff:
                    if (!unlocked)
                    {
                        return RequestVerdict.Locked;
                    }

                    return on ? RequestVerdict.Accepted : RequestVerdict.AlreadyApplied;

                case LightCommand.TurnOn:
                    if (!unlocked)
                    {
                        return RequestVerdict.Locked;
                    }

                    return on ? RequestVerdict.AlreadyApplied : RequestVerdict.Accepted;

                default:
                    return RequestVerdict.Disabled;
            }
        }

        public static RequestVerdict Evaluate(LightCommand command, bool unlocked, bool on, bool areaDiscovered, in RequesterFacts requester)
        {
            return HostRequestRule.Combine(requester, CheckTransition(command, unlocked, on, areaDiscovered), RequestVerdict.NotAtTerminal);
        }
    }
}

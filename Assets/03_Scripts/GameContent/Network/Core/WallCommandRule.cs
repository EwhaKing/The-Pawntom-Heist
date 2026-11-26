namespace Pawntom.Network
{
    /// <summary>격벽 요청 종류. 값을 바꾸지 않는다 — RPC 가 숫자로 주고받는다.</summary>
    public enum WallCommand : byte
    {
        /// <summary>본부: 해킹 성공 → 해금(Locked → Unlocked).</summary>
        Unlock = 0,

        /// <summary>현장: 해금된 격벽 열기(Unlocked → Open, 자동 닫힘 예약).</summary>
        FieldOpen = 1,

        /// <summary>본부: 해금된 격벽 수동 열기.</summary>
        RemoteOpen = 2,

        /// <summary>본부: 열린 격벽 즉시 닫기.</summary>
        RemoteClose = 3,
    }

    /// <summary>
    /// GAME_DESIGN §3.4 격벽 상태 머신의 요청 판정. Destroyed(브루투스)는 아직 구현되지 않았다.
    /// </summary>
    public static class WallCommandRule
    {
        /// <summary>현장 명령이면 "문 근처", 아니면 "단말기 앞"이 위치 조건이다.</summary>
        public static bool IsField(WallCommand command)
        {
            return command == WallCommand.FieldOpen;
        }

        public static RequestVerdict CheckTransition(WallCommand command, bool unlocked, bool open, bool fieldOpenAllowed)
        {
            switch (command)
            {
                case WallCommand.Unlock:
                    return unlocked ? RequestVerdict.AlreadyApplied : RequestVerdict.Accepted;

                case WallCommand.FieldOpen:
                    if (!fieldOpenAllowed)
                    {
                        return RequestVerdict.Disabled;
                    }

                    if (!unlocked)
                    {
                        return RequestVerdict.Locked;
                    }

                    return open ? RequestVerdict.AlreadyApplied : RequestVerdict.Accepted;

                case WallCommand.RemoteOpen:
                    if (!unlocked)
                    {
                        return RequestVerdict.Locked;
                    }

                    return open ? RequestVerdict.AlreadyApplied : RequestVerdict.Accepted;

                case WallCommand.RemoteClose:
                    // 닫기는 잠김 여부와 무관하게 열려 있기만 하면 된다(체크포인트 복원으로 잠긴 채 열린 경우 포함).
                    return open ? RequestVerdict.Accepted : RequestVerdict.AlreadyApplied;

                default:
                    return RequestVerdict.Disabled;
            }
        }

        public static RequestVerdict Evaluate(WallCommand command, bool unlocked, bool open, bool fieldOpenAllowed, in RequesterFacts requester)
        {
            return HostRequestRule.Combine(requester,
                CheckTransition(command, unlocked, open, fieldOpenAllowed),
                IsField(command) ? RequestVerdict.OutOfRange : RequestVerdict.NotAtTerminal);
        }
    }
}

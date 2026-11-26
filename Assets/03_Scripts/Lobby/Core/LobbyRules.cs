namespace Pawntom.Lobby
{
    /// <summary>
    /// 로비 판정에 필요한 참가자 한 명의 상태. 사전 할당 버퍼에 담아 넘긴다(할당 없음).
    /// </summary>
    public readonly struct LobbyParticipant
    {
        public readonly bool IsHost;
        public readonly bool HasSelectedCat;
        public readonly bool IsReady;

        public LobbyParticipant(bool isHost, bool hasSelectedCat, bool isReady)
        {
            IsHost = isHost;
            HasSelectedCat = hasSelectedCat;
            IsReady = isReady;
        }
    }

    /// <summary>
    /// 로비 요청을 호스트가 다시 검증하는 규칙. Fusion·UnityEngine·CatType 비의존.
    /// 근거: docs/project/UI_SPEC.md §2.2, .claude/runs/20261125-lobby-room-ui/plan.md §4.
    /// </summary>
    public static class LobbyRules
    {
        /// <summary>고양이 값이 0 ~ catCount-1 범위인가.</summary>
        public static bool IsValidCat(int value, int catCount)
        {
            return value >= 0 && value < catCount;
        }

        /// <summary>
        /// Ready 토글 요청을 받아들일 수 있는가.
        /// 호스트는 Ready가 필요 없으므로(항상 Ready로 인식) 거부, 캐릭터 미선택도 거부.
        /// 캐릭터를 바꾸면 Ready가 풀리므로 Ready=true면 항상 선택된 상태다 → 취소도 이 조건으로 충분하다.
        /// </summary>
        public static bool CanToggleReady(bool isHost, bool hasSelectedCat)
        {
            return !isHost && hasSelectedCat;
        }

        /// <summary>
        /// 게임 시작 가능 여부. participants의 앞 count개만 읽는다.
        /// 호스트가 목록에 있어야 하고, 모두(호스트 포함) 캐릭터를 골랐고, 호스트를 뺀 모두가 Ready여야 한다.
        /// 같은 고양이 중복 선택은 허용한다.
        /// </summary>
        public static bool CanStartGame(LobbyParticipant[] participants, int count)
        {
            if (participants == null || count <= 0)
                return false;

            if (count > participants.Length)
                count = participants.Length;

            bool hasHost = false;

            for (int i = 0; i < count; i++)
            {
                LobbyParticipant p = participants[i];

                if (!p.HasSelectedCat)
                    return false;

                if (p.IsHost)
                {
                    hasHost = true;
                    continue;
                }

                if (!p.IsReady)
                    return false;
            }

            return hasHost;
        }

        /// <summary>스팀 연동 전 기본 이름. 슬롯 0 → "Player 1".</summary>
        public static string DefaultPlayerName(int slotIndex)
        {
            return "Player " + (slotIndex + 1);
        }
    }
}

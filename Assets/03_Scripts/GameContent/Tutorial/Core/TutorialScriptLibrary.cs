using System.Collections.Generic;

namespace Pawntom.Tutorial.Core
{
    /// <summary>
    /// 튜토리얼 기획서(1~8단계 대본)를 비트로 옮긴 기본 대본.
    ///
    /// <para><b>표기</b></para>
    /// <list type="bullet">
    /// <item>키 안내 문구의 <c>[...]</c>는 강조 표시(키 칩)로 바뀐다.</item>
    /// <item><c>{Interact}</c>·<c>{TerminalExit}</c>는 실행 시 실제 키로 치환된다. 기획서는 상호작용을 F로 적었지만
    ///       현재 입력 바인딩은 E이고, 단말기는 Esc로 닫힌다. 바인딩을 따라가야 안내가 거짓말을 하지 않는다.</item>
    /// </list>
    ///
    /// <para>대사 원문은 기획서 그대로다. 수정은 기획서를 먼저 고치고 여기를 맞춘다.</para>
    /// </summary>
    public static class TutorialScriptLibrary
    {
        private const TutorialSpeaker Hq = TutorialSpeaker.Command;
        private const TutorialSpeaker Cat = TutorialSpeaker.Kkamnyang;

        public static TutorialScript CreateDefault()
        {
            return new TutorialScript(CreateDefaultBeats());
        }

        public static List<TutorialBeat> CreateDefaultBeats()
        {
            List<TutorialBeat> beats = new List<TutorialBeat>(48);

            // ───────────── 1단계 오프닝 (본부) ─────────────
            beats.Add(TutorialBeat.Main("1-1", 1, TutorialSignal.EyesOpened,
                L("1-1", Hq, "칙... 들리십니까, 깜냥이 씨. 약속드린 대로 연결했습니다"),
                L("1-2", Cat, "들려요. 그럼 오늘부터 제가 현장을 맡는 거죠"),
                L("1-3", Hq, "그렇습니다. 말로 설명드릴 것은 이미 다 드렸으니, 이제 몸으로 익히시면 됩니다"),
                L("1-4", Cat, "그 전에 하나만요. 오늘 훔치는 게 정확히 뭐예요"),
                L("1-5", Hq, "전설의 캣닢입니다. 가장 안쪽 금고에 있습니다. 쓰임새는 나중에 말씀드리겠습니다"),
                L("1-6", Cat, "...알겠어요. 일단 믿고 가볼게요"),
                L("1-7", Hq, "좋습니다. 하나씩 안내해 드리겠습니다. 복도로 나가 보십시오")
                    .WithAction(TutorialLineAction.EnableControl)));

            // ───────────── 2단계 기본 조작 (본부, 복도 A) ─────────────
            beats.Add(TutorialBeat.Main("2-1", 2, TutorialSignal.None,
                L("2-1", Hq, "먼저 걸어 보십시오. 고양이시니 어렵지 않으실 겁니다")
                    .WithHint("[WASD]로 이동하고 [마우스]로 둘러봅시다", TutorialSignal.MovedAndLooked)));

            beats.Add(TutorialBeat.Main("2-2", 2, TutorialSignal.FirstFootprint,
                L("2-2", Hq, "뒤를 돌아보십시오. 지나온 자리에 무엇이 남았습니까?")
                    .WithHint("[마우스]로 뒤를 돌아봅시다", TutorialSignal.LookedBack)));

            beats.Add(TutorialBeat.Main("2-3", 2, TutorialSignal.LookedBack,
                L("2-3", Cat, "발자국이네요"),
                L("2-4", Hq, "경비견은 저것을 따라옵니다. 저도 저것 때문에 한 번 크게 당했습니다"),
                L("2-5", Cat, "...지휘부도 현장에 계셨던 거예요?"),
                L("2-6", Hq, "그건 나중에 말씀드리지요. 지금은 달리는 법을 배우실 차례입니다")
                    .WithHint("[Shift]키를 누르고 달려봅시다", TutorialSignal.SprintStarted))
                .WithFreshTrigger());

            beats.Add(TutorialBeat.Main("2-7", 2, TutorialSignal.SprintStarted,
                L("2-7", Hq, "발자국이 커졌지요. 달릴수록 더 눈에 띕니다. 숨도 차오르고요")
                    .WithTimedHint("[스태미나 바]를 확인합시다", 5f, TutorialSignal.StaminaBelowHalf)));

            beats.Add(TutorialBeat.Side("2-8", 2, TutorialSignal.StaminaBelowHalf, "2-7", "3-1",
                L("2-8", Hq, "숨이 차면 쉬십시오. 바닥나면 한동안 뛰지 못합니다")));

            beats.Add(TutorialBeat.Main("2-9", 2, TutorialSignal.ObstacleAhead,
                // 안내는 상자 반대편(ObstacleCleared 영역)에 도착해야 사라진다 — 제자리 점프로는 지워지지 않는다.
                L("2-9", Hq, "앞의 상자를 뛰어넘어 보십시오")
                    .WithHint("[Space]키로 점프해봅시다", TutorialSignal.ObstacleCleared))
                .AsSkippable());

            beats.Add(TutorialBeat.Main("2-10", 2, TutorialSignal.CorridorAEndReached,
                L("2-10", Hq, "신호가 잡혔습니다. 이 문은 이제 본부 단말기에서 열 수 있습니다"),
                L("2-11", Cat, "지금은 닫혀 있는데요"),
                L("2-12", Hq, "나중에 열겠습니다. 먼저 복도 B의 분전반부터 찾으십시오"))
                .AsSkippable());

            // ───────────── 3단계 첫 마주침 (복도 B, 본부) ─────────────
            // 3-1·3-3 은 건너뛸 수 없다 — 분전반 접속 없이 단말기(4단계)로 넘어가지 않는다.
            // (분전반이 씬에 없으면 여기서 멈춘다. 디버그 F10 으로 강제 진행할 수 있다.)
            beats.Add(TutorialBeat.Main("3-1", 3, TutorialSignal.NearSecurityNode,
                L("3-1", Hq, "저 상자가 분전반입니다. 연결하면 금고 쪽 구역이 이쪽에 잡힙니다. 하는 동안은 움직일 수 없으니 주변부터 살피십시오")
                    .WithHint("[{Interact}]키를 길게 눌러 분전반에 접속합시다",
                        TutorialSignal.SecurityNodeCastStarted, TutorialSignal.SecurityNodeConnected)));

            beats.Add(TutorialBeat.Main("3-2", 3, TutorialSignal.SecurityNodeThreatCue,
                L("3-2", Cat, "...뭔가 오는 소리가 나요"))
                .AsSkippable());

            beats.Add(TutorialBeat.Main("3-3", 3, TutorialSignal.SecurityNodeConnected,
                L("3-3", Hq, "연결됐습니다. 금고 쪽 구역이 잡혔습니다. 그런데...")));

            beats.Add(TutorialBeat.Main("3-4", 3, TutorialSignal.EnemyChasing,
                L("3-4", Hq, "경비견입니다! 발자국을 따라왔습니다. 한 마리가 짖으면 나머지도 몰려옵니다. 본부로 돌아오십시오, 지금 당장")
                    .WithHint("[Shift]키로 달려 본부로 돌아갑시다", TutorialSignal.PlayerInMainbase))
                .AsSkippable());

            beats.Add(TutorialBeat.Side("3-5", 3, TutorialSignal.StaminaBelow30, "3-4", "4-1",
                L("3-5", Hq, "숨 관리하십시오! 바닥나면 끝입니다")
                    .WithTimedHint("[스태미나 바]를 확인합시다", 4f)));

            beats.Add(TutorialBeat.Main("3-6", 3, TutorialSignal.SafeInMainbase,
                L("3-6", Hq, "본부 안까지는 들어오지 못합니다. 숨을 돌리십시오"),
                L("3-7", Cat, "와, 진짜 발자국만 보고 쫓아오네. 생각보다 위험한데요"),
                L("3-8", Hq, "이제 아셨지요? 다음은 본부입니다. 단말기 앞으로 가십시오"))
                .AsSkippable()
                .RequiringPlayed("3-4"));

            // ───────────── 4단계 오퍼레이터 전환 (본부 터미널) ─────────────
            beats.Add(TutorialBeat.Main("4-1", 4, TutorialSignal.NearTerminal,
                L("4-1", Hq, "그 단말기가 보안망의 입구입니다. 보안망은 현장에서만 열립니다. 제가 직접 갈 수 있었다면 벌써 갔겠지요")
                    .WithHint("[{Interact}]키를 눌러 단말기에 접속합시다", TutorialSignal.TerminalOpened),
                L("4-2", Cat, "본부 일도 제가 하라는 거네요"),
                L("4-3", Hq, "지금은 그렇습니다. 둘 다 익혀 두십시오. 나중에 쓸모가 있을 겁니다")));

            beats.Add(TutorialBeat.Main("4-4", 4, TutorialSignal.TerminalOpened,
                L("4-4", Hq, "먼저 CCTV 탭을 보십시오. 파란 점이 현재 위치, 붉은 점이 경비견입니다")
                    .WithHint("[CCTV] 탭에서 상황을 확인합시다", TutorialSignal.CctvChecked)));

            beats.Add(TutorialBeat.Main("4-5", 4, TutorialSignal.CctvChecked,
                L("4-5", Cat, "아직 입구 근처에 있네요"),
                L("4-6", Hq, "경비견들이 돌아가기 전에 문을 하나 열어 두겠습니다. 격벽 탭에서 복도 A 끝의 붉은 문을 고르십시오. 제가 말씀드리기 전에는 다른 문을 건드리지 마십시오")
                    .WithHint("[격벽] 탭에서 붉은 격벽을 더블클릭합시다", TutorialSignal.DoorHackStarted))
                .AsSkippable());

            beats.Add(TutorialBeat.Main("4-7", 4, TutorialSignal.DoorHackStarted,
                L("4-7", Hq, "화면에 뜬 순서를 그대로 입력하십시오. 틀리면 처음부터이니 침착하게 하십시오")
                    .WithHint("[방향키]로 순서를 입력합시다 ([Esc]키로 취소)",
                        TutorialSignal.DoorHackSucceeded, TutorialSignal.DoorHackFailed))
                .AsSkippable());

            TutorialLine exitTerminal = L("4-9", Hq, "이제 나와서 문 쪽으로 가십시오")
                .WithHint("[{TerminalExit}]키를 눌러 단말기에서 나옵시다", TutorialSignal.TerminalClosed);

            beats.Add(TutorialBeat.Main("4-8", 4, TutorialSignal.DoorHackSucceeded,
                L("4-8", Hq, "좋습니다, 푸른색이 됐습니다. 저 문 안에는 경비견이 들어오지 못합니다. 쓸 만한 은신처 겸 창고입니다"),
                exitTerminal)
                .WithVariant("4-10",
                    L("4-11", Hq, "됐습니다. 소음이 났으니 복도로 나가실 때는 조심하십시오"),
                    exitTerminal));

            beats.Add(TutorialBeat.Side("4-10", 4, TutorialSignal.DoorHackFailed, "4-4", "4-8",
                L("4-10", Hq, "소리가 났습니다. 경비견이 문 쪽으로 쏠리고 있습니다. 잠시 쉬었다가 다시 시도하십시오")
                    .WithTimedHint("3초 뒤 다시 시도합시다", 3f, TutorialSignal.DoorHackStarted))
                .AsRepeatable(1f));

            // ───────────── 5단계 침투조 복귀 (복도 A, 파밍 방) ─────────────
            beats.Add(TutorialBeat.Main("5-1", 5, TutorialSignal.BlueDoorAhead,
                L("5-1", Hq, "푸른 문이 열려 있는 시간은 잠깐입니다. 열고 바로 들어가십시오")
                    .WithHint("[{Interact}]키로 격벽을 열어봅시다", TutorialSignal.DoorOpened, TutorialSignal.LootRoomEntered))
                .AsSkippable());

            beats.Add(TutorialBeat.Main("5-2", 5, TutorialSignal.LootRoomEntered,
                L("5-2", Hq, "전리품 창고입니다. 마음에 드는 것을 챙기십시오"))
                .AsSkippable());

            beats.Add(TutorialBeat.Main("5-3", 5, TutorialSignal.NearLoot,
                L("5-3", Hq, "눈에 띄는 대로 주워 담으십시오")
                    .WithHint("[{Interact}]키로 전리품을 주워봅시다", TutorialSignal.ItemLooted))
                .AsSkippable());

            beats.Add(TutorialBeat.Main("5-4", 5, TutorialSignal.ItemLooted,
                L("5-4", Hq, "걸음이 느려지셨지요? 살이 찐 게 아니라 짐입니다")
                    .WithTimedHint("[무게 표시]를 확인합시다", 4f),
                L("5-5", Cat, "지금 놀리신 거죠?")));

            beats.Add(TutorialBeat.Main("5-6", 5, TutorialSignal.InventoryThreeFilled,
                L("5-6", Hq, "사실을 말씀드렸을 뿐입니다. 가방은 네 칸이 전부입니다. 한 칸은 캣닢 자리로 비워 두십시오. 필요 없는 것은 버리셔도 됩니다")
                    .WithHint("[Q]키로 아이템을 버려봅시다", TutorialSignal.ItemDropped))
                .AsSkippable());

            beats.Add(TutorialBeat.Side("5-7", 5, TutorialSignal.InventoryFullRejected, "5-4", "6-1",
                L("5-7", Cat, "더는 못 들겠어요")));

            beats.Add(TutorialBeat.Main("5-8", 5, TutorialSignal.LootRoomExiting,
                L("5-8", Hq, "욕심 부린 만큼 느려집니다. 경비견 앞에서 후회하지 마십시오"))
                .AsSkippable());

            // ───────────── 6단계 금고 복도 돌파 (복도 B, 금고 복도) ─────────────
            beats.Add(TutorialBeat.Main("6-1", 6, TutorialSignal.VaultCorridorEntered,
                L("6-1", Hq, "금고방은 저 복도가 감싸고 있습니다. 경비견 두 마리가 서로 반대로 돌고 있습니다"),
                L("6-2", Cat, "둘 다 피해서 들어가야 하네요"),
                L("6-3", Hq, "순찰 방향을 눈에 익혀 두십시오. 둘 사이에 틈이 생기는 순간이 있습니다")
                    .WithPreDelay(3f))
                .AsSkippable());

            beats.Add(TutorialBeat.Side("6-4", 6, TutorialSignal.EnemyChasing, "6-1", "7-4",
                L("6-4", Hq, "들켰습니다! 무거워도 달리십시오")
                    .WithHint("[Shift]키로 달려 도망칩시다", TutorialSignal.SprintStarted)));

            beats.Add(TutorialBeat.Main("6-5", 6, TutorialSignal.VaultRoomEntered,
                L("6-5", Hq, "저기입니다. 이제 금고만 열면 됩니다"))
                .AsSkippable());

            // ───────────── 7단계 캣닢 획득 및 탈출 (금고방, 복도, 본부) ─────────────
            beats.Add(TutorialBeat.Main("7-1", 7, TutorialSignal.VaultFront,
                L("7-1", Hq, "금고입니다. 아까 문보다 훨씬 까다롭습니다. 소리가 나면 경비견이 몰려옵니다")
                    .WithHint("[{Interact}]키를 눌러 금고 해킹을 시작합시다", TutorialSignal.VaultHackStarted))
                .AsSkippable());

            beats.Add(TutorialBeat.Main("7-2", 7, TutorialSignal.VaultHackStarted,
                L("7-2", Hq, "움직이는 표시가 초록 구간에 들어오는 순간을 노리십시오")
                    .WithHint("[Space]키로 타이밍을 맞춰봅시다",
                        TutorialSignal.VaultHackSucceeded, TutorialSignal.VaultHackFailed))
                .AsSkippable()
                .AsAnchor());

            beats.Add(TutorialBeat.Side("7-3", 7, TutorialSignal.VaultHackFailed, "7-2", "7-4",
                L("7-3", Hq, "경보가 울렸습니다. 서둘러 다시 하십시오"))
                .AsRepeatable(1f));

            beats.Add(TutorialBeat.Main("7-4", 7, TutorialSignal.CatnipAcquired,
                L("7-4", Cat, "...이게 전설의 캣닢"),
                L("7-5", Hq, "...드디어입니다. 정말 해냈군요, 깜냥이"),
                L("7-6", Hq, "하지만 냄새가 퍼집니다. 발자국이 계속 생기고 몸도 무거워질 겁니다")
                    .WithTimedHint("[디버프 아이콘]을 확인합시다", 5f),
                L("7-7", Cat, "경비견이 전부 쫓아오겠네요"),
                L("7-8", Hq, "네. 그것은 반드시 본부로 가져오십시오. 다른 곳에 넘기지 마십시오")
                    .WithHint("[Shift]키로 달려 본부로 탈출합시다", TutorialSignal.PlayerInMainbase))
                .AsAnchor());

            beats.Add(TutorialBeat.Side("7-9", 7, TutorialSignal.EnemyChasing, "7-4", "7-10",
                L("7-9", Hq, "뒤를 보지 마십시오. 앞만 보십시오")));

            beats.Add(TutorialBeat.Main("7-10", 7, TutorialSignal.PlayerInMainbase,
                L("7-10", Hq, "...됐습니다. 잘하셨습니다")
                    .WithAction(TutorialLineAction.OpenSettlement))
                .RequiringPlayed("7-4"));

            // ───────────── 8단계 클로징 (본부) ─────────────
            beats.Add(TutorialBeat.Main("8-1", 8, TutorialSignal.SettlementShown,
                L("8-1", Hq, "정산입니다. 가져오신 만큼 값을 쳐 드리겠습니다")
                    .WithAction(TutorialLineAction.RevealSettlement)));

            beats.Add(TutorialBeat.Main("8-2", 8, TutorialSignal.SettlementRevealed,
                L("8-2", Cat, "저기, 이름이라도 알려주면 안 돼요?"),
                L("8-3", Hq, "다음에 말씀드리겠습니다"),
                L("8-4", Cat, "항상 그렇게 넘기시네요"),
                L("8-5", Hq, "지금은 팀부터 꾸리십시오. 현장과 본부를 나눠 맡을 동료가 필요합니다"),
                L("8-6", Cat, "...알겠어요. 다음엔 꼭 같이 올게요"),
                L("8-7", Hq, "기대하겠습니다. 교신 종료합니다")
                    .WithAction(TutorialLineAction.EndTransmission)));

            AddSituationalBeats(beats);
            return beats;
        }

        /// <summary>
        /// 상황별 반복 대사. 진행이 막히거나 시연 중 변수가 생겼을 때의 보조 대사다.
        /// 후보가 여러 개면 발동할 때마다 번갈아 쓰고(연달아 같은 대사 금지), 대사 뒤에는 그 구간의 키 안내를 다시 띄운다.
        /// 단계(Stage)는 0 — 특정 단계에 속하지 않는다. 반복 간격은 기획서에 없어 임의값이다(사람 확인 대상).
        /// </summary>
        private static void AddSituationalBeats(List<TutorialBeat> beats)
        {
            // 입력 없음(이동): 10초간 이동 입력 없음
            beats.Add(TutorialBeat.Side("R-move", 0, TutorialSignal.IdleNoMove, "2-1", "8-1",
                    L("R-move-1", Hq, "거기 서서 무엇을 하십니까"),
                    L("R-move-2", Hq, "현장에서 멍하니 서 계시면 경비견의 먹잇감이 됩니다"))
                .AsRotating()
                .AsRepeatable(20f)
                .WithHintRecall());

            // 입력 없음(격벽): 4단계 터미널 진입 후 15초간 조작 없음
            beats.Add(TutorialBeat.Side("R-door", 0, TutorialSignal.IdleAtTerminal, "4-4", "4-8",
                    L("R-door-1", Hq, "붉은 문을 골라 더블클릭하시라고 말씀드렸습니다"),
                    L("R-door-2", Hq, "서두르십시오, 경비견이 돌아오기 전에 끝내야 합니다"))
                .AsRotating()
                .AsRepeatable(15f)
                .WithHintRecall());

            // 입력 없음(금고): 금고 앞 도착 후 10초간 조작 없음
            beats.Add(TutorialBeat.Side("R-vault", 0, TutorialSignal.IdleAtVault, null, "7-4",
                    L("R-vault-1", Hq, "금고 앞입니다. 시작하십시오"),
                    L("R-vault-2", Hq, "시간을 끌면 순찰이 한 바퀴 돌아옵니다"))
                .AsRotating()
                .AsRepeatable(12f)
                .WithHintRecall());

            // 추격 종료: Chase 에서 Investigate 로 복귀 시
            beats.Add(TutorialBeat.Side("R-chase-end", 0, TutorialSignal.ChaseEnded, "2-1", "8-1",
                    L("R-chase-end-1", Hq, "따돌린 것 같습니다. 하지만 방심하지 마십시오, 발자국을 따라올 겁니다"))
                .AsRotating()
                .AsRepeatable(10f)
                .WithHintRecall());

            // 체력 낮음: 체력 30% 이하
            beats.Add(TutorialBeat.Side("R-health", 0, TutorialSignal.HealthLow, "2-1", "8-1",
                    L("R-health-1", Hq, "상처가 깊습니다. 더는 버티지 못합니다"))
                .AsRotating()
                .AsRepeatable(20f)
                .WithHintRecall());

            // 사망 시: 떠 있는 대사를 끊고 바로 부른다.
            beats.Add(TutorialBeat.Side("R-death", 0, TutorialSignal.PlayerDied, "2-1", "8-1",
                    L("R-death-1", Hq, "...깜냥이. 응답하십시오. 깜냥이"))
                .AsUrgent()
                .AsRepeatable(3f));

            // 재시도 시: 사망 후 재시작 지점에서 재개
            beats.Add(TutorialBeat.Side("R-retry", 0, TutorialSignal.PlayerRespawned, "2-1", "8-1",
                    L("R-retry-1", Hq, "정신이 드셨습니까. 아까 그 구간부터 다시 가겠습니다"))
                .AsRotating()
                .AsRepeatable(3f)
                .WithHintRecall());
        }

        private static TutorialLine L(string id, TutorialSpeaker speaker, string text)
        {
            return new TutorialLine(id, speaker, text);
        }
    }
}

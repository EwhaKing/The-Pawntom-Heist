using System.Collections.Generic;

namespace Pawntom.Tutorial.Core
{
    /// <summary>
    /// 튜토리얼 대본(최종 수정본, docs/project/TUTORIAL_SCRIPT.md)을 비트로 옮긴 기본 대본.
    ///
    /// <para><b>표기</b></para>
    /// <list type="bullet">
    /// <item>키 안내 문구의 <c>[...]</c>는 강조 표시(키 칩)로 바뀐다.</item>
    /// <item><c>{Interact}</c>·<c>{TerminalExit}</c>는 실행 시 실제 키로 치환된다. 문서는 E·Esc 로 적었지만
    ///       입력 바인딩을 따라가야 안내가 거짓말을 하지 않는다.</item>
    /// </list>
    ///
    /// <para>대사 원문은 문서 그대로다. 수정은 문서를 먼저 고치고 여기를 맞춘다.</para>
    /// </summary>
    public static class TutorialScriptLibrary
    {
        private const TutorialSpeaker Hq = TutorialSpeaker.Command;
        private const TutorialSpeaker Cat = TutorialSpeaker.Kkamnyang;

        private const string SlotDropHint = "[1~4] 번호키 또는 [마우스 스크롤]로 슬롯 선택 후 [Q]키로 버리기";

        public static TutorialScript CreateDefault()
        {
            return new TutorialScript(CreateDefaultBeats());
        }

        public static List<TutorialBeat> CreateDefaultBeats()
        {
            List<TutorialBeat> beats = new List<TutorialBeat>(56);

            // ───────────── 1단계 오프닝 (본부) ─────────────
            beats.Add(TutorialBeat.Main("1-1", 1, TutorialSignal.EyesOpened,
                L("1-1", Hq, "칙... 들리십니까, 깜냥이 씨. 약속드린 대로 연결했습니다"),
                L("1-2", Cat, "들려요. 그럼 오늘부터 제가 현장을 맡는 거죠"),
                L("1-3", Hq, "그렇습니다. 말로 설명드릴 것은 이미 다 드렸으니, 이제 몸으로 익히시면 됩니다"),
                L("1-4", Cat, "그 전에 하나만요. 오늘 훔치는 게 정확히 뭐예요"),
                L("1-5", Hq, "전설의 캣닢입니다. 가장 안쪽 금고에 있습니다. 쓰임새는 나중에 말씀드리겠습니다"),
                L("1-6", Cat, "...알겠어요. 근데 솔직히 좀 떨려요. 현장은 처음이라서"),
                L("1-7", Hq, "누구에게나 처음은 있습니다. 저도 그랬고, 그래서 이렇게 하나씩 알려 드리는 겁니다"),
                L("1-8", Cat, "...네. 일단 믿고 가볼게요"),
                L("1-9", Hq, "좋습니다. 하나씩 안내해 드리겠습니다. 복도로 나가 보십시오")
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
                L("2-8", Hq, "숨이 차면 쉬십시오. 바닥나면 한동안 달리지도, 뛰지도 못합니다")));

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

            // ───────────── 3단계 분전반 해금 및 첫 마주침 (복도 B, 본부) ─────────────
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
                L("3-4", Cat, "으앗, 진짜 쫓아와요!"),
                L("3-5", Hq, "경비견입니다! 발자국을 따라왔습니다. 한 마리가 짖으면 나머지도 몰려옵니다. 본부로 돌아오십시오, 지금 당장")
                    .WithHint("[Shift]키로 달려 본부로 돌아갑시다", TutorialSignal.PlayerInMainbase))
                .AsSkippable());

            beats.Add(TutorialBeat.Side("3-6", 3, TutorialSignal.StaminaBelow30, "3-4", "4-1",
                L("3-6", Hq, "숨 관리하십시오! 바닥나면 끝입니다")
                    .WithTimedHint("[스태미나 바]를 확인합시다", 4f)));

            beats.Add(TutorialBeat.Main("3-7", 3, TutorialSignal.SafeInMainbase,
                L("3-7", Hq, "본부 안까지는 들어오지 못합니다. 숨을 돌리십시오"),
                L("3-8", Cat, "와, 진짜 발자국만 보고 쫓아오네. 생각보다 위험한데요"),
                L("3-9", Hq, "이제 아셨지요? 다음은 본부입니다. 단말기 앞으로 가십시오"))
                .AsSkippable()
                .RequiringPlayed("3-4"));

            // ───────────── 4단계 본부 터미널 접속, 격벽 조작, 미니게임 3종 안내 (본부 단말기) ─────────────
            beats.Add(TutorialBeat.Main("4-1", 4, TutorialSignal.NearTerminal,
                L("4-1", Hq, "그 단말기가 보안망의 입구입니다. 보안망은 현장에서만 열립니다. 제가 직접 갈 수 있었다면 벌써 갔겠지요")
                    .WithHint("[{Interact}]키를 눌러 단말기에 접속합시다", TutorialSignal.TerminalOpened),
                L("4-2", Cat, "본부 일도 제가 하라는 거네요"),
                L("4-3", Hq, "지금은 그렇습니다. 둘 다 익혀 두십시오. 나중에 쓸모가 있을 겁니다")));

            // 단말기는 처음부터 CCTV 화면으로 열린다 — 탭을 누르라는 안내 없이 접속하자마자 4-4~4-6 이 이어진다.
            beats.Add(TutorialBeat.Main("4-4", 4, TutorialSignal.TerminalOpened,
                L("4-4", Hq, "지금 보이는 것이 CCTV 화면입니다. 파란 점이 현재 위치, 붉은 점이 경비견입니다"),
                L("4-5", Cat, "아직 입구 근처에 있네요"),
                L("4-6", Hq, "경비견들이 돌아가기 전에 문을 하나 열어 두겠습니다. 격벽 탭에서 복도 A 끝의 붉은 문을 고르십시오. 제가 말씀드리기 전에는 다른 문을 건드리지 마십시오")
                    .WithHint("[격벽] 탭에서 붉은 격벽을 클릭합시다", TutorialSignal.DoorHackStarted))
                .AsSkippable());

            beats.Add(TutorialBeat.Main("4-7", 4, TutorialSignal.DoorHackStarted,
                L("4-7", Hq, "해킹 미니게임은 세 가지입니다. 방향키 순서 입력, 두 키 번갈아 누르기, 타이밍 맞추기. 그중 하나가 무작위로 걸리고 제한 시간이 있으니, 화면의 지시를 정확히 따르십시오"))
                .AsSkippable());

            // 4-8~4-10 [택1]: 해킹 시작 직후 수집기가 걸린 종류의 신호를 한 번 보낸다. 시도마다 걸린 종류에 맞는 안내가 나온다.
            beats.Add(TutorialBeat.Side("4-8", 4, TutorialSignal.DoorGameCommand, "4-7", "4-11",
                    L("4-8", Hq, "화면에 뜬 순서를 그대로 입력하십시오. 틀리면 처음부터이니 침착하게 하십시오")
                        .WithHint("[방향키]로 순서를 입력합시다 ([Esc]키로 취소)",
                            TutorialSignal.DoorHackSucceeded, TutorialSignal.DoorHackFailed))
                .AsRepeatable(2f)
                .WithConfirm());

            beats.Add(TutorialBeat.Side("4-9", 4, TutorialSignal.DoorGameSignal, "4-7", "4-11",
                    L("4-9", Hq, "화면에 표시된 두 키를 번갈아 누르십시오. 같은 키를 연달아 눌러도 소용없고, 손을 멈추면 게이지가 줄어듭니다")
                        .WithHint("[화면에 표시된 두 키]를 번갈아 눌러 게이지를 채웁시다",
                            TutorialSignal.DoorHackSucceeded, TutorialSignal.DoorHackFailed))
                .AsRepeatable(2f)
                .WithConfirm());

            beats.Add(TutorialBeat.Side("4-10", 4, TutorialSignal.DoorGameSync, "4-7", "4-11",
                    L("4-10", Hq, "돌아가는 바늘이 색칠된 구간에 들어오는 순간을 노리십시오. 기회는 세 번뿐입니다")
                        .WithHint("[Space]키로 타이밍을 맞춰봅시다",
                            TutorialSignal.DoorHackSucceeded, TutorialSignal.DoorHackFailed))
                .AsRepeatable(2f)
                .WithConfirm());

            beats.Add(TutorialBeat.Main("4-11", 4, TutorialSignal.DoorHackSucceeded,
                L("4-11", Hq, "좋습니다, 푸른색이 됐습니다. 문이 닫혀 있는 동안 저 안에는 경비견이 들어오지 못합니다. 쓸 만한 은신처 겸 창고입니다"),
                L("4-12", Hq, "해금된 격벽은 이 단말기에서만 여닫을 수 있습니다. 문 앞에서는 열 수 없으니, 지금 푸른 격벽을 더블클릭해서 열어 두십시오")
                    .WithHint("[격벽] 탭에서 푸른 격벽을 더블클릭해 엽시다", TutorialSignal.DoorOpened)));

            beats.Add(TutorialBeat.Main("4-13", 4, TutorialSignal.DoorOpened,
                L("4-13", Hq, "열렸습니다. 이제 나와서 문 쪽으로 가십시오. 열린 문으로는 경비견도 들어올 수 있으니, 용무가 끝나면 이 단말기에서 다시 닫으십시오")
                    .WithHint("[{TerminalExit}]키를 눌러 단말기에서 나옵시다", TutorialSignal.TerminalClosed)));

            beats.Add(TutorialBeat.Side("4-14", 4, TutorialSignal.DoorHackFailed, "4-7", "4-11",
                    L("4-14", Hq, "소리가 났습니다. 경비견이 문 쪽으로 쏠리고 있습니다. 잠시 쉬었다가 다시 시도하십시오")
                        .WithTimedHint("3초 뒤 다시 시도합시다", 3f, TutorialSignal.DoorHackStarted))
                .AsRepeatable(1f));

            // ───────────── 5단계 아이템 획득, 무게 감속 고지, 아이템 버리기, 열쇠 설명 (전리품 방) ─────────────
            // 5-1 은 문이 열려 있을 때만 나온다(수집기가 열린 문 앞 = BlueDoorAhead, 닫힌 해금 문 앞 = BlueDoorClosedAhead 로 나눈다).
            beats.Add(TutorialBeat.Main("5-1", 5, TutorialSignal.BlueDoorAhead,
                L("5-1", Hq, "열어 둔 푸른 문입니다. 경비견이 따라붙기 전에 바로 들어가십시오")
                    .WithHint("[Shift]키로 달려 열린 문 안으로 들어갑시다", TutorialSignal.LootRoomEntered))
                .AsSkippable());

            beats.Add(TutorialBeat.Side("5-1b", 5, TutorialSignal.BlueDoorClosedAhead, "4-11", "5-2",
                    L("5-1b", Hq, "문이 닫혀 있습니다. 문 앞에서는 열 수 없으니, 본부 단말기로 돌아가 푸른 격벽을 더블클릭해서 여십시오")
                        .WithHint("본부 [단말기]에서 푸른 격벽을 더블클릭해 엽시다", TutorialSignal.DoorOpened))
                .AsRepeatable(15f));

            beats.Add(TutorialBeat.Main("5-2", 5, TutorialSignal.LootRoomEntered,
                L("5-2", Hq, "전리품 창고입니다. 눈에 띄는 대로 주워 담으십시오")
                    .WithHint("[{Interact}]키로 전리품을 주워봅시다", TutorialSignal.ItemLooted))
                .AsSkippable());

            beats.Add(TutorialBeat.Main("5-3", 5, TutorialSignal.ItemLooted,
                L("5-3", Hq, "걸음이 느려진 게 느껴지십니까? 무게를 알려 주는 표시는 따로 없습니다. 몸이 느끼는 속도로 가방 상태를 판단하셔야 합니다"),
                L("5-4", Cat, "몸이 바로 무거워지는 게 확 느껴지네요..."))
                // 열쇠는 전리품으로 세지 않으므로, 전리품을 하나도 안 줍고 열쇠부터 챙겨도 이후 단계가 막히지 않게 한다.
                .AsSkippable());

            beats.Add(TutorialBeat.Main("5-5", 5, TutorialSignal.InventoryThreeFilled,
                L("5-5", Hq, "무거워진 만큼 이동 속도가 떨어집니다. 가방은 네 칸이 전부이고, 곧 챙기실 중요한 물건이 있으니 한 칸은 비워 두십시오. 필요 없는 물건은 즉시 버리십시오")
                    .WithHint(SlotDropHint, TutorialSignal.ItemDropped))
                .AsSkippable());

            beats.Add(TutorialBeat.Side("5-6", 5, TutorialSignal.InventoryFullRejected, "5-3", "6-1",
                L("5-6", Cat, "가방이 꽉 차서 더는 못 들겠어요!"),
                L("5-6b", Hq, "그렇다면 필요 없는 것을 버려서 칸을 비우십시오")
                    .WithHint(SlotDropHint, TutorialSignal.ItemDropped)));

            beats.Add(TutorialBeat.Main("5-7", 5, TutorialSignal.NearKey,
                L("5-7", Cat, "방 한가운데 반짝이는 열쇠가 있어요!"),
                L("5-8", Hq, "그게 바로 금고를 열 마스터 열쇠입니다. 반드시 챙기십시오")
                    .WithHint("[{Interact}]키로 열쇠를 획득합시다", TutorialSignal.KeyAcquired))
                .AsSkippable());

            beats.Add(TutorialBeat.Main("5-9", 5, TutorialSignal.KeyAcquired,
                L("5-9", Hq, "열쇠는 금고 앞에서 한 번 쓰면 사라집니다. 버리거나 잃어버리면 금고를 열 수 없으니 가방에서 꼭 지키십시오"))
                .AsSkippable());

            beats.Add(TutorialBeat.Main("5-10", 5, TutorialSignal.LootRoomExiting,
                L("5-10", Hq, "열쇠를 챙겼으니 이제 금고 복도로 이동하십시오. 욕심 부려 짐을 많이 들수록 경비견을 피하기 어렵습니다"))
                .AsSkippable()
                .RequiringPlayed("5-9"));

            // ───────────── 6단계 적 NPC 순찰 안내 (금고 복도) ─────────────
            beats.Add(TutorialBeat.Main("6-1", 6, TutorialSignal.VaultCorridorEntered,
                L("6-1", Hq, "금고방 주변 복도입니다. 경비견 NPC들이 정해진 경로를 따라 순찰하고 있습니다"),
                L("6-2", Cat, "벽 뒤로 경비견들이 돌아다니는 게 보여요"),
                L("6-3", Hq, "순찰 동선을 잘 살피십시오. 경비견들의 시선과 발자국 추적을 피해 타이밍을 맞춰 가야 합니다")
                    .WithPreDelay(3f),
                L("6-4", Hq, "한 번 눈에 띄면 끝까지 쫓아옵니다. 틈이 보일 때 한 번에 지나가십시오. 쫓기면 본부로 피하십시오. 본부 안까지는 들어오지 못합니다"))
                .AsSkippable());

            beats.Add(TutorialBeat.Side("6-5", 6, TutorialSignal.EnemyChasing, "6-1", "7-8",
                L("6-5", Hq, "들켰습니다! 짐이 무거워도 달리십시오!")
                    .WithHint("[Shift]키로 도망칩시다", TutorialSignal.SprintStarted)));

            // ───────────── 7단계 열쇠 사용, 주의사항, 금고 미니게임, 긴급 탈출 (금고방) ─────────────
            // 금고 미니게임은 Space 타이밍(DigitalSync)으로 고정이다.
            beats.Add(TutorialBeat.Main("7-1", 7, TutorialSignal.VaultFront,
                L("7-1", Hq, "금고 앞입니다. 금고를 바라본 채, 가방에서 아까 얻은 열쇠 칸을 골라 손에 들고 사용하십시오")
                    .WithHint("[1~4]키로 열쇠 슬롯을 선택하고 금고를 본 채 [{Interact}]키로 사용", TutorialSignal.VaultUnlocked))
                .AsSkippable());

            // 열쇠는 잠금만 푼다. 해킹은 같은 키를 한 번 더 눌러야 시작하므로 그 안내를 7-2 에 붙인다.
            beats.Add(TutorialBeat.Main("7-2", 7, TutorialSignal.VaultUnlocked,
                L("7-2", Hq, "열쇠가 맞았습니다. 이제 한 번 더 상호작용해서 금고 해킹을 시작하십시오. 놓치면 소리가 나서 순찰견이 눈치챌 수 있으니 주의하십시오")
                    .WithHint("[{Interact}]키로 금고 해킹을 시작합시다", TutorialSignal.VaultHackStarted),
                L("7-3", Cat, "...숨 한 번 고르고 시작할게요"))
                .AsSkippable());

            beats.Add(TutorialBeat.Main("7-4", 7, TutorialSignal.VaultHackStarted,
                L("7-4", Hq, "집중하십시오. 바늘이 색칠된 구간에 들어오는 순간에 눌러야 하고, 기회는 세 번뿐입니다")
                    .WithHint("[Space]키로 타이밍을 맞춰봅시다",
                        TutorialSignal.VaultHackSucceeded, TutorialSignal.VaultHackFailed))
                .AsSkippable()
                .AsAnchor());

            beats.Add(TutorialBeat.Side("7-5", 7, TutorialSignal.VaultHackFailed, "7-4", "7-8",
                L("7-5", Hq, "실패했습니다! 소리가 새어 나갔을지 모릅니다. 서둘러 다시 시도하십시오!"))
                .AsRepeatable(1f));

            // 금고가 부서지고 캣닢이 바닥에 생성되며 사이렌이 시작된다. 캣닢은 직접 주워야 한다.
            beats.Add(TutorialBeat.Main("7-6", 7, TutorialSignal.VaultHackSucceeded,
                L("7-6", Hq, "금고가 열렸습니다! 안에서 캣닢이 나왔습니다. 어서 집으십시오")
                    .WithHint("[{Interact}]키로 캣닢을 주워봅시다", TutorialSignal.CatnipAcquired))
                .AsSkippable());

            beats.Add(TutorialBeat.Side("7-7", 7, TutorialSignal.InventoryFullRejected, "7-6", "7-8",
                    L("7-7", Hq, "가방이 가득입니다! 필요 없는 것을 버려서 캣닢 자리를 만드십시오")
                        .WithHint(SlotDropHint, TutorialSignal.ItemDropped))
                .AsRepeatable(5f)
                .WithHintRecall());

            beats.Add(TutorialBeat.Main("7-8", 7, TutorialSignal.CatnipAcquired,
                L("7-8", Cat, "...이게 전설의 캣닢!"),
                L("7-9", Hq, "...드디어 잡았군요! 하지만 방심하지 마십시오. 캣닢 냄새로 인해 긴급 탈출 상황이 발동되었습니다!"),
                L("7-10", Hq, "경비견들이 금고방으로 몰려들고 있습니다. 발자국이 계속 생기고 가방도 더욱 무거워질 테니 앞만 보고 달리십시오!")
                    .WithTimedHint("[긴급 탈출] 본부로 탈출합시다", 5f),
                L("7-11", Hq, "캣닢을 들고 본부로 돌아오십시오. 지금 당장입니다!")
                    .WithHint("[Shift]키로 달려 본부로 탈출합시다", TutorialSignal.PlayerInMainbase),
                L("7-12", Cat, "으아, 소리가 장난 아니에요! 당장 본부로 튈게요!"))
                .AsAnchor());

            // 정산은 대사가 끝난다고 바로 뜨지 않는다. 본부 안에서 출발 버튼과 상호작용하면(탈출 성공) 화면이 까맣게
            // 페이드된 뒤 뜬다(TutorialDirector 가 결과 상태를 받아 연다). 그래서 출발 버튼을 누르라는 안내를 붙인다.
            beats.Add(TutorialBeat.Main("7-13", 7, TutorialSignal.PlayerInMainbase,
                L("7-13", Hq, "...됐습니다. 무사히 들어왔군요. 잘하셨습니다")
                    .WithHint("[{Interact}]키로 출발 버튼을 눌러 탈출합시다", TutorialSignal.SettlementShown))
                .RequiringPlayed("7-8"));

            // ───────────── 8단계 정산 및 엔딩 (본부) ─────────────
            beats.Add(TutorialBeat.Main("8-1", 8, TutorialSignal.SettlementShown,
                L("8-1", Hq, "정산입니다. 가져오신 캣닢과 전리품만큼 값을 쳐 드리겠습니다")
                    .WithAction(TutorialLineAction.RevealSettlement)));

            beats.Add(TutorialBeat.Main("8-2", 8, TutorialSignal.SettlementRevealed,
                L("8-2", Cat, "저기, 이름이라도 알려주면 안 돼요? 이만큼 잘 아시는 걸 보면 분명 대단한 괴도 선배일 텐데"),
                L("8-3", Hq, "...다음에 말씀드리겠습니다"),
                L("8-4", Cat, "항상 그렇게 넘기시네요"),
                L("8-5", Hq, "지금은 팀부터 꾸리십시오. 현장과 본부를 나눠 맡을 동료가 필요합니다"),
                L("8-6", Cat, "...알겠어요. 다음엔 꼭 근사한 팀원들과 같이 올게요"),
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

            // 입력 없음(격벽 해킹): 단말기를 연 채 15초간 조작 없음 (4-4 이후 ~ 해킹 성공 전)
            beats.Add(TutorialBeat.Side("R-door", 0, TutorialSignal.IdleAtTerminal, "4-4", "4-11",
                    L("R-door-1", Hq, "붉은 문을 골라 클릭하시라고 말씀드렸습니다"),
                    L("R-door-2", Hq, "서두르십시오, 경비견이 돌아오기 전에 끝내야 합니다"))
                .AsRotating()
                .AsRepeatable(15f)
                .WithHintRecall());

            // 입력 없음(격벽 열기): 해킹 성공 뒤 단말기를 연 채 15초간 조작 없음 (4-11 이후 ~ 격벽이 열리기 전)
            beats.Add(TutorialBeat.Side("R-gate", 0, TutorialSignal.IdleAtTerminal, "4-11", "4-13",
                    L("R-gate-1", Hq, "문이 푸른색이 됐습니다. 이제 그 푸른 격벽을 더블클릭해서 여십시오"),
                    L("R-gate-2", Hq, "열어 두지 않으면 문 앞에서는 들어갈 수 없습니다"))
                .AsRotating()
                .AsRepeatable(15f)
                .WithHintRecall());

            // 입력 없음(금고): 금고 앞 도착 후 10초간 해킹을 시작하지 않음 (캣닢 획득 전까지)
            beats.Add(TutorialBeat.Side("R-vault", 0, TutorialSignal.IdleAtVault, null, "7-8",
                    L("R-vault-1", Hq, "금고 앞입니다. 열쇠를 사용한 뒤, 한 번 더 눌러 해킹을 시작하십시오"),
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

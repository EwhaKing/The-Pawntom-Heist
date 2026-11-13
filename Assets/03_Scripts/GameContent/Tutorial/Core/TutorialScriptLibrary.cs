using System.Collections.Generic;

namespace Pawntom.Tutorial.Core
{
    /// <summary>
    /// 튜토리얼 대본(최종 수정본)을 비트로 옮긴 기본 대본.
    ///
    /// <para><b>표기</b></para>
    /// <list type="bullet">
    /// <item>키 안내 문구의 <c>[...]</c>는 강조 표시(키 칩)로 바뀐다.</item>
    /// <item><c>{Interact}</c>·<c>{TerminalExit}</c>는 실행 시 실제 키로 치환된다. 문서는 E·Esc 로 적었지만
    ///       입력 바인딩을 따라가야 안내가 거짓말을 하지 않는다.</item>
    /// </list>
    ///
    /// <para>
    /// 대사 원문은 docs/project/TUTORIAL_SCRIPT.md 그대로다.
    /// 수정은 문서를 먼저 고치고 여기를 맞춘다. 인물·말투 기준은 docs/project/TUTORIAL_HQ_CHARACTER.md.
    /// </para>
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
            List<TutorialBeat> beats = new List<TutorialBeat>(60);

            // ───────────── 1단계 오프닝 (본부) ─────────────
            beats.Add(TutorialBeat.Main("1-1", 1, TutorialSignal.EyesOpened,
                L("1-1", Hq, "... 아, 들리십니까, 후배님? 도착했으니 이제 일어나셔야죠."),
                L("1-2", Cat, "... 들려요! 아, 안 잤어요."),
                L("1-3", Hq, "흠흠... 아기 고양이들은 하루에 19시간 잔다고 하니 너무 부끄러워하지 않으셔도 됩니다. 어쨌든, 말로 할 설명은 이미 다 드렸으니, 실전을 통해 부딪혀 보셔야겠지요?"),
                L("1-4", Cat, "전 이제 아기가 아니에요! ... 아, 아무튼, 그 전에 하나만요. 오늘 훔치는 게 정확히 뭐예요?"),
                L("1-5", Hq, "'{{전설의 캣닢}}'이라는 귀중품입니다. 가장 안쪽 금고에 있고요. 그걸 챙겨 본부로 돌아와 {{출발 버튼}}을 누르면 임무가 끝납니다. 쓰임새는 차차 말씀드리겠습니다. ... 많이 떨리시나 봅니다?"),
                L("1-6", Cat, "...알겠어요. 안 떨려요. 진짜예요. ...조금 떨리긴 하는데, 그, 제가 현장은 처음이라서요..."),
                L("1-7", Hq, "처음은 누구나 떨리는 법입니다. 저도 그랬습니다. 그래서 하나씩 짚어 드리는 것이고요"),
                L("1-8", Cat, "...네. 일단 믿고 가볼게요. 걱정 마세요, 저 이래 봬도 꽤 의젓하거든요"),
                L("1-9", Hq, "좋습니다. 그럼 시작하지요. 복도로 나가 보시겠습니까?")
                    .WithAction(TutorialLineAction.EnableControl)));

            // ───────────── 2단계 기본 조작 (본부, 복도 A) ─────────────
            beats.Add(TutorialBeat.Main("2-1", 2, TutorialSignal.None,
                L("2-1", Hq, "먼저 앞으로 걸어 봅시다. 저 복도 보이죠? 저기로 가면 된답니다.")
                    .WithHint("[WASD]로 이동하고 [마우스]로 둘러봅시다", TutorialSignal.MovedAndLooked)));

            // 순서: 달리기(2-2) → 뒤를 돌아보기(2-3) → 발자국 설명(2-4~2-7). 달린 뒤에 돌아봐야 커진 발자국이 보인다.
            beats.Add(TutorialBeat.Main("2-2", 2, TutorialSignal.FirstFootprint,
                L("2-2", Hq, "저런, 긴장이 많이 됐습니까? 잠입하는 폼이 아직 어색하네요. 몸도 풀 겸 가볍게 달려 볼까요?")
                    .WithHint("[Shift]키를 누르고 달려봅시다", TutorialSignal.SprintStarted)));

            beats.Add(TutorialBeat.Main("2-3", 2, TutorialSignal.SprintStarted,
                L("2-3", Hq, "자, 한 번 뒤를 돌아보시겠습니까? 지나온 자리에 뭐가 남았는지 봅시다.")
                    .WithHint("[마우스]로 뒤를 돌아봅시다", TutorialSignal.LookedBack)));

            beats.Add(TutorialBeat.Main("2-4", 2, TutorialSignal.LookedBack,
                L("2-4", Cat, "어, 발자국이다! ...아니, 발자국이네요. 걸을 때보다 훨씬 커졌어요"),
                L("2-5a", Hq, "지나간 길에는 다 {{발자국}}이 남습니다. 빠르게 달릴수록 더 눈에 띄지요. 경비견 그것들은 저걸 따라오고요."),
                L("2-5b", Hq, "시간이 조금 지나면 사라지니까, 너무 걱정은 마세요. 처음에는 저걸 의식하면서 다니는 게 조금 어렵긴 하겠습니다만ㅡ 은밀함은 괴도의 소양이니 조심하셔야겠지요?"),
                L("2-6", Cat, "...선배님도 현장에 계셨던 거예요?"),
                L("2-7", Hq, "네, 뭐... 재미없는 이야기입니다. 그보다 달리면 숨이 차니, 남은 기력도 틈틈이 봐 두시는 게 좋습니다")
                    .WithTimedHint("[스태미나 바]를 확인합시다", 5f, TutorialSignal.StaminaBelowHalf))
                .WithFreshTrigger());

            beats.Add(TutorialBeat.Side("2-8", 2, TutorialSignal.StaminaBelowHalf, "2-4", "3-1",
                L("2-8", Hq, "숨이 차면 쉬어야 합니다. 어린 고양이는 금방 지치는 법이니까요. {{기력}}이 바닥나면 한동안 달리지도, 뛰지도 못하게 되니 잘 조절하면서 뛰셔야겠지요?")));

            beats.Add(TutorialBeat.Main("2-9", 2, TutorialSignal.ObstacleAhead,
                // 안내는 상자 반대편(ObstacleCleared 영역)에 도착해야 사라진다 — 제자리 점프로는 지워지지 않는다.
                L("2-9", Hq, "아, 앞에 장애물이 있네요. 앞의 상자를 {{뛰어넘어}} 보십시오")
                    .WithHint("[Space]키로 점프해봅시다", TutorialSignal.ObstacleCleared)));

            // 파쿠르(벽 타기)가 없는 이유: 아직 어린 고양이라서. 상자를 넘은 직후에 한 번 짚는다.
            beats.Add(TutorialBeat.Main("2-9b", 2, TutorialSignal.ObstacleCleared,
                L("2-9b", Hq, "점프는 잘 하시는군요. 좋습니다. 벽을 타거나 높은 데 오르는 건 더 크고 나서 도전해보세요. 어린 고양이들은 나무에 올라갔다가 못 내려와서 내려달라고 꺼이꺼이 한 번씩은 울어봤다는 게 관례라는 말도 있잖습니까."),
                L("2-9c", Cat, "저 그런 적 없거든요! ...한 번밖에요"))
                .AsSkippable());

            beats.Add(TutorialBeat.Main("2-10", 2, TutorialSignal.CorridorAEndReached,
                L("2-10a", Hq, "복도 끝의 이 격벽 너머에 틈틈이 챙길 물건들이 있을 겁니다. 기껏 박물관에 왔는데 빈손으로 돌아가긴 아쉽잖아요?"),
                L("2-10b", Cat, "... 어, 그건 도둑질이잖아요!"),
                L("2-10c", Hq, "괴도 일도 다 돈이 있어야 하는 겁니다. 냉정한 소리 같겠지만, 뭐, 자본주의 사회란 게 원래 그런 걸요."),
                L("2-10d", Hq, "어쨌거나, 힘이 아주 센 동료가 있다면 모를까, 이 철문은 힘으로는 안 열릴겁니다. 그래도 들어가봅시다."),
                L("2-11", Cat, "근데 지금은 닫혀 있잖아요?"),
                L("2-12", Hq, "그래요. 하지만 본부 컴퓨터로 해킹해서 보안망을 연결하면, 문을 열 수 있습니다."),
                L("2-13", Hq, "그 전에... 여기 벽에 붙은 {{분전반}}에 다가가 보세요.")));

            // ───────────── 3단계 분전반 해금 및 첫 마주침 (복도 A 끝 열쇠방 격벽 옆, 본부) ─────────────
            // 3-1·3-3 은 건너뛸 수 없다 — 분전반 접속 없이 단말기(4단계)로 넘어가지 않는다.
            // (분전반이 씬에 없으면 여기서 멈춘다. 디버그 F10 으로 강제 진행할 수 있다.)
            // 3-0: 2단계를 마치기 전에 분전반부터 열어 버린 경우. 3-1 접속 안내·3-3 연결 대사 대신 나온다.
            // 평소에는 3-1(분전반 근처) 신호가 먼저 와서 건너뛰어진다.
            beats.Add(TutorialBeat.Main("3-0", 3, TutorialSignal.SecurityNodeConnected,
                L("3-0", Hq, "오, 분전반을 벌써 여셨군요. 부지런하십니다, 후배님. 금고 쪽 구역도 이미 잡히니, 바로 본부로 돌아와 단말기 앞으로 가시면 됩니다."))
                .AsSkippable());

            beats.Add(TutorialBeat.Main("3-1", 3, TutorialSignal.NearSecurityNode,
                L("3-1", Hq, "벽 오른쪽의 저 네모난 상자가 분전반입니다. 저기에 손을 대고 있으면 보안망이 살아나서 금고 쪽 구역도 잡힐 겁니다. 이어 두는 동안은 꼼짝 못 하니, 주변부터 잘 살펴 두시고요.")
                    .WithHint("[{Interact}]키를 길게 눌러 분전반에 접속합시다",
                        TutorialSignal.SecurityNodeCastStarted, TutorialSignal.SecurityNodeConnected))
                .SkippingIfPlayed("3-0"));

            // 3-2 "눌렀어요!"는 분전반을 100% 채워 연결이 끝난 뒤에 나온다(SecurityNodeConnected = 진행도 1.0).
            // 접속을 시작만 한 시점이 아니라 끝난 시점이므로 바로 이어서 3-3 이 말한다. 한 비트로 묶는다.
            beats.Add(TutorialBeat.Main("3-3", 3, TutorialSignal.SecurityNodeConnected,
                L("3-2", Cat, "... 눌렀어요! 이제 열린 건가요?"),
                L("3-3", Hq, "네, 잘 연결됐습니다. 이제 본부로 돌아오시죠. 컴퓨터로 문을 마저 열어야 하니까요."))
                .SkippingIfPlayed("3-0"));

            // 첫 추격 대사(3-4~3-8)는 진행 순서에 묶지 않는다. 어느 장소에서든 처음 쫓겼다가 따돌렸을 때(ChaseEscaped) 한 번만 나온다.
            // 금고 캣닢 경보(7-8) 이후의 긴급 탈출은 이 대사의 대상이 아니라 그 전에 닫는다.
            beats.Add(TutorialBeat.Side("3-4", 3, TutorialSignal.ChaseEscaped, "2-1", "7-8",
                L("3-4", Cat, "으아, 겨우 따돌렸어요! 진짜 발자국만 보고 쫓아오네요. ... 하나도 안 무서웠지만요"),
                L("3-5", Hq, "경비견입니다. 발자국을 따라오고, 한 마리가 짖으면 나머지도 몰려옵니다"),
                L("3-5b", Hq, "맞서 싸울 생각은 접어 두시는 게 좋겠습니다. 아기 발톱으로는 간지럽지도 않을 테니"),
                L("3-5c", Cat, "저 아기 아니거든요! ...아직 좀 작을 뿐이에요"),
                L("3-7", Hq, "본부 안까지는 못 들어오니까, 들키면 이리로 피하시면 됩니다"),
                L("3-8", Cat, "...조, 조금 위험하긴 하네요. 조심할게요. 조금만요")));

            beats.Add(TutorialBeat.Side("3-6", 3, TutorialSignal.StaminaBelow30, "2-1", "4-1",
                L("3-6", Hq, "슬슬 숨이 턱 끝까지 찰 것 같습니다만, 조금 쉬었다 가세요.")
                    .WithTimedHint("[스태미나 바]를 확인합시다", 4f)));

            // ───────────── 4단계 본부 터미널 접속, 격벽 조작, 미니게임 3종 안내 (본부 단말기) ─────────────
            beats.Add(TutorialBeat.Main("4-1", 4, TutorialSignal.NearTerminal,
                L("4-1", Hq, "이 기다란 컴퓨터가 보안망의 입구입니다. 보안망은 현장에서 분전반을 이어야 열립니다.")
                    .WithHint("[{Interact}]키를 눌러 단말기에 접속합시다", TutorialSignal.TerminalOpened),
                L("4-2", Cat, "본부 일도 제가 하는 거군요. 저 현장 담당인 줄 알았는데... 선배님이 열어주시는 게 아니었나요?"),
                L("4-3", Hq, "제가 직접 갈 수 있었다면 그랬겠지요. 지금처럼 도와주는 동료가 없을 때를 대비해, 이것도 익혀 두시는 게 좋습니다. 해킹 없이는 물건 구경도 못 할 테니까.")));

            // 단말기는 처음부터 CCTV 화면으로 열린다 — 탭을 누르라는 안내 없이 접속하자마자 4-4~4-6 이 이어진다.
            beats.Add(TutorialBeat.Main("4-4", 4, TutorialSignal.TerminalOpened,
                L("4-4", Hq, "지금 보이는 게 CCTV 화면입니다. 여기 작은 파란 점이 후배님이고, 저 붉은 점이 경비견이지요. 나중에 팀원들을 구하게 되면, 팀원들도 파란색 점으로 표시될 겁니다."),
                L("4-5", Cat, "개들이 금고 근처를 돌고 있는 것 같네요..."),
                L("4-6", Hq, "우리 후배님을 발견하기 전까진, 저놈들은 계속 저렇게 순찰을 다닐 겁니다."),
                L("4-6b", Hq, "자, 아까 봤던 철문들을 열어 볼까요. 격벽 탭에서 위쪽 복도의 {{붉은색 철문}} 표시를 골라 보시겠습니까?")
                    .WithHint("[격벽] 탭에서 붉은 격벽을 클릭합시다", TutorialSignal.DoorHackStarted)));

            // 4-6a: 위쪽 복도의 문이 아니라 금고 쪽 문을 먼저 골랐을 때. 해킹은 그대로 진행된다(막지 않는다).
            beats.Add(TutorialBeat.Side("4-6a", 4, TutorialSignal.WrongDoorHackStarted, "4-4", "4-11",
                L("4-6a", Hq, "그래요, 지금은 순서대로 안 해도 됩니다. 이맘때쯤의 아기 고양이들은 이렇게 하라고 하면 저렇게 하고 싶어하는 경향이 크더라니.")));

            beats.Add(TutorialBeat.Main("4-7", 4, TutorialSignal.DoorHackStarted,
                L("4-7a", Hq, "자, 화면에 무언가 떴을 겁니다. 여기서 명령어를 하나하나 입력하면서 해킹하면 한세월 걸리겠지요?"),
                L("4-7b", Hq, "그래서 이 컴퓨터가 해킹 과정을 미니게임으로 변경해서 보여줍니다. 우리 후배님은 그걸 깨기만 하면 되는 겁니다. 편리하지요."),
                L("4-7c", Hq, "미니게임은 세 종류가 있는데, 그 중 하나가 무작위로 걸립니다.")));

            // 4-8~4-10 [택1]: 해킹 시작 직후 수집기가 걸린 종류의 신호를 한 번 보낸다. 시도마다 걸린 종류에 맞는 안내가 나온다.
            beats.Add(TutorialBeat.Side("4-8", 4, TutorialSignal.DoorGameCommand, "4-7", "4-11",
                    L("4-8", Hq, "화면에 뜬 순서를 그대로 누르시면 됩니다. 틀리면 처음부터이니 침착하게요.")
                        .WithHint("[방향키]로 순서를 입력합시다 ([Esc]키로 취소)",
                            TutorialSignal.DoorHackSucceeded, TutorialSignal.DoorHackFailed))
                .AsRepeatable(2f)
                .WithConfirm());

            beats.Add(TutorialBeat.Side("4-9", 4, TutorialSignal.DoorGameSignal, "4-7", "4-11",
                    L("4-9", Hq, "화면에 표시된 두 키를 번갈아 누르세요. 같은 키를 연달아 누르거나, 누르다가 멈추면 해킹 진행도 게이지가 줄어듭니다.")
                        .WithHint("[화면에 표시된 두 키]를 번갈아 눌러 게이지를 채웁시다",
                            TutorialSignal.DoorHackSucceeded, TutorialSignal.DoorHackFailed))
                .AsRepeatable(2f)
                .WithConfirm());

            beats.Add(TutorialBeat.Side("4-10", 4, TutorialSignal.DoorGameSync, "4-7", "4-11",
                    L("4-10", Hq, "자, 집중하세요. 돌아가는 바늘이 색칠된 구간에 들어오는 완벽한 순간을 노리면 됩니다.")
                        .WithHint("[Space]키로 타이밍을 맞춰봅시다",
                            TutorialSignal.DoorHackSucceeded, TutorialSignal.DoorHackFailed))
                .AsRepeatable(2f)
                .WithConfirm());

            beats.Add(TutorialBeat.Main("4-11", 4, TutorialSignal.DoorHackSucceeded,
                L("4-11a", Hq, "좋습니다, {{푸른색}}이 됐지요? 저 표시는 이제 저 철문을 마음껏 여닫을 수 있다는 표시입니다."),
                L("4-11b", Hq, "철문이 닫혀 있는 동안은 저 안에 경비견이 못 들어오니, 쫓기고 있다면 본부에 있는 친구에게 문을 닫아달라고 해도 좋을 것 같습니다."),
                L("4-12", Hq, "해금된 철문은 이 단말기에서만 열고 닫을 수 있습니다. 철문 바로 앞에서는 못 여니까, 지금 이 파란색 철문을 {{미리 열어}} 두십시오")
                    .WithHint("[격벽] 탭에서 푸른 격벽을 더블클릭해 엽시다", TutorialSignal.DoorOpened)));

            // 4-13: 첫 문이 열리면 나머지(금고 쪽) 문도 미리 해금하게 한다. 해금하지 않고 나가도 막지 않는다(4-14 는 건너뛸 수 있다).
            beats.Add(TutorialBeat.Main("4-13", 4, TutorialSignal.DoorOpened,
                L("4-13", Hq, "열렸습니다. 이제 나머지 철문도 미리 해제해볼까요?")
                    .WithHint("[격벽] 탭에서 나머지 붉은 격벽을 클릭합시다",
                        TutorialSignal.DoorHackStarted, TutorialSignal.TerminalClosed)));

            // 4-14: 두 문이 모두 해금된 뒤. 이정표 신호(DoorHackSucceeded)가 아니라 AllDoorsUnlocked 를 쓴다 —
            // 첫 해킹 성공이 이정표로 남아 4-13 을 건너뛰고 이 비트가 먼저 열리는 것을 막는다.
            // 4-13b: 둘째 문(캣닢 쪽 등)도 해금하면 이것도 더블클릭해 열어 두라고 알린다. 안 열고 나가면 문 앞에서 막히기 때문.
            beats.Add(TutorialBeat.Main("4-13b", 4, TutorialSignal.AllDoorsUnlocked,
                    L("4-13b", Hq, "이쪽 철문도 푸른색이 됐군요. 이것도 더블클릭해서 미리 열어 두십시오. 닫힌 채로는 문 앞에서 열 수 없습니다.")
                        .WithHint("[격벽] 탭에서 푸른 격벽을 더블클릭해 엽시다", TutorialSignal.AllDoorsOpened, TutorialSignal.TerminalClosed))
                .WithFreshTrigger()
                .AsSkippable());

            beats.Add(TutorialBeat.Main("4-14", 4, TutorialSignal.AllDoorsOpened,
                L("4-14", Hq,"나와서 아까 막혀있던 철문 쪽으로 가 봅시다. 열린 철문 사이로는 경비견도 들어올 수 있으니, 그 점 유의하셔서 다니면 되겠습니다.")
                    .WithHint("[{TerminalExit}]키를 눌러 단말기에서 나옵시다", TutorialSignal.TerminalClosed))
                .WithFreshTrigger()
                .AsSkippable());

            // 해킹 실패: 첫 실패는 4-10b, 다음은 4-15 가 번갈아 나온다.
            beats.Add(TutorialBeat.Side("4-10b", 4, TutorialSignal.DoorHackFailed, "4-7", "4-14",
                    L("4-10b", Hq, "원래라면 해킹 시도를 감추기 위해 실패 패널티로 본부에 무언가 큰일이 벌어집니다만... 여기는 보안이 그리 치밀하지는 않나 보군요. 다시 해봅시다."),
                    L("4-15", Hq, "저런, 소리가 났군요. 여긴 그나마 보안이 삼엄하지 않아서 그렇지, 실전이었다면 벌써 터그 장난감 신세가 됐을 겁니다. 물론 후배님은 처음이니 그럴 수 있지요. 잠시 숨을 돌렸다가 다시 해봅시다.")
                        .WithTimedHint("3초 뒤 다시 시도합시다", 3f, TutorialSignal.DoorHackStarted))
                .AsRotating()
                .AsRepeatable(1f));

            // ───────────── 5단계 아이템 획득, 무게 감속 고지, 아이템 버리기, 열쇠 설명 (전리품 방) ─────────────
            // 5-1 은 문이 열려 있을 때만 나온다(수집기가 열린 문 앞 = BlueDoorAhead, 닫힌 해금 문 앞 = BlueDoorClosedAhead 로 나눈다).
            beats.Add(TutorialBeat.Main("5-1", 5, TutorialSignal.BlueDoorAhead,
                L("5-1", Hq, "열어 둔 푸른 격벽입니다. 경비견이 따라붙기 전에 바로 들어가십시오")
                    .WithHint("[Shift]키로 달려 열린 문 안으로 들어갑시다", TutorialSignal.LootRoomEntered)));

            beats.Add(TutorialBeat.Side("5-1b", 5, TutorialSignal.BlueDoorClosedAhead, "4-11", "5-2",
                    L("5-1b", Hq, "후배님, 철문이 닫혀 있습니다. 여기서는 못 여니까, 본부 단말기로 돌아가 철문을 더블클릭하셔야 합니다")
                        .WithHint("본부 [단말기]에서 푸른 격벽을 더블클릭해 엽시다", TutorialSignal.DoorOpened))
                .AsRepeatable(15f));

            beats.Add(TutorialBeat.Main("5-2", 5, TutorialSignal.LootRoomEntered,
                L("5-2", Hq, "자, 전리품 창고입니다. 눈에 띄는 대로 주워 담으시면 됩니다")
                    // 열쇠를 먼저 챙기면 전리품 없이 금고 쪽으로 넘어가므로 이 안내도 같이 내린다.
                    .WithHint("[{Interact}]키로 전리품을 주워봅시다", TutorialSignal.ItemLooted, TutorialSignal.KeyAcquired)));

            beats.Add(TutorialBeat.Main("5-3", 5, TutorialSignal.ItemLooted,
                L("5-3", Hq, "걸음이 느려진 게 느껴지십니까? 아직 몸이 작으시니 더 크게 느껴질 겁니다. 무게를 알려 주는 표시는 따로 없으니, 몸이 느끼는 속도로 가방 상태를 가늠하셔야 합니다"),
                L("5-4", Cat, "으, 몸이 바로 무거워져요... 이 정도는 괜찮아요!")
                    // 주운 직후에 버리는 법도 알려 둔다. 가방이 차면 5-5 가 같은 안내를 다시 띄운다.
                    .WithHint("필요 없는 물건은 [1~4]·[스크롤]로 슬롯을 고르고 [Q]키로 버릴 수 있습니다",
                        TutorialSignal.ItemDropped, TutorialSignal.InventoryThreeFilled))
                // 열쇠는 전리품으로 세지 않는다. 전리품을 하나도 안 줍고 열쇠부터 챙기면 5-3 은 건너뛰고 금고 쪽(5-7~)으로 간다.
                .SkippableOn(TutorialSignal.KeyAcquired));

            beats.Add(TutorialBeat.Main("5-5", 5, TutorialSignal.InventoryThreeFilled,
                L("5-5", Hq, "무거워진 만큼 걸음이 느려집니다. 가방은 네 칸이 전부이고, 곧 챙기실 중요한 물건이 있으니 한 칸은 비워 두십시오. 필요 없는 건 바로 버리시고요")
                    .WithHint(SlotDropHint, TutorialSignal.ItemDropped))
                .AsSkippable());

            beats.Add(TutorialBeat.Side("5-6", 5, TutorialSignal.InventoryFullRejected, "5-3", "6-1",
                L("5-6", Cat, "으아, 더 들 자리가 없어요, 진짜로!"),
                L("5-6b", Hq, "욕심이 많으시군요. 필요 없는 걸 버려서 칸을 비우셔야 합니다.")
                    .WithHint(SlotDropHint, TutorialSignal.ItemDropped)));

            beats.Add(TutorialBeat.Main("5-7", 5, TutorialSignal.NearKey,
                L("5-7", Cat, "저기! 가운데 전시대 위에 반짝이는 열쇠가 있어요!"),
                L("5-8", Hq, "그게 바로 금고를 열 수 있는 마스터 열쇠일 겁니다. 금고를 열려면 꼭 챙기셔야 하겠죠?")
                    .WithHint("[{Interact}]키로 열쇠를 획득합시다", TutorialSignal.KeyAcquired))
                // 열쇠를 먼저 주워 버리면 NearKey(줍기 전 근처)는 다시 안 나므로, 가진 상태로도 열리게 한다.
                .OrTriggeredBy(TutorialSignal.KeyAcquired));

            beats.Add(TutorialBeat.Main("5-9", 5, TutorialSignal.KeyAcquired,
                L("5-9", Hq, "열쇠는 금고 앞에서 한 번 쓰면 사라집니다. 버리거나 잃어버린 채로 금고 앞으로 가면 못 열게 되니까, 가방에 꼭 들고 계시고요.")));

            beats.Add(TutorialBeat.Main("5-10", 5, TutorialSignal.LootRoomExiting,
                L("5-10", Hq, "열쇠를 챙기셨으니 이제 금고 복도로 가시면 됩니다. 아, 조언 하나 하자면..."),
                L("5-11", Hq, "그 전에 본부에 들러서 물건을 바닥에 내려놓고 와도 좋겠군요. 욕심내서 짐을 많이 들수록 무거워지니, 개들을 피하기 어려워지니 말입니다."))
                .RequiringPlayed("5-9"));

            // ───────────── 6단계 적 NPC 순찰 안내 (금고 복도) ─────────────
            beats.Add(TutorialBeat.Main("6-1", 6, TutorialSignal.VaultCorridorEntered,
                L("6-1", Hq, "이 앞이 금고방입니다. 저기 복도 보이시지요. 경비견들이 정해진 길을 돌며 순찰하고 있으니 주의하셔야 합니다."),
                L("6-2", Cat, "벽 너머로 경비견들이 돌아다니는 게 보여요. ...생각보다 크네요. 잡히면 뼈, 뼈도 못 추리겠어요!"),
                L("6-3", Hq, "자, 순찰 동선을 잘 보십시오. 개가 보고 있지 않을 때 타이밍을 맞춰 지나가야 합니다. 물론... 경비견들이 그렇게 바보는 아니라서, 너무 뒤에 가까이 다가가시면 눈치챌 겁니다.")
                    .WithPreDelay(3f),
                L("6-4", Hq, "발자국을 개들이 발견하게 되면 숨었다 해도 그 발자국을 따라오니 조심하세요. 한 번 눈에 띄면 한동안 쫓아옵니다."),
                L("6-5", Hq, "저 근육 덩어리들에게 걸려 고깃덩어리가 되고 싶은 게 아니라면, 틈이 보일 때 한 번에 지나가십시오. 쫓기면 본부로 피하시고요. 안까지는 못 들어오니까.")));

            beats.Add(TutorialBeat.Side("6-6", 6, TutorialSignal.EnemyChasing, "6-1", "7-8",
                L("6-6", Hq, "들켰습니다! 빨리 본부로 도망가세요!")
                    .WithHint("[Shift]키로 도망칩시다", TutorialSignal.SprintStarted)));

            // ───────────── 7단계 열쇠 사용, 주의사항, 금고 미니게임, 긴급 탈출 (금고방) ─────────────
            // 금고 미니게임은 Space 타이밍(DigitalSync)으로 고정이다.
            beats.Add(TutorialBeat.Main("7-1", 7, TutorialSignal.VaultFront,
                L("7-1", Hq, "금고 앞입니다. 금고를 바라본 채로, 가방에서 아까 얻은 {{열쇠 칸}}을 골라 손에 들고 쓰시면 됩니다")
                    .WithHint("[1~4]·[스크롤]로 열쇠 슬롯을 고르고 금고를 본 채 [{Interact}]키로 사용", TutorialSignal.VaultUnlocked)));

            // 열쇠는 잠금만 푼다. 해킹은 같은 키를 한 번 더 눌러야 시작하므로 그 안내를 7-2 에 붙인다.
            beats.Add(TutorialBeat.Main("7-2", 7, TutorialSignal.VaultUnlocked,
                L("7-2", Hq, "열쇠가 맞았군요. 이제 한 번 더 누르십시오. 해킹이 시작됩니다. 놓치면 소리가 나서 경비견이 눈치챌 수 있으니 조심하시고요")
                    .WithHint("[{Interact}]키로 금고 해킹을 시작합시다", TutorialSignal.VaultHackStarted),
                L("7-3", Cat, "...숨 한 번 고르고 시작할게요. 프로는 원래 이렇게 하는 거라던데요"),
                L("7-3b", Hq, "호오, 숨까지 고르시고. 기특하십니다, 후배님")));

            beats.Add(TutorialBeat.Main("7-4", 7, TutorialSignal.VaultHackStarted,
                L("7-4", Hq, "집중하십시오. 바늘이 {{색칠된 구간}}에 들어오는 순간에 눌러야 하고, 기회는 {{세 번뿐}}입니다")
                    .WithHint("[Space]키로 타이밍을 맞춰봅시다",
                        TutorialSignal.VaultHackSucceeded, TutorialSignal.VaultHackFailed))
                .AsAnchor());

            beats.Add(TutorialBeat.Side("7-5", 7, TutorialSignal.VaultHackFailed, "7-4", "7-8",
                L("7-5", Hq, "저런, 실패했습니다! 소리가 새어 나갔을지도 모르겠군요. 일단 서둘러 다시 해 보십시오!"))
                .AsRepeatable(1f));

            // 금고가 부서지고 캣닢이 바닥에 생성되며 사이렌이 시작된다. 캣닢은 직접 주워야 한다.
            beats.Add(TutorialBeat.Main("7-6", 7, TutorialSignal.VaultHackSucceeded,
                L("7-6", Hq, "아, 열렸군요! 안에서 캣닢이 나왔습니다. 어서, 어서 집으십시오!")
                    .WithHint("[{Interact}]키로 캣닢을 주워봅시다", TutorialSignal.CatnipAcquired)));

            beats.Add(TutorialBeat.Side("7-7", 7, TutorialSignal.InventoryFullRejected, "7-6", "7-8",
                    L("7-7", Hq, "아, 가방이 꽉 차서 자리가 없잖습니까! 필요 없는 걸 버려서 캣닢 자리를 만드셔야 합니다!")
                        .WithHint(SlotDropHint, TutorialSignal.ItemDropped))
                .AsRepeatable(5f)
                .WithHintRecall());

            beats.Add(TutorialBeat.Main("7-8", 7, TutorialSignal.CatnipAcquired,
                L("7-8", Cat, "...이게 전설의 캣닢! 제, 제가 진짜 해냈어요!"),
                L("7-8b", Cat, "그, 그런데 이 사이렌 소리들은 대체 뭐죠?!"),
                L("7-9", Hq, "아, 드디어! 드디어 잡으셨군요! 그래도 아직 끝난 게 아니니 방심하지 마세요! 사이렌이 울리고, 캣닢 냄새도 퍼져서 경비견들이 곧 여기로 몰려들 겁니다!"),
                L("7-10", Hq, "개들이 들이닥치기 전에, 빨리 {{본부로}} 내달리십시오! 발자국이 계속 생기고 가방도 더 무거워질 테니 죽을 힘을 다해 달려야 합니다!")
                    .WithTimedHint("[긴급 탈출] 본부로 탈출합시다", 5f),
                L("7-11", Hq, "{{캣닢}}을 들고 {{본부}}로 돌아오십시오. 어서어서요!")
                    .WithHint("[Shift]키로 달려 본부로 탈출합시다", TutorialSignal.PlayerInMainbase),
                L("7-12", Cat, "으아, 소리가 장난 아니에요! 당장 본부로 튈게요!"))
                .AsAnchor());

            // 정산은 대사가 끝난다고 바로 뜨지 않는다. 본부 안에서 출발 버튼과 상호작용하면(탈출 성공) 화면이 까맣게
            // 페이드된 뒤 뜬다(TutorialDirector 가 결과 상태를 받아 연다). 그래서 출발 버튼을 누르라는 안내를 붙인다.
            beats.Add(TutorialBeat.Main("7-13", 7, TutorialSignal.PlayerInMainbase,
                L("7-13", Hq, "...됐습니다. 무사히 들어오셨군요. 잘하셨습니다, 후배님."),
                L("7-14", Hq, "이제 컴퓨터 왼쪽에 있는 {{출발 버튼}}을 누르면 여기서 나갈 수 있을 겁니다. 아, {{캣닢을 본부 안에 들여놓은 채로}} 버튼을 눌러야 출발한다는 점은 명심하고요.")
                    .WithHint("[{Interact}]키로 출발 버튼을 눌러 탈출합시다", TutorialSignal.SettlementShown))
                .RequiringPlayed("7-8"));

            // 7-15: 본부 안에서 출발 버튼을 눌렀는데 캣닢이 없을 때(RuleManager 가 알린다).
            beats.Add(TutorialBeat.Side("7-15", 7, TutorialSignal.DepartRejectedNoCatnip, "7-1", "8-1",
                    L("7-15a", Hq, "아, 후배님, 뭐하시는 겁니까? '전설의 캣닢'이 없으면 여기서 탈출이 불가능합니다!"),
                    L("7-15b", Hq, "애초에 그걸 위해 왔잖습니까? 오는 길에 흘렸는지, 다시 돌아가서 찾아보셔야겠네요."))
                .AsRepeatable(5f));

            // ───────────── 8단계 정산 및 엔딩 (본부) ─────────────
            beats.Add(TutorialBeat.Main("8-1", 8, TutorialSignal.SettlementShown,
                L("8-1", Hq, "훔쳐낸 것들은 이제 여기서 알아서 처리해줄 겁니다. 이렇게, 가져오신 캣닢과 전리품만큼 돈을 받게 되지요.")
                    .WithAction(TutorialLineAction.RevealSettlement)));

            beats.Add(TutorialBeat.Main("8-2", 8, TutorialSignal.SettlementRevealed,
                L("8-2", Cat, "... 그, 저기, 이름이라도 알려 주시면 안 되나요? 이만큼 잘 아시는 걸 보면 분명 대단한 괴도 선배일 텐데요..."),
                L("8-3", Hq, "저를 볼 수 있게 되는 건 먼 훗날이 될 겁니다. 언젠가 훌륭한 괴도가 되시면, 자연히 만나게 되어 있으니 얌전히 더 커서 오세요, 후배님."),
                L("8-4", Cat, "저 아기 아닌데... 맨날 그렇게 넘기시네요."),
                L("8-5", Hq, "어쨌든, 혼자 현장에 와보니 혼자서는 힘들다는 걸 아셨겠지요. 이젠 우리 후배님과 협력해서 현장과 본부를 나눠 맡을 동료들을 모아, 팀을 꾸리셔야 할 겁니다."),
                L("8-6", Cat, "...알겠어요. 다음엔 꼭 멋진 팀원들이랑 같이 올 거예요. 두고 보세요!"),
                L("8-7", Hq, "수고하셨습니다, 후배님. 앞으로도 이렇게 해나가시면 됩니다. 이제 알려드릴만한 것들은 다 알려드렸으니..."),
                L("8-7b", Hq, "행운을 빕니다.")
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
                    L("R-move-1", Hq, "거기 서서 뭘 하십니까, 후배님?"),
                    L("R-move-2", Hq, "길을 잃으셨습니까? 현장에서 멍하니 서 계시면 경비견의 먹잇감이 됩니다."))
                .AsRotating()
                .AsRepeatable(20f)
                .WithHintRecall());

            // 입력 없음(격벽 해킹): 단말기를 연 채 15초간 조작 없음 (4-4 이후 ~ 해킹 성공 전)
            beats.Add(TutorialBeat.Side("R-door", 0, TutorialSignal.IdleAtTerminal, "4-4", "4-11",
                    L("R-door-1", Hq, "붉은 철문을 골라 클릭하시라고 말씀드렸지요."),
                    L("R-door-2", Hq, "자자, 서두르십시오. 경비견이 돌아오기 전에 끝내야 합니다."))
                .AsRotating()
                .AsRepeatable(15f)
                .WithHintRecall());

            // 입력 없음(격벽 열기): 해킹 성공 뒤 단말기를 연 채 15초간 조작 없음 (4-11 이후 ~ 격벽이 열리기 전)
            beats.Add(TutorialBeat.Side("R-gate", 0, TutorialSignal.IdleAtTerminal, "4-11", "4-13",
                    L("R-gate-1", Hq, "철문이 푸른색이 됐습니다. 이제 더블클릭해서 여시면 됩니다."),
                    L("R-gate-2", Hq, "열어 두지 않으면 격벽 앞에서 발만 구르게 됩니다."))
                .AsRotating()
                .AsRepeatable(15f)
                .WithHintRecall());

            // 입력 없음(금고): 금고 앞 도착 후 10초간 해킹을 시작하지 않음 (캣닢 획득 전까지)
            beats.Add(TutorialBeat.Side("R-vault", 0, TutorialSignal.IdleAtVault, null, "7-8",
                    L("R-vault-1", Hq, "금고 앞입니다. 열쇠를 쓰고, 한 번 더 눌러서 해킹을 시작하셔야 합니다."),
                    L("R-vault-2", Hq, "시간을 끌면 순찰이 한 바퀴 돌아옵니다. 이런 데서는 시간이 생명이랍니다, 후배님"))
                .AsRotating()
                .AsRepeatable(12f)
                .WithHintRecall());

            // 추격 종료: Chase 에서 Investigate 로 복귀 시
            beats.Add(TutorialBeat.Side("R-chase-end", 0, TutorialSignal.ChaseEnded, "2-1", "8-1",
                    L("R-chase-end-1", Hq, "오, 따돌리셨군요. 그래도 방심은 금물입니다. 개들은 언제나 후배님이 남긴 발자국을 따라올 테니까요."))
                .AfterSidePlayed("3-4") // 첫 추격은 3-4 가 같은 말을 한다
                .AsRotating()
                .AsRepeatable(10f)
                .WithHintRecall());

            // 체력 낮음: 체력 30% 이하
            beats.Add(TutorialBeat.Side("R-health", 0, TutorialSignal.HealthLow, "2-1", "8-1",
                    L("R-health-1", Hq, "후배님, 상처가 깊습니다. 더는 못 버팁니다"))
                .AsRotating()
                .AsRepeatable(20f)
                .WithHintRecall());

            // 사망 시: 떠 있는 대사를 끊고 바로 부른다. 가면이 식는 순간이라 말투가 짧아진다.
            beats.Add(TutorialBeat.Side("R-death", 0, TutorialSignal.PlayerDied, "2-1", "8-1",
                    L("R-death-1", Hq, "... 후배님! 정신 차리세요, 지금ㅡ ..."))
                .AsUrgent()
                .AsRepeatable(3f));

            // 재시도 시: 사망 후 재시작 지점에서 재개
            beats.Add(TutorialBeat.Side("R-retry", 0, TutorialSignal.PlayerRespawned, "2-1", "8-1",
                    L("R-retry-1", Hq, "현장에서 멍하니 서계시다니, 방금 무슨 일이라도 있었나요? 아직 갈 길이 머니까, 계속 가봅시다."))
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

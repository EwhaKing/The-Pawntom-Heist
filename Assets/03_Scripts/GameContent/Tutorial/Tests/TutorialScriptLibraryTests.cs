using System.Collections.Generic;
using NUnit.Framework;
using Pawntom.Tutorial.Core;

namespace Pawntom.Tutorial.Tests
{
    public class TutorialScriptLibraryTests
    {
        // 튜토리얼 기획서(튜토리얼.txt) 1~8단계 대본의 번호 전체.
        private static readonly string[] ScriptIds =
        {
            "1-1", "1-2", "1-3", "1-4", "1-5", "1-6", "1-7",
            "2-1", "2-2", "2-3", "2-4", "2-5", "2-6", "2-7", "2-8", "2-9", "2-10", "2-11", "2-12",
            "3-1", "3-2", "3-3", "3-4", "3-5", "3-6", "3-7", "3-8",
            "4-1", "4-2", "4-3", "4-4", "4-5", "4-6", "4-7", "4-8", "4-9", "4-10", "4-11",
            "5-1", "5-2", "5-3", "5-4", "5-5", "5-6", "5-7", "5-8",
            "6-1", "6-2", "6-3", "6-4", "6-5",
            "7-1", "7-2", "7-3", "7-4", "7-5", "7-6", "7-7", "7-8", "7-9", "7-10",
            "8-1", "8-2", "8-3", "8-4", "8-5", "8-6", "8-7",

            // 상황별 반복 대사(후보 포함)
            "R-move-1", "R-move-2", "R-door-1", "R-door-2", "R-vault-1", "R-vault-2",
            "R-chase-end-1", "R-health-1", "R-death-1", "R-retry-1"
        };

        // 모든 상황이 순조로울 때(실패·추격·스태미나 경고 없이) 재생되는 순서.
        private static readonly string[] HappyPathOrder =
        {
            "1-1", "1-2", "1-3", "1-4", "1-5", "1-6", "1-7",
            "2-1", "2-2", "2-3", "2-4", "2-5", "2-6", "2-7", "2-9", "2-10", "2-11", "2-12",
            "3-1", "3-2", "3-3", "3-4", "3-6", "3-7", "3-8",
            "4-1", "4-2", "4-3", "4-4", "4-5", "4-6", "4-7", "4-8", "4-9",
            "5-1", "5-2", "5-3", "5-4", "5-5", "5-6", "5-8",
            "6-1", "6-2", "6-3", "6-5",
            "7-1", "7-2", "7-4", "7-5", "7-6", "7-7", "7-8", "7-10",
            "8-1", "8-2", "8-3", "8-4", "8-5", "8-6", "8-7"
        };

        private static readonly TutorialSignal[] HappyPathSignals =
        {
            TutorialSignal.EyesOpened,
            TutorialSignal.FirstFootprint,
            TutorialSignal.LookedBack,
            TutorialSignal.SprintStarted,
            TutorialSignal.ObstacleAhead,
            TutorialSignal.CorridorAEndReached,
            TutorialSignal.NearSecurityNode,
            TutorialSignal.SecurityNodeThreatCue,
            TutorialSignal.SecurityNodeConnected,
            TutorialSignal.EnemyChasing,
            TutorialSignal.SafeInMainbase,
            TutorialSignal.NearTerminal,
            TutorialSignal.TerminalOpened,
            TutorialSignal.CctvChecked,
            TutorialSignal.DoorHackStarted,
            TutorialSignal.DoorHackSucceeded,
            TutorialSignal.BlueDoorAhead,
            TutorialSignal.LootRoomEntered,
            TutorialSignal.NearLoot,
            TutorialSignal.ItemLooted,
            TutorialSignal.InventoryThreeFilled,
            TutorialSignal.LootRoomExiting,
            TutorialSignal.VaultCorridorEntered,
            TutorialSignal.VaultRoomEntered,
            TutorialSignal.VaultFront,
            TutorialSignal.VaultHackStarted,
            TutorialSignal.CatnipAcquired,
            TutorialSignal.PlayerInMainbase,
            TutorialSignal.SettlementShown,
            TutorialSignal.SettlementRevealed
        };

        private static readonly Dictionary<string, string> Tokens = new Dictionary<string, string>
        {
            { "Interact", "E" },
            { "TerminalExit", "Esc" }
        };

        private static void Pump(TutorialSequencer sequencer, float deltaTime)
        {
            sequencer.Tick(deltaTime);

            int guard = 0;

            while (sequencer.CurrentLine != null && guard++ < 200)
            {
                sequencer.CompleteCurrentLine();
            }
        }

        private static TutorialSequencer RunHappyPath(List<string> started)
        {
            TutorialSequencer sequencer = new TutorialSequencer(TutorialScriptLibrary.CreateDefault());
            sequencer.LineStarted += line => started.Add(line.Id);

            foreach (TutorialSignal signal in HappyPathSignals)
            {
                sequencer.Raise(signal);
                Pump(sequencer, 0f);
                Pump(sequencer, 3.5f); // 6-3 "6-2 이후 3초 경과" 같은 대기를 넘긴다
            }

            return sequencer;
        }

        [Test]
        public void CreateDefault_ScriptLines_CoverEveryScriptId()
        {
            HashSet<string> ids = new HashSet<string>();

            foreach (TutorialLine line in TutorialScriptLibrary.CreateDefault().AllLines())
            {
                ids.Add(line.Id);
            }

            CollectionAssert.AreEquivalent(ScriptIds, ids);
        }

        [Test]
        public void CreateDefault_HappyPath_PlaysMainLinesInScriptOrder()
        {
            List<string> started = new List<string>();

            RunHappyPath(started);

            CollectionAssert.AreEqual(HappyPathOrder, started);
        }

        [Test]
        public void CreateDefault_HappyPath_Finishes()
        {
            TutorialSequencer sequencer = RunHappyPath(new List<string>());

            Assert.IsTrue(sequencer.IsFinished);
        }

        [Test]
        public void CreateDefault_EveryHint_HasCompletionOrTimeout()
        {
            List<string> orphans = new List<string>();

            foreach (TutorialLine line in TutorialScriptLibrary.CreateDefault().AllLines())
            {
                if (line.HasHint && line.HintCompleteOn.Length == 0 && line.HintAutoHideSeconds <= 0f)
                {
                    orphans.Add(line.Id);
                }
            }

            CollectionAssert.IsEmpty(orphans);
        }

        [Test]
        public void CreateDefault_EveryHintToken_IsResolvable()
        {
            List<string> unresolved = new List<string>();

            foreach (TutorialLine line in TutorialScriptLibrary.CreateDefault().AllLines())
            {
                if (line.HasHint && TutorialHintFormatter.Plain(line.Hint, Tokens).IndexOf('{') >= 0)
                {
                    unresolved.Add(line.Id);
                }
            }

            CollectionAssert.IsEmpty(unresolved);
        }

        [Test]
        public void CreateDefault_DoorHackFailedThenSucceeded_PlaysRetryLine()
        {
            TutorialSequencer sequencer = new TutorialSequencer(TutorialScriptLibrary.CreateDefault());
            List<string> started = new List<string>();
            sequencer.LineStarted += line => started.Add(line.Id);

            TutorialSignal[] untilHack =
            {
                TutorialSignal.EyesOpened, TutorialSignal.FirstFootprint, TutorialSignal.LookedBack,
                TutorialSignal.SprintStarted, TutorialSignal.CorridorAEndReached, TutorialSignal.NearSecurityNode,
                TutorialSignal.SecurityNodeConnected, TutorialSignal.NearTerminal, TutorialSignal.TerminalOpened,
                TutorialSignal.DoorHackStarted, TutorialSignal.DoorHackFailed, TutorialSignal.DoorHackSucceeded
            };

            foreach (TutorialSignal signal in untilHack)
            {
                sequencer.Raise(signal);
                Pump(sequencer, 0f);
            }

            // 4-10(실패) → 4-11(재시도 성공) → 4-9(단말기 나오기)
            CollectionAssert.AreEqual(new[] { "4-10", "4-11", "4-9" }, started.GetRange(started.Count - 3, 3));
        }

        [Test]
        public void CreateDefault_NodeConnectedWithoutChase_TerminalStartsStageFour()
        {
            // 분전반 접속(3-3)까지 마쳤으면, 경비견 추격(3-4)이 없었어도 단말기 앞에서 4단계로 넘어간다.
            TutorialSequencer sequencer = new TutorialSequencer(TutorialScriptLibrary.CreateDefault());
            TutorialSignal[] signals =
            {
                TutorialSignal.EyesOpened, TutorialSignal.FirstFootprint, TutorialSignal.LookedBack,
                TutorialSignal.SprintStarted, TutorialSignal.CorridorAEndReached, TutorialSignal.NearSecurityNode,
                TutorialSignal.SecurityNodeConnected, TutorialSignal.NearTerminal
            };

            foreach (TutorialSignal signal in signals)
            {
                sequencer.Raise(signal);
                Pump(sequencer, 0f);
            }

            Assert.AreEqual(TutorialBeatState.Done, sequencer.GetMainState("4-1"));
        }

        private static TutorialSequencer StartedAtStageTwo(List<string> started)
        {
            TutorialSequencer sequencer = new TutorialSequencer(TutorialScriptLibrary.CreateDefault());
            sequencer.LineStarted += line => started.Add(line.Id);
            sequencer.Raise(TutorialSignal.EyesOpened);
            Pump(sequencer, 0f); // 1-1 ~ 1-7, 2-1
            return sequencer;
        }

        [Test]
        public void CreateDefault_IdleTwice_AlternatesMoveReminders()
        {
            List<string> started = new List<string>();
            TutorialSequencer sequencer = StartedAtStageTwo(started);

            sequencer.Raise(TutorialSignal.IdleNoMove);
            Pump(sequencer, 0f);
            Pump(sequencer, 25f); // 반복 간격(20초) 경과
            sequencer.Raise(TutorialSignal.IdleNoMove);
            Pump(sequencer, 0f);

            CollectionAssert.AreEqual(new[] { "R-move-1", "R-move-2" }, started.GetRange(started.Count - 2, 2));
        }

        [Test]
        public void CreateDefault_IdleReminder_ReshowsSectionHint()
        {
            // 2-1 "[WASD]로 이동하고 [마우스]로 둘러봅시다"를 아직 안 했으면 상황 대사 뒤 다시 걸린다.
            TutorialSequencer sequencer = StartedAtStageTwo(new List<string>());

            sequencer.Raise(TutorialSignal.IdleNoMove);
            Pump(sequencer, 0f);

            Assert.AreEqual("2-1", sequencer.Hints[sequencer.Hints.Count - 1].Line.Id);
        }

        [Test]
        public void CreateDefault_TerminalIdleBeforeTerminalOpened_DoesNotPlay()
        {
            List<string> started = new List<string>();
            TutorialSequencer sequencer = StartedAtStageTwo(started);

            sequencer.Raise(TutorialSignal.IdleAtTerminal);
            Pump(sequencer, 0f);

            CollectionAssert.DoesNotContain(started, "R-door-1");
        }

        [Test]
        public void CreateDefault_PlayerDiedMidLine_DeathLineCutsIn()
        {
            TutorialSequencer sequencer = new TutorialSequencer(TutorialScriptLibrary.CreateDefault());
            sequencer.Raise(TutorialSignal.EyesOpened);
            Pump(sequencer, 0f);
            sequencer.Raise(TutorialSignal.FirstFootprint);
            sequencer.Tick(0f); // 2-2 표시 중

            sequencer.Raise(TutorialSignal.PlayerDied);
            sequencer.Tick(0f);

            Assert.AreEqual("R-death-1", sequencer.CurrentLine.Id);
        }

        [Test]
        public void CreateDefault_JumpInPlace_KeepsObstacleHint()
        {
            // 제자리 점프로는 2-9 안내가 지워지지 않는다 — 상자 반대편에 도착해야 한다.
            TutorialSequencer sequencer = StartedAtStageTwo(new List<string>());
            TutorialSignal[] signals =
            {
                TutorialSignal.FirstFootprint, TutorialSignal.LookedBack, TutorialSignal.SprintStarted,
                TutorialSignal.ObstacleAhead, TutorialSignal.Jumped
            };

            foreach (TutorialSignal signal in signals)
            {
                sequencer.Raise(signal);
                Pump(sequencer, 0f);
            }

            Assert.AreEqual("2-9", sequencer.Hints[sequencer.Hints.Count - 1].Line.Id);
        }

        [Test]
        public void CreateDefault_ReachFarSideOfObstacle_ClearsObstacleHint()
        {
            TutorialSequencer sequencer = StartedAtStageTwo(new List<string>());
            TutorialSignal[] signals =
            {
                TutorialSignal.FirstFootprint, TutorialSignal.LookedBack, TutorialSignal.SprintStarted,
                TutorialSignal.ObstacleAhead, TutorialSignal.ObstacleCleared
            };

            foreach (TutorialSignal signal in signals)
            {
                sequencer.Raise(signal);
                Pump(sequencer, 0f);
            }

            bool stillShown = false;

            for (int i = 0; i < sequencer.Hints.Count; i++)
            {
                stillShown |= sequencer.Hints[i].Line.Id == "2-9";
            }

            Assert.IsFalse(stillShown);
        }

        [Test]
        public void CreateDefault_StageThreePending_TerminalDoesNotSkipToStageFour()
        {
            // 2단계를 마친 뒤 본부 단말기 앞에 가도 분전반 접속(3단계) 없이는 4단계로 넘어가지 않는다.
            TutorialSequencer sequencer = new TutorialSequencer(TutorialScriptLibrary.CreateDefault());
            TutorialSignal[] signals =
            {
                TutorialSignal.EyesOpened, TutorialSignal.FirstFootprint, TutorialSignal.LookedBack,
                TutorialSignal.SprintStarted, TutorialSignal.CorridorAEndReached, TutorialSignal.NearTerminal
            };

            foreach (TutorialSignal signal in signals)
            {
                sequencer.Raise(signal);
                Pump(sequencer, 0f);
            }

            Assert.AreEqual(TutorialBeatState.Pending, sequencer.GetMainState("4-1"));
        }
    }
}

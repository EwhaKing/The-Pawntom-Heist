using System.Collections.Generic;
using NUnit.Framework;
using Pawntom.Tutorial.Core;

namespace Pawntom.Tutorial.Tests
{
    public class TutorialScriptLibraryTests
    {
        // 튜토리얼 대본(최종 수정본, docs/project/TUTORIAL_SCRIPT.md) 1~8단계 대본의 번호 전체.
        private static readonly string[] ScriptIds =
        {
            "1-1", "1-2", "1-3", "1-4", "1-5", "1-6", "1-7", "1-8", "1-9",
            "2-1", "2-2", "2-3", "2-4", "2-5a", "2-5b", "2-6", "2-7", "2-8", "2-9", "2-9b", "2-9c", "2-10a", "2-10b", "2-10c", "2-10d", "2-11", "2-12", "2-13",
            "3-0", "3-1", "3-2", "3-3", "3-4", "3-5", "3-5b", "3-5c", "3-6", "3-7", "3-8",
            "4-1", "4-2", "4-3", "4-4", "4-5", "4-6", "4-6b", "4-6a", "4-7a", "4-7b", "4-7c", "4-8", "4-9", "4-10", "4-10b", "4-11a", "4-11b", "4-12", "4-13", "4-13b", "4-14", "4-15",
            "5-1", "5-1b", "5-2", "5-3", "5-4", "5-5", "5-6", "5-6b", "5-7", "5-8", "5-9", "5-10", "5-11",
            "6-1", "6-2", "6-3", "6-4", "6-5", "6-6",
            "7-1", "7-2", "7-3", "7-3b", "7-4", "7-5", "7-6", "7-7", "7-8", "7-8b", "7-9", "7-10", "7-11", "7-12", "7-13", "7-14", "7-15a", "7-15b",
            "8-1", "8-2", "8-3", "8-4", "8-5", "8-6", "8-7", "8-7b",

            // 상황별 반복 대사(후보 포함)
            "R-move-1", "R-move-2", "R-door-1", "R-door-2", "R-gate-1", "R-gate-2", "R-vault-1", "R-vault-2",
            "R-chase-end-1", "R-health-1", "R-death-1", "R-retry-1"
        };

        // 모든 상황이 순조로울 때(실패·추격·스태미나 경고 없이) 재생되는 순서.
        // 4-8~4-10 은 걸린 종류 하나만(여기서는 방향키 순서 입력) 나온다.
        private static readonly string[] HappyPathOrder =
        {
            "1-1", "1-2", "1-3", "1-4", "1-5", "1-6", "1-7", "1-8", "1-9",
            "2-1", "2-2", "2-3", "2-4", "2-5a", "2-5b", "2-6", "2-7", "2-9", "2-9b", "2-9c", "2-10a", "2-10b", "2-10c", "2-10d", "2-11", "2-12", "2-13",
            "3-1", "3-2", "3-3", "3-4", "3-5", "3-5b", "3-5c", "3-7", "3-8",
            "4-1", "4-2", "4-3", "4-4", "4-5", "4-6", "4-6b", "4-7a", "4-7b", "4-7c", "4-8", "4-11a", "4-11b", "4-12", "4-13",
            "5-1", "5-2", "5-3", "5-4", "5-5", "5-7", "5-8", "5-9", "5-10", "5-11",
            "6-1", "6-2", "6-3", "6-4", "6-5",
            "7-1", "7-2", "7-3", "7-3b", "7-4", "7-6", "7-8", "7-8b", "7-9", "7-10", "7-11", "7-12", "7-13", "7-14",
            "8-1", "8-2", "8-3", "8-4", "8-5", "8-6", "8-7", "8-7b"
        };

        private static readonly TutorialSignal[] HappyPathSignals =
        {
            TutorialSignal.EyesOpened,
            TutorialSignal.FirstFootprint,
            TutorialSignal.SprintStarted,
            TutorialSignal.LookedBack,
            TutorialSignal.ObstacleAhead,
            TutorialSignal.ObstacleCleared,
            TutorialSignal.CorridorAEndReached,
            TutorialSignal.NearSecurityNode,
            TutorialSignal.SecurityNodeCastStarted,
            TutorialSignal.SecurityNodeConnected,
            TutorialSignal.ChaseEscaped,
            TutorialSignal.NearTerminal,
            TutorialSignal.TerminalOpened,
            TutorialSignal.DoorHackStarted,
            TutorialSignal.DoorGameCommand,
            TutorialSignal.DoorHackSucceeded,
            TutorialSignal.DoorOpened,
            TutorialSignal.BlueDoorAhead,
            TutorialSignal.LootRoomEntered,
            TutorialSignal.ItemLooted,
            TutorialSignal.InventoryThreeFilled,
            TutorialSignal.NearKey,
            TutorialSignal.KeyAcquired,
            TutorialSignal.LootRoomExiting,
            TutorialSignal.VaultCorridorEntered,
            TutorialSignal.VaultFront,
            TutorialSignal.VaultUnlocked,
            TutorialSignal.VaultHackStarted,
            TutorialSignal.VaultHackSucceeded,
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
                TutorialSignal.EyesOpened, TutorialSignal.FirstFootprint, TutorialSignal.SprintStarted,
                TutorialSignal.LookedBack, TutorialSignal.ObstacleAhead, TutorialSignal.CorridorAEndReached, TutorialSignal.NearSecurityNode,
                TutorialSignal.SecurityNodeConnected, TutorialSignal.NearTerminal, TutorialSignal.TerminalOpened,
                TutorialSignal.DoorHackStarted, TutorialSignal.DoorHackFailed, TutorialSignal.DoorHackSucceeded
            };

            foreach (TutorialSignal signal in untilHack)
            {
                sequencer.Raise(signal);
                Pump(sequencer, 0f);
            }

            // 4-14(실패) → 4-11·4-12(재시도 성공, 푸른 격벽 열기 안내)
            CollectionAssert.AreEqual(new[] { "4-10b", "4-11a", "4-11b", "4-12" }, started.GetRange(started.Count - 4, 4));
        }

        // 격벽 해킹 시작(4-7)까지 마친 상태.
        private static TutorialSequencer StartedAtDoorHack(List<string> started)
        {
            TutorialSequencer sequencer = new TutorialSequencer(TutorialScriptLibrary.CreateDefault());
            sequencer.LineStarted += line => started.Add(line.Id);

            TutorialSignal[] signals =
            {
                TutorialSignal.EyesOpened, TutorialSignal.FirstFootprint, TutorialSignal.SprintStarted,
                TutorialSignal.LookedBack, TutorialSignal.ObstacleAhead, TutorialSignal.CorridorAEndReached, TutorialSignal.NearSecurityNode,
                TutorialSignal.SecurityNodeConnected, TutorialSignal.NearTerminal, TutorialSignal.TerminalOpened,
                TutorialSignal.DoorHackStarted
            };

            foreach (TutorialSignal signal in signals)
            {
                sequencer.Raise(signal);
                Pump(sequencer, 0f);
            }

            return sequencer;
        }

        [Test]
        public void CreateDefault_DoorHackKind_PlaysOnlyTheMatchingGuide()
        {
            // 4-8~4-10 [택1]: 걸린 종류의 안내만 나온다.
            foreach (var pair in new[]
            {
                new KeyValuePair<TutorialSignal, string>(TutorialSignal.DoorGameCommand, "4-8"),
                new KeyValuePair<TutorialSignal, string>(TutorialSignal.DoorGameSignal, "4-9"),
                new KeyValuePair<TutorialSignal, string>(TutorialSignal.DoorGameSync, "4-10")
            })
            {
                List<string> started = new List<string>();
                TutorialSequencer sequencer = StartedAtDoorHack(started);

                sequencer.Raise(pair.Key);
                Pump(sequencer, 0f);

                Assert.AreEqual(pair.Value, started[started.Count - 1], pair.Key.ToString());

                foreach (string other in new[] { "4-8", "4-9", "4-10" })
                {
                    if (other != pair.Value)
                    {
                        CollectionAssert.DoesNotContain(started, other, pair.Key.ToString());
                    }
                }
            }
        }

        [Test]
        public void CreateDefault_DoorHackRetryWithOtherKind_PlaysThatGuideToo()
        {
            // 실패 뒤 다시 걸린 종류가 달라도 그 종류의 안내가 나온다(시도마다 안내).
            List<string> started = new List<string>();
            TutorialSequencer sequencer = StartedAtDoorHack(started);

            sequencer.Raise(TutorialSignal.DoorGameCommand);
            Pump(sequencer, 0f);
            sequencer.Raise(TutorialSignal.DoorHackFailed);
            Pump(sequencer, 0f);
            Pump(sequencer, 5f); // 반복 간격 경과
            sequencer.Raise(TutorialSignal.DoorGameSignal);
            Pump(sequencer, 0f);

            CollectionAssert.AreEqual(new[] { "4-8", "4-10b", "4-9" }, started.GetRange(started.Count - 3, 3));
        }

        [Test]
        public void CreateDefault_ClosedBlueDoor_PlaysClosedHintThenOpenHint()
        {
            // 문 앞에 왔는데 문이 닫혀 있으면 5-1b, 열려 있으면 5-1.
            List<string> started = new List<string>();
            TutorialSequencer sequencer = StartedAtDoorHack(started);

            TutorialSignal[] signals =
            {
                TutorialSignal.DoorHackSucceeded, TutorialSignal.DoorOpened, TutorialSignal.BlueDoorClosedAhead
            };

            foreach (TutorialSignal signal in signals)
            {
                sequencer.Raise(signal);
                Pump(sequencer, 0f);
            }

            Assert.AreEqual("5-1b", started[started.Count - 1]);

            sequencer.Raise(TutorialSignal.BlueDoorAhead);
            Pump(sequencer, 0f);

            Assert.AreEqual("5-1", started[started.Count - 1]);
        }

        [Test]
        public void CreateDefault_KeyAcquired_ClearsKeyHintAndPlaysKeyExplanation()
        {
            List<string> started = new List<string>();
            TutorialSequencer sequencer = StartedAtDoorHack(started);

            TutorialSignal[] signals =
            {
                TutorialSignal.DoorHackSucceeded, TutorialSignal.DoorOpened, TutorialSignal.BlueDoorAhead,
                TutorialSignal.LootRoomEntered, TutorialSignal.ItemLooted, TutorialSignal.NearKey
            };

            foreach (TutorialSignal signal in signals)
            {
                sequencer.Raise(signal);
                Pump(sequencer, 0f);
            }

            Assert.AreEqual("5-8", sequencer.Hints[sequencer.Hints.Count - 1].Line.Id);

            sequencer.Raise(TutorialSignal.KeyAcquired);
            Pump(sequencer, 0f);

            Assert.AreEqual("5-9", started[started.Count - 1]);

            bool stillShown = false;

            for (int i = 0; i < sequencer.Hints.Count; i++)
            {
                stillShown |= sequencer.Hints[i].Line.Id == "5-8";
            }

            Assert.IsFalse(stillShown);
        }

        // 금고 앞(7-1)까지 마친 상태.
        private static TutorialSequencer StartedAtVault(List<string> started)
        {
            TutorialSequencer sequencer = new TutorialSequencer(TutorialScriptLibrary.CreateDefault());
            sequencer.LineStarted += line => started.Add(line.Id);

            TutorialSignal[] signals =
            {
                TutorialSignal.EyesOpened, TutorialSignal.FirstFootprint, TutorialSignal.SprintStarted,
                TutorialSignal.LookedBack, TutorialSignal.ObstacleAhead, TutorialSignal.CorridorAEndReached, TutorialSignal.NearSecurityNode,
                TutorialSignal.SecurityNodeConnected, TutorialSignal.NearTerminal, TutorialSignal.TerminalOpened,
                TutorialSignal.DoorHackStarted, TutorialSignal.DoorHackSucceeded, TutorialSignal.DoorOpened,
                TutorialSignal.BlueDoorAhead, TutorialSignal.LootRoomEntered, TutorialSignal.ItemLooted, TutorialSignal.NearKey,
                TutorialSignal.KeyAcquired, TutorialSignal.LootRoomExiting, TutorialSignal.VaultCorridorEntered,
                TutorialSignal.VaultFront
            };

            foreach (TutorialSignal signal in signals)
            {
                sequencer.Raise(signal);
                Pump(sequencer, 0f);
                Pump(sequencer, 3.5f);
            }

            return sequencer;
        }

        [Test]
        public void CreateDefault_VaultKeyUsed_ClearsKeyHintAndPlaysUnlockLines()
        {
            List<string> started = new List<string>();
            TutorialSequencer sequencer = StartedAtVault(started);

            Assert.AreEqual("7-1", sequencer.Hints[sequencer.Hints.Count - 1].Line.Id);

            sequencer.Raise(TutorialSignal.VaultUnlocked);
            Pump(sequencer, 0f);

            CollectionAssert.AreEqual(new[] { "7-2", "7-3", "7-3b" }, started.GetRange(started.Count - 3, 3));
        }

        [Test]
        public void CreateDefault_CatnipPickupWithFullBag_PlaysFullBagLine()
        {
            // 금고 성공 뒤 캣닢을 주우려는데 가방이 가득이면 7-7. 7단계 한정(캣닢을 얻으면 닫힌다).
            List<string> started = new List<string>();
            TutorialSequencer sequencer = StartedAtVault(started);

            TutorialSignal[] signals =
            {
                TutorialSignal.VaultUnlocked, TutorialSignal.VaultHackStarted, TutorialSignal.VaultHackSucceeded,
                TutorialSignal.InventoryFullRejected
            };

            foreach (TutorialSignal signal in signals)
            {
                sequencer.Raise(signal);
                Pump(sequencer, 0f);
            }

            Assert.AreEqual("7-7", started[started.Count - 1]);
            Assert.AreEqual("7-7", sequencer.Hints[sequencer.Hints.Count - 1].Line.Id);
        }

        [Test]
        public void CreateDefault_FullBagBeforeCatnipDrop_DoesNotPlayStageSevenLine()
        {
            List<string> started = new List<string>();
            TutorialSequencer sequencer = StartedAtVault(started);

            sequencer.Raise(TutorialSignal.InventoryFullRejected);
            Pump(sequencer, 0f);

            CollectionAssert.DoesNotContain(started, "7-7");
        }

        [Test]
        public void TutorialLine_EmphasisMarkup_IsStrippedAndRangesReturned()
        {
            // {{ }} 로 감싼 말은 글자 수에 세지 않고, 강조 구간(시작 이상·끝 미만)으로 따로 돌려준다.
            TutorialLine line = new TutorialLine("t-1", TutorialSpeaker.Command, "가{{나다}}라{{마}}");

            Assert.AreEqual("가나다라마", line.Text);
            CollectionAssert.AreEqual(new[] { 1, 3, 4, 5 }, line.Emphasis);

            // 사본(WithHint 등)도 강조를 유지한다.
            Assert.AreEqual(4, line.WithHint("힌트").Emphasis.Length);

            // 닫히지 않은 표시는 무시한다.
            TutorialLine open = new TutorialLine("t-2", TutorialSpeaker.Command, "가{{나다");
            Assert.AreEqual("가나다", open.Text);
            Assert.AreEqual(0, open.Emphasis.Length);
        }

        [Test]
        public void CreateDefault_DirectorBeatIds_AreMainBeats()
        {
            // 디렉터(터미널 안내·탈출 버튼 안내)가 GetMainState 로 읽는 id 는 메인 비트여야 한다. 아니면 매 프레임 예외가 난다.
            TutorialSequencer sequencer = new TutorialSequencer(TutorialScriptLibrary.CreateDefault());

            foreach (string id in new[] { "4-4", "4-11", "4-14", "7-13", "6-1" })
            {
                Assert.DoesNotThrow(() => sequencer.GetMainState(id), id);
            }
        }

        [Test]
        public void CreateDefault_EventLine_CutsInOverIdleReminder()
        {
            // 가만히 있을 때 나오는 재촉 대사가 떠 있어도, 이벤트(해킹 성공 4-11)가 오면 그 대사가 바로 나온다.
            List<string> started = new List<string>();
            TutorialSequencer sequencer = StartedAtDoorHack(started);

            sequencer.Raise(TutorialSignal.IdleAtTerminal);
            sequencer.Tick(0f); // Pump 로 넘기지 않아 재촉 대사가 떠 있는 채로 둔다
            Assert.AreEqual("R-door-1", sequencer.CurrentLine.Id);

            sequencer.Raise(TutorialSignal.DoorHackSucceeded);
            sequencer.Tick(0f);

            Assert.AreEqual("4-11a", sequencer.CurrentLine.Id);
        }

        [Test]
        public void CreateDefault_TerminalIdleAfterDoorUnlocked_PlaysGateReminder()
        {
            // 격벽 해킹 성공 뒤 단말기를 연 채 가만히 있으면 R-door 가 아니라 R-gate 가 나온다.
            List<string> started = new List<string>();
            TutorialSequencer sequencer = StartedAtDoorHack(started);

            sequencer.Raise(TutorialSignal.DoorHackSucceeded);
            Pump(sequencer, 0f);
            sequencer.Raise(TutorialSignal.IdleAtTerminal);
            Pump(sequencer, 0f);

            Assert.AreEqual("R-gate-1", started[started.Count - 1]);
        }

        [Test]
        public void CreateDefault_NodeConnectedWithoutChase_TerminalStartsStageFour()
        {
            // 분전반 접속(3-3)까지 마쳤으면, 경비견 추격(3-4)이 없었어도 단말기 앞에서 4단계로 넘어간다.
            TutorialSequencer sequencer = new TutorialSequencer(TutorialScriptLibrary.CreateDefault());
            TutorialSignal[] signals =
            {
                TutorialSignal.EyesOpened, TutorialSignal.FirstFootprint, TutorialSignal.SprintStarted,
                TutorialSignal.LookedBack, TutorialSignal.ObstacleAhead, TutorialSignal.CorridorAEndReached, TutorialSignal.NearSecurityNode,
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
                TutorialSignal.FirstFootprint, TutorialSignal.SprintStarted, TutorialSignal.LookedBack,
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
                TutorialSignal.FirstFootprint, TutorialSignal.SprintStarted, TutorialSignal.LookedBack,
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
                TutorialSignal.EyesOpened, TutorialSignal.FirstFootprint, TutorialSignal.SprintStarted,
                TutorialSignal.LookedBack, TutorialSignal.ObstacleAhead, TutorialSignal.CorridorAEndReached, TutorialSignal.NearTerminal
            };

            foreach (TutorialSignal signal in signals)
            {
                sequencer.Raise(signal);
                Pump(sequencer, 0f);
            }

            Assert.AreEqual(TutorialBeatState.Pending, sequencer.GetMainState("4-1"));
        }

        [Test]
        public void CreateDefault_RunAheadToSecurityNode_KeepsStageTwoAndGuidesBack()
        {
            // 2단계 장애물(2-9)·복도 끝(2-10)을 지나치지 않고 바로 분전반으로 뛰어도, 2-9·2-10 은 건너뛰지 않는다.
            // 3-1 은 기다리고, 안내 상자는 아직 안 한 가장 이른 단계(2-9 장애물)를 가리킨다.
            List<string> started = new List<string>();
            TutorialSequencer sequencer = new TutorialSequencer(TutorialScriptLibrary.CreateDefault());
            sequencer.LineStarted += line => started.Add(line.Id);
            TutorialSignal[] signals =
            {
                TutorialSignal.EyesOpened, TutorialSignal.FirstFootprint, TutorialSignal.SprintStarted,
                TutorialSignal.LookedBack, TutorialSignal.NearSecurityNode
            };

            foreach (TutorialSignal signal in signals)
            {
                sequencer.Raise(signal);
                Pump(sequencer, 0f);
            }

            Assert.AreEqual(TutorialBeatState.Pending, sequencer.GetMainState("2-9"));
            Assert.AreEqual(TutorialBeatState.Pending, sequencer.GetMainState("2-10"));
            Assert.AreEqual(TutorialBeatState.Pending, sequencer.GetMainState("3-1"));
            Assert.AreEqual(TutorialSignal.ObstacleAhead, TutorialGuidePlanner.Choose(sequencer, signal => true));

            // 되돌아가서 순서대로 하면 2-9 → 2-10 → 3-1 이 모두 재생된다.
            foreach (TutorialSignal signal in new[]
                     { TutorialSignal.ObstacleAhead, TutorialSignal.CorridorAEndReached, TutorialSignal.NearSecurityNode })
            {
                sequencer.Raise(signal);
                Pump(sequencer, 0f);
            }

            int obstacle = started.IndexOf("2-9");
            int corridor = started.IndexOf("2-10a");
            int node = started.IndexOf("3-1");
            Assert.IsTrue(obstacle >= 0 && obstacle < corridor && corridor < node, string.Join(",", started));
        }

        [Test]
        public void CreateDefault_NodeOpenedBeforeStageTwoDone_PlaysAlreadyOpenLineInstead()
        {
            // 2단계를 마치기 전에 분전반부터 열면, 2단계를 마친 뒤 3-0 "이미 여셨군요"가 나오고
            // 3-1 접속 안내·3-3 연결 대사는 건너뛴 채 단말기(4-1)로 이어진다.
            List<string> started = new List<string>();
            TutorialSequencer sequencer = new TutorialSequencer(TutorialScriptLibrary.CreateDefault());
            sequencer.LineStarted += line => started.Add(line.Id);
            TutorialSignal[] signals =
            {
                TutorialSignal.EyesOpened, TutorialSignal.FirstFootprint, TutorialSignal.SprintStarted,
                TutorialSignal.LookedBack, TutorialSignal.NearSecurityNode, TutorialSignal.SecurityNodeConnected,
                TutorialSignal.ObstacleAhead, TutorialSignal.CorridorAEndReached,
                TutorialSignal.SecurityNodeConnected, // 분전반은 연결된 동안 계속 알린다
                TutorialSignal.NearTerminal
            };

            foreach (TutorialSignal signal in signals)
            {
                sequencer.Raise(signal);
                Pump(sequencer, 0f);
            }

            CollectionAssert.Contains(started, "3-0");
            CollectionAssert.DoesNotContain(started, "3-1");
            CollectionAssert.DoesNotContain(started, "3-3");
            Assert.AreEqual(TutorialBeatState.Skipped, sequencer.GetMainState("3-1"));
            Assert.AreEqual(TutorialBeatState.Skipped, sequencer.GetMainState("3-3"));
            Assert.IsTrue(started.IndexOf("2-10a") < started.IndexOf("3-0"), string.Join(",", started));
            Assert.AreEqual(TutorialBeatState.Done, sequencer.GetMainState("4-1"));
        }

        [Test]
        public void CreateDefault_KeyPickedBeforeKeyLine_StillPlaysKeyLines()
        {
            // 전리품 대사(5-3)가 나오기 전에 열쇠부터 주우면 5-3 은 건너뛰고, 열쇠 안내(5-7)와 설명(5-9)이 순서대로 나온다.
            List<string> started = new List<string>();
            TutorialSequencer sequencer = new TutorialSequencer(TutorialScriptLibrary.CreateDefault());
            sequencer.LineStarted += line => started.Add(line.Id);
            TutorialSignal[] signals =
            {
                TutorialSignal.EyesOpened, TutorialSignal.FirstFootprint, TutorialSignal.SprintStarted,
                TutorialSignal.LookedBack, TutorialSignal.ObstacleAhead, TutorialSignal.CorridorAEndReached,
                TutorialSignal.NearSecurityNode, TutorialSignal.SecurityNodeConnected, TutorialSignal.NearTerminal,
                TutorialSignal.TerminalOpened, TutorialSignal.DoorHackStarted, TutorialSignal.DoorHackSucceeded,
                TutorialSignal.DoorOpened, TutorialSignal.BlueDoorAhead, TutorialSignal.LootRoomEntered,
                TutorialSignal.KeyAcquired, TutorialSignal.ItemLooted, TutorialSignal.KeyAcquired
            };

            foreach (TutorialSignal signal in signals)
            {
                sequencer.Raise(signal);
                Pump(sequencer, 0f);
                Pump(sequencer, 3.5f);
            }

            Assert.AreEqual(TutorialBeatState.Skipped, sequencer.GetMainState("5-3"));
            int keyGuide = started.IndexOf("5-7");
            int keyInfo = started.IndexOf("5-9");
            Assert.IsTrue(keyGuide >= 0 && keyGuide < keyInfo, string.Join(",", started));
        }

        [Test]
        public void CreateDefault_KeyOnlyWithoutLoot_SkipsLootLineAndGoesToVault()
        {
            // 전리품을 하나도 안 줍고 열쇠만 챙기면 5-3(전리품)은 건너뛰고 열쇠·금고 쪽(5-7~)으로 이어진다.
            // 열쇠 근처에 가기만 해서는(NearKey) 5-3 을 건너뛰지 않는다.
            List<string> started = new List<string>();
            TutorialSequencer sequencer = StartedAtDoorHack(started);

            foreach (TutorialSignal signal in new[]
                     {
                         TutorialSignal.DoorHackSucceeded, TutorialSignal.DoorOpened, TutorialSignal.BlueDoorAhead,
                         TutorialSignal.LootRoomEntered, TutorialSignal.NearKey
                     })
            {
                sequencer.Raise(signal);
                Pump(sequencer, 0f);
            }

            Assert.AreEqual(TutorialBeatState.Pending, sequencer.GetMainState("5-3"));
            Assert.AreEqual(TutorialBeatState.Pending, sequencer.GetMainState("5-7"));

            foreach (TutorialSignal signal in new[]
                     {
                         TutorialSignal.KeyAcquired, TutorialSignal.KeyAcquired, TutorialSignal.LootRoomExiting,
                         TutorialSignal.VaultCorridorEntered, TutorialSignal.VaultFront
                     })
            {
                sequencer.Raise(signal);
                Pump(sequencer, 0f);
                Pump(sequencer, 3.5f);
            }

            Assert.AreEqual(TutorialBeatState.Skipped, sequencer.GetMainState("5-3"));
            CollectionAssert.DoesNotContain(started, "5-3");

            foreach (string id in new[] { "5-7", "5-9", "5-10", "6-1", "7-1" })
            {
                Assert.AreEqual(TutorialBeatState.Done, sequencer.GetMainState(id), id);
            }

            for (int i = 0; i < sequencer.Hints.Count; i++)
            {
                Assert.AreNotEqual("5-2", sequencer.Hints[i].Line.Id, "전리품 안내가 남아 있으면 안 된다");
            }
        }

        [Test]
        public void CreateDefault_WrongDoorHack_PlaysWrongDoorLineOnce()
        {
            // 4-6a: 위쪽 복도의 문이 아닌 격벽을 먼저 골라도 해킹은 진행되고, 안내 한 줄만 끼어든다.
            List<string> started = new List<string>();
            TutorialSequencer sequencer = StartedAtDoorHack(started);

            sequencer.Raise(TutorialSignal.WrongDoorHackStarted);
            Pump(sequencer, 0f);

            Assert.AreEqual("4-6a", started[started.Count - 1]);

            sequencer.Raise(TutorialSignal.WrongDoorHackStarted);
            Pump(sequencer, 0f);

            Assert.AreEqual(1, started.FindAll(id => id == "4-6a").Count);
        }

        [Test]
        public void CreateDefault_SecondDoorUnlocked_PlaysExitLineAfterOpenLine()
        {
            // 4-13(나머지 철문도 해제) → 두 문이 모두 해금되면 4-14(나와서 이동). 첫 해킹 성공만으로는 4-14 가 열리지 않는다.
            List<string> started = new List<string>();
            TutorialSequencer sequencer = StartedAtDoorHack(started);

            sequencer.Raise(TutorialSignal.DoorHackSucceeded);
            Pump(sequencer, 0f);
            sequencer.Raise(TutorialSignal.DoorOpened);
            Pump(sequencer, 0f);

            Assert.AreEqual("4-13", started[started.Count - 1]);
            CollectionAssert.DoesNotContain(started, "4-14");

            sequencer.Raise(TutorialSignal.AllDoorsUnlocked);
            Pump(sequencer, 0f);

            // 둘째 문은 해금만 됐고 아직 닫혀 있다 → 열라는 안내(4-13b)가 먼저 나온다.
            Assert.AreEqual("4-13b", started[started.Count - 1]);
            CollectionAssert.DoesNotContain(started, "4-14");

            sequencer.Raise(TutorialSignal.AllDoorsOpened);
            Pump(sequencer, 0f);

            Assert.AreEqual("4-14", started[started.Count - 1]);
        }

        [Test]
        public void CreateDefault_SecondDoorSkipped_StillReachesBlueDoorLine()
        {
            // 나머지 문을 해금하지 않고 나가도 막히지 않는다(4-14 는 건너뛴다).
            List<string> started = new List<string>();
            TutorialSequencer sequencer = StartedAtDoorHack(started);

            foreach (TutorialSignal signal in new[]
                     { TutorialSignal.DoorHackSucceeded, TutorialSignal.DoorOpened, TutorialSignal.BlueDoorAhead })
            {
                sequencer.Raise(signal);
                Pump(sequencer, 0f);
            }

            Assert.AreEqual(TutorialBeatState.Skipped, sequencer.GetMainState("4-14"));
            Assert.AreEqual("5-1", started[started.Count - 1]);
        }

        [Test]
        public void CreateDefault_DepartWithoutCatnip_PlaysRejectLine()
        {
            // 7-14: 본부에서 출발 버튼을 눌렀는데 캣닢이 없을 때.
            List<string> started = new List<string>();
            TutorialSequencer sequencer = new TutorialSequencer(TutorialScriptLibrary.CreateDefault());
            sequencer.LineStarted += line => started.Add(line.Id);

            foreach (TutorialSignal signal in HappyPathSignals)
            {
                sequencer.Raise(signal);
                Pump(sequencer, 0f);
                Pump(sequencer, 3.5f);

                if (signal == TutorialSignal.VaultFront)
                {
                    break;
                }
            }

            sequencer.Raise(TutorialSignal.DepartRejectedNoCatnip);
            Pump(sequencer, 0f);

            Assert.AreEqual("7-15b", started[started.Count - 1]);
        }
    }
}

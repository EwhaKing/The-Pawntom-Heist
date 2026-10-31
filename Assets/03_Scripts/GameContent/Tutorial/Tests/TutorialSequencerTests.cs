using System.Collections.Generic;
using NUnit.Framework;
using Pawntom.Tutorial.Core;

namespace Pawntom.Tutorial.Tests
{
    public class TutorialSequencerTests
    {
        private const TutorialSpeaker Hq = TutorialSpeaker.Command;

        [Test]
        public void RestartStage_ReplaysFirstLineAndPreservesEarlierStageCompletion()
        {
            var sequencer = Make(
                TutorialBeat.Main("a", 1, TutorialSignal.None, L("a")),
                TutorialBeat.Main("b", 2, TutorialSignal.CorridorAEndReached, L("b")),
                TutorialBeat.Main("c", 2, TutorialSignal.Jumped, L("c")));
            sequencer.RestartStage(2);
            Assert.AreEqual("b", sequencer.CurrentLine.Id);
            Assert.AreEqual(TutorialBeatState.Done, sequencer.GetMainState("a"));
            Assert.AreEqual(TutorialBeatState.Pending, sequencer.GetMainState("c"));
            sequencer.Raise(TutorialSignal.Jumped);
            Pump(sequencer);
            sequencer.RestartStage(2);
            Assert.AreEqual("b", sequencer.CurrentLine.Id);
        }

        [Test]
        public void RestartStage_DiscardsQueuedSignalsAndOldHints()
        {
            var sequencer = Make(
                TutorialBeat.Main("a", 2, TutorialSignal.None,
                    L("a").WithHint("Jump", TutorialSignal.Jumped)),
                TutorialBeat.Main("b", 2, TutorialSignal.Jumped, L("b")));
            Pump(sequencer);
            Assert.AreEqual(1, sequencer.Hints.Count);
            sequencer.Raise(TutorialSignal.Jumped);
            sequencer.RestartStage(2);
            Assert.AreEqual(0, sequencer.Hints.Count);
            Pump(sequencer);
            Assert.IsNull(sequencer.CurrentLine);
            Assert.AreEqual(TutorialBeatState.Pending, sequencer.GetMainState("b"));
        }

        [Test]
        public void RestartStage_InvalidStageDoesNotChangeCurrentLine()
        {
            var sequencer = Make(TutorialBeat.Main("a", 1, TutorialSignal.None, L("a")));
            sequencer.Tick(0f);
            Assert.Throws<System.ArgumentOutOfRangeException>(() => sequencer.RestartStage(99));
            Assert.AreEqual("a", sequencer.CurrentLine.Id);
        }

        private static TutorialLine L(string id)
        {
            return new TutorialLine(id, Hq, "대사 " + id);
        }

        private static TutorialSequencer Make(params TutorialBeat[] beats)
        {
            return new TutorialSequencer(new TutorialScript(beats));
        }

        private static List<string> RecordLines(TutorialSequencer sequencer)
        {
            List<string> started = new List<string>();
            sequencer.LineStarted += line => started.Add(line.Id);
            return started;
        }

        /// <summary>한 틱 흘리고 떠 있는 대사를 모두 즉시 끝낸다.</summary>
        private static void Pump(TutorialSequencer sequencer, float deltaTime = 0f)
        {
            sequencer.Tick(deltaTime);

            int guard = 0;

            while (sequencer.CurrentLine != null && guard++ < 100)
            {
                sequencer.CompleteCurrentLine();
            }
        }

        private static void Signal(TutorialSequencer sequencer, TutorialSignal signal)
        {
            sequencer.Raise(signal);
            Pump(sequencer);
        }

        // ── 메인 진행 ──

        [Test]
        public void Tick_BeforeTrigger_StartsNoLine()
        {
            TutorialSequencer sequencer = Make(TutorialBeat.Main("a", 1, TutorialSignal.EyesOpened, L("a")));

            sequencer.Tick(1f);

            Assert.IsNull(sequencer.CurrentLine);
        }

        [Test]
        public void Tick_AfterTrigger_StartsFirstLine()
        {
            TutorialSequencer sequencer = Make(TutorialBeat.Main("a", 1, TutorialSignal.EyesOpened, L("a"), L("b")));

            sequencer.Raise(TutorialSignal.EyesOpened);
            sequencer.Tick(0f);

            Assert.AreEqual("a", sequencer.CurrentLine.Id);
        }

        [Test]
        public void CompleteCurrentLine_InChain_StartsNextLineImmediately()
        {
            TutorialSequencer sequencer = Make(TutorialBeat.Main("a", 1, TutorialSignal.EyesOpened, L("a"), L("b")));
            sequencer.Raise(TutorialSignal.EyesOpened);
            sequencer.Tick(0f);

            sequencer.CompleteCurrentLine();

            Assert.AreEqual("b", sequencer.CurrentLine.Id);
        }

        [Test]
        public void MainWithoutTrigger_AfterPreviousBeat_StartsImmediately()
        {
            TutorialSequencer sequencer = Make(
                TutorialBeat.Main("a", 1, TutorialSignal.EyesOpened, L("a")),
                TutorialBeat.Main("b", 2, TutorialSignal.None, L("b")));
            sequencer.Raise(TutorialSignal.EyesOpened);
            sequencer.Tick(0f);

            sequencer.CompleteCurrentLine();

            Assert.AreEqual("b", sequencer.CurrentLine.Id);
        }

        [Test]
        public void Skippable_LaterTriggerArrivesFirst_SkipsWaitingBeat()
        {
            TutorialSequencer sequencer = Make(
                TutorialBeat.Main("a", 2, TutorialSignal.None, L("a")),
                TutorialBeat.Main("b", 2, TutorialSignal.ObstacleAhead, L("b")).AsSkippable(),
                TutorialBeat.Main("c", 2, TutorialSignal.CorridorAEndReached, L("c")));
            Pump(sequencer);

            Signal(sequencer, TutorialSignal.CorridorAEndReached);

            Assert.AreEqual(TutorialBeatState.Skipped, sequencer.GetMainState("b"));
        }

        [Test]
        public void NonSkippable_LaterTriggerArrivesFirst_KeepsWaiting()
        {
            TutorialSequencer sequencer = Make(
                TutorialBeat.Main("a", 2, TutorialSignal.None, L("a")),
                TutorialBeat.Main("b", 2, TutorialSignal.ObstacleAhead, L("b")),
                TutorialBeat.Main("c", 2, TutorialSignal.CorridorAEndReached, L("c")));
            Pump(sequencer);

            Signal(sequencer, TutorialSignal.CorridorAEndReached);

            Assert.AreEqual(TutorialBeatState.Pending, sequencer.GetMainState("c"));
        }

        [Test]
        public void Skippable_TriggerTwoStagesAhead_IsIgnored()
        {
            // 본부 스폰 직후 단말기 근처 신호가 2단계에서 4단계로 튀게 만들면 안 된다.
            TutorialSequencer sequencer = Make(
                TutorialBeat.Main("a", 2, TutorialSignal.None, L("a")),
                TutorialBeat.Main("b", 2, TutorialSignal.ObstacleAhead, L("b")).AsSkippable(),
                TutorialBeat.Main("c", 3, TutorialSignal.NearSecurityNode, L("c")).AsSkippable(),
                TutorialBeat.Main("d", 4, TutorialSignal.NearTerminal, L("d")));
            Pump(sequencer);

            Signal(sequencer, TutorialSignal.NearTerminal);

            Assert.AreEqual(TutorialBeatState.Pending, sequencer.GetMainState("d"));
        }

        [Test]
        public void Anchor_TriggerTwoStagesAhead_IsReached()
        {
            TutorialSequencer sequencer = Make(
                TutorialBeat.Main("a", 5, TutorialSignal.None, L("a")),
                TutorialBeat.Main("b", 5, TutorialSignal.InventoryThreeFilled, L("b")).AsSkippable(),
                TutorialBeat.Main("c", 6, TutorialSignal.VaultCorridorEntered, L("c")).AsSkippable(),
                TutorialBeat.Main("d", 7, TutorialSignal.CatnipAcquired, L("d")).AsAnchor());
            Pump(sequencer);

            Signal(sequencer, TutorialSignal.CatnipAcquired);

            Assert.AreEqual(TutorialBeatState.Done, sequencer.GetMainState("d"));
        }

        [Test]
        public void RequiresPlayed_WhenRequiredBeatWasSkipped_SkipsBeat()
        {
            TutorialSequencer sequencer = Make(
                TutorialBeat.Main("a", 3, TutorialSignal.None, L("a")),
                TutorialBeat.Main("chase", 3, TutorialSignal.EnemyChasing, L("chase")).AsSkippable(),
                TutorialBeat.Main("safe", 3, TutorialSignal.SafeInMainbase, L("safe")).AsSkippable().RequiringPlayed("chase"),
                TutorialBeat.Main("terminal", 4, TutorialSignal.NearTerminal, L("terminal")));
            Pump(sequencer);

            Signal(sequencer, TutorialSignal.NearTerminal);

            Assert.AreEqual(TutorialBeatState.Skipped, sequencer.GetMainState("safe"));
        }

        [Test]
        public void RequiresPlayed_WhenRequiredBeatPending_IsNotACandidate()
        {
            TutorialSequencer sequencer = Make(
                TutorialBeat.Main("a", 3, TutorialSignal.None, L("a")),
                TutorialBeat.Main("chase", 3, TutorialSignal.EnemyChasing, L("chase")).AsSkippable(),
                TutorialBeat.Main("safe", 3, TutorialSignal.SafeInMainbase, L("safe")).AsSkippable().RequiringPlayed("chase"),
                TutorialBeat.Main("terminal", 4, TutorialSignal.NearTerminal, L("terminal")));
            Pump(sequencer);

            Signal(sequencer, TutorialSignal.SafeInMainbase);

            Assert.AreEqual(TutorialBeatState.Pending, sequencer.GetMainState("chase"));
        }

        [Test]
        public void FreshTrigger_SignalBeforePreviousBeatStarted_IsIgnored()
        {
            TutorialSequencer sequencer = Make(
                TutorialBeat.Main("a", 2, TutorialSignal.None, L("a")),
                TutorialBeat.Main("b", 2, TutorialSignal.FirstFootprint, L("b")),
                TutorialBeat.Main("c", 2, TutorialSignal.LookedBack, L("c")).WithFreshTrigger());
            Pump(sequencer);

            // a 재생이 끝난 뒤, b 시작 전에 돌아봤다 — b("뒤를 돌아보십시오") 이후가 아니므로 인정하지 않는다.
            sequencer.Raise(TutorialSignal.LookedBack);
            sequencer.Raise(TutorialSignal.FirstFootprint);
            Pump(sequencer);

            Assert.AreEqual(TutorialBeatState.Pending, sequencer.GetMainState("c"));
        }

        [Test]
        public void Trigger_ArrivingDuringPreviousBeatPlayback_IsKept()
        {
            // 4-7(해킹 시작) 재생 중 해킹이 바로 성공해도 4-8 이 이어져야 한다.
            TutorialSequencer sequencer = Make(
                TutorialBeat.Main("a", 4, TutorialSignal.None, L("a")),
                TutorialBeat.Main("start", 4, TutorialSignal.DoorHackStarted, L("start")).AsSkippable(),
                TutorialBeat.Main("success", 4, TutorialSignal.DoorHackSucceeded, L("success")));
            Pump(sequencer);

            sequencer.Raise(TutorialSignal.DoorHackStarted);
            sequencer.Tick(0f);
            sequencer.Raise(TutorialSignal.DoorHackSucceeded);
            Pump(sequencer);

            Assert.AreEqual(TutorialBeatState.Done, sequencer.GetMainState("success"));
        }

        [Test]
        public void Trigger_FromTwoBeatsAgo_IsIgnored()
        {
            TutorialSequencer sequencer = Make(
                TutorialBeat.Main("a", 2, TutorialSignal.None, L("a")),
                TutorialBeat.Main("b", 2, TutorialSignal.SprintStarted, L("b")),
                TutorialBeat.Main("c", 2, TutorialSignal.Jumped, L("c")),
                TutorialBeat.Main("d", 3, TutorialSignal.EnemyChasing, L("d")));
            Pump(sequencer);

            Signal(sequencer, TutorialSignal.EnemyChasing); // a 이후 — d 에게는 오래된 신호
            Signal(sequencer, TutorialSignal.SprintStarted);
            Signal(sequencer, TutorialSignal.Jumped);

            Assert.AreEqual(TutorialBeatState.Pending, sequencer.GetMainState("d"));
        }

        [Test]
        public void ActionRequested_AfterLineCompletes_CarriesLineAction()
        {
            TutorialSequencer sequencer = Make(TutorialBeat.Main("a", 1, TutorialSignal.EyesOpened,
                L("a").WithAction(TutorialLineAction.EnableControl)));
            TutorialLineAction received = TutorialLineAction.None;
            sequencer.ActionRequested += (action, line) => received = action;

            Signal(sequencer, TutorialSignal.EyesOpened);

            Assert.AreEqual(TutorialLineAction.EnableControl, received);
        }

        [Test]
        public void PreDelay_BeforeElapsed_HoldsLine()
        {
            TutorialSequencer sequencer = Make(TutorialBeat.Main("a", 6, TutorialSignal.None, L("a"), L("b").WithPreDelay(3f)));
            Pump(sequencer);

            sequencer.Tick(2.9f);

            Assert.IsNull(sequencer.CurrentLine);
        }

        [Test]
        public void PreDelay_AfterElapsed_StartsLine()
        {
            TutorialSequencer sequencer = Make(TutorialBeat.Main("a", 6, TutorialSignal.None, L("a"), L("b").WithPreDelay(3f)));
            Pump(sequencer);

            sequencer.Tick(2f);
            sequencer.Tick(1.1f);

            Assert.AreEqual("b", sequencer.CurrentLine.Id);
        }

        [Test]
        public void Finished_AfterLastMainBeat_IsTrue()
        {
            TutorialSequencer sequencer = Make(TutorialBeat.Main("a", 1, TutorialSignal.EyesOpened, L("a")));

            Signal(sequencer, TutorialSignal.EyesOpened);

            Assert.IsTrue(sequencer.IsFinished);
        }

        [Test]
        public void ForceAdvance_WhileWaiting_StartsPendingMainWithoutTrigger()
        {
            TutorialSequencer sequencer = Make(TutorialBeat.Main("a", 1, TutorialSignal.EyesOpened, L("a")));

            sequencer.ForceAdvance();

            Assert.AreEqual("a", sequencer.CurrentLine.Id);
        }

        // ── 키 안내 문구 ──

        [Test]
        public void Hint_WhileLineShowing_IsNotListed()
        {
            TutorialSequencer sequencer = Make(TutorialBeat.Main("a", 2, TutorialSignal.None,
                L("a").WithHint("[Shift]", TutorialSignal.SprintStarted)));

            sequencer.Tick(0f);

            Assert.AreEqual(0, sequencer.Hints.Count);
        }

        [Test]
        public void Hint_AfterLineFinishes_IsListed()
        {
            TutorialSequencer sequencer = Make(TutorialBeat.Main("a", 2, TutorialSignal.None,
                L("a").WithHint("[Shift]", TutorialSignal.SprintStarted)));

            Pump(sequencer);

            Assert.AreEqual(1, sequencer.Hints.Count);
        }

        [Test]
        public void Hint_ActionDoneDuringLine_IsNeverListed()
        {
            TutorialSequencer sequencer = Make(TutorialBeat.Main("a", 2, TutorialSignal.None,
                L("a").WithHint("[Shift]", TutorialSignal.SprintStarted)));
            sequencer.Tick(0f);

            sequencer.Raise(TutorialSignal.SprintStarted);
            Pump(sequencer);

            Assert.AreEqual(0, sequencer.Hints.Count);
        }

        [Test]
        public void Hint_CompletionSignal_RemovesHintImmediately()
        {
            TutorialSequencer sequencer = Make(TutorialBeat.Main("a", 2, TutorialSignal.None,
                L("a").WithHint("[Shift]", TutorialSignal.SprintStarted)));
            Pump(sequencer);
            TutorialHintRemoval reason = TutorialHintRemoval.Expired;
            sequencer.HintRemoved += (hint, why) => reason = why;

            Signal(sequencer, TutorialSignal.SprintStarted);

            Assert.AreEqual(TutorialHintRemoval.Completed, reason);
        }

        [Test]
        public void TimedHint_WhileDialogueShowing_DoesNotExpire()
        {
            TutorialSequencer sequencer = Make(
                TutorialBeat.Main("a", 2, TutorialSignal.None, L("a").WithTimedHint("[스태미나 바]", 2f)),
                TutorialBeat.Side("s", 2, TutorialSignal.StaminaBelowHalf, "a", null, L("s")));
            Pump(sequencer);

            sequencer.Raise(TutorialSignal.StaminaBelowHalf);
            sequencer.Tick(0f); // 곁가지 대사가 떠 있는 동안
            sequencer.Tick(5f);

            Assert.AreEqual(1, sequencer.Hints.Count);
        }

        [Test]
        public void TimedHint_WhileIdle_ExpiresAfterSeconds()
        {
            TutorialSequencer sequencer = Make(TutorialBeat.Main("a", 2, TutorialSignal.None,
                L("a").WithTimedHint("[스태미나 바]", 2f)));
            Pump(sequencer);

            sequencer.Tick(2.1f);

            Assert.AreEqual(0, sequencer.Hints.Count);
        }

        [Test]
        public void Hint_NextStageMainStarts_IsRemoved()
        {
            TutorialSequencer sequencer = Make(
                TutorialBeat.Main("a", 2, TutorialSignal.None, L("a").WithHint("[Space]", TutorialSignal.Jumped)),
                TutorialBeat.Main("b", 3, TutorialSignal.NearSecurityNode, L("b")));
            Pump(sequencer);

            Signal(sequencer, TutorialSignal.NearSecurityNode);

            Assert.AreEqual(0, sequencer.Hints.Count);
        }

        [Test]
        public void Hint_BeyondCapacity_SupersedesOldest()
        {
            TutorialSequencer sequencer = Make(TutorialBeat.Main("a", 7, TutorialSignal.None,
                L("a").WithHint("첫째", TutorialSignal.Jumped),
                L("b").WithHint("둘째", TutorialSignal.Jumped),
                L("c").WithHint("셋째", TutorialSignal.PlayerInMainbase)));

            Pump(sequencer);

            Assert.AreEqual("b", sequencer.Hints[0].Line.Id);
        }

        // ── 곁가지 ──

        [Test]
        public void Side_BeforeWindowOpens_DoesNotPlay()
        {
            TutorialSequencer sequencer = Make(
                TutorialBeat.Main("a", 2, TutorialSignal.None, L("a")),
                TutorialBeat.Main("b", 2, TutorialSignal.SprintStarted, L("b")),
                TutorialBeat.Side("s", 2, TutorialSignal.StaminaBelowHalf, "b", null, L("s")));
            List<string> started = RecordLines(sequencer);
            Pump(sequencer);

            Signal(sequencer, TutorialSignal.StaminaBelowHalf);

            CollectionAssert.DoesNotContain(started, "s");
        }

        [Test]
        public void Side_InsideWindow_PlaysAfterCurrentLine()
        {
            TutorialSequencer sequencer = Make(
                TutorialBeat.Main("a", 2, TutorialSignal.None, L("a"), L("a2")),
                TutorialBeat.Side("s", 2, TutorialSignal.StaminaBelowHalf, "a", null, L("s")));
            List<string> started = RecordLines(sequencer);
            sequencer.Tick(0f);
            sequencer.Raise(TutorialSignal.StaminaBelowHalf);
            Pump(sequencer);

            CollectionAssert.AreEqual(new[] { "a", "a2", "s" }, started);
        }

        [Test]
        public void Side_AfterWindowCloses_DoesNotPlay()
        {
            TutorialSequencer sequencer = Make(
                TutorialBeat.Main("a", 2, TutorialSignal.None, L("a")),
                TutorialBeat.Main("b", 3, TutorialSignal.NearSecurityNode, L("b")),
                TutorialBeat.Side("s", 2, TutorialSignal.StaminaBelowHalf, "a", "b", L("s")));
            List<string> started = RecordLines(sequencer);
            Pump(sequencer);
            Signal(sequencer, TutorialSignal.NearSecurityNode);

            Signal(sequencer, TutorialSignal.StaminaBelowHalf);

            CollectionAssert.DoesNotContain(started, "s");
        }

        [Test]
        public void Side_NonRepeatable_PlaysOnlyOnce()
        {
            TutorialSequencer sequencer = Make(
                TutorialBeat.Main("a", 2, TutorialSignal.None, L("a")),
                TutorialBeat.Side("s", 2, TutorialSignal.StaminaBelowHalf, "a", null, L("s")));
            Pump(sequencer);

            Signal(sequencer, TutorialSignal.StaminaBelowHalf);
            Signal(sequencer, TutorialSignal.StaminaBelowHalf);

            Assert.AreEqual(1, sequencer.GetSidePlayCount("s"));
        }

        [Test]
        public void Side_RepeatableWithinCooldown_DoesNotReplay()
        {
            TutorialSequencer sequencer = Make(
                TutorialBeat.Main("a", 4, TutorialSignal.None, L("a")),
                TutorialBeat.Side("fail", 4, TutorialSignal.DoorHackFailed, "a", null, L("fail")).AsRepeatable(2f));
            Pump(sequencer);

            Signal(sequencer, TutorialSignal.DoorHackFailed);
            sequencer.Tick(1f);
            Signal(sequencer, TutorialSignal.DoorHackFailed);

            Assert.AreEqual(1, sequencer.GetSidePlayCount("fail"));
        }

        [Test]
        public void Side_RepeatableAfterCooldown_Replays()
        {
            TutorialSequencer sequencer = Make(
                TutorialBeat.Main("a", 4, TutorialSignal.None, L("a")),
                TutorialBeat.Side("fail", 4, TutorialSignal.DoorHackFailed, "a", null, L("fail")).AsRepeatable(2f));
            Pump(sequencer);

            Signal(sequencer, TutorialSignal.DoorHackFailed);
            sequencer.Tick(2.5f);
            Signal(sequencer, TutorialSignal.DoorHackFailed);

            Assert.AreEqual(2, sequencer.GetSidePlayCount("fail"));
        }

        [Test]
        public void Variant_WhenSideWasPlayed_UsesVariantLines()
        {
            TutorialSequencer sequencer = Make(
                TutorialBeat.Main("a", 4, TutorialSignal.None, L("a")),
                TutorialBeat.Main("ok", 4, TutorialSignal.DoorHackSucceeded, L("4-8")).WithVariant("fail", L("4-11")),
                TutorialBeat.Side("fail", 4, TutorialSignal.DoorHackFailed, "a", "ok", L("4-10")));
            List<string> started = RecordLines(sequencer);
            Pump(sequencer);
            Signal(sequencer, TutorialSignal.DoorHackFailed);

            Signal(sequencer, TutorialSignal.DoorHackSucceeded);

            Assert.AreEqual("4-11", started[started.Count - 1]);
        }

        [Test]
        public void Variant_WhenSideNotPlayed_UsesNormalLines()
        {
            TutorialSequencer sequencer = Make(
                TutorialBeat.Main("a", 4, TutorialSignal.None, L("a")),
                TutorialBeat.Main("ok", 4, TutorialSignal.DoorHackSucceeded, L("4-8")).WithVariant("fail", L("4-11")),
                TutorialBeat.Side("fail", 4, TutorialSignal.DoorHackFailed, "a", "ok", L("4-10")));
            List<string> started = RecordLines(sequencer);
            Pump(sequencer);

            Signal(sequencer, TutorialSignal.DoorHackSucceeded);

            Assert.AreEqual("4-8", started[started.Count - 1]);
        }

        // ── 상황별 반복 대사: 순환·긴급·안내 재표시 ──

        private static TutorialBeat IdleReminder()
        {
            return TutorialBeat.Side("idle", 0, TutorialSignal.IdleNoMove, "a", null, L("idle-1"), L("idle-2"))
                .AsRotating()
                .AsRepeatable(0f)
                .WithHintRecall();
        }

        [Test]
        public void Side_Rotating_PlaysOneLinePerTriggerInTurn()
        {
            TutorialSequencer sequencer = Make(TutorialBeat.Main("a", 2, TutorialSignal.None, L("a")), IdleReminder());
            List<string> started = RecordLines(sequencer);
            Pump(sequencer);

            Signal(sequencer, TutorialSignal.IdleNoMove);
            Signal(sequencer, TutorialSignal.IdleNoMove);
            Signal(sequencer, TutorialSignal.IdleNoMove);

            // 같은 대사가 연달아 나오지 않는다.
            CollectionAssert.AreEqual(new[] { "a", "idle-1", "idle-2", "idle-1" }, started);
        }

        [Test]
        public void Side_Urgent_InterruptsCurrentLine()
        {
            TutorialSequencer sequencer = Make(
                TutorialBeat.Main("a", 2, TutorialSignal.None, L("a"), L("a2")),
                TutorialBeat.Side("death", 0, TutorialSignal.PlayerDied, "a", null, L("death")).AsUrgent());
            sequencer.Tick(0f);

            sequencer.Raise(TutorialSignal.PlayerDied);
            sequencer.Tick(0f);

            Assert.AreEqual("death", sequencer.CurrentLine.Id);
        }

        [Test]
        public void Side_Urgent_KeepsRemainingQueuedLinesAfterIt()
        {
            TutorialSequencer sequencer = Make(
                TutorialBeat.Main("a", 2, TutorialSignal.None, L("a"), L("a2")),
                TutorialBeat.Side("death", 0, TutorialSignal.PlayerDied, "a", null, L("death")).AsUrgent());
            List<string> started = RecordLines(sequencer);
            sequencer.Tick(0f);
            sequencer.Raise(TutorialSignal.PlayerDied);

            Pump(sequencer);

            CollectionAssert.AreEqual(new[] { "a", "death", "a2" }, started);
        }

        [Test]
        public void HintRecall_AfterHintWasPushedOut_ReaddsIt()
        {
            // a 의 행동 안내가 시간제 안내 두 개에 밀려났다가, 상황 대사 뒤 다시 걸린다.
            TutorialSequencer sequencer = Make(
                TutorialBeat.Main("a", 2, TutorialSignal.None,
                    L("a").WithHint("[Space]", TutorialSignal.Jumped),
                    L("b").WithTimedHint("시간1", 9f),
                    L("c").WithTimedHint("시간2", 9f)),
                IdleReminder());
            Pump(sequencer);

            Signal(sequencer, TutorialSignal.IdleNoMove);

            Assert.AreEqual("a", sequencer.Hints[sequencer.Hints.Count - 1].Line.Id);
        }

        [Test]
        public void HintRecall_AfterActionDone_DoesNotReadd()
        {
            TutorialSequencer sequencer = Make(
                TutorialBeat.Main("a", 2, TutorialSignal.None, L("a").WithHint("[Space]", TutorialSignal.Jumped)),
                IdleReminder());
            Pump(sequencer);
            Signal(sequencer, TutorialSignal.Jumped);

            Signal(sequencer, TutorialSignal.IdleNoMove);

            Assert.AreEqual(0, sequencer.Hints.Count);
        }

        [Test]
        public void HintRecall_HintStillListed_RestartsItsVisibleTime()
        {
            TutorialSequencer sequencer = Make(
                TutorialBeat.Main("a", 2, TutorialSignal.None, L("a").WithHint("[Space]", TutorialSignal.Jumped)),
                IdleReminder());
            Pump(sequencer);
            sequencer.Tick(4f);

            Signal(sequencer, TutorialSignal.IdleNoMove);

            Assert.AreEqual(0f, sequencer.Hints[0].VisibleSeconds, 0.0001f);
        }

        [Test]
        public void HintRecall_TimedHintOnly_ReaddsNothing()
        {
            TutorialSequencer sequencer = Make(
                TutorialBeat.Main("a", 2, TutorialSignal.None, L("a").WithTimedHint("[스태미나 바]", 1f)),
                IdleReminder());
            Pump(sequencer);
            sequencer.Tick(1.5f);

            Signal(sequencer, TutorialSignal.IdleNoMove);

            Assert.AreEqual(0, sequencer.Hints.Count);
        }

        // ── 대본 무결성 ──

        [Test]
        public void Script_DuplicateBeatId_Throws()
        {
            Assert.Throws<System.ArgumentException>(() => new TutorialScript(new[]
            {
                TutorialBeat.Main("a", 1, TutorialSignal.None, L("a")),
                TutorialBeat.Main("a", 1, TutorialSignal.None, L("b"))
            }));
        }

        [Test]
        public void Script_RequiresPlayedPointingForward_Throws()
        {
            Assert.Throws<System.ArgumentException>(() => new TutorialScript(new[]
            {
                TutorialBeat.Main("a", 1, TutorialSignal.None, L("a")).RequiringPlayed("b"),
                TutorialBeat.Main("b", 1, TutorialSignal.None, L("b"))
            }));
        }

        [Test]
        public void Script_SideWindowClosingBeforeOpening_Throws()
        {
            Assert.Throws<System.ArgumentException>(() => new TutorialScript(new[]
            {
                TutorialBeat.Main("a", 1, TutorialSignal.None, L("a")),
                TutorialBeat.Main("b", 1, TutorialSignal.None, L("b")),
                TutorialBeat.Side("s", 1, TutorialSignal.Jumped, "b", "a", L("s"))
            }));
        }
    }
}

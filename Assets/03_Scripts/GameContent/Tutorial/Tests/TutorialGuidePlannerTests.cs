using System;
using NUnit.Framework;
using Pawntom.Tutorial.Core;

namespace Pawntom.Tutorial.Tests
{
    public class TutorialGuidePlannerTests
    {
        private static readonly Func<TutorialSignal, bool> Everything = signal => true;

        private static TutorialSequencer StartedDefault()
        {
            TutorialSequencer sequencer = new TutorialSequencer(TutorialScriptLibrary.CreateDefault());
            sequencer.Raise(TutorialSignal.EyesOpened);
            Pump(sequencer);
            return sequencer;
        }

        private static void Pump(TutorialSequencer sequencer)
        {
            sequencer.Tick(0f);

            int guard = 0;

            while (sequencer.CurrentLine != null && guard++ < 200)
            {
                sequencer.CompleteCurrentLine();
            }
        }

        private static void Signal(TutorialSequencer sequencer, TutorialSignal signal)
        {
            sequencer.Raise(signal);
            Pump(sequencer);
        }

        [Test]
        public void Choose_WaitingForNonLocationBeat_ReturnsNone()
        {
            // 2-2 는 "첫 발자국"을 기다린다 — 갈 곳이 없다.
            Assert.AreEqual(TutorialSignal.None, TutorialGuidePlanner.Choose(StartedDefault(), Everything));
        }

        [Test]
        public void Choose_WaitingForObstacle_PointsToObstacle()
        {
            TutorialSequencer sequencer = StartedDefault();
            Signal(sequencer, TutorialSignal.FirstFootprint);
            Signal(sequencer, TutorialSignal.LookedBack);
            Signal(sequencer, TutorialSignal.SprintStarted);

            Assert.AreEqual(TutorialSignal.ObstacleAhead, TutorialGuidePlanner.Choose(sequencer, Everything));
        }

        [Test]
        public void Choose_ObstacleNotInScene_LooksAheadToCorridorEnd()
        {
            TutorialSequencer sequencer = StartedDefault();
            Signal(sequencer, TutorialSignal.FirstFootprint);
            Signal(sequencer, TutorialSignal.LookedBack);
            Signal(sequencer, TutorialSignal.SprintStarted);

            TutorialSignal chosen = TutorialGuidePlanner.Choose(sequencer, signal => signal != TutorialSignal.ObstacleAhead);

            Assert.AreEqual(TutorialSignal.CorridorAEndReached, chosen);
        }

        [Test]
        public void Choose_ReturnHomeHintActive_PointsToMainbase()
        {
            // 3-4 "Shift키로 달려 본부로 돌아갑시다" 가 걸려 있으면 본부를 가리킨다.
            TutorialSequencer sequencer = StartedDefault();
            TutorialSignal[] path =
            {
                TutorialSignal.FirstFootprint, TutorialSignal.LookedBack, TutorialSignal.SprintStarted,
                TutorialSignal.CorridorAEndReached, TutorialSignal.NearSecurityNode,
                TutorialSignal.SecurityNodeConnected, TutorialSignal.EnemyChasing
            };

            foreach (TutorialSignal signal in path)
            {
                Signal(sequencer, signal);
            }

            Assert.AreEqual(TutorialSignal.PlayerInMainbase, TutorialGuidePlanner.Choose(sequencer, Everything));
        }

        [Test]
        public void Choose_NothingResolvable_ReturnsNone()
        {
            TutorialSequencer sequencer = StartedDefault();
            Signal(sequencer, TutorialSignal.FirstFootprint);
            Signal(sequencer, TutorialSignal.LookedBack);
            Signal(sequencer, TutorialSignal.SprintStarted);

            Assert.AreEqual(TutorialSignal.None, TutorialGuidePlanner.Choose(sequencer, signal => false));
        }

        [TestCase(TutorialSignal.LootRoomExiting, false)]
        [TestCase(TutorialSignal.NearTerminal, true)]
        [TestCase(TutorialSignal.SprintStarted, false)]
        public void IsLocationSignal_Signal_MatchesExpectation(TutorialSignal signal, bool expected)
        {
            Assert.AreEqual(expected, TutorialGuidePlanner.IsLocationSignal(signal));
        }
    }
}

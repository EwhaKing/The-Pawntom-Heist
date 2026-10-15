using System;
using NUnit.Framework;
using Pawntom.Tutorial.Core;

namespace Pawntom.Tutorial.Tests
{
    public class EyelidTimelineTests
    {
        [Test]
        public void Opening_AtStart_IsClosed()
        {
            Assert.AreEqual(0f, EyelidTimeline.CreateOpening().Evaluate(0f), 0.0001f);
        }

        [Test]
        public void Opening_AfterDuration_IsFullyOpen()
        {
            EyelidTimeline opening = EyelidTimeline.CreateOpening();

            Assert.AreEqual(1f, opening.Evaluate(opening.Duration + 1f), 0.0001f);
        }

        [Test]
        public void Opening_MidBlink_IsPartlyClosedAgain()
        {
            // 1.45초 0.26 → 1.7초 0.05: 한 번 떴다가 다시 감긴다.
            EyelidTimeline opening = EyelidTimeline.CreateOpening();

            Assert.Less(opening.Evaluate(1.7f), opening.Evaluate(1.45f));
        }

        [Test]
        public void Closing_AfterDuration_IsClosed()
        {
            EyelidTimeline closing = EyelidTimeline.CreateClosing();

            Assert.AreEqual(0f, closing.Evaluate(closing.Duration), 0.0001f);
        }

        [Test]
        public void Haze_AtStart_IsFull()
        {
            Assert.AreEqual(1f, EyelidTimeline.CreateOpening().Haze(0f), 0.0001f);
        }

        [Test]
        public void Haze_LongAfterOpening_IsClear()
        {
            Assert.AreEqual(0f, EyelidTimeline.CreateOpening().Haze(10f), 0.0001f);
        }

        [Test]
        public void Constructor_UnsortedTimes_Throws()
        {
            Assert.Throws<ArgumentException>(() => new EyelidTimeline(new[] { 1f, 0f }, new[] { 0f, 1f }));
        }
    }
}

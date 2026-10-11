using NUnit.Framework;
using Pawntom.Tutorial.Core;

namespace Pawntom.Tutorial.Tests
{
    public class TypewriterModelTests
    {
        [Test]
        public void Advance_OneSecondAtTenCps_RevealsTenCharacters()
        {
            TypewriterModel model = new TypewriterModel(10f);
            model.Begin("가나다라마바사아자차카타");

            model.Advance(1.0001f);

            Assert.AreEqual(10, model.Visible);
        }

        [Test]
        public void Advance_AfterSentenceEnd_WaitsForPause()
        {
            TypewriterModel model = new TypewriterModel(10f) { SentencePauseSeconds = 0.5f };
            model.Begin("가. 나");

            // '가'(0.1) '.'(0.1) 다음 ' '은 0.1 + 0.5 쉼을 기다린다.
            model.Advance(0.5f);

            Assert.AreEqual(2, model.Visible);
        }

        [Test]
        public void Advance_EllipsisDots_UseShortPause()
        {
            TypewriterModel model = new TypewriterModel(10f) { SentencePauseSeconds = 1f, EllipsisDotPauseSeconds = 0.1f };
            model.Begin("...네");

            model.Advance(0.6001f);

            Assert.AreEqual(3, model.Visible);
        }

        [Test]
        public void Advance_LastCharacterIsPunctuation_CompletesWithoutTrailingPause()
        {
            TypewriterModel model = new TypewriterModel(10f) { SentencePauseSeconds = 5f };
            model.Begin("네.");

            model.Advance(0.2001f);

            Assert.IsTrue(model.IsComplete);
        }

        [Test]
        public void Complete_MidLine_RevealsEverything()
        {
            TypewriterModel model = new TypewriterModel(5f);
            model.Begin("들리십니까");

            model.Complete();

            Assert.AreEqual(5, model.Visible);
        }

        [Test]
        public void EstimateDuration_MatchesAdvanceToCompletion()
        {
            TypewriterModel model = new TypewriterModel(20f);
            const string text = "칙... 들리십니까, 깜냥이 씨.";
            float estimate = model.EstimateDuration(text);
            model.Begin(text);

            model.Advance(estimate + 0.0001f);

            Assert.IsTrue(model.IsComplete);
        }

        [TestCase('가', true)]
        [TestCase('A', true)]
        [TestCase(' ', false)]
        [TestCase('.', false)]
        [TestCase('?', false)]
        public void IsAudible_Character_MatchesExpectation(char c, bool expected)
        {
            Assert.AreEqual(expected, TypewriterModel.IsAudible(c));
        }

        [Test]
        public void HoldSecondsFor_VeryLongText_ClampsToMax()
        {
            Assert.AreEqual(4f, TypewriterModel.HoldSecondsFor(new string('가', 500), 1.5f, 4f, 0.04f), 0.0001f);
        }

        [Test]
        public void HoldSecondsFor_ShortText_ClampsToMin()
        {
            Assert.AreEqual(1.5f, TypewriterModel.HoldSecondsFor("네", 1.5f, 4f, 0f), 0.0001f);
        }
    }
}

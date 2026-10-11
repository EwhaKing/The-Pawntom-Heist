using System.Collections.Generic;
using NUnit.Framework;
using Pawntom.Tutorial.Core;

namespace Pawntom.Tutorial.Tests
{
    public class TutorialHintFormatterTests
    {
        private static readonly Dictionary<string, string> Tokens = new Dictionary<string, string>
        {
            { "Interact", "E" }
        };

        [Test]
        public void Format_TokenInsideHighlight_ResolvesAndWraps()
        {
            Assert.AreEqual("<b>E</b>키를 눌러", TutorialHintFormatter.Format("[{Interact}]키를 눌러", Tokens, "<b>", "</b>"));
        }

        [Test]
        public void Format_UnknownToken_IsKeptVerbatim()
        {
            Assert.AreEqual("{Nope}키", TutorialHintFormatter.Format("{Nope}키", Tokens, "<b>", "</b>"));
        }

        [Test]
        public void Plain_Highlight_IsStripped()
        {
            Assert.AreEqual("Shift키로 달려", TutorialHintFormatter.Plain("[Shift]키로 달려", Tokens));
        }

        [Test]
        public void Format_Empty_ReturnsEmpty()
        {
            Assert.AreEqual(string.Empty, TutorialHintFormatter.Format(null, Tokens, "<b>", "</b>"));
        }
    }
}

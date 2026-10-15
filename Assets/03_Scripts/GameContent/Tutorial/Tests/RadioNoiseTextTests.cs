using System;
using NUnit.Framework;
using Pawntom.Tutorial.Core;

namespace Pawntom.Tutorial.Tests
{
    public class RadioNoiseTextTests
    {
        private const string Source = "들리십니까 깜냥이 씨";

        private static char[] Compose(int visible, int tail, double glitch, int seed)
        {
            char[] buffer = new char[Source.Length];
            RadioNoiseText.Compose(Source, buffer, visible, tail, glitch, new Random(seed));
            return buffer;
        }

        [Test]
        public void Compose_NoTailNoGlitch_CopiesSource()
        {
            Assert.AreEqual(Source, new string(Compose(Source.Length, 0, 0d, 1)));
        }

        [Test]
        public void Compose_DecodeTail_ScramblesLastVisibleCharacters()
        {
            char[] buffer = Compose(5, 2, 0d, 1);

            Assert.IsTrue(RadioNoiseText.Glyphs.IndexOf(buffer[4]) >= 0 && RadioNoiseText.Glyphs.IndexOf(buffer[3]) >= 0);
        }

        [Test]
        public void Compose_DecodeTail_KeepsCharactersBeforeTail()
        {
            char[] buffer = Compose(5, 2, 0d, 1);

            Assert.AreEqual(Source.Substring(0, 3), new string(buffer, 0, 3));
        }

        [Test]
        public void Compose_FullGlitch_PreservesSpaces()
        {
            char[] buffer = Compose(Source.Length, 0, 1d, 7);

            Assert.AreEqual(' ', buffer[5]);
        }

        [Test]
        public void Compose_HiddenCharacters_AreNeverScrambled()
        {
            char[] buffer = Compose(3, 0, 1d, 3);

            Assert.AreEqual(Source.Substring(3), new string(buffer, 3, Source.Length - 3));
        }

        [Test]
        public void Compose_SameSeed_IsDeterministic()
        {
            Assert.AreEqual(new string(Compose(Source.Length, 3, 0.3d, 42)), new string(Compose(Source.Length, 3, 0.3d, 42)));
        }

        [Test]
        public void Compose_ShortBuffer_Throws()
        {
            Assert.Throws<ArgumentException>(() => RadioNoiseText.Compose(Source, new char[2], 1, 0, 0d, new Random(1)));
        }
    }
}

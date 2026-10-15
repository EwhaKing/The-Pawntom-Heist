using System;
using NUnit.Framework;
using Pawntom.Tutorial.Core;

namespace Pawntom.Tutorial.Tests
{
    public class TutorialSfxSynthTests
    {
        private const int Rate = 44100;

        private static float Peak(float[] data)
        {
            float peak = 0f;

            for (int i = 0; i < data.Length; i++)
            {
                peak = Math.Max(peak, Math.Abs(data[i]));
            }

            return peak;
        }

        [Test]
        public void Blip_Duration_ProducesExpectedSampleCount()
        {
            Assert.AreEqual(1764, TutorialSfxSynth.Blip(Rate, 600d, 0.04d, 0.5d, 0.1d, 1).Length);
        }

        [Test]
        public void Blip_Peak_IsNormalizedBelowClipping()
        {
            Assert.AreEqual(0.42f, Peak(TutorialSfxSynth.Blip(Rate, 600d, 0.04d, 0.5d, 0.1d, 1)), 0.0001f);
        }

        [Test]
        public void Static_Peak_IsNormalizedBelowClipping()
        {
            Assert.AreEqual(0.4f, Peak(TutorialSfxSynth.Static(Rate, 0.4d, 3, 60d)), 0.0001f);
        }

        [Test]
        public void Static_SameSeed_IsDeterministic()
        {
            CollectionAssert.AreEqual(TutorialSfxSynth.Static(Rate, 0.1d, 9, 60d), TutorialSfxSynth.Static(Rate, 0.1d, 9, 60d));
        }

        [Test]
        public void Squelch_Peak_IsAudibleAndBelowClipping()
        {
            Assert.AreEqual(0.38f, Peak(TutorialSfxSynth.Squelch(Rate, 5)), 0.0001f);
        }

        [Test]
        public void Heartbeat_Peak_IsAudibleAndBelowClipping()
        {
            Assert.AreEqual(0.75f, Peak(TutorialSfxSynth.Heartbeat(Rate)), 0.0001f);
        }

        [Test]
        public void Normalize_Silence_StaysSilent()
        {
            float[] silence = new float[16];

            TutorialSfxSynth.Normalize(silence, 0.5f);

            Assert.AreEqual(0f, Peak(silence));
        }

        [Test]
        public void SampleCount_InvalidRate_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => TutorialSfxSynth.SampleCount(0, 1d));
        }
    }
}

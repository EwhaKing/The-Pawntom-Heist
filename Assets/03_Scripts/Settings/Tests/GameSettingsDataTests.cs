using NUnit.Framework;
using Pawntom.Settings;

namespace Pawntom.Settings.Tests
{
    public sealed class GameSettingsDataTests
    {
        private const int QualityLevels = 3;

        [TestCase(-0.5f, 0f)]
        [TestCase(0.4f, 0.4f)]
        [TestCase(7f, 1f)]
        [TestCase(float.NaN, 1f)]
        public void Sanitize_MasterVolume_ClampsToZeroOne(float stored, float expected)
        {
            GameSettingsData data = new GameSettingsData { MasterVolume = stored };
            data.Sanitize(QualityLevels);
            Assert.AreEqual(expected, data.MasterVolume, 0.0001f);
        }

        [TestCase(0f, 0.1f)]
        [TestCase(1.5f, 1.5f)]
        [TestCase(99f, 3f)]
        [TestCase(float.PositiveInfinity, 1f)]
        public void Sanitize_LookSensitivity_ClampsToRange(float stored, float expected)
        {
            GameSettingsData data = new GameSettingsData { LookSensitivity = stored };
            data.Sanitize(QualityLevels);
            Assert.AreEqual(expected, data.LookSensitivity, 0.0001f);
        }

        [TestCase(2, 2)]
        [TestCase(3, GameSettingsData.QualityUnset)]
        [TestCase(-4, GameSettingsData.QualityUnset)]
        public void Sanitize_QualityLevel_OutOfRangeBecomesUnset(int stored, int expected)
        {
            GameSettingsData data = new GameSettingsData { QualityLevel = stored };
            data.Sanitize(QualityLevels);
            Assert.AreEqual(expected, data.QualityLevel);
        }

        [TestCase(0, 0)]
        [TestCase(1, 1)]
        [TestCase(2, -1)]
        [TestCase(3, 3)]
        [TestCase(42, -1)]
        public void Sanitize_ScreenMode_OnlyKnownModesSurvive(int stored, int expected)
        {
            GameSettingsData data = new GameSettingsData { ScreenMode = stored };
            data.Sanitize(QualityLevels);
            Assert.AreEqual(expected, data.ScreenMode);
        }

        [Test]
        public void Sanitize_HalfSpecifiedResolution_BecomesUnset()
        {
            GameSettingsData data = new GameSettingsData { ResolutionWidth = 1920, ResolutionHeight = 0 };
            data.Sanitize(QualityLevels);
            Assert.IsFalse(data.HasResolution);
        }

        [Test]
        public void LookScaleY_WhenInverted_IsNegativeSensitivity()
        {
            GameSettingsData data = new GameSettingsData { LookSensitivity = 1.5f, InvertLookY = true };
            Assert.AreEqual(-1.5f, data.LookScaleY, 0.0001f);
        }

        [Test]
        public void ResetControls_ClearsBindingOverrides()
        {
            GameSettingsData data = new GameSettingsData { BindingOverridesJson = "{\"x\":1}", LookSensitivity = 2f };
            data.ResetControls();
            Assert.AreEqual(string.Empty, data.BindingOverridesJson);
        }

        [Test]
        public void ResetControls_KeepsVolumes()
        {
            GameSettingsData data = new GameSettingsData { SfxVolume = 0.25f };
            data.ResetControls();
            Assert.AreEqual(0.25f, data.SfxVolume, 0.0001f);
        }

        [Test]
        public void Clone_IsIndependentCopy()
        {
            GameSettingsData data = new GameSettingsData { BgmVolume = 0.3f };
            GameSettingsData copy = data.Clone();
            copy.BgmVolume = 0.9f;
            Assert.AreEqual(0.3f, data.BgmVolume, 0.0001f);
        }
    }
}

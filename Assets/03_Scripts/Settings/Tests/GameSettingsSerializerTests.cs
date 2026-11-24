using System.Collections.Generic;
using NUnit.Framework;
using Pawntom.Settings;

namespace Pawntom.Settings.Tests
{
    public sealed class GameSettingsSerializerTests
    {
        private const int QualityLevels = 4;

        private sealed class MemoryStore : ISettingsStore
        {
            public readonly Dictionary<string, object> Values = new Dictionary<string, object>();
            public int SaveCount;

            public bool HasKey(string key) { return Values.ContainsKey(key); }
            public float GetFloat(string key, float fallback) { object v; return Values.TryGetValue(key, out v) ? (float)v : fallback; }
            public int GetInt(string key, int fallback) { object v; return Values.TryGetValue(key, out v) ? (int)v : fallback; }
            public string GetString(string key, string fallback) { object v; return Values.TryGetValue(key, out v) ? (string)v : fallback; }
            public void SetFloat(string key, float value) { Values[key] = value; }
            public void SetInt(string key, int value) { Values[key] = value; }
            public void SetString(string key, string value) { Values[key] = value; }
            public void Save() { SaveCount++; }
        }

        private static GameSettingsData Sample()
        {
            return new GameSettingsData
            {
                MasterVolume = 0.7f,
                BgmVolume = 0.2f,
                SfxVolume = 0.9f,
                LookSensitivity = 1.75f,
                InvertLookY = true,
                BindingOverridesJson = "{\"bindings\":[]}",
                ResolutionWidth = 1600,
                ResolutionHeight = 900,
                ScreenMode = GameSettingsData.ScreenModeWindowed,
                QualityLevel = 2,
                VSync = false,
            };
        }

        [Test]
        public void Load_EmptyStore_ReturnsDefaults()
        {
            GameSettingsData data = GameSettingsSerializer.Load(new MemoryStore(), QualityLevels);
            Assert.AreEqual(GameSettingsData.DefaultLookSensitivity, data.LookSensitivity, 0.0001f);
        }

        [Test]
        public void Load_EmptyStore_LeavesScreenModeUnset()
        {
            GameSettingsData data = GameSettingsSerializer.Load(new MemoryStore(), QualityLevels);
            Assert.AreEqual(GameSettingsData.ScreenModeUnset, data.ScreenMode);
        }

        [Test]
        public void SaveThenLoad_RoundTripsSensitivity()
        {
            MemoryStore store = new MemoryStore();
            GameSettingsSerializer.Save(Sample(), store);
            Assert.AreEqual(1.75f, GameSettingsSerializer.Load(store, QualityLevels).LookSensitivity, 0.0001f);
        }

        [Test]
        public void SaveThenLoad_RoundTripsInvertY()
        {
            MemoryStore store = new MemoryStore();
            GameSettingsSerializer.Save(Sample(), store);
            Assert.IsTrue(GameSettingsSerializer.Load(store, QualityLevels).InvertLookY);
        }

        [Test]
        public void SaveThenLoad_RoundTripsBindings()
        {
            MemoryStore store = new MemoryStore();
            GameSettingsSerializer.Save(Sample(), store);
            Assert.AreEqual("{\"bindings\":[]}", GameSettingsSerializer.Load(store, QualityLevels).BindingOverridesJson);
        }

        [Test]
        public void SaveThenLoad_RoundTripsResolution()
        {
            MemoryStore store = new MemoryStore();
            GameSettingsSerializer.Save(Sample(), store);
            GameSettingsData loaded = GameSettingsSerializer.Load(store, QualityLevels);
            Assert.AreEqual(new[] { 1600, 900 }, new[] { loaded.ResolutionWidth, loaded.ResolutionHeight });
        }

        [Test]
        public void SaveThenLoad_RoundTripsVSyncOff()
        {
            MemoryStore store = new MemoryStore();
            GameSettingsSerializer.Save(Sample(), store);
            Assert.IsFalse(GameSettingsSerializer.Load(store, QualityLevels).VSync);
        }

        [Test]
        public void Save_FlushesStore()
        {
            MemoryStore store = new MemoryStore();
            GameSettingsSerializer.Save(Sample(), store);
            Assert.AreEqual(1, store.SaveCount);
        }

        [Test]
        public void Load_CorruptedVolume_IsClamped()
        {
            MemoryStore store = new MemoryStore();
            store.SetFloat(GameSettingsSerializer.SfxVolumeKey, 12f);
            Assert.AreEqual(1f, GameSettingsSerializer.Load(store, QualityLevels).SfxVolume, 0.0001f);
        }

        [Test]
        public void Load_QualityBeyondProjectLevels_IsUnset()
        {
            MemoryStore store = new MemoryStore();
            store.SetInt(GameSettingsSerializer.QualityLevelKey, 9);
            Assert.AreEqual(GameSettingsData.QualityUnset, GameSettingsSerializer.Load(store, QualityLevels).QualityLevel);
        }
    }
}

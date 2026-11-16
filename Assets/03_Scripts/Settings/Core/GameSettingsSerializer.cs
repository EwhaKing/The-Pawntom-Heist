namespace Pawntom.Settings
{
    /// <summary>
    /// <see cref="GameSettingsData"/> 를 저장소 키 하나하나로 읽고 쓴다.
    /// 키 이름을 바꾸면 기존 플레이어의 설정이 사라지므로, 형식을 바꿀 때는 접두사의 버전을 올린다.
    /// </summary>
    public static class GameSettingsSerializer
    {
        public const string Prefix = "pawntom.settings.v1.";

        public const string MasterVolumeKey = Prefix + "masterVolume";
        public const string BgmVolumeKey = Prefix + "bgmVolume";
        public const string SfxVolumeKey = Prefix + "sfxVolume";
        public const string LookSensitivityKey = Prefix + "lookSensitivity";
        public const string InvertLookYKey = Prefix + "invertLookY";
        public const string BindingsKey = Prefix + "bindings";
        public const string ResolutionWidthKey = Prefix + "resolutionWidth";
        public const string ResolutionHeightKey = Prefix + "resolutionHeight";
        public const string ScreenModeKey = Prefix + "screenMode";
        public const string QualityLevelKey = Prefix + "qualityLevel";
        public const string VSyncKey = Prefix + "vsync";

        public static GameSettingsData Load(ISettingsStore store, int qualityLevelCount)
        {
            GameSettingsData defaults = new GameSettingsData();
            GameSettingsData data = new GameSettingsData();

            if (store != null)
            {
                data.MasterVolume = store.GetFloat(MasterVolumeKey, defaults.MasterVolume);
                data.BgmVolume = store.GetFloat(BgmVolumeKey, defaults.BgmVolume);
                data.SfxVolume = store.GetFloat(SfxVolumeKey, defaults.SfxVolume);
                data.LookSensitivity = store.GetFloat(LookSensitivityKey, defaults.LookSensitivity);
                data.InvertLookY = store.GetInt(InvertLookYKey, defaults.InvertLookY ? 1 : 0) != 0;
                data.BindingOverridesJson = store.GetString(BindingsKey, defaults.BindingOverridesJson);
                data.ResolutionWidth = store.GetInt(ResolutionWidthKey, defaults.ResolutionWidth);
                data.ResolutionHeight = store.GetInt(ResolutionHeightKey, defaults.ResolutionHeight);
                data.ScreenMode = store.GetInt(ScreenModeKey, defaults.ScreenMode);
                data.QualityLevel = store.GetInt(QualityLevelKey, defaults.QualityLevel);
                data.VSync = store.GetInt(VSyncKey, defaults.VSync ? 1 : 0) != 0;
            }

            data.Sanitize(qualityLevelCount);
            return data;
        }

        public static void Save(GameSettingsData data, ISettingsStore store)
        {
            if (data == null || store == null)
            {
                return;
            }

            store.SetFloat(MasterVolumeKey, data.MasterVolume);
            store.SetFloat(BgmVolumeKey, data.BgmVolume);
            store.SetFloat(SfxVolumeKey, data.SfxVolume);
            store.SetFloat(LookSensitivityKey, data.LookSensitivity);
            store.SetInt(InvertLookYKey, data.InvertLookY ? 1 : 0);
            store.SetString(BindingsKey, data.BindingOverridesJson ?? string.Empty);
            store.SetInt(ResolutionWidthKey, data.ResolutionWidth);
            store.SetInt(ResolutionHeightKey, data.ResolutionHeight);
            store.SetInt(ScreenModeKey, data.ScreenMode);
            store.SetInt(QualityLevelKey, data.QualityLevel);
            store.SetInt(VSyncKey, data.VSync ? 1 : 0);
            store.Save();
        }
    }
}

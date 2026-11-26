namespace Pawntom.Settings
{
    /// <summary>
    /// 설정 저장소. 실행 중에는 PlayerPrefs, 테스트에서는 메모리 사전을 쓴다.
    /// </summary>
    public interface ISettingsStore
    {
        bool HasKey(string key);
        float GetFloat(string key, float fallback);
        int GetInt(string key, int fallback);
        string GetString(string key, string fallback);
        void SetFloat(string key, float value);
        void SetInt(string key, int value);
        void SetString(string key, string value);
        void Save();
    }
}

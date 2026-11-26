using Pawntom.Settings;
using UnityEngine;

/// <summary>설정 저장소의 실제 구현. 플레이어 PC 의 PlayerPrefs(Windows 레지스트리)에 저장한다.</summary>
public sealed class PlayerPrefsSettingsStore : ISettingsStore
{
    public bool HasKey(string key) { return PlayerPrefs.HasKey(key); }
    public float GetFloat(string key, float fallback) { return PlayerPrefs.GetFloat(key, fallback); }
    public int GetInt(string key, int fallback) { return PlayerPrefs.GetInt(key, fallback); }
    public string GetString(string key, string fallback) { return PlayerPrefs.GetString(key, fallback); }
    public void SetFloat(string key, float value) { PlayerPrefs.SetFloat(key, value); }
    public void SetInt(string key, int value) { PlayerPrefs.SetInt(key, value); }
    public void SetString(string key, string value) { PlayerPrefs.SetString(key, value); }
    public void Save() { PlayerPrefs.Save(); }
}

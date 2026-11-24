using System;
using System.Collections.Generic;
using Pawntom.Settings;
using UnityEngine;

/// <summary>
/// 옵션 값을 저장·로드하고 게임에 적용한다(음량·화면·품질·감도·키 설정).
/// <list type="bullet">
/// <item>첫 씬이 뜬 직후 스스로 생겨(DontDestroyOnLoad) 저장된 값을 한 번 적용한다. 씬·프리팹에 둘 필요가 없다.</item>
/// <item>옵션창은 <see cref="Current"/> 의 사본을 고쳐 <see cref="Apply"/> 로 넘긴다. 바뀐 항목만 실제로 다시 적용한다
///   (음량 슬라이더를 움직일 때마다 해상도를 다시 바꾸지 않게).</item>
/// <item>키 설정은 InputManager 가 들고 있다. 키를 바꾸면 <see cref="InputManager.BindingsChanged"/> 로 알려 와 저장한다.</item>
/// </list>
/// 시간(Time.timeScale)이나 네트워크 시뮬레이션은 건드리지 않는다.
/// </summary>
public class GameSettingsManager : PawntomSingleton<GameSettingsManager>
{
    private readonly ISettingsStore _store = new PlayerPrefsSettingsStore();
    private GameSettingsData _data;

    private bool _loaded;
    private bool _applyingBindings;
    private string _appliedBindingsJson;
    private int _appliedWidth = -1;
    private int _appliedHeight = -1;
    private int _appliedScreenMode = int.MinValue;
    private int _appliedQuality = int.MinValue;

    private List<ResolutionOption> _resolutions;

    /// <summary>값이 바뀌어 적용된 뒤.</summary>
    public static event Action Changed;

    /// <summary>지금 적용된 설정의 사본. 고쳐서 <see cref="Apply"/> 로 넘긴다.</summary>
    public GameSettingsData Current
    {
        get
        {
            EnsureLoaded();
            return _data.Clone();
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        // 첫 씬의 매니저(InputManager 등)가 Awake 를 마친 뒤라 중복 생성 없이 찾아 쓴다.
        Instance.EnsureLoaded();
    }

    private void OnEnable()
    {
        InputManager.BindingsChanged += HandleBindingsChanged;
    }

    private void OnDisable()
    {
        InputManager.BindingsChanged -= HandleBindingsChanged;
    }

    private void EnsureLoaded()
    {
        if (_loaded)
        {
            return;
        }

        _loaded = true;
        _data = GameSettingsSerializer.Load(_store, QualitySettings.names.Length);

        // 처음 실행이면 화면·품질은 "지정 안 함"이다. 옵션창에 보일 값은 지금 상태에서 채우되, 적용은 건너뛴다.
        if (!_store.HasKey(GameSettingsSerializer.VSyncKey))
        {
            _data.VSync = QualitySettings.vSyncCount > 0;
        }

        ApplyInternal(_data, true);
    }

    /// <summary>설정을 적용한다. save 면 PlayerPrefs 에도 쓴다.</summary>
    public void Apply(GameSettingsData data, bool save)
    {
        EnsureLoaded();

        if (data == null)
        {
            return;
        }

        GameSettingsData copy = data.Clone();
        copy.Sanitize(QualitySettings.names.Length);
        _data = copy;
        ApplyInternal(_data, false);

        if (save)
        {
            Save();
        }

        Changed?.Invoke();
    }

    public void Save()
    {
        EnsureLoaded();
        GameSettingsSerializer.Save(_data, _store);
    }

    private void ApplyInternal(GameSettingsData data, bool startup)
    {
        // ── 사운드 ──
        AudioListener.volume = data.MasterVolume;
        GameAudioVolume.Set(data.BgmVolume, data.SfxVolume);

        // ── 조작 ──
        InputManager input = InputManager.Instance;
        input.LookScaleX = data.LookScaleX;
        input.LookScaleY = data.LookScaleY;

        if (!string.Equals(_appliedBindingsJson, data.BindingOverridesJson, StringComparison.Ordinal))
        {
            _applyingBindings = true;

            try
            {
                input.LoadBindingOverrides(data.BindingOverridesJson);
            }
            finally
            {
                _applyingBindings = false;
            }

            _appliedBindingsJson = data.BindingOverridesJson;
        }

        // ── 화면 ──
        // 지정 안 한 항목(처음 실행)은 건드리지 않는다. Unity 가 기억하는 창 크기·모드를 그대로 쓴다.
        if (data.QualityLevel >= 0 && data.QualityLevel != _appliedQuality)
        {
            QualitySettings.SetQualityLevel(data.QualityLevel, true);
            _appliedQuality = data.QualityLevel;
        }

        // 품질 단계마다 vSync 값이 따로 있어서, 품질을 바꾼 뒤에 다시 건다.
        if (!startup || _store.HasKey(GameSettingsSerializer.VSyncKey))
        {
            QualitySettings.vSyncCount = data.VSync ? 1 : 0;
        }

        ApplyScreen(data);
    }

    private void ApplyScreen(GameSettingsData data)
    {
        if (!data.HasResolution && data.ScreenMode == GameSettingsData.ScreenModeUnset)
        {
            return;
        }

        int width = data.HasResolution ? data.ResolutionWidth : Screen.width;
        int height = data.HasResolution ? data.ResolutionHeight : Screen.height;
        int mode = data.ScreenMode != GameSettingsData.ScreenModeUnset ? data.ScreenMode : (int)Screen.fullScreenMode;

        if (width == _appliedWidth && height == _appliedHeight && mode == _appliedScreenMode)
        {
            return;
        }

        _appliedWidth = width;
        _appliedHeight = height;
        _appliedScreenMode = mode;

#if !UNITY_EDITOR
        Screen.SetResolution(width, height, (FullScreenMode)mode);
#endif
    }

    private void HandleBindingsChanged()
    {
        if (_applyingBindings || !_loaded)
        {
            return;
        }

        // 옵션창에서 키를 바꿨다. 오버라이드를 그대로 저장해 다음 실행에도 쓴다.
        _data.BindingOverridesJson = InputManager.Instance.SaveBindingOverrides();
        _appliedBindingsJson = _data.BindingOverridesJson;
        Save();
        Changed?.Invoke();
    }

    // ── 옵션창용 목록 ──

    /// <summary>모니터가 지원하는 해상도(크기만, 작은 것부터).</summary>
    public IReadOnlyList<ResolutionOption> Resolutions
    {
        get
        {
            if (_resolutions == null)
            {
                Resolution[] raw = Screen.resolutions;
                List<ResolutionOption> options = new List<ResolutionOption>(raw.Length);

                for (int i = 0; i < raw.Length; i++)
                {
                    options.Add(new ResolutionOption(raw[i].width, raw[i].height));
                }

                _resolutions = ResolutionList.Build(options);

                // 에디터나 특이한 모니터에서 목록이 비면 지금 크기 하나라도 보여 준다.
                if (_resolutions.Count == 0)
                {
                    _resolutions.Add(new ResolutionOption(Screen.width, Screen.height));
                }
            }

            return _resolutions;
        }
    }

    /// <summary>옵션창에 보일 현재 해상도 칸.</summary>
    public int CurrentResolutionIndex(GameSettingsData data)
    {
        IReadOnlyList<ResolutionOption> list = Resolutions;
        int width = data.HasResolution ? data.ResolutionWidth : Screen.width;
        int height = data.HasResolution ? data.ResolutionHeight : Screen.height;
        return ResolutionList.IndexOfClosest(list, width, height);
    }

    /// <summary>옵션창에 보일 현재 화면 모드(지정 안 했으면 지금 상태).</summary>
    public int CurrentScreenMode(GameSettingsData data)
    {
        if (data.ScreenMode != GameSettingsData.ScreenModeUnset)
        {
            return data.ScreenMode;
        }

        FullScreenMode mode = Screen.fullScreenMode;

        if (mode == FullScreenMode.ExclusiveFullScreen)
        {
            return GameSettingsData.ScreenModeExclusive;
        }

        return mode == FullScreenMode.FullScreenWindow ? GameSettingsData.ScreenModeBorderless : GameSettingsData.ScreenModeWindowed;
    }

    /// <summary>옵션창에 보일 현재 품질 단계(지정 안 했으면 지금 상태).</summary>
    public int CurrentQuality(GameSettingsData data)
    {
        return data.QualityLevel >= 0 ? data.QualityLevel : QualitySettings.GetQualityLevel();
    }
}

using System;
using System.Collections.Generic;
using Pawntom.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 옵션창(사운드 / 화면 / 조작 탭). 타이틀의 OPTIONS 버튼과 인게임 ESC 메뉴가 같은 창을 연다(<see cref="GameMenuHost"/>).
/// <list type="bullet">
/// <item>값을 바꾸면 바로 적용된다(미리 보기 = 실제). 슬라이더는 창을 닫을 때, 좌우 선택·키 바꾸기는 바꿀 때 저장한다.</item>
/// <item>키 바꾸기: 칸을 누르고 원하는 키를 누른다. Esc 는 취소. 이미 다른 동작이 쓰던 키면 두 칸의 키를 맞바꾼다.</item>
/// <item>씬·프리팹 없이 코드로 만든다.</item>
/// </list>
/// </summary>
public sealed class OptionsMenuView : MonoBehaviour
{
    private enum Tab
    {
        Sound = 0,
        Display = 1,
        Controls = 2,
    }

    private static readonly string[] TabLabels = { "사운드", "화면", "조작" };

    // 화면 모드 칸 순서와 이름. 값은 UnityEngine.FullScreenMode 숫자(GameSettingsData 참고).
    private static readonly int[] ScreenModes =
    {
        GameSettingsData.ScreenModeBorderless,
        GameSettingsData.ScreenModeExclusive,
        GameSettingsData.ScreenModeWindowed,
    };

    private static readonly string[] ScreenModeLabels = { "전체 화면", "전체 화면(독점)", "창 모드" };

    private sealed class KeyRow
    {
        public RebindableBinding Binding;
        public Button Button;
        public TextMeshProUGUI Text;
    }

    private RuntimeUIFactory _factory;
    private GameSettingsData _working;
    private Action _onClosed;

    private readonly Button[] _tabButtons = new Button[3];
    private readonly TextMeshProUGUI[] _tabTexts = new TextMeshProUGUI[3];
    private readonly RectTransform[] _pages = new RectTransform[3];
    private Tab _tab;

    private Slider _master;
    private Slider _bgm;
    private Slider _sfx;
    private TextMeshProUGUI _masterText;
    private TextMeshProUGUI _bgmText;
    private TextMeshProUGUI _sfxText;

    private TextMeshProUGUI _resolutionText;
    private TextMeshProUGUI _screenModeText;
    private TextMeshProUGUI _qualityText;
    private TextMeshProUGUI _vsyncText;

    private Slider _sensitivity;
    private TextMeshProUGUI _sensitivityText;
    private TextMeshProUGUI _invertText;
    private readonly List<KeyRow> _keyRows = new List<KeyRow>();
    private KeyRow _listening;

    private bool _refreshing;
    private bool _dirty;

    public bool IsOpen
    {
        get { return gameObject.activeSelf; }
    }

    /// <summary>키 입력을 기다리는 중(이때 Esc 는 키 바꾸기 취소다).</summary>
    public bool IsListeningForKey
    {
        get { return _listening != null; }
    }

    public void Build(RuntimeUIFactory factory)
    {
        _factory = factory;
        RectTransform root = (RectTransform)transform;
        RuntimeUIFactory.Stretch(root);

        // 뒤를 어둡게 하고 클릭이 게임 화면으로 새지 않게 막는다.
        Image backdrop = factory.CreateImage("Backdrop", root, OptionsTheme.Backdrop);
        backdrop.raycastTarget = true;
        RuntimeUIFactory.Stretch(backdrop.rectTransform);

        Image panel = OptionsWidgets.CreatePanel(factory, "OptionsPanel", root, OptionsTheme.OptionsPanelSize);
        RectTransform panelRect = panel.rectTransform;

        // ── 머리: 제목 + 탭 ──
        TextMeshProUGUI title = factory.CreateText("Title", panelRect, OptionsTheme.TitleFontSize, OptionsTheme.Text, TextAlignmentOptions.MidlineLeft);
        title.fontStyle = FontStyles.Bold;
        title.text = "옵션";
        RuntimeUIFactory.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(48f, -14f), new Vector2(300f, OptionsTheme.HeaderHeight - 20f));

        const float TabWidth = 150f;
        const float TabGap = 8f;

        for (int i = 0; i < TabLabels.Length; i++)
        {
            int index = i;
            Button tab = OptionsWidgets.CreateButton(factory, "Tab_" + TabLabels[i], panelRect, TabLabels[i], OptionsTheme.BodyFontSize, () => SelectTab((Tab)index), out _tabTexts[i]);
            float x = -48f - (TabLabels.Length - 1 - i) * (TabWidth + TabGap);
            RuntimeUIFactory.Place((RectTransform)tab.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(x, -22f), new Vector2(TabWidth, 44f));
            _tabButtons[i] = tab;
        }

        Image headerLine = factory.CreateImage("HeaderLine", panelRect, OptionsTheme.Divider);
        RuntimeUIFactory.Place(headerLine.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -OptionsTheme.HeaderHeight), new Vector2(OptionsTheme.OptionsPanelSize.x - 96f, 2f));

        // ── 본문 페이지 ──
        for (int i = 0; i < _pages.Length; i++)
        {
            RectTransform page = factory.CreateRect("Page_" + TabLabels[i], panelRect);
            RuntimeUIFactory.Inset(page, 64f, 64f, OptionsTheme.HeaderHeight + 22f, OptionsTheme.FooterHeight + 8f);
            VerticalLayoutGroup layout = page.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = OptionsTheme.RowSpacing;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            _pages[i] = page;
        }

        BuildSoundPage(_pages[(int)Tab.Sound]);
        BuildDisplayPage(_pages[(int)Tab.Display]);
        BuildControlsPage(_pages[(int)Tab.Controls]);

        // ── 바닥: 안내 + 닫기 ──
        TextMeshProUGUI hint = factory.CreateText("Hint", panelRect, OptionsTheme.SmallFontSize, OptionsTheme.SubText, TextAlignmentOptions.MidlineLeft);
        hint.textWrappingMode = TextWrappingModes.NoWrap;
        hint.text = "바꾼 값은 바로 적용되고 저장됩니다.  Esc: 닫기";
        RuntimeUIFactory.Place(hint.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(48f, 18f), new Vector2(620f, 44f));

        TextMeshProUGUI closeText;
        Button close = OptionsWidgets.CreateButton(factory, "Close", panelRect, "닫기", OptionsTheme.BodyFontSize, Close, out closeText);
        RuntimeUIFactory.Place((RectTransform)close.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-48f, 18f), new Vector2(180f, 46f));

        gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        InputManager.BindingsChanged += RefreshKeyRows;
    }

    private void OnDisable()
    {
        InputManager.BindingsChanged -= RefreshKeyRows;
    }

    // ==========================================
    // 열기 / 닫기
    // ==========================================

    public void Open(Action onClosed)
    {
        _onClosed = onClosed;
        _working = GameSettingsManager.Instance.Current;
        _dirty = false;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        SelectTab(Tab.Sound);
        RefreshAll();
    }

    public void Close()
    {
        if (!IsOpen)
        {
            return;
        }

        StopListening();

        if (_dirty)
        {
            GameSettingsManager.Instance.Save();
            _dirty = false;
        }

        gameObject.SetActive(false);

        Action closed = _onClosed;
        _onClosed = null;
        closed?.Invoke();
    }

    /// <summary>Esc: 키 입력 대기 중이면 그것만 취소(InputManager 가 처리), 아니면 창을 닫는다.</summary>
    public void HandleEscape()
    {
        if (IsListeningForKey)
        {
            return;
        }

        Close();
    }

    private void SelectTab(Tab tab)
    {
        StopListening();
        _tab = tab;

        for (int i = 0; i < _pages.Length; i++)
        {
            bool selected = i == (int)tab;
            _pages[i].gameObject.SetActive(selected);
            OptionsWidgets.SetButtonColors(_tabButtons[i], selected);
            _tabTexts[i].color = selected ? OptionsTheme.SelectedText : OptionsTheme.Text;
        }
    }

    // ==========================================
    // 사운드
    // ==========================================

    private void BuildSoundPage(RectTransform page)
    {
        _master = OptionsWidgets.CreateSlider(_factory, OptionsWidgets.CreateRow(_factory, page, "전체 음량"), 0f, 1f, out _masterText);
        _bgm = OptionsWidgets.CreateSlider(_factory, OptionsWidgets.CreateRow(_factory, page, "배경음"), 0f, 1f, out _bgmText);
        _sfx = OptionsWidgets.CreateSlider(_factory, OptionsWidgets.CreateRow(_factory, page, "효과음"), 0f, 1f, out _sfxText);

        _master.onValueChanged.AddListener(value => { if (!_refreshing) { _working.MasterVolume = value; Push(false); } SetPercent(_masterText, value); });
        _bgm.onValueChanged.AddListener(value => { if (!_refreshing) { _working.BgmVolume = value; Push(false); } SetPercent(_bgmText, value); });
        _sfx.onValueChanged.AddListener(value => { if (!_refreshing) { _working.SfxVolume = value; Push(false); } SetPercent(_sfxText, value); });
    }

    private static void SetPercent(TextMeshProUGUI text, float value)
    {
        text.SetText("{0}%", Mathf.RoundToInt(value * 100f));
    }

    // ==========================================
    // 화면
    // ==========================================

    private void BuildDisplayPage(RectTransform page)
    {
        _resolutionText = OptionsWidgets.CreateStepper(_factory, OptionsWidgets.CreateRow(_factory, page, "해상도"), StepResolution);
        _screenModeText = OptionsWidgets.CreateStepper(_factory, OptionsWidgets.CreateRow(_factory, page, "화면 모드"), StepScreenMode);
        _qualityText = OptionsWidgets.CreateStepper(_factory, OptionsWidgets.CreateRow(_factory, page, "그래픽 품질"), StepQuality);
        _vsyncText = OptionsWidgets.CreateStepper(_factory, OptionsWidgets.CreateRow(_factory, page, "수직 동기화"), step => { _working.VSync = !_working.VSync; Push(true); RefreshDisplay(); });

#if UNITY_EDITOR
        OptionsWidgets.CreateSectionHeader(_factory, page, "에디터에서는 해상도·화면 모드가 저장만 되고 빌드에서 적용됩니다.");
#endif
    }

    private void StepResolution(int step)
    {
        IReadOnlyList<ResolutionOption> list = GameSettingsManager.Instance.Resolutions;

        if (list.Count == 0)
        {
            return;
        }

        int index = GameSettingsManager.Instance.CurrentResolutionIndex(_working);
        index = Mathf.Clamp(index + step, 0, list.Count - 1);
        _working.ResolutionWidth = list[index].Width;
        _working.ResolutionHeight = list[index].Height;
        Push(true);
        RefreshDisplay();
    }

    private void StepScreenMode(int step)
    {
        int current = Array.IndexOf(ScreenModes, GameSettingsManager.Instance.CurrentScreenMode(_working));
        int next = ((current < 0 ? 0 : current) + step + ScreenModes.Length) % ScreenModes.Length;
        _working.ScreenMode = ScreenModes[next];
        Push(true);
        RefreshDisplay();
    }

    private void StepQuality(int step)
    {
        int count = QualitySettings.names.Length;

        if (count == 0)
        {
            return;
        }

        int current = GameSettingsManager.Instance.CurrentQuality(_working);
        _working.QualityLevel = Mathf.Clamp(current + step, 0, count - 1);
        Push(true);
        RefreshDisplay();
    }

    // ==========================================
    // 조작
    // ==========================================

    private void BuildControlsPage(RectTransform page)
    {
        _sensitivity = OptionsWidgets.CreateSlider(_factory, OptionsWidgets.CreateRow(_factory, page, "마우스 감도"),
            GameSettingsData.MinLookSensitivity, GameSettingsData.MaxLookSensitivity, out _sensitivityText);
        _sensitivity.onValueChanged.AddListener(value =>
        {
            // 0.05 단위로 맞춘다(같은 값을 다시 찾기 쉽게).
            float snapped = Mathf.Round(value * 20f) / 20f;

            if (!_refreshing)
            {
                _working.LookSensitivity = snapped;
                Push(false);
            }

            _sensitivityText.SetText("{0:2}x", snapped);
        });

        _invertText = OptionsWidgets.CreateStepper(_factory, OptionsWidgets.CreateRow(_factory, page, "마우스 상하 반전"), step =>
        {
            _working.InvertLookY = !_working.InvertLookY;
            Push(true);
            RefreshControls();
        });

        OptionsWidgets.CreateSectionHeader(_factory, page, "키 설정  (칸을 누른 뒤 새 키를 누르세요 · Esc 취소)");

        IReadOnlyList<RebindableBinding> bindings = InputManager.Instance.Rebindables;

        for (int i = 0; i < bindings.Count; i++)
        {
            KeyRow row = new KeyRow { Binding = bindings[i] };
            RectTransform control = OptionsWidgets.CreateRow(_factory, page, bindings[i].Label);
            row.Button = OptionsWidgets.CreateButton(_factory, "Key", control, "-", OptionsTheme.BodyFontSize, () => BeginListening(row), out row.Text);
            RuntimeUIFactory.Stretch((RectTransform)row.Button.transform);
            _keyRows.Add(row);
        }

        RectTransform resetControl = OptionsWidgets.CreateRow(_factory, page, string.Empty);
        TextMeshProUGUI resetText;
        Button reset = OptionsWidgets.CreateButton(_factory, "ResetControls", resetControl, "조작 기본값으로", OptionsTheme.BodyFontSize, ResetControls, out resetText);
        RuntimeUIFactory.Stretch((RectTransform)reset.transform);
    }

    private void BeginListening(KeyRow row)
    {
        if (_listening != null)
        {
            return;
        }

        _listening = row;
        row.Text.text = "키를 누르세요...";
        row.Text.color = OptionsTheme.Accent;

        InputManager.Instance.StartRebind(row.Binding, changed =>
        {
            _listening = null;
            RefreshKeyRows();
        });
    }

    private void StopListening()
    {
        // 창을 닫거나 탭을 바꿀 때, 정리 중일 수도 있으므로 새로 만들지 않는 Live 를 쓴다.
        InputManager input = InputManager.Live;

        if (_listening != null && input != null && input.IsRebinding)
        {
            input.CancelRebind();
        }

        _listening = null;
    }

    private void ResetControls()
    {
        StopListening();
        _working.ResetControls();
        InputManager.Instance.ResetBindings();
        Push(true);
        RefreshControls();
    }

    // ==========================================
    // 적용·표시
    // ==========================================

    /// <summary>작업 사본을 적용한다. 키 설정은 InputManager 가 가진 최신 값을 그대로 쓴다(옵션창이 옛 값으로 되돌리지 않게).</summary>
    private void Push(bool save)
    {
        _working.BindingOverridesJson = InputManager.Instance.SaveBindingOverrides();
        GameSettingsManager.Instance.Apply(_working, save);
        _working = GameSettingsManager.Instance.Current;
        _dirty |= !save;
    }

    private void RefreshAll()
    {
        _refreshing = true;

        try
        {
            _master.value = _working.MasterVolume;
            _bgm.value = _working.BgmVolume;
            _sfx.value = _working.SfxVolume;
            SetPercent(_masterText, _working.MasterVolume);
            SetPercent(_bgmText, _working.BgmVolume);
            SetPercent(_sfxText, _working.SfxVolume);

            _sensitivity.value = _working.LookSensitivity;
            _sensitivityText.SetText("{0:2}x", _working.LookSensitivity);
        }
        finally
        {
            _refreshing = false;
        }

        RefreshDisplay();
        RefreshControls();
    }

    private void RefreshDisplay()
    {
        GameSettingsManager manager = GameSettingsManager.Instance;
        IReadOnlyList<ResolutionOption> list = manager.Resolutions;
        int resolution = manager.CurrentResolutionIndex(_working);
        _resolutionText.text = resolution >= 0 ? list[resolution].ToString() : "-";

        int mode = Array.IndexOf(ScreenModes, manager.CurrentScreenMode(_working));
        _screenModeText.text = mode >= 0 ? ScreenModeLabels[mode] : "-";

        string[] qualities = QualitySettings.names;
        int quality = manager.CurrentQuality(_working);
        _qualityText.text = quality >= 0 && quality < qualities.Length ? qualities[quality] : "-";

        _vsyncText.text = _working.VSync ? "켜짐" : "꺼짐";
    }

    private void RefreshControls()
    {
        _invertText.text = _working.InvertLookY ? "켜짐" : "꺼짐";
        RefreshKeyRows();
    }

    private void RefreshKeyRows()
    {
        for (int i = 0; i < _keyRows.Count; i++)
        {
            KeyRow row = _keyRows[i];

            if (row == _listening)
            {
                continue;
            }

            row.Text.text = row.Binding.DisplayName;
            row.Text.color = OptionsTheme.Text;
        }
    }
}

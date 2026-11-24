using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 게임 중 ESC 메뉴(계속하기 / 옵션 / 게임 종료).
/// <para>
/// 멀티플레이라 게임을 멈추지 않는다 — Time.timeScale·Fusion 시뮬레이션은 그대로 돌고, 이 클라이언트의 입력만 막는다
/// (<see cref="GameMenuHost"/> 가 GameplayInputBlocker 를 건다). 그래서 안내 문구로 "게임은 계속 진행된다"고 알린다.
/// </para>
/// </summary>
public sealed class PauseMenuView : MonoBehaviour
{
    private const float QuitConfirmSeconds = 3f;

    private Action _onResume;
    private Action _onOptions;

    private TextMeshProUGUI _quitText;
    private float _quitArmedUntil = -1f;

    public bool IsOpen
    {
        get { return gameObject.activeSelf; }
    }

    public void Build(RuntimeUIFactory factory, Action onResume, Action onOptions)
    {
        _onResume = onResume;
        _onOptions = onOptions;

        RectTransform root = (RectTransform)transform;
        RuntimeUIFactory.Stretch(root);

        Image backdrop = factory.CreateImage("Backdrop", root, OptionsTheme.Backdrop);
        backdrop.raycastTarget = true;
        RuntimeUIFactory.Stretch(backdrop.rectTransform);

        Image panel = OptionsWidgets.CreatePanel(factory, "PausePanel", root, OptionsTheme.PausePanelSize);
        RectTransform panelRect = panel.rectTransform;

        TextMeshProUGUI title = factory.CreateText("Title", panelRect, OptionsTheme.TitleFontSize, OptionsTheme.Text, TextAlignmentOptions.Center);
        title.fontStyle = FontStyles.Bold;
        title.text = "메뉴";
        RuntimeUIFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(380f, 52f));

        TextMeshProUGUI note = factory.CreateText("Note", panelRect, OptionsTheme.SmallFontSize, OptionsTheme.SubText, TextAlignmentOptions.Center);
        note.text = "메뉴가 열려 있어도 게임은 계속 진행됩니다";
        RuntimeUIFactory.Place(note.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -76f), new Vector2(400f, 30f));

        const float ButtonWidth = 320f;
        const float ButtonHeight = 54f;
        const float FirstY = -130f;
        const float Step = 66f;

        TextMeshProUGUI ignored;
        Button resume = OptionsWidgets.CreateButton(factory, "Resume", panelRect, "계속하기", OptionsTheme.BodyFontSize, () => _onResume?.Invoke(), out ignored);
        RuntimeUIFactory.Place((RectTransform)resume.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, FirstY), new Vector2(ButtonWidth, ButtonHeight));

        Button options = OptionsWidgets.CreateButton(factory, "Options", panelRect, "옵션", OptionsTheme.BodyFontSize, () => _onOptions?.Invoke(), out ignored);
        RuntimeUIFactory.Place((RectTransform)options.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, FirstY - Step), new Vector2(ButtonWidth, ButtonHeight));

        Button quit = OptionsWidgets.CreateButton(factory, "Quit", panelRect, "게임 종료", OptionsTheme.BodyFontSize, HandleQuit, out _quitText);
        RuntimeUIFactory.Place((RectTransform)quit.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, FirstY - Step * 2f), new Vector2(ButtonWidth, ButtonHeight));

        gameObject.SetActive(false);
    }

    public void Show()
    {
        DisarmQuit();
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
    }

    public void Hide()
    {
        DisarmQuit();
        gameObject.SetActive(false);
    }

    private void Update()
    {
        if (_quitArmedUntil > 0f && Time.unscaledTime > _quitArmedUntil)
        {
            DisarmQuit();
        }
    }

    // 한 번 누르면 확인 문구로 바뀌고, 3초 안에 한 번 더 누르면 종료한다(실수로 눌러 방이 사라지지 않게).
    private void HandleQuit()
    {
        if (_quitArmedUntil < 0f)
        {
            _quitArmedUntil = Time.unscaledTime + QuitConfirmSeconds;
            _quitText.text = "한 번 더 누르면 종료";
            _quitText.color = OptionsTheme.Accent;
            return;
        }

        Debug.Log("[PauseMenuView] 게임 종료");

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void DisarmQuit()
    {
        _quitArmedUntil = -1f;

        if (_quitText != null)
        {
            _quitText.text = "게임 종료";
            _quitText.color = OptionsTheme.Text;
        }
    }
}

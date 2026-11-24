using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 타이틀 메뉴(HOST GAME / JOIN GAME / TUTORIAL / OPTIONS / QUIT).
/// <list type="bullet">
/// <item>선택된 버튼 하나만 흰 막대로 바뀌고 오른쪽으로 살짝 밀려 나온다. 마우스를 올리면 그 버튼이 선택된다.</item>
/// <item>키보드: ↑↓ / W·S 로 이동, Enter·Space 로 누른다.</item>
/// <item>처음 들어오면 위에서부터 차례로 미끄러져 나타난다.</item>
/// <item>로비 상태(접속 전)에서만 보인다. 접속 중·대기실에서는 숨긴다.</item>
/// <item>OPTIONS: <see cref="optionsButton"/> 이 연결돼 있으면 그 버튼을 쓰고, 비어 있으면 QUIT 버튼을 복제해
///   코드로 만든다(전용 아트가 아직 없다). 만든 버튼은 QUIT 바로 위에 들어가고 위쪽 버튼들이 한 칸 올라간다.</item>
/// <item>옵션창(<see cref="GameMenuHost"/>)이 열려 있는 동안 키보드 조작은 옵션창 몫이다.</item>
/// </list>
/// </summary>
public class TitleMenuUI : MonoBehaviour
{
    [Tooltip("위에서부터 순서대로")]
    [SerializeField] private TitleMenuButton[] buttons;

    [Tooltip("선택된 버튼이 오른쪽으로 밀려 나오는 거리(px)")]
    [SerializeField] private float highlightOffset = 18f;

    [Tooltip("밀려 나오는 빠르기(1/초)")]
    [SerializeField, Min(1f)] private float slideSpeed = 16f;

    [Tooltip("처음 나타날 때 버튼 사이 간격(초)")]
    [SerializeField, Min(0f)] private float introStagger = 0.07f;

    [Tooltip("처음 나타날 때 한 버튼이 미끄러져 들어오는 시간(초)")]
    [SerializeField, Min(0.01f)] private float introSeconds = 0.35f;

    [Tooltip("처음 나타날 때 왼쪽에서 들어오는 거리(px)")]
    [SerializeField] private float introDistance = 60f;

    [Tooltip("OPTIONS 버튼(선택). 비우면 QUIT 버튼을 복제해 코드로 만든다. 연결한다면 buttons 배열에도 넣는다.")]
    [SerializeField] private TitleMenuButton optionsButton;

    private const string GeneratedOptionsLabel = "OPTIONS";

    private CanvasGroup _group;
    private RectTransform[] _rects;
    private Vector2[] _home;
    private CanvasGroup[] _buttonGroups;
    private float[] _offsets;
    private int _current;
    private float _introTime;

    private void Awake()
    {
        _group = GetComponent<CanvasGroup>();

        if (_group == null)
        {
            _group = gameObject.AddComponent<CanvasGroup>();
        }

        EnsureOptionsButton();

        int count = buttons != null ? buttons.Length : 0;
        _rects = new RectTransform[count];
        _home = new Vector2[count];
        _buttonGroups = new CanvasGroup[count];
        _offsets = new float[count];

        for (int i = 0; i < count; i++)
        {
            buttons[i].Bind(this);
            _rects[i] = (RectTransform)buttons[i].transform;
            _home[i] = _rects[i].anchoredPosition;
            _buttonGroups[i] = buttons[i].GetComponent<CanvasGroup>();

            if (_buttonGroups[i] == null)
            {
                _buttonGroups[i] = buttons[i].gameObject.AddComponent<CanvasGroup>();
            }
        }

        RefreshHighlights();
    }

    private void OnEnable()
    {
        GameManager.OnStateChanged += HandleStateChanged;
        _introTime = 0f;

        if (GameManager.Instance != null)
        {
            ApplyState(GameManager.Instance.CurrentState);
        }
    }

    private void OnDisable()
    {
        GameManager.OnStateChanged -= HandleStateChanged;
    }

    private void HandleStateChanged(GameState previous, GameState next)
    {
        ApplyState(next);
    }

    private void ApplyState(GameState state)
    {
        bool visible = state == GameState.Lobby;
        _group.alpha = visible ? 1f : 0f;
        _group.interactable = visible;
        _group.blocksRaycasts = visible;
    }

    public void Highlight(TitleMenuButton button)
    {
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] == button)
            {
                _current = i;
                RefreshHighlights();
                return;
            }
        }
    }

    private void RefreshHighlights()
    {
        for (int i = 0; i < buttons.Length; i++)
        {
            buttons[i].SetHighlighted(i == _current);
        }
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        _introTime += dt;

        if (_group.interactable && !GameMenuHost.IsOpen)
        {
            HandleKeyboard();
        }

        for (int i = 0; i < buttons.Length; i++)
        {
            // 선택 칸은 오른쪽으로 밀려 나오고, 나머지는 제자리로
            float target = i == _current ? highlightOffset : 0f;
            _offsets[i] = Mathf.Lerp(_offsets[i], target, 1f - Mathf.Exp(-slideSpeed * dt));

            // 처음 등장: 위에서부터 차례로 왼쪽에서 미끄러져 들어오며 나타난다
            float u = Mathf.Clamp01((_introTime - i * introStagger) / introSeconds);
            float eased = 1f - (1f - u) * (1f - u) * (1f - u);
            _buttonGroups[i].alpha = eased * (buttons[i].UnderDevelopment ? 0.35f : 1f);

            _rects[i].anchoredPosition = _home[i] + new Vector2(_offsets[i] - (1f - eased) * introDistance, 0f);
        }
    }

    private void HandleKeyboard()
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard == null || buttons.Length == 0)
        {
            return;
        }

        if (keyboard.downArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame)
        {
            _current = (_current + 1) % buttons.Length;
            RefreshHighlights();
        }
        else if (keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame)
        {
            _current = (_current - 1 + buttons.Length) % buttons.Length;
            RefreshHighlights();
        }
        else if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame)
        {
            // Invoke 는 interactable 을 보지 않으므로 잠긴 버튼은 여기서 막는다.
            if (!buttons[_current].UnderDevelopment)
            {
                buttons[_current].Button.onClick.Invoke();
            }
        }
    }

    /// <summary>OPTIONS 버튼. 인게임 ESC 메뉴와 같은 옵션창을 연다.</summary>
    public void OnClickOptions()
    {
        GameMenuHost.OpenOptionsFromTitle();
    }

    /// <summary>
    /// OPTIONS 버튼을 준비한다. 씬에 연결된 버튼이 있으면 클릭만 잇고, 없으면 마지막(QUIT) 버튼을 복제해 바로 위에 끼운다.
    /// 반드시 위치(_home)를 기록하기 전에 부른다.
    /// </summary>
    private void EnsureOptionsButton()
    {
        if (optionsButton != null)
        {
            optionsButton.Button.onClick.RemoveListener(OnClickOptions);
            optionsButton.Button.onClick.AddListener(OnClickOptions);
            return;
        }

        if (buttons == null || buttons.Length < 2)
        {
            return;
        }

        int last = buttons.Length - 1;
        TitleMenuButton quit = buttons[last];
        RectTransform quitRect = (RectTransform)quit.transform;
        RectTransform aboveRect = (RectTransform)buttons[last - 1].transform;

        // 버튼 사이 간격(기울어진 배치라 x 도 조금씩 어긋난다). 새 버튼은 QUIT 바로 위 칸, 그 위 버튼들은 한 칸씩 올린다
        // — 아래쪽 끝(QUIT)이 화면 밖으로 밀려나지 않게 위로 늘린다.
        Vector2 step = quitRect.anchoredPosition - aboveRect.anchoredPosition;
        Vector2 optionsPosition = aboveRect.anchoredPosition;

        for (int i = 0; i < last; i++)
        {
            RectTransform rect = (RectTransform)buttons[i].transform;
            rect.anchoredPosition -= step;
        }

        GameObject clone = Instantiate(quit.gameObject, quit.transform.parent);
        clone.name = "OptionsButton (Generated)";
        clone.transform.SetSiblingIndex(quit.transform.GetSiblingIndex());
        ((RectTransform)clone.transform).anchoredPosition = optionsPosition;

        optionsButton = clone.GetComponent<TitleMenuButton>();

        // 복제하면 QUIT 의 클릭 연결(종료)까지 따라온다. 이벤트를 통째로 바꿔 지운다.
        optionsButton.Button.onClick = new Button.ButtonClickedEvent();
        optionsButton.Button.onClick.AddListener(OnClickOptions);
        optionsButton.UseGeneratedLabel(GeneratedOptionsLabel);

        TitleMenuButton[] expanded = new TitleMenuButton[buttons.Length + 1];

        for (int i = 0; i < last; i++)
        {
            expanded[i] = buttons[i];
        }

        expanded[last] = optionsButton;
        expanded[last + 1] = quit;
        buttons = expanded;
    }

    /// <summary>TUTORIAL 버튼(onClick 연결용). 혼자 Host 로 접속해 튜토리얼 씬으로 간다.</summary>
    public void OnClickTutorial()
    {
        if (GameManager.Instance != null)
        {
            _ = GameManager.Instance.StartTutorialAsync();
        }
    }
}

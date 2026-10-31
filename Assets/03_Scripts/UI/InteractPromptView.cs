using Pawntom.TutorialBridge;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 화면 중앙 아래의 "[E] 이름" 안내. <see cref="InteractPrompt.Show"/> 가 요청한 프레임에만 보인다.
///
/// 구성:
/// - 왼쪽 키 칩(둥근 흰 네모 + 키 글자), 오른쪽 이름(흰 글씨 + 어두운 테두리). 씬·프리팹 없이 코드로 만든다.
/// - 키 글자는 상호작용 입력 바인딩에서 읽는다(바인딩을 바꾸면 따라간다). 읽지 못하면 E.
/// - 폰트는 한글이 모자라지 않도록 튜토리얼 UI 와 같은 방식의 동적 폰트를 쓴다(<see cref="TutorialUIFactory"/>).
/// </summary>
[DisallowMultipleComponent]
public sealed class InteractPromptView : MonoBehaviour
{
    private const float FadeSpeed = 14f;
    private const float ChipSize = 52f;

    private static InteractPromptView _instance;

    private TutorialUIFactory _factory;
    private CanvasGroup _group;
    private TextMeshProUGUI _nameText;
    private TextMeshProUGUI _keyText;
    private Texture2D _chipTexture;
    private Sprite _chipSprite;

    private string _requested;
    private string _shown;
    private int _lastRequestFrame = -10;

    public static InteractPromptView GetOrCreate()
    {
        if (_instance == null)
        {
            GameObject go = new GameObject("InteractPrompt");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<InteractPromptView>();
        }

        return _instance;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _instance = null;
    }

    public void Request(string interactName)
    {
        _requested = interactName;
        _lastRequestFrame = Time.frameCount;
    }

    private void Awake()
    {
        Build();
    }

    private void OnDestroy()
    {
        if (_chipSprite != null)
        {
            Destroy(_chipSprite);
        }

        if (_chipTexture != null)
        {
            Destroy(_chipTexture);
        }

        if (_factory != null)
        {
            _factory.DestroyOwnedTextures();
        }

        if (_instance == this)
        {
            _instance = null;
        }
    }

    private void LateUpdate()
    {
        bool visible = Time.frameCount - _lastRequestFrame <= 1;

        if (visible && _requested != _shown)
        {
            _shown = _requested;
            _nameText.text = _shown;
        }

        float target = visible ? 1f : 0f;
        _group.alpha = Mathf.MoveTowards(_group.alpha, target, FadeSpeed * Time.unscaledDeltaTime);
    }

    private void Build()
    {
        _factory = new TutorialUIFactory(null);

        Canvas canvas = _factory.CreateCanvas("InteractPromptCanvas", 40, transform);

        // 한 줄 묶음: 화면 중앙에서 조금 아래. 내용 폭에 맞춰 늘어난다.
        RectTransform row = _factory.CreateRect("Row", canvas.transform);
        row.anchorMin = new Vector2(0.5f, 0.5f);
        row.anchorMax = new Vector2(0.5f, 0.5f);
        row.pivot = new Vector2(0.5f, 0.5f);
        row.anchoredPosition = new Vector2(0f, -135f);

        _group = row.gameObject.AddComponent<CanvasGroup>();
        _group.alpha = 0f;
        _group.blocksRaycasts = false;
        _group.interactable = false;

        HorizontalLayoutGroup layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 14f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = row.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // 키 칩
        Image chip = _factory.CreateImage("KeyChip", row, new Color(1f, 1f, 1f, 0.94f));
        chip.sprite = CreateChipSprite();
        chip.type = Image.Type.Sliced;
        LayoutElement chipSize = chip.gameObject.AddComponent<LayoutElement>();
        chipSize.preferredWidth = ChipSize;
        chipSize.preferredHeight = ChipSize;

        _keyText = _factory.CreateText("KeyText", chip.transform, 32f, new Color(0.16f, 0.12f, 0.09f, 1f), TextAlignmentOptions.Center);
        _keyText.fontStyle = FontStyles.Bold;
        _keyText.text = ReadInteractKey();
        Stretch(_keyText.rectTransform);

        // 이름
        _nameText = _factory.CreateText("Name", row, 36f, Color.white, TextAlignmentOptions.MidlineLeft);
        _nameText.fontStyle = FontStyles.Bold;
        _nameText.enableWordWrapping = false;
        _nameText.outlineWidth = 0.22f;
        _nameText.outlineColor = new Color32(24, 16, 10, 255);
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    // 둥근 네모 스프라이트(9분할). 코드로 그려서 에셋이 필요 없다.
    private Sprite CreateChipSprite()
    {
        const int size = 64;
        const float radius = 16f;
        _chipTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "InteractChip",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.DontSave
        };

        Color[] pixels = new Color[size * size];
        float half = size * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Max(Mathf.Abs(x + 0.5f - half) - (half - radius), 0f);
                float dy = Mathf.Max(Mathf.Abs(y + 0.5f - half) - (half - radius), 0f);
                float outside = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = Mathf.Clamp01(radius - outside + 0.5f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        _chipTexture.SetPixels(pixels);
        _chipTexture.Apply();

        _chipSprite = Sprite.Create(
            _chipTexture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect,
            new Vector4(radius, radius, radius, radius));
        _chipSprite.hideFlags = HideFlags.DontSave;
        return _chipSprite;
    }

    private static string ReadInteractKey()
    {
        InputSystemActions actions = null;

        try
        {
            actions = new InputSystemActions();
            // 길게 누르기 같은 상호작용 문구("HOLD E")는 빼고 키 이름만 읽는다.
            string display = actions.Player.Interact.GetBindingDisplayString(
                InputBinding.DisplayStringOptions.DontIncludeInteractions, "Keyboard&Mouse");

            if (!string.IsNullOrEmpty(display))
            {
                return display.ToUpperInvariant();
            }
        }
        catch (System.Exception)
        {
            // 바인딩을 못 읽어도 안내는 떠야 한다.
        }
        finally
        {
            if (actions != null)
            {
                actions.Dispose();
            }
        }

        return "E";
    }
}

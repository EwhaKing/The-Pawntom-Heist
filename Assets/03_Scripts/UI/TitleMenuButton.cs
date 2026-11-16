using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 타이틀 메뉴 버튼 하나. 기본(검은 막대)·선택(흰 막대) 두 이미지를 바꿔 끼우고,
/// 마우스를 올리면 메뉴에 "이 버튼을 선택"이라고 알린다. 실제 동작은 같은 오브젝트의 <see cref="Button"/> onClick.
/// </summary>
[RequireComponent(typeof(Image))]
[RequireComponent(typeof(Button))]
public class TitleMenuButton : MonoBehaviour, IPointerEnterHandler
{
    [SerializeField] private Sprite normalSprite;
    [SerializeField] private Sprite highlightSprite;

    [Tooltip("임시 잠금. 켜면 클릭·Enter 모두 막고 메뉴에서 흐리게 보인다. 개발이 끝나면 끈다.")]
    [SerializeField] private bool underDevelopment;

    public bool UnderDevelopment => underDevelopment;

    private Image _image;
    private Button _button;
    private TitleMenuUI _menu;

    private bool _initialized;

    // 전용 아트가 없는 버튼(OPTIONS)용: 코드로 그린 막대 스프라이트 + 글자. UseGeneratedLabel 참고.
    private TextMeshProUGUI _generatedLabel;
    private Texture2D _generatedNormalTexture;
    private Texture2D _generatedHighlightTexture;

    public Button Button
    {
        get
        {
            Initialize();
            return _button;
        }
    }

    private void Awake()
    {
        Initialize();
    }

    // TitleMenuUI.Awake 가 이 버튼의 Awake 보다 먼저 실행되면 SetHighlighted 가 먼저 불린다(오브젝트 사이 Awake 순서는 정해져 있지 않다).
    // 그때 _image 가 비어 NullReferenceException 이 나고, 콘솔 Error Pause 가 켜져 있으면 플레이가 2프레임째에 멈춘다.
    // 그래서 필요한 참조를 쓰기 직전에 한 번만 채운다.
    private void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        _image = GetComponent<Image>();
        _button = GetComponent<Button>();

        // 상태 표시는 이 스크립트가 맡는다(Unity 기본 색 변화와 겹치지 않게).
        _button.transition = Selectable.Transition.None;
        Navigation none = new Navigation { mode = Navigation.Mode.None };
        _button.navigation = none;
        _button.interactable = !underDevelopment;
    }

    public void Bind(TitleMenuUI menu)
    {
        _menu = menu;
    }

    public void SetHighlighted(bool highlighted)
    {
        Initialize();

        Sprite sprite = highlighted ? highlightSprite : normalSprite;

        if (_image.sprite != sprite)
        {
            _image.sprite = sprite;

            // 두 이미지 폭이 1px 다르다. 원본 크기로 맞춰 1:1 픽셀로 그린다(늘이거나 줄이면 글자가 흐려진다).
            _image.SetNativeSize();
        }

        if (_generatedLabel != null)
        {
            // 기존 아트와 같은 규칙: 평소 = 검은 막대 + 흰 글자, 선택 = 흰 막대 + 검은 글자.
            _generatedLabel.color = highlighted ? Color.black : Color.white;
        }
    }

    /// <summary>
    /// 글자가 구워진 전용 스프라이트가 없을 때, 기존 버튼과 같은 크기의 기울어진 막대를 코드로 그리고 그 위에 글자를 얹는다.
    /// (복제한 버튼에 쓴다. 전용 아트가 생기면 normalSprite/highlightSprite 를 넣은 진짜 버튼으로 바꾼다.)
    /// </summary>
    public void UseGeneratedLabel(string label)
    {
        Initialize();

        Vector2Int size = normalSprite != null
            ? new Vector2Int((int)normalSprite.rect.width, (int)normalSprite.rect.height)
            : new Vector2Int(368, 82);

        normalSprite = CreateBarSprite(size, Color.black, out _generatedNormalTexture);
        highlightSprite = CreateBarSprite(size, Color.white, out _generatedHighlightTexture);

        if (_generatedLabel == null)
        {
            RuntimeUIFactory factory = new RuntimeUIFactory(RuntimeUIFactory.Shared.Font);
            _generatedLabel = factory.CreateText("GeneratedLabel", transform, 40f, Color.white, TextAlignmentOptions.Center);
            _generatedLabel.fontStyle = FontStyles.Bold | FontStyles.Italic;
            _generatedLabel.characterSpacing = 2f;
            _generatedLabel.textWrappingMode = TextWrappingModes.NoWrap;
            RuntimeUIFactory.Stretch(_generatedLabel.rectTransform);
        }

        _generatedLabel.text = label;
        _image.sprite = null; // 다음 SetHighlighted 가 새 스프라이트로 바꾸도록
    }

    // 기존 막대 아트를 흉내 낸 평행사변형(오른쪽으로 기울어짐). 가장자리는 1px 부드럽게.
    private static Sprite CreateBarSprite(Vector2Int size, Color color, out Texture2D texture)
    {
        const float Slant = 22f;
        const float VerticalMargin = 6f;

        int width = Mathf.Max(8, size.x);
        int height = Mathf.Max(8, size.y);
        texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.name = "TitleGeneratedBar";
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.hideFlags = HideFlags.DontSave;

        Color32[] pixels = new Color32[width * height];
        float top = height - VerticalMargin;

        for (int y = 0; y < height; y++)
        {
            float py = y + 0.5f;
            float vertical = Mathf.Clamp01(Mathf.Min(py - VerticalMargin, top - py) + 0.5f);
            float t = Mathf.InverseLerp(VerticalMargin, top, py);
            float left = Mathf.Lerp(0f, Slant, t);
            float right = Mathf.Lerp(width - Slant, width, t);

            for (int x = 0; x < width; x++)
            {
                float px = x + 0.5f;
                float horizontal = Mathf.Clamp01(Mathf.Min(px - left, right - px) + 0.5f);
                Color c = color;
                c.a = vertical * horizontal;
                pixels[y * width + x] = c;
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);

        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0f, 1f), 100f);
        sprite.name = "TitleGeneratedBar";
        sprite.hideFlags = HideFlags.DontSave;
        return sprite;
    }

    private void OnDestroy()
    {
        if (_generatedNormalTexture == null)
        {
            return;
        }

        Destroy(normalSprite);
        Destroy(highlightSprite);
        Destroy(_generatedNormalTexture);
        Destroy(_generatedHighlightTexture);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_menu != null)
        {
            _menu.Highlight(this);
        }
    }
}

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI 를 코드로 만드는 공용 도우미. 씬·프리팹(직렬화 에셋)을 고치지 않고 런타임에 계층을 조립할 때 쓴다
/// (옵션창·ESC 메뉴·상호작용 안내, 튜토리얼 UI 는 이것을 상속한 TutorialUIFactory).
/// 만든 텍스처는 <see cref="OwnedTextures"/>에 모아 두었다가 소유자가 파괴할 때 함께 정리한다.
/// </summary>
public class RuntimeUIFactory
{
    /// <summary>UI_SPEC 공통 기준 해상도.</summary>
    public static readonly Vector2 DefaultReferenceResolution = new Vector2(1800f, 950f);

    // 런타임 폰트 아틀라스. 한글이 수백 자라 여러 장을 허용한다.
    private const int RuntimeSamplingPointSize = 64;
    private const int RuntimeAtlasPadding = 6;
    private const int RuntimeAtlasSize = 2048;

    private static RuntimeUIFactory _shared;

    private readonly TMP_FontAsset _font;
    private readonly TMP_FontAsset _runtimeFont;
    private readonly Vector2 _referenceResolution;
    private readonly string _logTag;

    /// <summary>
    /// 게임 전체가 함께 쓰는 공용 인스턴스(폰트 아틀라스 하나를 공유한다). 파괴하지 않는다.
    /// 새 UI 는 <c>new RuntimeUIFactory(RuntimeUIFactory.Shared.Font)</c> 로 자기 텍스처 목록만 따로 가진다.
    /// </summary>
    public static RuntimeUIFactory Shared
    {
        get
        {
            if (_shared == null)
            {
                _shared = new RuntimeUIFactory(null, DefaultReferenceResolution, "Shared");
            }

            return _shared;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _shared = null;
    }

    public RuntimeUIFactory(TMP_FontAsset fontOverride)
        : this(fontOverride, DefaultReferenceResolution, "Runtime")
    {
    }

    /// <param name="fontOverride">
    /// 지정하면 그대로 쓴다. 비우면 TMP 기본 폰트(NanumBarunpenR SDF)의 원본 TTF 로 동적 폰트를 만든다.
    /// <para>
    /// 공용 폰트 에셋은 1024 아틀라스 한 장·90pt·여백 9·멀티 아틀라스 꺼짐이라 80자 남짓만 들어간다.
    /// 다른 UI 가 먼저 채우면 새 한글이 □로 나온다. 에셋 설정 변경은 직렬화 에셋 작업(R3)이므로
    /// 같은 TTF 로 만든 별도 아틀라스를 써서 영향을 받지 않게 한다.
    /// </para>
    /// </param>
    protected RuntimeUIFactory(TMP_FontAsset fontOverride, Vector2 referenceResolution, string fontLabel)
    {
        _referenceResolution = referenceResolution;
        _logTag = "[" + GetType().Name + "]";

        if (fontOverride != null)
        {
            _font = fontOverride;
            return;
        }

        TMP_FontAsset shared = TMP_Settings.defaultFontAsset;
        _runtimeFont = CreateRuntimeFont(shared, fontLabel, _logTag);
        _font = _runtimeFont != null ? _runtimeFont : shared;
    }

    public List<Texture2D> OwnedTextures { get; } = new List<Texture2D>();

    public TMP_FontAsset Font
    {
        get { return _font; }
    }

    /// <param name="interactive">
    /// true 면 GraphicRaycaster 를 붙여 버튼을 누를 수 있게 한다. 안내·HUD 처럼 보기만 하는 캔버스는 false —
    /// 클릭을 가로채면 본부 터미널·해킹 팝업 조작이 막힌다.
    /// </param>
    public Canvas CreateCanvas(string name, int sortingOrder, Transform parent, bool interactive = false)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        CanvasScaler scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = _referenceResolution;
        scaler.matchWidthOrHeight = 0.5f;

        if (interactive)
        {
            go.AddComponent<GraphicRaycaster>();
        }

        return canvas;
    }

    public RectTransform CreateRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    public Image CreateImage(string name, Transform parent, Color color)
    {
        RectTransform rect = CreateRect(name, parent);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    public RawImage CreateRawImage(string name, Transform parent, Texture texture, Color color)
    {
        RectTransform rect = CreateRect(name, parent);
        RawImage image = rect.gameObject.AddComponent<RawImage>();
        image.texture = texture;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    public TextMeshProUGUI CreateText(string name, Transform parent, float fontSize, Color color, TextAlignmentOptions alignment)
    {
        RectTransform rect = CreateRect(name, parent);
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();

        if (_font != null)
        {
            text.font = _font;
        }

        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Overflow;
        return text;
    }

    // ── 배치 ──

    public static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    public static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    public static void Inset(RectTransform rect, float left, float right, float top, float bottom)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }

    // ── 텍스처 ──

    /// <summary>이 팩토리가 소유하는(정리 대상) 빈 텍스처.</summary>
    protected Texture2D NewTexture(int width, int height, string name)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.name = name;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        texture.hideFlags = HideFlags.DontSave;
        OwnedTextures.Add(texture);
        return texture;
    }

    /// <summary>
    /// 쓸 글자를 미리 아틀라스에 굽는다. 타자 출력 도중 글자마다 굽느라 끊기지 않도록 시작 때 한 번 부른다.
    /// </summary>
    public void Prewarm(string characters)
    {
        if (_runtimeFont == null || string.IsNullOrEmpty(characters))
        {
            return;
        }

        string missing;

        if (!_runtimeFont.TryAddCharacters(characters, out missing) && !string.IsNullOrEmpty(missing))
        {
            Debug.LogWarning(_logTag + " 원본 폰트에 없는 글자: " + missing);
        }
    }

    private static TMP_FontAsset CreateRuntimeFont(TMP_FontAsset shared, string label, string logTag)
    {
        Font source = shared != null ? shared.sourceFontFile : null;

        if (source == null)
        {
            Debug.LogWarning(logTag + " 기본 폰트의 원본 TTF 를 찾지 못해 공용 폰트 에셋을 그대로 씁니다(한글이 □로 보일 수 있음).");
            return null;
        }

        TMP_FontAsset font = TMP_FontAsset.CreateFontAsset(
            source,
            RuntimeSamplingPointSize,
            RuntimeAtlasPadding,
            UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,
            RuntimeAtlasSize,
            RuntimeAtlasSize,
            AtlasPopulationMode.Dynamic,
            true);

        if (font == null)
        {
            Debug.LogWarning(logTag + " 런타임 폰트 생성에 실패해 공용 폰트 에셋을 그대로 씁니다.");
            return null;
        }

        font.name = label + " Runtime (" + source.name + ")";
        font.hideFlags = HideFlags.DontSave;
        return font;
    }

    /// <summary>이 팩토리가 만든 런타임 폰트를 파괴한다. 공용 인스턴스(<see cref="Shared"/>)에는 부르지 않는다.</summary>
    public void DestroyRuntimeFont()
    {
        if (_runtimeFont == null || ReferenceEquals(this, _shared))
        {
            return;
        }

        Texture2D[] atlases = _runtimeFont.atlasTextures;

        if (atlases != null)
        {
            for (int i = 0; i < atlases.Length; i++)
            {
                if (atlases[i] != null)
                {
                    Object.Destroy(atlases[i]);
                }
            }
        }

        if (_runtimeFont.material != null)
        {
            Object.Destroy(_runtimeFont.material);
        }

        Object.Destroy(_runtimeFont);
    }

    public void DestroyOwnedTextures()
    {
        for (int i = 0; i < OwnedTextures.Count; i++)
        {
            if (OwnedTextures[i] != null)
            {
                Object.Destroy(OwnedTextures[i]);
            }
        }

        OwnedTextures.Clear();
    }
}

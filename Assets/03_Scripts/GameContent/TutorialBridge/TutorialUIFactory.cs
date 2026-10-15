using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Pawntom.TutorialBridge
{
    /// <summary>
    /// 튜토리얼 UI를 코드로 만드는 도우미. 씬·프리팹을 텍스트로 고칠 수 없으므로 런타임에 계층을 조립한다.
    /// 만든 텍스처는 <see cref="OwnedTextures"/>에 모아 두었다가 소유자가 파괴할 때 함께 정리한다.
    /// </summary>
    public sealed class TutorialUIFactory
    {
        // 튜토리얼 전용 런타임 폰트 아틀라스. 대사·안내에 쓰이는 한글이 수백 자라 여러 장을 허용한다.
        private const int RuntimeSamplingPointSize = 64;
        private const int RuntimeAtlasPadding = 6;
        private const int RuntimeAtlasSize = 2048;

        private readonly TMP_FontAsset _font;
        private readonly TMP_FontAsset _runtimeFont;

        /// <param name="fontOverride">
        /// 지정하면 그대로 쓴다. 비우면 TMP 기본 폰트(NanumBarunpenR SDF)의 원본 TTF 로 튜토리얼 전용 동적 폰트를 만든다.
        /// <para>
        /// 공용 폰트 에셋은 1024 아틀라스 한 장·90pt·여백 9·멀티 아틀라스 꺼짐이라 80자 남짓만 들어간다.
        /// 다른 UI 가 먼저 채우면 새 한글이 □로 나온다. 에셋 설정 변경은 직렬화 에셋 작업(R3)이므로
        /// 튜토리얼은 같은 TTF 로 만든 별도 아틀라스를 써서 영향을 받지 않게 한다.
        /// </para>
        /// </param>
        public TutorialUIFactory(TMP_FontAsset fontOverride)
        {
            if (fontOverride != null)
            {
                _font = fontOverride;
                return;
            }

            TMP_FontAsset shared = TMP_Settings.defaultFontAsset;
            _runtimeFont = CreateRuntimeFont(shared);
            _font = _runtimeFont != null ? _runtimeFont : shared;
        }

        public List<Texture2D> OwnedTextures { get; } = new List<Texture2D>();

        public TMP_FontAsset Font
        {
            get { return _font; }
        }

        public Canvas CreateCanvas(string name, int sortingOrder, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            Canvas canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            CanvasScaler scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = TutorialTheme.ReferenceResolution;
            scaler.matchWidthOrHeight = 0.5f;

            // 클릭을 가로채지 않도록 GraphicRaycaster 는 붙이지 않는다(본부 터미널·해킹 팝업 조작을 막으면 안 된다).
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

        // ── 절차적 텍스처 ──

        /// <summary>
        /// 위 눈꺼풀. 텍스처 아래쪽(v=0)에 볼록한 곡선 가장자리가 있고 그 위는 불투명 검정이다.
        /// 가운데가 더 높게 패여 두 꺼풀 사이가 아몬드 모양으로 열린다. 아래 꺼풀은 UV 를 뒤집어 같은 텍스처를 쓴다.
        /// </summary>
        public Texture2D CreateEyelidTexture(int width, int height, float curve, float feather)
        {
            Texture2D texture = NewTexture(width, height, "TutorialEyelid");
            Color32[] pixels = new Color32[width * height];

            for (int y = 0; y < height; y++)
            {
                float v = (y + 0.5f) / height;

                for (int x = 0; x < width; x++)
                {
                    float u = (x + 0.5f) / width;
                    float centered = u * 2f - 1f;
                    float edge = curve * (1f - centered * centered);
                    float alpha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(edge - feather, edge + feather, v));
                    pixels[y * width + x] = new Color32(0, 0, 0, (byte)(alpha * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        /// <summary>가장자리로 갈수록 짙어지는 원형 비네트(중앙 투명).</summary>
        public Texture2D CreateVignetteTexture(int size, float inner, float outer)
        {
            Texture2D texture = NewTexture(size, size, "TutorialVignette");
            Color32[] pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f;
                    float dy = (y + 0.5f) / size * 2f - 1f;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(inner, outer, distance));
                    pixels[y * size + x] = new Color32(0, 0, 0, (byte)(alpha * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        /// <summary>가로 주사선. RawImage.uvRect 로 세로 반복해 쓴다.</summary>
        public Texture2D CreateScanlineTexture()
        {
            Texture2D texture = NewTexture(1, 4, "TutorialScanline");
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.filterMode = FilterMode.Point;
            texture.SetPixels32(new[]
            {
                new Color32(255, 255, 255, 255),
                new Color32(255, 255, 255, 0),
                new Color32(255, 255, 255, 0),
                new Color32(255, 255, 255, 120)
            });
            texture.Apply(false, true);
            return texture;
        }

        private Texture2D NewTexture(int width, int height, string name)
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
        /// 쓸 글자를 미리 아틀라스에 굽는다. 타자 출력 도중 글자마다 굽느라 끊기지 않도록 씬 시작 때 한 번 부른다.
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
                Debug.LogWarning("[TutorialUIFactory] 원본 폰트에 없는 글자: " + missing);
            }
        }

        private static TMP_FontAsset CreateRuntimeFont(TMP_FontAsset shared)
        {
            Font source = shared != null ? shared.sourceFontFile : null;

            if (source == null)
            {
                Debug.LogWarning("[TutorialUIFactory] 기본 폰트의 원본 TTF 를 찾지 못해 공용 폰트 에셋을 그대로 씁니다(한글이 □로 보일 수 있음).");
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
                Debug.LogWarning("[TutorialUIFactory] 런타임 폰트 생성에 실패해 공용 폰트 에셋을 그대로 씁니다.");
                return null;
            }

            font.name = "Tutorial Runtime (" + source.name + ")";
            font.hideFlags = HideFlags.DontSave;
            return font;
        }

        public void DestroyRuntimeFont()
        {
            if (_runtimeFont == null)
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
}

using TMPro;
using UnityEngine;

namespace Pawntom.TutorialBridge
{
    /// <summary>
    /// 튜토리얼 UI를 코드로 만드는 도우미. 공용 <see cref="RuntimeUIFactory"/>(캔버스·텍스트·배치·런타임 폰트)에
    /// 튜토리얼 연출 전용 절차적 텍스처(눈꺼풀·비네트·화살표·주사선)를 더한다.
    /// 튜토리얼은 대사 한글이 수백 자라 공용 아틀라스와 섞이지 않도록 자기 런타임 폰트를 따로 만든다.
    /// </summary>
    public sealed class TutorialUIFactory : RuntimeUIFactory
    {
        /// <param name="fontOverride">지정하면 그대로 쓴다. 비우면 튜토리얼 전용 동적 폰트를 만든다.</param>
        public TutorialUIFactory(TMP_FontAsset fontOverride)
            : base(fontOverride, TutorialTheme.ReferenceResolution, "Tutorial")
        {
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

        /// <summary>
        /// 아래를 가리키는 흰 삼각형(가장자리 부드럽게). 대사 "다음" 표시용 — 폰트에 ▼ 글리프가 없어도 깨지지 않도록 그림으로 만든다.
        /// </summary>
        public Texture2D CreateDownTriangleTexture(int width, int height)
        {
            Texture2D texture = NewTexture(width, height, "TutorialNextArrow");
            Color32[] pixels = new Color32[width * height];

            for (int y = 0; y < height; y++)
            {
                // y = 0 이 아래(꼭짓점), y = height - 1 이 위(밑변)
                float row = (y + 0.5f) / height;
                float halfWidth = row * width * 0.5f;

                for (int x = 0; x < width; x++)
                {
                    float fromCenter = Mathf.Abs(x + 0.5f - width * 0.5f);
                    float alpha = Mathf.Clamp01(halfWidth - fromCenter + 0.5f);
                    pixels[y * width + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
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
    }
}

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 게임 씬 HUD 가 쓰는 코드 생성 스프라이트(원형 링·붉은 비네트). 에셋이 필요 없다. 소유자가 <see cref="Dispose"/> 로 정리한다.
/// </summary>
public sealed class StageHudSprites
{
    private readonly List<Object> _owned = new List<Object>(4);

    /// <summary>얇은 링. Image(Filled, Radial360) 로 시계방향 게이지를 만든다.</summary>
    public Sprite Ring { get; private set; }

    /// <summary>가장자리가 진한 방사형 비네트(중앙 투명).</summary>
    public Sprite Vignette { get; private set; }

    public StageHudSprites()
    {
        Ring = CreateRing(128, 0.78f, 1f);
        Vignette = CreateVignette(128);
    }

    public void Dispose()
    {
        for (int i = 0; i < _owned.Count; i++)
        {
            if (_owned[i] != null)
            {
                Object.Destroy(_owned[i]);
            }
        }

        _owned.Clear();
        Ring = null;
        Vignette = null;
    }

    private Sprite CreateRing(int size, float innerRadius, float outerRadius)
    {
        var pixels = new Color[size * size];
        float half = size * 0.5f;
        float feather = 1.5f / half;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f - half) / half;
                float dy = (y + 0.5f - half) / half;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float inside = Mathf.Clamp01((r - innerRadius) / feather + 0.5f);
                float outside = Mathf.Clamp01((outerRadius - r) / feather + 0.5f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Min(inside, outside));
            }
        }

        return Bake(pixels, size, "StageHudRing");
    }

    private Sprite CreateVignette(int size)
    {
        var pixels = new Color[size * size];
        float half = size * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f - half) / half;
                float dy = (y + 0.5f - half) / half;
                float r = Mathf.Sqrt(dx * dx + dy * dy) / 1.414f; // 모서리가 1
                float alpha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.3f, 1f, r));
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        return Bake(pixels, size, "StageHudVignette");
    }

    private Sprite Bake(Color[] pixels, int size, string name)
    {
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = name,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.DontSave
        };
        texture.SetPixels(pixels);
        texture.Apply();

        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        sprite.name = name;
        sprite.hideFlags = HideFlags.DontSave;
        _owned.Add(texture);
        _owned.Add(sprite);
        return sprite;
    }
}

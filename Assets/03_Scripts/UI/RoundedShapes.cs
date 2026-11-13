using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 터미널 지도의 도형을 둥글게 만들기 위한 도구.
/// - RoundedSprite: 어떤 크기로 늘려도 모서리가 둥근 9-slice 스프라이트(UI 격벽 버튼용)
/// - BuildBarMesh: 미니맵 위에서 내려다본 알약(pill) 모양 평면 메쉬(3D 격벽 막대용)
/// </summary>
public static class RoundedShapes
{
    private const int SpriteSize = 64;
    private const int ArcSteps = 8;

    private static Sprite _roundedSprite;

    /// <summary>가장자리가 부드럽게 안티앨리어싱된 알약 스프라이트. Image.Type.Sliced 로 쓴다.</summary>
    public static Sprite RoundedSprite
    {
        get
        {
            if (_roundedSprite == null)
            {
                _roundedSprite = CreateRoundedSprite();
            }

            return _roundedSprite;
        }
    }

    private static Sprite CreateRoundedSprite()
    {
        int size = SpriteSize;
        float radius = size * 0.5f;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "RoundedShape",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.HideAndDontSave
        };

        Color32[] pixels = new Color32[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - radius;
                float dy = y + 0.5f - radius;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = Mathf.Clamp01(radius - distance); // 1px 안티앨리어싱
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);

        // 모서리 반지름 = 절반이므로 border 도 절반. 작은 버튼에서는 자동으로 줄어들어 알약이 된다.
        Vector4 border = new Vector4(radius - 1f, radius - 1f, radius - 1f, radius - 1f);
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect, border);
        sprite.name = "RoundedShape";
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }

    /// <summary>
    /// 월드 크기 (lossyX, lossyZ) 의 막대를 위에서 본 알약 모양으로 만든다.
    /// 메쉬는 로컬 좌표(-0.5~0.5, y=+0.5 윗면)에 만들어 큐브와 같은 자리·크기에 겹친다.
    /// openGapFraction 이 0보다 크면 가운데를 비우고 양 끝 조각 둘만 만든다(열린 문).
    /// </summary>
    public static Mesh BuildBarMesh(Vector3 lossyScale, bool openGap, float endFraction)
    {
        float sx = Mathf.Max(0.0001f, Mathf.Abs(lossyScale.x));
        float sz = Mathf.Max(0.0001f, Mathf.Abs(lossyScale.z));

        bool longAlongX = sx >= sz;
        float length = longAlongX ? sx : sz;
        float thickness = longAlongX ? sz : sx;

        // 막대가 너무 얇으면 둥근 끝이 보이지 않으므로 반지름은 두께의 절반으로 한다.
        float radius = thickness * 0.5f;

        List<Vector3> vertices = new List<Vector3>();
        List<Vector3> normals = new List<Vector3>();
        List<int> triangles = new List<int>();

        if (openGap)
        {
            float piece = Mathf.Max(thickness, length * endFraction);
            piece = Mathf.Min(piece, length * 0.45f);
            AddPill(vertices, normals, triangles, -length * 0.5f + piece * 0.5f, 0f, piece, thickness, radius, longAlongX, sx, sz);
            AddPill(vertices, normals, triangles, length * 0.5f - piece * 0.5f, 0f, piece, thickness, radius, longAlongX, sx, sz);
        }
        else
        {
            AddPill(vertices, normals, triangles, 0f, 0f, length, thickness, radius, longAlongX, sx, sz);
        }

        Mesh mesh = new Mesh { name = "GateBarPill", hideFlags = HideFlags.HideAndDontSave };
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    // along: 긴 축 방향 중심 위치(월드 단위), across: 짧은 축 방향 중심 위치.
    private static void AddPill(List<Vector3> vertices, List<Vector3> normals, List<int> triangles,
        float along, float across, float length, float thickness, float radius,
        bool longAlongX, float sx, float sz)
    {
        float halfLength = length * 0.5f;
        float halfThickness = thickness * 0.5f;
        radius = Mathf.Min(radius, Mathf.Min(halfLength, halfThickness));

        // 모서리 4개에 호를 그려 외곽선 점들을 반시계(위에서 볼 때) 순서로 모은다.
        List<Vector2> outline = new List<Vector2>();
        Vector2[] centers =
        {
            new Vector2(halfLength - radius, halfThickness - radius),
            new Vector2(-halfLength + radius, halfThickness - radius),
            new Vector2(-halfLength + radius, -halfThickness + radius),
            new Vector2(halfLength - radius, -halfThickness + radius)
        };

        for (int corner = 0; corner < 4; corner++)
        {
            float start = corner * 90f;

            for (int step = 0; step <= ArcSteps; step++)
            {
                float angle = (start + 90f * step / ArcSteps) * Mathf.Deg2Rad;
                outline.Add(centers[corner] + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
            }
        }

        int baseIndex = vertices.Count;
        vertices.Add(ToLocal(along, across, 0f, 0f, longAlongX, sx, sz));
        normals.Add(Vector3.up);

        for (int i = 0; i < outline.Count; i++)
        {
            vertices.Add(ToLocal(along, across, outline[i].x, outline[i].y, longAlongX, sx, sz));
            normals.Add(Vector3.up);
        }

        for (int i = 0; i < outline.Count; i++)
        {
            int a = baseIndex + 1 + i;
            int b = baseIndex + 1 + (i + 1) % outline.Count;

            // 앞·뒤 양면으로 만들어 셰이더가 어느 쪽을 컬링해도 보이게 한다.
            triangles.Add(baseIndex);
            triangles.Add(b);
            triangles.Add(a);
            triangles.Add(baseIndex);
            triangles.Add(a);
            triangles.Add(b);
        }
    }

    private static Vector3 ToLocal(float along, float across, float offsetAlong, float offsetAcross,
        bool longAlongX, float sx, float sz)
    {
        float worldAlong = along + offsetAlong;
        float worldAcross = across + offsetAcross;

        float worldX = longAlongX ? worldAlong : worldAcross;
        float worldZ = longAlongX ? worldAcross : worldAlong;

        // 월드 단위를 큐브 로컬(스케일 전) 좌표로 되돌린다.
        return new Vector3(worldX / sx, 0.5f, worldZ / sz);
    }
}

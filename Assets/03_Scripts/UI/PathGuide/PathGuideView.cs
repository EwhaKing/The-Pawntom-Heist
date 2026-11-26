using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 길 안내 화살표를 그린다. 꺾이는 점 목록(<see cref="SetRoute"/>)을 받아 둥글게 다듬고, 바닥에 초록 화살표(⌃) 띠를 깔아
/// 무늬가 목적지 쪽으로 흐르게 한다. 점프 칸 위에서는 띠가 장애물 위로 솟았다 내려온다.
/// <para>
/// 에셋 없이 만든다(런타임 메시 + 코드로 그린 텍스처, 셰이더 Sprites/Default). 정점은 월드 좌표라 부모 없이 원점에 둔다.
/// 길을 고르는 일은 하지 않는다 — <see cref="PathGuide"/> 가 정해서 넘긴다.
/// </para>
/// </summary>
public sealed class PathGuideView : MonoBehaviour
{
    // 색은 연초록 계열. 바꾸려면 여기만 고친다.
    private static readonly Color ArrowColor = new Color(0.5f, 1f, 0.45f, 0.95f);

    private const float Width = 0.55f;            // 띠 너비(m)
    private const float ArrowLength = 0.8f;       // 화살표 하나가 차지하는 길이(m)
    private const float ScrollSpeed = 1.4f;       // 무늬가 흐르는 속도(m/초)
    private const float FloorOffset = 0.04f;      // 바닥에서 띄우는 높이(겹침 깜빡임 방지)
    private const float StartGap = 0.25f;         // 발밑에서 이만큼 앞부터 그린다
    private const float FadeInLength = 0.8f;      // 시작 쪽이 서서히 진해지는 길이
    private const float FadeOutLength = 0.6f;     // 끝 쪽이 서서히 옅어지는 길이
    private const float SampleStep = 0.2f;        // 띠를 나누는 간격
    private const float FadeSeconds = 0.3f;
    private const int MaxPoints = 768;

    // 장애물을 넘는 곳: 장애물 윗면보다 이만큼 위로, 앞뒤로 이 길이에 걸쳐 오르내린다.
    private const float JumpClearance = 0.12f;
    private const float JumpRampLength = 0.7f;

    private Mesh _mesh;
    private Material _material;
    private Texture2D _texture;
    private GameObject _root;

    private readonly List<Vector3> _route = new List<Vector3>(128);
    private readonly List<Vector3> _smooth = new List<Vector3>(256);
    private readonly List<Vector3> _samples = new List<Vector3>(MaxPoints);
    private readonly List<float> _distances = new List<float>(MaxPoints);
    private readonly List<float> _lifts = new List<float>(MaxPoints);
    private readonly List<float> _liftScratch = new List<float>(MaxPoints);
    private readonly List<Vector3> _vertices = new List<Vector3>(MaxPoints * 2);
    private readonly List<Vector2> _uvs = new List<Vector2>(MaxPoints * 2);
    private readonly List<Color> _colors = new List<Color>(MaxPoints * 2);
    private readonly List<int> _triangles = new List<int>(MaxPoints * 6);

    private float _totalLength;
    private float _scroll;
    private float _alpha;
    private float _targetAlpha;

    private void Awake()
    {
        Shader shader = Shader.Find("Sprites/Default");

        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        }

        _texture = CreateArrowTexture();
        _material = new Material(shader) { name = "PathGuide", hideFlags = HideFlags.DontSave };
        _material.mainTexture = _texture;
        _material.color = ArrowColor;
        _material.renderQueue = 3000;

        _mesh = new Mesh { name = "PathGuide", hideFlags = HideFlags.DontSave };
        _mesh.MarkDynamic();

        _root = new GameObject("PathGuideMesh");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(_root, gameObject.scene);

        _root.AddComponent<MeshFilter>().sharedMesh = _mesh;

        MeshRenderer meshRenderer = _root.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = _material;
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        meshRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        meshRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

        _root.SetActive(false);
    }

    /// <summary>꺾이는 점 목록(첫 점 = 발밑)으로 띠를 새로 깐다. <paramref name="grid"/> 로 바닥 높이·점프 높이를 맞춘다.</summary>
    public void SetRoute(List<Vector3> route, WalkGrid grid)
    {
        _route.Clear();
        _route.AddRange(route);

        Smooth();
        Resample();

        if (_samples.Count < 2)
        {
            Hide();
            return;
        }

        ComputeLifts(grid);
        _targetAlpha = 1f;

        if (!_root.activeSelf)
        {
            _root.SetActive(true);
        }

        RebuildMesh();
    }

    /// <summary>서서히 사라진다.</summary>
    public void Hide()
    {
        _targetAlpha = 0f;
    }

    private void Update()
    {
        if (_root == null || !_root.activeSelf)
        {
            return;
        }

        _alpha = Mathf.MoveTowards(_alpha, _targetAlpha, Time.unscaledDeltaTime / FadeSeconds);
        _scroll = Mathf.Repeat(_scroll + Time.unscaledDeltaTime * ScrollSpeed / ArrowLength, 1f);

        if (_alpha <= 0f && _targetAlpha <= 0f)
        {
            _root.SetActive(false);
            return;
        }

        RebuildMesh();
    }

    private void OnDestroy()
    {
        if (_material != null)
        {
            Destroy(_material);
        }

        if (_texture != null)
        {
            Destroy(_texture);
        }

        if (_mesh != null)
        {
            Destroy(_mesh);
        }

        if (_root != null)
        {
            Destroy(_root);
        }
    }

    // ───────────── 선 다듬기 ─────────────

    // 꺾이는 곳을 둥글게(Chaikin 2회). 처음과 끝 점은 그대로 둔다.
    private void Smooth()
    {
        _smooth.Clear();
        _smooth.AddRange(_route);

        for (int pass = 0; pass < 2 && _smooth.Count >= 3; pass++)
        {
            _route.Clear();
            _route.Add(_smooth[0]);

            for (int i = 0; i < _smooth.Count - 1; i++)
            {
                _route.Add(Vector3.Lerp(_smooth[i], _smooth[i + 1], 0.25f));
                _route.Add(Vector3.Lerp(_smooth[i], _smooth[i + 1], 0.75f));
            }

            _route.Add(_smooth[_smooth.Count - 1]);
            _smooth.Clear();
            _smooth.AddRange(_route);
        }
    }

    // 일정 간격으로 다시 나누고 누적 거리를 적는다. 발밑 StartGap 은 건너뛴다.
    private void Resample()
    {
        _samples.Clear();
        _distances.Clear();
        _totalLength = 0f;

        float walked = 0f;
        float nextSample = StartGap;

        for (int i = 0; i < _smooth.Count - 1 && _samples.Count < MaxPoints; i++)
        {
            Vector3 a = _smooth[i];
            Vector3 b = _smooth[i + 1];
            float length = Vector3.Distance(a, b);

            if (length <= 0.0001f)
            {
                continue;
            }

            while (nextSample <= walked + length && _samples.Count < MaxPoints)
            {
                _samples.Add(Vector3.Lerp(a, b, (nextSample - walked) / length));
                _distances.Add(nextSample - StartGap);
                nextSample += SampleStep;
            }

            walked += length;
        }

        if (walked > StartGap && _samples.Count < MaxPoints)
        {
            Vector3 last = _smooth[_smooth.Count - 1];

            if (_samples.Count == 0 || (last - _samples[_samples.Count - 1]).sqrMagnitude > 0.0025f)
            {
                _samples.Add(last);
                _distances.Add(walked - StartGap);
            }
        }

        _totalLength = _distances.Count > 0 ? _distances[_distances.Count - 1] : 0f;
    }

    // 점프 칸 위에서는 띠를 장애물 위로 올린다. 앞뒤로 넓히고(최댓값) 부드럽게(평균) 다듬는다.
    private void ComputeLifts(WalkGrid grid)
    {
        int count = _samples.Count;
        _lifts.Clear();
        _liftScratch.Clear();

        for (int i = 0; i < count; i++)
        {
            Vector3 p = _samples[i];
            float jump = grid != null ? grid.JumpHeightAt(p) : 0f;
            float floor = grid != null ? grid.FloorAt(p, p.y) : p.y;

            _samples[i] = new Vector3(p.x, floor, p.z);
            _lifts.Add(jump > 0f ? jump + JumpClearance : 0f);
            _liftScratch.Add(0f);
        }

        int ramp = Mathf.Max(1, Mathf.RoundToInt(JumpRampLength / SampleStep));
        int widen = Mathf.Max(1, ramp / 2);

        for (int i = 0; i < count; i++)
        {
            float best = 0f;

            for (int k = Mathf.Max(0, i - widen); k <= Mathf.Min(count - 1, i + widen); k++)
            {
                best = Mathf.Max(best, _lifts[k]);
            }

            _liftScratch[i] = best;
        }

        for (int i = 0; i < count; i++)
        {
            float sum = 0f;
            int n = 0;

            for (int k = Mathf.Max(0, i - ramp); k <= Mathf.Min(count - 1, i + ramp); k++)
            {
                sum += _liftScratch[k];
                n++;
            }

            _lifts[i] = Mathf.Max(_lifts[i], sum / n);
        }
    }

    // ───────────── 메시 ─────────────

    private void RebuildMesh()
    {
        int count = _samples.Count;

        _vertices.Clear();
        _uvs.Clear();
        _colors.Clear();
        _triangles.Clear();

        if (count < 2 || _lifts.Count != count)
        {
            _mesh.Clear();
            return;
        }

        float half = Width * 0.5f;

        for (int i = 0; i < count; i++)
        {
            Vector3 dir = _samples[Mathf.Min(i + 1, count - 1)] - _samples[Mathf.Max(i - 1, 0)];
            dir.y = 0f;

            if (dir.sqrMagnitude < 0.000001f)
            {
                dir = Vector3.forward;
            }

            dir.Normalize();
            Vector3 side = new Vector3(dir.z, 0f, -dir.x) * half; // 진행 방향의 오른쪽
            Vector3 center = _samples[i] + Vector3.up * (FloorOffset + _lifts[i]);

            _vertices.Add(center - side);
            _vertices.Add(center + side);

            // v 는 진행 방향으로 늘어난다. _scroll 만큼 빼서 무늬가 목적지 쪽으로 흐르게 한다.
            float v = _distances[i] / ArrowLength - _scroll;
            _uvs.Add(new Vector2(0f, v));
            _uvs.Add(new Vector2(1f, v));

            float d = _distances[i];
            float fade = Mathf.Clamp01(d / FadeInLength) * Mathf.Clamp01((_totalLength - d) / FadeOutLength + 0.35f);
            Color color = new Color(1f, 1f, 1f, fade * _alpha);
            _colors.Add(color);
            _colors.Add(color);

            if (i < count - 1)
            {
                int k = i * 2;
                _triangles.Add(k);
                _triangles.Add(k + 2);
                _triangles.Add(k + 1);
                _triangles.Add(k + 1);
                _triangles.Add(k + 2);
                _triangles.Add(k + 3);
            }
        }

        _mesh.Clear();
        _mesh.SetVertices(_vertices);
        _mesh.SetUVs(0, _uvs);
        _mesh.SetColors(_colors);
        _mesh.SetTriangles(_triangles, 0);
        _mesh.RecalculateBounds();
    }

    // ⌃ 모양 화살표 한 칸. u = 너비 방향(0~1), v = 진행 방향(0~1, 위쪽이 앞). 옅은 바탕 띠도 깐다.
    private static Texture2D CreateArrowTexture()
    {
        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "PathGuideArrow",
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.DontSave
        };

        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            float v = (y + 0.5f) / size;

            for (int x = 0; x < size; x++)
            {
                float au = Mathf.Abs(((x + 0.5f) / size) * 2f - 1f);
                float chevron = 1f - Edge(0.07f, 0.12f, Mathf.Abs(v - (0.72f - au * 0.42f)));
                chevron *= 1f - Edge(0.72f, 0.86f, au);
                float strip = 0.16f * (1f - Edge(0.75f, 1f, au));
                pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Max(chevron, strip));
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return texture;
    }

    // 셰이더의 smoothstep(edge0, edge1, x). (Unity 의 Mathf.SmoothStep 은 from→to 보간이라 다르다.)
    private static float Edge(float edge0, float edge1, float x)
    {
        float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
        return t * t * (3f - 2f * t);
    }
}
using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>
/// 털공의 생성 정보와 고정 위치를 동기화하고, 네트워크 시간으로 수명을 관리합니다.
/// </summary>
public class FurBallTrace : NetworkBehaviour
{
    public const float LifetimeSeconds = 10f;

    // 살아 있는 털공 전체. 감지 소스가 매 틱 물리 질의를 하지 않도록 여기에 모읍니다.
    private static readonly List<FurBallTrace> _active = new List<FurBallTrace>(64);

    [Tooltip("보이는 발자국 모양. 걷기/달리기에 따라 이 오브젝트의 크기만 바꿉니다(충돌체·판정은 그대로).")]
    [SerializeField] private Transform visual;

    [Networked] private TickTimer DespawnTimer { get; set; }

    // 발자국 크기 배율. 달릴 때 찍힌 발자국은 더 크게 보입니다. 모든 피어가 같은 크기를 보도록 동기화합니다.
    [Networked] private float VisualScale { get; set; }
    [Networked] private Vector3 SpawnPosition { get; set; }
    [Networked] private Quaternion SpawnRotation { get; set; }

    [Networked] public PlayerRef Creator { get; private set; }
    [Networked] public int Sequence { get; private set; }
    [Networked] public int SpawnTick { get; private set; }

    /// <summary>
    /// 현재 살아 있는 털공 전체입니다. 읽기 전용이며 스폰·디스폰에 따라 내용이 바뀝니다.
    /// </summary>
    public static IReadOnlyList<FurBallTrace> Active
    {
        get { return _active; }
    }

    /// <summary>
    /// 도메인 리로드가 꺼진 환경에서 이전 플레이 세션의 잔재가 목록에 남는 것을 막습니다.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetActive()
    {
        _active.Clear();
    }

    public override void Spawned()
    {
        if (Object.HasStateAuthority)
        {
            DespawnTimer = TickTimer.CreateFromSeconds(Runner, LifetimeSeconds);
            SpawnPosition = transform.position;
            SpawnRotation = transform.rotation;
        }

        // Runner.Spawn의 위치/회전 인자는 원격 클라이언트에 자동 전송되지 않습니다.
        // 생성 후 움직이지 않는 흔적이므로 최초 배치만 복원합니다.
        transform.SetPositionAndRotation(SpawnPosition, SpawnRotation);
        ApplyVisualScale();

        // 중복 추가 방지. 재진입이나 재스폰 경로에서 같은 개체가 두 번 들어올 수 있습니다.
        for (int i = 0; i < _active.Count; i++)
        {
            if (ReferenceEquals(_active[i], this))
            {
                return;
            }
        }

        _active.Add(this);
    }

    public override void FixedUpdateNetwork()
    {
        if (Object.HasStateAuthority && DespawnTimer.Expired(Runner))
        {
            Runner.Despawn(Object);
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        for (int i = 0; i < _active.Count; i++)
        {
            if (!ReferenceEquals(_active[i], this))
            {
                continue;
            }

            _active.RemoveAt(i);
            return;
        }
    }

    /// <summary>
    /// State Authority가 털공을 네트워크 스폰하기 직전에 호출합니다.
    /// </summary>
    public void Initialize(PlayerRef creator, int sequence, int spawnTick, float visualScale = 1f)
    {
        Creator = creator;
        Sequence = sequence;
        SpawnTick = spawnTick;
        VisualScale = visualScale;
    }

    private void ApplyVisualScale()
    {
        if (visual == null)
        {
            return;
        }

        float scale = VisualScale > 0f ? VisualScale : 1f;
        visual.localScale = Vector3.one * scale;
        BuildGlow();
    }

    // ───────────── 눈에 띄는 발자국(발광) ─────────────

    private static readonly Color GlowColor = new Color(0.9f, 1f, 0f, 1f); // 형광 노랑
    private const float GlowScale = 2.4f;

    private static Sprite _glowSprite;
    private SpriteRenderer _printRenderer;
    private SpriteRenderer _glowRenderer;
    private float _pulseOffset;

    /// <summary>
    /// 발자국을 형광 노랑 + 검정 테두리 고양이 발바닥으로 바꾸고 뒤에 부드러운 빛무리를 깐다. 바닥이 어두워도 어디를 지나왔는지 한눈에 보이고,
    /// 살아 있는 동안 은은하게 맥박치다 사라질 때 서서히 꺼진다. 모양·판정은 그대로(보이는 것만 바꾼다).
    /// </summary>
    private void BuildGlow()
    {
        if (_printRenderer != null)
        {
            return;
        }

        _printRenderer = visual.GetComponent<SpriteRenderer>();

        if (_printRenderer == null)
        {
            return;
        }

        _pulseOffset = Random.value * 6.28f;

        // 기존 발자국(곰발바닥 같은 그림)을 고양이 발바닥(큰 패드 + 작은 발가락 넷)으로 바꾼다. 크기는 기존 그림과 같게 맞춘다.
        float originalSize = 0.256f;

        if (_printRenderer.sprite != null)
        {
            Vector3 extents = _printRenderer.sprite.bounds.size;
            originalSize = Mathf.Max(0.01f, Mathf.Max(extents.x, extents.y));
        }

        _printRenderer.sprite = GetCatPawSprite(originalSize);
        _printRenderer.color = Color.white; // 색(형광 노랑 + 검정 테두리)은 그림에 구워져 있다

        GameObject glow = new GameObject("Glow", typeof(SpriteRenderer));
        glow.transform.SetParent(visual, false);
        glow.transform.localPosition = Vector3.zero;
        glow.transform.localRotation = Quaternion.identity;
        glow.transform.localScale = Vector3.one * GlowScale;

        _glowRenderer = glow.GetComponent<SpriteRenderer>();
        _glowRenderer.sprite = GetGlowSprite(originalSize);
        _glowRenderer.sharedMaterial = _printRenderer.sharedMaterial;
        _glowRenderer.sortingLayerID = _printRenderer.sortingLayerID;
        _glowRenderer.sortingOrder = _printRenderer.sortingOrder - 1;
        _glowRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _glowRenderer.receiveShadows = false;
        _glowRenderer.color = new Color(GlowColor.r, GlowColor.g, GlowColor.b, 0.5f);
    }

    private static Sprite _pawSprite;

    // 형광 노랑 + 검정 테두리의 고양이 발바닥. 큰 패드 하나와 둥근 발가락 넷을 거리장으로 합쳐 부드럽게 그린다.
    private static Sprite GetCatPawSprite(float worldSize)
    {
        if (_pawSprite != null)
        {
            return _pawSprite;
        }

        const int size = 128;
        const float outline = 5f;
        Color32 fill = new Color32(230, 255, 0, 255);
        Color32 edge = new Color32(10, 10, 10, 255);

        // x, y, 가로 반지름, 세로 반지름 (y 는 위쪽이 발가락)
        float[,] shapes =
        {
            { 64f, 38f, 30f, 22f },  // 큰 패드
            { 26f, 74f, 12f, 15f },  // 발가락(왼쪽 바깥)
            { 49f, 94f, 12f, 16f },  // 발가락(왼쪽 안)
            { 79f, 94f, 12f, 16f },  // 발가락(오른쪽 안)
            { 102f, 74f, 12f, 15f }  // 발가락(오른쪽 바깥)
        };

        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, true)
        {
            name = "CatPawFootprint",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.HideAndDontSave
        };

        Color32[] pixels = new Color32[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float best = float.MaxValue;

                for (int s = 0; s < shapes.GetLength(0); s++)
                {
                    float nx = (x + 0.5f - shapes[s, 0]) / shapes[s, 2];
                    float ny = (y + 0.5f - shapes[s, 1]) / shapes[s, 3];
                    // 타원의 대략적 부호 거리(픽셀). 안쪽이면 음수.
                    float distance = (Mathf.Sqrt(nx * nx + ny * ny) - 1f) * Mathf.Min(shapes[s, 2], shapes[s, 3]);
                    best = Mathf.Min(best, distance);
                }

                float inside = Mathf.Clamp01(0.5f - best);               // 채움(1px 안티앨리어싱)
                float outer = Mathf.Clamp01(outline + 0.5f - best);       // 테두리까지 포함한 바깥 경계

                if (outer <= 0f)
                {
                    pixels[y * size + x] = new Color32(0, 0, 0, 0);
                    continue;
                }

                Color32 color = Color32.Lerp(edge, fill, inside);
                color.a = (byte)(outer * 255f);
                pixels[y * size + x] = color;
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(true, true);

        _pawSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size / worldSize);
        _pawSprite.hideFlags = HideFlags.HideAndDontSave;
        return _pawSprite;
    }

    private static Sprite GetGlowSprite(float worldSize)
    {
        if (_glowSprite != null)
        {
            return _glowSprite;
        }

        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "FootprintGlow",
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };

        Color32[] pixels = new Color32[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f;
                float dy = (y + 0.5f) / size * 2f - 1f;
                float falloff = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                byte alpha = (byte)(falloff * falloff * 255f);
                pixels[y * size + x] = new Color32(255, 255, 255, alpha);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);

        // 발자국 그림과 같은 기준 크기로 잡는다(바깥 배율은 GlowScale).
        _glowSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size / worldSize);
        _glowSprite.hideFlags = HideFlags.HideAndDontSave;
        return _glowSprite;
    }

    private void Update()
    {
        if (_printRenderer == null || Runner == null || Object == null || !Object.IsValid)
        {
            return;
        }

        float remaining = DespawnTimer.RemainingTime(Runner) ?? LifetimeSeconds;
        float fade = Mathf.Clamp01(remaining / 2.5f); // 마지막 2.5초 동안 서서히 꺼진다
        float pulse = 0.85f + 0.15f * Mathf.Sin(Time.time * 3.5f + _pulseOffset);

        Color print = _printRenderer.color;
        print.a = fade * pulse;
        _printRenderer.color = print;

        if (_glowRenderer != null)
        {
            Color glow = _glowRenderer.color;
            glow.a = 0.5f * fade * pulse;
            _glowRenderer.color = glow;
        }
    }

    /// <summary>
    /// 이 털공이 지정한 플레이어가 생성한 흔적인지 반환합니다.
    /// </summary>
    public bool WasCreatedBy(PlayerRef player)
    {
        return Creator == player;
    }

    /// <summary>
    /// 같은 플레이어의 다른 털공보다 나중에 생성됐는지 반환합니다.
    /// </summary>
    public bool IsNewerThan(FurBallTrace other)
    {
        return TryCompareOrder(other, out int comparison) && comparison > 0;
    }

    /// <summary>
    /// 같은 플레이어가 만든 두 털공의 생성 순서를 비교합니다.
    /// 성공 시 comparison은 이전이면 음수, 같으면 0, 이후면 양수입니다.
    /// </summary>
    public bool TryCompareOrder(FurBallTrace other, out int comparison)
    {
        comparison = 0;

        if (other == null || Creator != other.Creator)
        {
            return false;
        }

        comparison = Sequence.CompareTo(other.Sequence);
        return true;
    }
}

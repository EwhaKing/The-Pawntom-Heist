using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 피격 피드백(내 화면 연출). <see cref="PlayerStatusUI"/> 가 체력이 줄어든 것을 보면 <see cref="Play"/> 를 부른다.
/// <list type="bullet">
/// <item>화면 가장자리 붉은 번쩍임(맞은 양이 클수록 진하게) + 체력이 낮으면 은은한 붉은 맥박</item>
/// <item>1인칭 카메라 흔들림(위치만 — 시점 회전은 PlayerController 가 매 틱 덮어쓴다)</item>
/// <item>짧은 타격음(코드로 합성 — 별도 사운드 애셋이 없다)</item>
/// </list>
/// 씬·프리팹을 고치지 않도록 화면 덮개와 소리는 실행 중에 만든다. Update 에서 할당하지 않는다.
/// </summary>
public class PlayerHitFeedback : MonoBehaviour
{
    private const int OverlaySortingOrder = 850; // 튜토리얼 HUD(900) 아래, 게임 HUD 위
    private const float FlashFadeSeconds = 0.55f;
    private const float ShakeSeconds = 0.28f;
    private const float ShakeWorldAmplitude = 0.035f; // m
    private const float LowHealthRatio = 0.3f;

    private static readonly Color HitColor = new Color(0.85f, 0.05f, 0.05f, 1f);

    private RawImage _overlay;
    private Texture2D _vignette;
    private AudioSource _audio;
    private AudioClip _hitClip;

    private float _flash;
    private float _lowHealthWeight;

    private Transform _shakeTarget;
    private Vector3 _shakeBase;
    private float _shakeRemaining;
    private float _shakeScale = 1f;

    private void Awake()
    {
        BuildOverlay();

        _audio = gameObject.AddComponent<AudioSource>();
        _audio.playOnAwake = false;
        _audio.spatialBlend = 0f;
        _hitClip = CreateHitClip();
    }

    private void OnDestroy()
    {
        StopShake();

        if (_vignette != null)
        {
            Destroy(_vignette);
        }

        if (_hitClip != null)
        {
            Destroy(_hitClip);
        }

        if (_overlay != null)
        {
            Destroy(_overlay.canvas.gameObject);
        }
    }

    /// <summary>흔들 카메라. 로컬 플레이어가 바뀔 때 넘긴다.</summary>
    public void SetCamera(Camera playerCamera)
    {
        StopShake();
        _shakeTarget = playerCamera != null ? playerCamera.transform : null;
    }

    /// <param name="severity">최대 체력 대비 이번에 깎인 비율(0~1).</param>
    public void Play(float severity)
    {
        severity = Mathf.Clamp01(severity);

        _flash = Mathf.Max(_flash, Mathf.Lerp(0.45f, 0.85f, severity * 3f));

        if (_hitClip != null)
        {
            _audio.pitch = Random.Range(0.92f, 1.06f);
            _audio.PlayOneShot(_hitClip, 0.9f);
        }

        if (_shakeTarget != null)
        {
            if (_shakeRemaining <= 0f)
            {
                _shakeBase = _shakeTarget.localPosition;
            }

            _shakeRemaining = ShakeSeconds;
            Transform parent = _shakeTarget.parent;
            float parentScale = parent != null ? Mathf.Max(0.0001f, parent.lossyScale.y) : 1f;
            _shakeScale = ShakeWorldAmplitude / parentScale * Mathf.Lerp(0.8f, 1.6f, severity * 3f);
        }
    }

    /// <summary>체력 비율. 낮으면 화면 가장자리가 붉게 맥박친다.</summary>
    public void SetHealthRatio(float ratio)
    {
        _lowHealthWeight = ratio > 0f && ratio < LowHealthRatio ? 1f - ratio / LowHealthRatio : 0f;
    }

    private void Update()
    {
        _flash = Mathf.Max(0f, _flash - Time.unscaledDeltaTime / FlashFadeSeconds);

        float pulse = _lowHealthWeight > 0f
            ? _lowHealthWeight * (0.18f + 0.12f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f)))
            : 0f;

        float alpha = Mathf.Max(_flash, pulse);
        Color color = HitColor;
        color.a = alpha;
        _overlay.color = color;

        if (_overlay.enabled != alpha > 0.001f)
        {
            _overlay.enabled = alpha > 0.001f;
        }
    }

    private void LateUpdate()
    {
        if (_shakeRemaining <= 0f || _shakeTarget == null)
        {
            return;
        }

        _shakeRemaining -= Time.unscaledDeltaTime;

        if (_shakeRemaining <= 0f)
        {
            _shakeRemaining = 0f;
            _shakeTarget.localPosition = _shakeBase;
            return;
        }

        float decay = _shakeRemaining / ShakeSeconds;
        Vector3 offset = new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), 0f) * (_shakeScale * decay);
        _shakeTarget.localPosition = _shakeBase + offset;
    }

    private void StopShake()
    {
        // 흔드는 중이었으면 원래 자리로 돌려놓는다.
        if (_shakeTarget != null && _shakeRemaining > 0f)
        {
            _shakeTarget.localPosition = _shakeBase;
        }

        _shakeRemaining = 0f;
    }

    private void BuildOverlay()
    {
        GameObject canvasObject = new GameObject("PlayerHitOverlay", typeof(RectTransform));
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = OverlaySortingOrder;

        // 덮개는 화면 전체를 덮어야 하므로 HUD(좌상단 작은 패널) 아래가 아니라 독립된 캔버스로 둔다.
        // 같은 씬에 생기고, 이 컴포넌트가 사라질 때 OnDestroy 에서 함께 지운다.

        GameObject imageObject = new GameObject("Vignette", typeof(RectTransform));
        imageObject.transform.SetParent(canvasObject.transform, false);
        RectTransform rect = (RectTransform)imageObject.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        _vignette = CreateVignette(128);
        _overlay = imageObject.AddComponent<RawImage>();
        _overlay.texture = _vignette;
        _overlay.raycastTarget = false; // 클릭을 가로채지 않는다
        _overlay.color = new Color(HitColor.r, HitColor.g, HitColor.b, 0f);
        _overlay.enabled = false;
    }

    /// <summary>가운데는 투명하고 가장자리로 갈수록 진해지는 원형 비네트(흰색 — 색은 RawImage.color 로 준다).</summary>
    private static Texture2D CreateVignette(int size)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.hideFlags = HideFlags.DontSave;
        Color32[] pixels = new Color32[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f;
                float dy = (y + 0.5f) / size * 2f - 1f;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1.25f, distance));
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return texture;
    }

    /// <summary>둔탁한 저음 쿵 + 짧게 할퀴는 잡음. 0.22초.</summary>
    private static AudioClip CreateHitClip()
    {
        const int sampleRate = 44100;
        int length = (int)(sampleRate * 0.22f);
        float[] data = new float[length];
        System.Random random = new System.Random(7);
        float phase = 0f;
        float noise = 0f;

        for (int i = 0; i < length; i++)
        {
            float t = (float)i / sampleRate;

            // 120Hz → 55Hz 로 떨어지는 저음 몸통
            float frequency = Mathf.Lerp(120f, 55f, t / 0.22f);
            phase += 2f * Mathf.PI * frequency / sampleRate;
            float thump = Mathf.Sin(phase) * Mathf.Exp(-t * 18f);

            // 앞부분만 짧게 섞이는 거친 잡음(저역 통과로 부드럽게)
            float white = (float)(random.NextDouble() * 2.0 - 1.0);
            noise += (white - noise) * 0.35f;
            float scratch = noise * Mathf.Exp(-t * 45f) * 0.6f;

            float attack = Mathf.Clamp01(t / 0.004f);
            data[i] = Mathf.Clamp((thump * 0.9f + scratch) * attack, -1f, 1f);
        }

        AudioClip clip = AudioClip.Create("PlayerHit", length, 1, sampleRate, false);
        clip.SetData(data, 0);
        clip.hideFlags = HideFlags.DontSave;
        return clip;
    }
}

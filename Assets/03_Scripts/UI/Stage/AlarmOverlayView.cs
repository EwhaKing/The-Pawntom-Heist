using Pawntom.Settings;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 캣닢 경보 화면 연출(GAME_DESIGN §3.7 "맵 전체 붉게 점멸"). 화면 전체 붉은 틴트 + 가장자리 붉은 비네트가 사이렌 주기로 맥동한다.
/// 경보가 켜져 있는 동안(<see cref="CatnipAlarm.IsActive"/>)만 보이고, 늦게 들어온 참가자도 상태로 켜진다.
/// <see cref="CatnipAlarm.Triggered"/> 를 받으면 한 번 더 크게 번쩍인다. 후처리 볼륨을 쓰지 않는 UI 덮개라
/// 플레이어 카메라 설정을 건드리지 않는다. Update 에서 할당하지 않는다.
/// </summary>
[DisallowMultipleComponent]
public sealed class AlarmOverlayView : MonoBehaviour
{
    private CanvasGroup _group;
    private Image _tint;
    private Image _vignette;

    private float _activeSince;
    private float _fade;
    private float _burstSince = -100f;
    private bool _wasActive;

    public void Build(RuntimeUIFactory factory, Transform parent, StageHudSprites sprites)
    {
        RectTransform root = factory.CreateRect("AlarmOverlay", parent);
        RuntimeUIFactory.Stretch(root);
        _group = root.gameObject.AddComponent<CanvasGroup>();
        _group.alpha = 0f;
        _group.blocksRaycasts = false;
        _group.interactable = false;

        Color tint = StageHudTheme.AlarmTintColor;
        _tint = factory.CreateImage("Tint", root, tint);
        RuntimeUIFactory.Stretch(_tint.rectTransform);

        _vignette = factory.CreateImage("Vignette", root, tint);
        _vignette.sprite = sprites.Vignette;
        RuntimeUIFactory.Stretch(_vignette.rectTransform);

        root.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        CatnipAlarm.Triggered += HandleTriggered;
    }

    private void OnDisable()
    {
        CatnipAlarm.Triggered -= HandleTriggered;
    }

    private void HandleTriggered(CatnipAlarm alarm)
    {
        _burstSince = Time.unscaledTime;
    }

    private void Update()
    {
        bool active = CatnipAlarm.IsActive;
        float now = Time.unscaledTime;

        if (active && !_wasActive)
        {
            _activeSince = now;
        }

        _wasActive = active;

        float seconds = active ? StageHudTheme.AlarmFadeInSeconds : StageHudTheme.AlarmFadeOutSeconds;
        _fade = Mathf.MoveTowards(_fade, active ? 1f : 0f, Time.unscaledDeltaTime / seconds);

        float burst = StageHudMath.BurstDecay(now - _burstSince, StageHudTheme.AlarmBurstSeconds);
        bool visible = _fade > 0.001f || burst > 0.001f;

        if (_group.gameObject.activeSelf != visible)
        {
            _group.gameObject.SetActive(visible);
        }

        if (!visible)
        {
            return;
        }

        float pulse = StageHudMath.AlarmPulse(now - _activeSince, StageHudTheme.AlarmCycleSeconds, StageHudTheme.AlarmPulseMin);
        float weight = Mathf.Clamp01(pulse * StageHudMath.SmoothFade(_fade) + burst * StageHudTheme.AlarmBurstAlpha);

        _group.alpha = 1f;
        SetAlpha(_tint, StageHudTheme.AlarmTintAlpha * weight);
        SetAlpha(_vignette, StageHudTheme.AlarmVignetteAlpha * weight);
    }

    private static void SetAlpha(Image image, float alpha)
    {
        Color color = image.color;
        color.a = alpha;
        image.color = color;
    }
}

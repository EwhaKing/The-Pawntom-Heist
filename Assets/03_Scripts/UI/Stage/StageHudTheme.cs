using UnityEngine;

/// <summary>
/// 게임 씬 HUD 피드백(캐스팅 바·경보·안내)의 색·크기·위치. UI_SPEC 에 HUD 픽셀 크기와 HEX 가 없어 임시 값이다 —
/// 확정되면 이 파일만 고친다. 위치는 기준 해상도(1800x950) 화면 중앙 기준.
/// </summary>
public static class StageHudTheme
{
    // 캔버스 순서: 상호작용 안내(40) < HUD(60) < 피격(850) 아래 경보(840) < 옵션/ESC(5000)
    public const int HudSortingOrder = 60;
    public const int AlarmSortingOrder = 840;

    // 문구 색
    public static readonly Color TextColor = Color.white;
    public static readonly Color TextOutline = new Color(0.09f, 0.06f, 0.04f, 1f);

    // 의미 색(UI_SPEC §3.2): 위험=적색(state/danger), 성공=초록(state/open), 비활성=회색(state/disabled)
    public static readonly Color DangerColor = new Color(0.9f, 0.2f, 0.17f, 1f);
    public static readonly Color SuccessColor = new Color(0.18f, 0.83f, 0.48f, 1f);
    public static readonly Color NoticeColor = new Color(1f, 0.82f, 0.35f, 1f);

    // 캐스팅 원형 게이지
    public static readonly Color GaugeTrackColor = new Color(0f, 0f, 0f, 0.55f);
    public static readonly Color GaugeFillColor = new Color(1f, 1f, 1f, 0.95f);
    public static readonly Vector2 GaugePosition = new Vector2(0f, -120f);
    public const float GaugeSize = 140f;
    public const float GaugeLabelOffsetY = -225f;
    public const float CastFadeSpeed = 8f;
    public const float CastResultHoldSeconds = 1.1f;

    // 캣닢 경보(붉은 점멸)
    public static readonly Color AlarmTintColor = new Color(0.95f, 0.08f, 0.06f, 1f);
    public const float AlarmTintAlpha = 0.16f;
    public const float AlarmVignetteAlpha = 0.85f;
    public const float AlarmCycleSeconds = 2.4f;   // TutorialCatnipAlarm 사이렌 한 주기와 같다
    public const float AlarmPulseMin = 0.45f;
    public const float AlarmFadeInSeconds = 0.7f;
    public const float AlarmFadeOutSeconds = 0.8f;
    public const float AlarmBurstSeconds = 1.2f;
    public const float AlarmBurstAlpha = 0.35f;

    // 안내 문구
    public static readonly Vector2 HintPosition = new Vector2(0f, 150f);
    public const float HintFontSize = 34f;
    public const float HintFadeSeconds = 0.25f;
    public const float ToastHoldSeconds = 2.6f;
}

using UnityEngine;

/// <summary>
/// 옵션창·ESC 메뉴 색·크기 상수. UI_SPEC 에 확정 토큰이 없어(FigJam 원본) 값은 이 한 곳에 모아 교체하기 쉽게 둔다.
/// 톤: 타이틀 메뉴의 흑백 막대(선택 = 흰 바탕 + 검은 글자)를 따르고, 강조색은 튜토리얼 키 강조와 같은 호박색 하나만 쓴다.
/// <b>색·크기의 최종 판단은 사람이 화면에서 확인한다(L3).</b>
/// </summary>
public static class OptionsTheme
{
    // ── 색 ──
    public static readonly Color Backdrop = new Color(0f, 0f, 0f, 0.55f);          // 메뉴 뒤 화면 어둡게(게임은 계속 보인다)
    public static readonly Color Panel = new Color(0.035f, 0.04f, 0.045f, 0.95f);
    public static readonly Color PanelEdge = new Color(1f, 1f, 1f, 0.10f);
    public static readonly Color Divider = new Color(1f, 1f, 1f, 0.08f);
    public static readonly Color Text = new Color(0.93f, 0.94f, 0.93f, 1f);
    public static readonly Color SubText = new Color(0.60f, 0.63f, 0.64f, 1f);
    public static readonly Color Accent = new Color(1f, 0.82f, 0.35f, 1f);         // #FFD15A (튜토리얼 키 강조와 같은 계열)
    public static readonly Color SelectedBackground = new Color(0.95f, 0.95f, 0.95f, 1f);
    public static readonly Color SelectedText = new Color(0.05f, 0.05f, 0.05f, 1f);

    // 버튼 바탕(투명한 흰색 단계). ColorTint 로 쓴다.
    public static readonly Color ButtonNormal = new Color(1f, 1f, 1f, 0.06f);
    public static readonly Color ButtonHover = new Color(1f, 1f, 1f, 0.16f);
    public static readonly Color ButtonPressed = new Color(1f, 1f, 1f, 0.26f);
    public static readonly Color ButtonDisabled = new Color(1f, 1f, 1f, 0.03f);

    public static readonly Color SliderTrack = new Color(1f, 1f, 1f, 0.14f);
    public static readonly Color SliderHandle = new Color(0.95f, 0.95f, 0.95f, 1f);

    // ── 크기(기준 해상도 1800×950 px) ──
    public static readonly Vector2 OptionsPanelSize = new Vector2(1040f, 800f);
    public static readonly Vector2 PausePanelSize = new Vector2(460f, 400f);
    public const float HeaderHeight = 84f;
    public const float FooterHeight = 76f;
    public const float RowHeight = 42f;
    public const float RowSpacing = 6f;
    public const float LabelWidth = 360f;
    public const float ControlWidth = 520f;
    public const float PanelEdgeThickness = 2f;

    public const float TitleFontSize = 34f;
    public const float BodyFontSize = 24f;
    public const float SmallFontSize = 19f;

    // 다른 UI(튜토리얼 오버레이 1000, 해킹 팝업 등)보다 위에 그린다.
    public const int CanvasSortingOrder = 5000;
}

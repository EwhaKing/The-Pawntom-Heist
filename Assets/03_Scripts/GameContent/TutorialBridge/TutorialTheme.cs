using UnityEngine;

namespace Pawntom.TutorialBridge
{
    /// <summary>
    /// 튜토리얼 UI 색·배치 상수.
    /// <para>
    /// UI_SPEC 상 HUD 색·크기 토큰이 아직 확정되지 않았다(FigJam 원본). 의미 규칙(무전 수신=초록, 차단=빨강+취소선)만 따르고,
    /// 값은 이 한 곳에 모아 교체하기 쉽게 둔다. <b>배치 크기는 사람이 확인해야 한다.</b>
    /// 기준 해상도는 UI_SPEC 공통 기준 1800×950.
    /// </para>
    /// </summary>
    public static class TutorialTheme
    {
        public static readonly Vector2 ReferenceResolution = new Vector2(1800f, 950f);

        // ── 색 ──
        public static readonly Color CommandColor = new Color(0.49f, 1f, 0.70f, 1f);   // 무전 수신(음성 활성 = 초록)
        public static readonly Color CatColor = new Color(1f, 0.79f, 0.42f, 1f);        // 깜냥이
        public static readonly Color BodyTextColor = new Color(0.91f, 0.95f, 0.93f, 1f);
        public static readonly Color PanelColor = new Color(0.02f, 0.04f, 0.05f, 0.84f);
        public static readonly Color PanelEdgeColor = new Color(0.49f, 1f, 0.70f, 0.18f);
        public static readonly Color HintBackgroundColor = new Color(0f, 0f, 0f, 0.72f);
        public static readonly Color HintAccentColor = new Color(1f, 0.82f, 0.35f, 1f);
        public static readonly Color HintDoneColor = new Color(0.49f, 1f, 0.70f, 1f);
        public static readonly Color RadioIdleColor = new Color(0.55f, 0.58f, 0.6f, 1f);  // 음성 평시 = 회색
        public static readonly Color RadioBlockedColor = new Color(1f, 0.28f, 0.25f, 1f); // 음성 차단 = 빨강
        public static readonly Color ScanlineColor = new Color(0.49f, 1f, 0.70f, 0.06f);

        /// <summary>TMP 리치 텍스트 강조(키 칩).</summary>
        public const string HighlightOpen = "<color=#FFD25A><b>";
        public const string HighlightClose = "</b></color>";

        // ── 배치(기준 해상도 px) ──
        // 가장 긴 대사(4-6, 88자)가 27pt 로 세 줄에 들어가는 크기.
        public static readonly Vector2 DialogueSize = new Vector2(1100f, 185f);
        public const float DialogueBottom = 160f;
        public const float DialogueFontSize = 27f;

        public static readonly Vector2 HintRowSize = new Vector2(760f, 54f);
        public const float HintBottom = 375f;
        public const float HintRowSpacing = 62f;

        public static readonly Vector2 CastBarSize = new Vector2(440f, 46f);
        public const float CastBarOffsetY = -150f;

        public static readonly Vector2 RadioSize = new Vector2(250f, 72f);
        public static readonly Vector2 RadioMargin = new Vector2(36f, 36f);

        public static readonly Vector2 SettlementSize = new Vector2(640f, 430f);

        // ── 정렬 순서 ──
        public const int HudSortingOrder = 900;
        public const int OverlaySortingOrder = 1000;

        // ── 화자 ──
        public const string CommandName = "지휘부";
        public const string CatName = "깜냥이";
    }
}

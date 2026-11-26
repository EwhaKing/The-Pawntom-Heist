using TMPro;
using UnityEngine;

/// <summary>게임 씬 HUD 문구 공통 스타일(흰 글씨 + 어두운 테두리, 줄바꿈 없음).</summary>
public static class StageHudText
{
    public static TextMeshProUGUI Create(RuntimeUIFactory factory, string name, Transform parent, float fontSize, Color color, TextAlignmentOptions alignment)
    {
        TextMeshProUGUI text = factory.CreateText(name, parent, fontSize, color, alignment);
        text.fontStyle = FontStyles.Bold;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.outlineWidth = 0.22f;
        text.outlineColor = StageHudTheme.TextOutline;
        return text;
    }
}

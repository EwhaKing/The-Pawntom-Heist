using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 옵션창·ESC 메뉴의 조작 요소(버튼·슬라이더·좌우 선택·키 칸)를 코드로 만든다. 프리팹 없이 같은 모양을 반복해서 쓰기 위함.
/// </summary>
public static class OptionsWidgets
{
    /// <summary>글자가 들어간 평평한 버튼. 마우스를 올리면 밝아진다.</summary>
    public static Button CreateButton(RuntimeUIFactory factory, string name, Transform parent, string label, float fontSize, UnityAction onClick, out TextMeshProUGUI text)
    {
        Image background = factory.CreateImage(name, parent, Color.white);
        background.raycastTarget = true;

        Button button = background.gameObject.AddComponent<Button>();
        button.targetGraphic = background;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        SetButtonColors(button, false);

        if (onClick != null)
        {
            button.onClick.AddListener(onClick);
        }

        text = factory.CreateText("Label", background.transform, fontSize, OptionsTheme.Text, TextAlignmentOptions.Center);
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.text = label;
        RuntimeUIFactory.Inset(text.rectTransform, 12f, 12f, 0f, 0f);
        return button;
    }

    /// <summary>선택된 탭은 타이틀 메뉴처럼 흰 바탕 + 검은 글자.</summary>
    public static void SetButtonColors(Button button, bool selected)
    {
        ColorBlock colors = button.colors;
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;

        if (selected)
        {
            colors.normalColor = OptionsTheme.SelectedBackground;
            colors.highlightedColor = OptionsTheme.SelectedBackground;
            colors.pressedColor = OptionsTheme.SelectedBackground;
            colors.selectedColor = OptionsTheme.SelectedBackground;
        }
        else
        {
            colors.normalColor = OptionsTheme.ButtonNormal;
            colors.highlightedColor = OptionsTheme.ButtonHover;
            colors.pressedColor = OptionsTheme.ButtonPressed;
            // 누른 뒤 "선택됨" 상태로 밝게 남지 않게 평소 색과 같게 둔다.
            colors.selectedColor = OptionsTheme.ButtonNormal;
        }

        colors.disabledColor = OptionsTheme.ButtonDisabled;
        button.colors = colors;
    }

    /// <summary>왼쪽 이름 + 오른쪽 조작 칸 한 줄. 조작 칸 RectTransform 을 돌려준다.</summary>
    public static RectTransform CreateRow(RuntimeUIFactory factory, Transform parent, string label)
    {
        RectTransform row = factory.CreateRect("Row_" + label, parent);
        LayoutElement layout = row.gameObject.AddComponent<LayoutElement>();
        layout.preferredHeight = OptionsTheme.RowHeight;
        layout.minHeight = OptionsTheme.RowHeight;

        TextMeshProUGUI name = factory.CreateText("Name", row, OptionsTheme.BodyFontSize, OptionsTheme.Text, TextAlignmentOptions.MidlineLeft);
        name.textWrappingMode = TextWrappingModes.NoWrap;
        name.text = label;
        RectTransform nameRect = name.rectTransform;
        nameRect.anchorMin = new Vector2(0f, 0f);
        nameRect.anchorMax = new Vector2(0f, 1f);
        nameRect.pivot = new Vector2(0f, 0.5f);
        nameRect.anchoredPosition = Vector2.zero;
        nameRect.sizeDelta = new Vector2(OptionsTheme.LabelWidth, 0f);

        RectTransform control = factory.CreateRect("Control", row);
        control.anchorMin = new Vector2(1f, 0f);
        control.anchorMax = new Vector2(1f, 1f);
        control.pivot = new Vector2(1f, 0.5f);
        control.anchoredPosition = Vector2.zero;
        control.sizeDelta = new Vector2(OptionsTheme.ControlWidth, 0f);
        return control;
    }

    /// <summary>작은 회색 제목 줄(구역 구분).</summary>
    public static TextMeshProUGUI CreateSectionHeader(RuntimeUIFactory factory, Transform parent, string label)
    {
        TextMeshProUGUI text = factory.CreateText("Section_" + label, parent, OptionsTheme.SmallFontSize, OptionsTheme.SubText, TextAlignmentOptions.BottomLeft);
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.text = label;
        LayoutElement layout = text.gameObject.AddComponent<LayoutElement>();
        layout.preferredHeight = OptionsTheme.RowHeight - 6f;
        return text;
    }

    /// <summary>슬라이더 + 오른쪽 값 글자. valueText 는 호출한 쪽이 갱신한다.</summary>
    public static Slider CreateSlider(RuntimeUIFactory factory, RectTransform control, float min, float max, out TextMeshProUGUI valueText)
    {
        const float ValueWidth = 92f;

        RectTransform root = factory.CreateRect("Slider", control);
        root.anchorMin = new Vector2(0f, 0f);
        root.anchorMax = new Vector2(1f, 1f);
        root.offsetMin = new Vector2(0f, 0f);
        root.offsetMax = new Vector2(-ValueWidth - 12f, 0f);

        // 트랙(누르면 그 위치로 이동)
        Image track = factory.CreateImage("Track", root, OptionsTheme.SliderTrack);
        track.raycastTarget = true;
        RectTransform trackRect = track.rectTransform;
        trackRect.anchorMin = new Vector2(0f, 0.5f);
        trackRect.anchorMax = new Vector2(1f, 0.5f);
        trackRect.sizeDelta = new Vector2(0f, 6f);
        trackRect.anchoredPosition = Vector2.zero;

        RectTransform fillArea = factory.CreateRect("FillArea", root);
        fillArea.anchorMin = new Vector2(0f, 0.5f);
        fillArea.anchorMax = new Vector2(1f, 0.5f);
        fillArea.sizeDelta = new Vector2(0f, 6f);
        fillArea.anchoredPosition = Vector2.zero;

        Image fill = factory.CreateImage("Fill", fillArea, OptionsTheme.Accent);
        fill.rectTransform.sizeDelta = Vector2.zero;

        RectTransform handleArea = factory.CreateRect("HandleArea", root);
        RuntimeUIFactory.Inset(handleArea, 8f, 8f, 0f, 0f);

        Image handle = factory.CreateImage("Handle", handleArea, Color.white);
        handle.raycastTarget = true;
        // 슬라이더가 손잡이의 앵커를 세로 전체(0~1)로 잡으므로, 높이는 줄 높이(42)에서 16 을 뺀 26px 이 된다.
        handle.rectTransform.sizeDelta = new Vector2(14f, -16f);

        Slider slider = root.gameObject.AddComponent<Slider>();
        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = min;
        slider.maxValue = max;
        slider.wholeNumbers = false;
        slider.navigation = new Navigation { mode = Navigation.Mode.None };

        ColorBlock colors = slider.colors;
        colors.normalColor = OptionsTheme.SliderHandle;
        colors.highlightedColor = Color.white;
        colors.pressedColor = OptionsTheme.Accent;
        colors.selectedColor = OptionsTheme.SliderHandle;
        slider.colors = colors;

        valueText = factory.CreateText("Value", control, OptionsTheme.BodyFontSize, OptionsTheme.Text, TextAlignmentOptions.MidlineRight);
        valueText.textWrappingMode = TextWrappingModes.NoWrap;
        RectTransform valueRect = valueText.rectTransform;
        valueRect.anchorMin = new Vector2(1f, 0f);
        valueRect.anchorMax = new Vector2(1f, 1f);
        valueRect.pivot = new Vector2(1f, 0.5f);
        valueRect.anchoredPosition = Vector2.zero;
        valueRect.sizeDelta = new Vector2(ValueWidth, 0f);
        return slider;
    }

    /// <summary>"&lt; 값 &gt;" 좌우 선택. step(-1/+1) 이 눌릴 때 onStep 을 부른다. 가운데 글자는 호출한 쪽이 갱신한다.</summary>
    public static TextMeshProUGUI CreateStepper(RuntimeUIFactory factory, RectTransform control, Action<int> onStep)
    {
        const float ArrowWidth = 48f;

        TextMeshProUGUI ignored;
        Button left = CreateButton(factory, "Prev", control, "<", OptionsTheme.BodyFontSize, () => onStep(-1), out ignored);
        RuntimeUIFactory.Place((RectTransform)left.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(ArrowWidth, OptionsTheme.RowHeight - 4f));

        Button right = CreateButton(factory, "Next", control, ">", OptionsTheme.BodyFontSize, () => onStep(1), out ignored);
        RuntimeUIFactory.Place((RectTransform)right.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(ArrowWidth, OptionsTheme.RowHeight - 4f));

        TextMeshProUGUI value = factory.CreateText("Value", control, OptionsTheme.BodyFontSize, OptionsTheme.Text, TextAlignmentOptions.Center);
        value.textWrappingMode = TextWrappingModes.NoWrap;
        value.overflowMode = TextOverflowModes.Ellipsis;
        RuntimeUIFactory.Inset(value.rectTransform, ArrowWidth + 8f, ArrowWidth + 8f, 0f, 0f);
        return value;
    }

    /// <summary>패널 바탕 + 얇은 테두리.</summary>
    public static Image CreatePanel(RuntimeUIFactory factory, string name, Transform parent, Vector2 size)
    {
        Image edge = factory.CreateImage(name, parent, OptionsTheme.PanelEdge);
        edge.raycastTarget = true;
        RuntimeUIFactory.Place(edge.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);

        Image panel = factory.CreateImage("Body", edge.transform, OptionsTheme.Panel);
        float t = OptionsTheme.PanelEdgeThickness;
        RuntimeUIFactory.Inset(panel.rectTransform, t, t, t, t);
        return edge;
    }
}

using UnityEngine;

/// <summary>
/// MapHiddenAreaUI
///
/// 담당:
/// - CCTV 지도에서 아직 발견되지 않은 구역을 네모칸으로 가림
/// - 연결된 DiscoverableArea가 발견되면 가림막을 숨김
///
/// 사용 위치:
/// - Layer_Position / Layer_Sprinkler / Layer_Light 안의 HiddenArea_01 같은 UI Image에 붙임
/// </summary>
public class MapHiddenAreaUI : MonoBehaviour
{
    [Header("Target Area")]
    [SerializeField] private DiscoverableArea targetArea;

    [Header("Style")]
    [Tooltip("가림막을 단색 사각형 대신 모서리 둥근 어두운 패널 + 테두리 + ? 표시로 그린다")]
    [SerializeField] private bool polishedStyle = true;

    [Tooltip("모서리 둥글기. 클수록 더 둥글다(화면 픽셀 대략 31 / 값)")]
    [SerializeField, Range(1f, 8f)] private float cornerSoftness = 3f;

    private void Awake()
    {
        if (polishedStyle)
        {
            ApplyStyle();
        }
    }

    private void ApplyStyle()
    {
        UnityEngine.UI.Image image = GetComponent<UnityEngine.UI.Image>();

        if (image == null)
        {
            return;
        }

        image.sprite = RoundedShapes.RoundedSprite;
        image.type = UnityEngine.UI.Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = cornerSoftness;
        image.color = new Color(TerminalPalette.Dark.r, TerminalPalette.Dark.g, TerminalPalette.Dark.b, 1f);

        UnityEngine.UI.Outline outline = GetComponent<UnityEngine.UI.Outline>();

        if (outline == null)
        {
            outline = gameObject.AddComponent<UnityEngine.UI.Outline>();
        }

        outline.effectColor = TerminalPalette.Mid;
        outline.effectDistance = new Vector2(2f, -2f);

        if (transform.Find("UnknownMark") != null)
        {
            return;
        }

        GameObject mark = new GameObject("UnknownMark", typeof(RectTransform), typeof(CanvasRenderer), typeof(TMPro.TextMeshProUGUI));
        mark.layer = gameObject.layer;

        RectTransform rect = (RectTransform)mark.transform;
        rect.SetParent(transform, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        TMPro.TextMeshProUGUI text = mark.GetComponent<TMPro.TextMeshProUGUI>();
        text.text = "?";
        text.alignment = TMPro.TextAlignmentOptions.Center;
        text.fontSize = Mathf.Clamp(Mathf.Min(((RectTransform)transform).rect.width, ((RectTransform)transform).rect.height) * 0.45f, 24f, 96f);
        text.fontStyle = TMPro.FontStyles.Bold;
        text.color = new Color(TerminalPalette.Mid.r, TerminalPalette.Mid.g, TerminalPalette.Mid.b, 0.9f);
        text.raycastTarget = false;
    }

    private void Update()
    {
        if (targetArea == null)
        {
            return;
        }

        gameObject.SetActive(!targetArea.IsDiscovered);
    }
}
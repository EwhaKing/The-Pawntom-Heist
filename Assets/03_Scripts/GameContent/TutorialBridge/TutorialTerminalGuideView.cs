using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Pawntom.TutorialBridge
{
    /// <summary>
    /// 본부 터미널 창 안에서 "여기를 누르세요"를 알려 주는 초록 안내 틀. 3D 안내 상자(<see cref="TutorialGuideMarker"/>)의 화면 UI 판이다.
    /// 대상 UI(탭 버튼·격벽 버튼)를 초록 테두리로 감싸고 위에 짧은 말풍선("격벽 탭 클릭", "더블클릭해서 열기")을 단다.
    /// 클릭을 가로채지 않는다(GraphicRaycaster 없음, raycastTarget 끔).
    /// </summary>
    public sealed class TutorialTerminalGuideView : MonoBehaviour
    {
        private static readonly Color EdgeColor = new Color(0.62f, 1f, 0.56f, 0.95f);
        private static readonly Color FillColor = new Color(0.56f, 1f, 0.5f, 0.16f);
        private static readonly Color LabelBackColor = new Color(0.05f, 0.09f, 0.05f, 0.92f);

        private const float Border = 4f;
        private const float Pad = 6f;

        private RectTransform _canvasRect;
        private RectTransform _frame;
        private Image _fill;
        private readonly Image[] _bars = new Image[4];
        private RectTransform _labelRect;
        private TextMeshProUGUI _text;
        private CanvasGroup _group;

        // 누를 곳만 뚫린 옅은 회색 막. 네 조각이 구멍 둘레를 덮고, 클릭을 막는다(구멍 안은 그대로 눌린다).
        private static readonly Color DimColor = new Color(0.45f, 0.47f, 0.45f, 0.5f);
        private RectTransform _dimRoot;
        private CanvasGroup _dimGroup;
        private readonly RectTransform[] _dim = new RectTransform[4];

        private RectTransform _target;
        private readonly Vector3[] _corners = new Vector3[4];

        public void Build(TutorialUIFactory factory, Canvas overlay)
        {
            _canvasRect = (RectTransform)overlay.transform;

            // 별도 캔버스에 둔다: 클릭을 받으려면 GraphicRaycaster 가 필요한데, 안내 틀이 있는
            // 오버레이 캔버스에 붙이면 다른 튜토리얼 화면 요소까지 클릭을 가로챌 수 있다.
            // 터미널(기본 HUD 캔버스)보다 위, 대사창(HudSortingOrder)보다 아래 — 대사는 가려지지 않는다.
            Canvas dimCanvas = factory.CreateCanvas("TerminalGuideDimCanvas", TutorialTheme.HudSortingOrder - 100, overlay.transform.parent);
            dimCanvas.gameObject.AddComponent<GraphicRaycaster>();
            _dimRoot = factory.CreateRect("Dim", dimCanvas.transform);
            TutorialUIFactory.Stretch(_dimRoot);
            _dimGroup = _dimRoot.gameObject.AddComponent<CanvasGroup>();
            _dimGroup.alpha = 0f;
            _dimGroup.blocksRaycasts = false;

            for (int i = 0; i < 4; i++)
            {
                Image piece = factory.CreateImage("DimPiece" + i, _dimRoot, DimColor);
                piece.raycastTarget = true;
                _dim[i] = piece.rectTransform;
                _dim[i].anchorMin = _dim[i].anchorMax = new Vector2(0.5f, 0.5f);
                _dim[i].pivot = new Vector2(0.5f, 0.5f);
            }

            _frame = factory.CreateRect("TerminalGuide", overlay.transform);
            _frame.anchorMin = _frame.anchorMax = new Vector2(0.5f, 0.5f);
            _frame.pivot = new Vector2(0.5f, 0.5f);
            _group = _frame.gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;

            _fill = factory.CreateImage("Fill", _frame, FillColor);
            _fill.sprite = RoundedShapes.RoundedSprite;
            _fill.type = Image.Type.Sliced;
            _fill.pixelsPerUnitMultiplier = 4f;
            _fill.raycastTarget = false;
            TutorialUIFactory.Stretch(_fill.rectTransform);

            for (int i = 0; i < 4; i++)
            {
                _bars[i] = factory.CreateImage("Edge" + i, _frame, EdgeColor);
                _bars[i].raycastTarget = false;
            }

            // 위·아래·왼쪽·오른쪽 띠
            SetBar(_bars[0], new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -Border), Vector2.zero);
            SetBar(_bars[1], new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, Border));
            SetBar(_bars[2], new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, new Vector2(Border, 0f));
            SetBar(_bars[3], new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-Border, 0f), Vector2.zero);

            // 말풍선: 어두운 바탕 + 초록 글자
            Image back = factory.CreateImage("LabelBack", _frame, LabelBackColor);
            back.sprite = RoundedShapes.RoundedSprite;
            back.type = Image.Type.Sliced;
            back.pixelsPerUnitMultiplier = 3f;
            back.raycastTarget = false;
            _labelRect = back.rectTransform;
            _labelRect.anchorMin = _labelRect.anchorMax = new Vector2(0.5f, 1f);
            _labelRect.pivot = new Vector2(0.5f, 0f);
            _labelRect.sizeDelta = new Vector2(240f, 40f);

            _text = factory.CreateText("Label", _labelRect, 24f, EdgeColor, TextAlignmentOptions.Center);
            _text.fontStyle = FontStyles.Bold;
            _text.raycastTarget = false;
            TutorialUIFactory.Stretch(_text.rectTransform);
        }

        // 구멍(누를 곳) 둘레 네 조각으로 화면 전체를 덮는다. 구멍 안은 비어 있어 아래 UI 가 그대로 눌린다.
        private void UpdateDim(Vector2 holeMin, Vector2 holeMax)
        {
            Rect screen = _canvasRect.rect;
            float left = screen.xMin - 50f;
            float right = screen.xMax + 50f;
            float bottom = screen.yMin - 50f;
            float top = screen.yMax + 50f;

            PlaceDim(_dim[0], left, holeMax.y, right, top);                 // 위
            PlaceDim(_dim[1], left, bottom, right, holeMin.y);              // 아래
            PlaceDim(_dim[2], left, holeMin.y, holeMin.x, holeMax.y);       // 왼쪽
            PlaceDim(_dim[3], holeMax.x, holeMin.y, right, holeMax.y);      // 오른쪽

            _dimGroup.alpha = Mathf.MoveTowards(_dimGroup.alpha, 1f, Time.unscaledDeltaTime * 8f);
            _dimGroup.blocksRaycasts = true;
        }

        private static void PlaceDim(RectTransform piece, float xMin, float yMin, float xMax, float yMax)
        {
            piece.anchoredPosition = new Vector2((xMin + xMax) * 0.5f, (yMin + yMax) * 0.5f);
            piece.sizeDelta = new Vector2(Mathf.Max(0f, xMax - xMin), Mathf.Max(0f, yMax - yMin));
        }

        private static void SetBar(Image bar, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            RectTransform rect = bar.rectTransform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        /// <summary>대상 UI 를 초록 틀로 감싸고 말풍선 문구를 띄운다. 매 프레임 불러도 된다.</summary>
        public void Show(RectTransform target, string label)
        {
            _target = target;

            if (_text.text != label)
            {
                _text.text = label;
            }
        }

        public void Hide()
        {
            _target = null;
        }

        private void LateUpdate()
        {
            if (_frame == null)
            {
                return;
            }

            if (_target == null || !_target.gameObject.activeInHierarchy)
            {
                _group.alpha = Mathf.MoveTowards(_group.alpha, 0f, Time.unscaledDeltaTime * 8f);
                _dimGroup.alpha = Mathf.MoveTowards(_dimGroup.alpha, 0f, Time.unscaledDeltaTime * 8f);
                _dimGroup.blocksRaycasts = false; // 안내가 없으면 클릭을 막지 않는다
                return;
            }

            Canvas canvas = _target.GetComponentInParent<Canvas>();
            Camera camera = canvas != null && canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.rootCanvas.worldCamera
                : null;

            _target.GetWorldCorners(_corners);

            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);

            for (int i = 0; i < 4; i++)
            {
                Vector2 screen = RectTransformUtility.WorldToScreenPoint(camera, _corners[i]);

                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screen, null, out Vector2 local))
                {
                    _group.alpha = 0f;
                    _dimGroup.alpha = 0f;
                    _dimGroup.blocksRaycasts = false;
                    return;
                }

                min = Vector2.Min(min, local);
                max = Vector2.Max(max, local);
            }

            float pulse = 1f + 0.06f * Mathf.Sin(Time.unscaledTime * 6f);
            Vector2 size = (max - min) + Vector2.one * (Pad * 2f * pulse);

            _frame.anchoredPosition = (min + max) * 0.5f;
            _frame.sizeDelta = size;

            UpdateDim(min - Vector2.one * Pad, max + Vector2.one * Pad);

            // 말풍선은 틀 위에 둔다. 화면 위쪽에 닿으면 아래로 내린다.
            bool belowFlip = _frame.anchoredPosition.y + size.y * 0.5f + 60f > _canvasRect.rect.height * 0.5f;
            _labelRect.anchorMin = _labelRect.anchorMax = new Vector2(0.5f, belowFlip ? 0f : 1f);
            _labelRect.pivot = new Vector2(0.5f, belowFlip ? 1f : 0f);
            float bob = Mathf.Sin(Time.unscaledTime * 5f) * 4f;
            _labelRect.anchoredPosition = new Vector2(0f, (belowFlip ? -8f : 8f) + bob);

            float edgePulse = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 6f);

            for (int i = 0; i < 4; i++)
            {
                Color color = EdgeColor;
                color.a = EdgeColor.a * edgePulse;
                _bars[i].color = color;
            }

            _group.alpha = Mathf.MoveTowards(_group.alpha, 1f, Time.unscaledDeltaTime * 8f);
        }
    }
}

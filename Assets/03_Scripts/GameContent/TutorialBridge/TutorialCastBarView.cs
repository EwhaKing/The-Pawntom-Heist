using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Pawntom.TutorialBridge
{
    /// <summary>
    /// 분전반 접속 같은 "길게 누르기" 진행 막대. 화면 중앙 조금 아래.
    /// </summary>
    public sealed class TutorialCastBarView : MonoBehaviour
    {
        private RectTransform _root;
        private CanvasGroup _group;
        private RectTransform _fill;
        private TextMeshProUGUI _label;
        private TextMeshProUGUI _percent;
        private float _targetAlpha;
        private string _lastLabel;
        private int _lastPercent = -1;

        public void Build(TutorialUIFactory factory, Transform parent)
        {
            _root = factory.CreateRect("CastBar", parent);
            TutorialUIFactory.Place(_root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, TutorialTheme.CastBarOffsetY), TutorialTheme.CastBarSize);
            _group = _root.gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;

            Image background = factory.CreateImage("Background", _root, TutorialTheme.HintBackgroundColor);
            TutorialUIFactory.Stretch(background.rectTransform);

            Image fill = factory.CreateImage("Fill", _root, new Color(TutorialTheme.CommandColor.r, TutorialTheme.CommandColor.g, TutorialTheme.CommandColor.b, 0.35f));
            _fill = fill.rectTransform;
            _fill.anchorMin = new Vector2(0f, 0f);
            _fill.anchorMax = new Vector2(0f, 1f);
            _fill.pivot = new Vector2(0f, 0.5f);
            _fill.anchoredPosition = Vector2.zero;
            _fill.sizeDelta = new Vector2(0f, 0f);

            _label = factory.CreateText("Label", _root, 22f, TutorialTheme.BodyTextColor, TextAlignmentOptions.MidlineLeft);
            TutorialUIFactory.Inset(_label.rectTransform, 16f, 90f, 4f, 4f);

            _percent = factory.CreateText("Percent", _root, 22f, TutorialTheme.CommandColor, TextAlignmentOptions.MidlineRight);
            TutorialUIFactory.Inset(_percent.rectTransform, 300f, 16f, 4f, 4f);
        }

        public void Show(string label, float progress)
        {
            _targetAlpha = 1f;
            progress = Mathf.Clamp01(progress);

            if (!ReferenceEquals(label, _lastLabel))
            {
                _lastLabel = label;
                _label.text = label;
            }

            _fill.sizeDelta = new Vector2(TutorialTheme.CastBarSize.x * progress, 0f);

            int percent = Mathf.FloorToInt(progress * 100f);

            if (percent != _lastPercent)
            {
                _lastPercent = percent;
                _percent.SetText("{0:0}%", percent);
            }
        }

        public void Hide()
        {
            _targetAlpha = 0f;
        }

        private void Update()
        {
            _group.alpha = Mathf.MoveTowards(_group.alpha, _targetAlpha, Time.unscaledDeltaTime * 6f);
        }
    }
}

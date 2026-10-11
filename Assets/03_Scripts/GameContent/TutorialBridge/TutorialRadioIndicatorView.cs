using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Pawntom.TutorialBridge
{
    /// <summary>
    /// HUD 우하단 무전기 아이콘(UI_SPEC 1인칭 HUD 4모서리 규칙). 수신·발신은 초록, 평시는 회색,
    /// 비활성(8-7 "무전 아이콘 비활성화")은 빨강 + 취소선.
    /// 정식 무전기 HUD 가 생기면 그쪽으로 상태만 넘기면 된다.
    /// </summary>
    public sealed class TutorialRadioIndicatorView : MonoBehaviour
    {
        public enum Mode
        {
            Hidden,
            Idle,
            Receiving,
            Transmitting,
            Disabled
        }

        private const float BarRefreshSeconds = 0.07f;

        private RectTransform _root;
        private CanvasGroup _group;
        private Image _dot;
        private TextMeshProUGUI _label;
        private TextMeshProUGUI _status;
        private Image _strike;
        private Image[] _bars;
        private Mode _mode = Mode.Hidden;
        private float _nextRefresh;
        private float _targetAlpha;

        public void Build(TutorialUIFactory factory, Transform parent)
        {
            _root = factory.CreateRect("Radio", parent);
            TutorialUIFactory.Place(_root, new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-TutorialTheme.RadioMargin.x, TutorialTheme.RadioMargin.y), TutorialTheme.RadioSize);
            _group = _root.gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;

            Image background = factory.CreateImage("Background", _root, TutorialTheme.HintBackgroundColor);
            TutorialUIFactory.Stretch(background.rectTransform);

            _dot = factory.CreateImage("Dot", _root, TutorialTheme.RadioIdleColor);
            TutorialUIFactory.Place(_dot.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(22f, 0f), new Vector2(12f, 12f));

            _label = factory.CreateText("Label", _root, 22f, TutorialTheme.RadioIdleColor, TextAlignmentOptions.MidlineLeft);
            _label.fontStyle = FontStyles.Bold;
            _label.text = "RADIO";
            TutorialUIFactory.Inset(_label.rectTransform, 38f, 100f, 6f, 30f);

            _status = factory.CreateText("Status", _root, 18f, TutorialTheme.RadioIdleColor, TextAlignmentOptions.MidlineLeft);
            TutorialUIFactory.Inset(_status.rectTransform, 38f, 100f, 36f, 6f);

            _bars = new Image[4];

            for (int i = 0; i < _bars.Length; i++)
            {
                Image bar = factory.CreateImage("Bar" + i, _root, TutorialTheme.RadioIdleColor);
                RectTransform rect = bar.rectTransform;
                rect.anchorMin = new Vector2(1f, 0f);
                rect.anchorMax = new Vector2(1f, 0f);
                rect.pivot = new Vector2(0.5f, 0f);
                rect.anchoredPosition = new Vector2(-78f + i * 16f, 16f);
                rect.sizeDelta = new Vector2(9f, 10f + i * 9f);
                _bars[i] = bar;
            }

            _strike = factory.CreateImage("Strike", _root, TutorialTheme.RadioBlockedColor);
            TutorialUIFactory.Place(_strike.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(TutorialTheme.RadioSize.x * 0.92f, 4f));
            _strike.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 14f);
            _strike.gameObject.SetActive(false);

            SetMode(Mode.Hidden);
        }

        public void SetMode(Mode mode)
        {
            if (_mode == mode)
            {
                return;
            }

            _mode = mode;
            _targetAlpha = mode == Mode.Hidden ? 0f : 1f;

            Color color;
            string status;

            switch (mode)
            {
                case Mode.Receiving:
                    color = TutorialTheme.CommandColor;
                    status = "수신 중";
                    break;
                case Mode.Transmitting:
                    color = TutorialTheme.CommandColor;
                    status = "발신 중";
                    break;
                case Mode.Disabled:
                    color = TutorialTheme.RadioBlockedColor;
                    status = "교신 종료";
                    break;
                default:
                    color = TutorialTheme.RadioIdleColor;
                    status = "대기";
                    break;
            }

            _dot.color = color;
            _label.color = color;
            _status.color = color;
            _status.text = status;
            _strike.gameObject.SetActive(mode == Mode.Disabled);

            for (int i = 0; i < _bars.Length; i++)
            {
                _bars[i].color = color;
                _bars[i].rectTransform.localScale = Vector3.one;
            }
        }

        private void Update()
        {
            float deltaTime = Time.unscaledDeltaTime;
            _group.alpha = Mathf.MoveTowards(_group.alpha, _targetAlpha, deltaTime * 3f);

            float now = Time.unscaledTime;

            if (now < _nextRefresh)
            {
                return;
            }

            _nextRefresh = now + BarRefreshSeconds;

            for (int i = 0; i < _bars.Length; i++)
            {
                float level;

                switch (_mode)
                {
                    case Mode.Receiving:
                        level = Random.Range(0.25f, 1f);
                        break;
                    case Mode.Transmitting:
                        level = 0.75f + 0.25f * Mathf.Sin(now * 9f + i);
                        break;
                    case Mode.Disabled:
                        level = 0.2f;
                        break;
                    default:
                        level = 0.45f;
                        break;
                }

                _bars[i].rectTransform.localScale = new Vector3(1f, level, 1f);
            }

            // 수신 중에는 점이 깜빡인다.
            if (_mode == Mode.Receiving)
            {
                Color color = _dot.color;
                color.a = Mathf.PingPong(now * 4f, 1f) > 0.5f ? 1f : 0.3f;
                _dot.color = color;
            }
        }
    }
}

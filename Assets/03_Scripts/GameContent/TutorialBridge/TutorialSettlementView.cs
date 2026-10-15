using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Pawntom.TutorialBridge
{
    /// <summary>
    /// 튜토리얼 정산 결과창(8-1 "정산 결과창 표시").
    /// 정식 정산 화면(GameManager.EscapeSuccess 의 TODO)이 아직 없어서 튜토리얼이 가방 내용과 판매가 합계를 보여준다.
    /// 정식 화면이 생기면 디렉터의 정산 열기를 그쪽 호출로 바꾸면 된다.
    /// </summary>
    public sealed class TutorialSettlementView : MonoBehaviour
    {
        public struct Entry
        {
            public string Name;
            public int Value;
        }

        private const int MaxRows = 4;
        private const float FadeSeconds = 0.35f;
        private const float CountSeconds = 1.6f;

        private RectTransform _root;
        private CanvasGroup _group;
        private TextMeshProUGUI[] _rowNames;
        private TextMeshProUGUI[] _rowValues;
        private TextMeshProUGUI _empty;
        private TextMeshProUGUI _total;
        private TextMeshProUGUI _stamp;
        private TutorialAudio _audio;

        private float _targetAlpha;
        private bool _counting;
        private float _countTime;
        private int _totalValue;
        private int _lastShown = -1;
        private Action _onRevealed;

        public bool IsOpen
        {
            get { return _targetAlpha > 0f; }
        }

        public void Build(TutorialUIFactory factory, Transform parent, TutorialAudio audioPlayer)
        {
            _audio = audioPlayer;

            _root = factory.CreateRect("Settlement", parent);
            TutorialUIFactory.Place(_root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), TutorialTheme.SettlementSize);
            _group = _root.gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;

            Image background = factory.CreateImage("Background", _root, TutorialTheme.PanelColor);
            TutorialUIFactory.Stretch(background.rectTransform);

            Image edge = factory.CreateImage("Edge", _root, TutorialTheme.CommandColor);
            TutorialUIFactory.Place(edge.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero,
                new Vector2(TutorialTheme.SettlementSize.x, 3f));

            TextMeshProUGUI title = factory.CreateText("Title", _root, 36f, TutorialTheme.CommandColor, TextAlignmentOptions.Center);
            title.fontStyle = FontStyles.Bold;
            title.text = "정산";
            TutorialUIFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(560f, 50f));

            TextMeshProUGUI caption = factory.CreateText("Caption", _root, 18f, TutorialTheme.RadioIdleColor, TextAlignmentOptions.Center);
            caption.text = "가져온 전리품";
            TutorialUIFactory.Place(caption.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(560f, 28f));

            _rowNames = new TextMeshProUGUI[MaxRows];
            _rowValues = new TextMeshProUGUI[MaxRows];

            for (int i = 0; i < MaxRows; i++)
            {
                float y = -118f - i * 46f;

                _rowNames[i] = factory.CreateText("Item" + i, _root, 26f, TutorialTheme.BodyTextColor, TextAlignmentOptions.MidlineLeft);
                TutorialUIFactory.Place(_rowNames[i].rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-90f, y), new Vector2(360f, 40f));

                _rowValues[i] = factory.CreateText("Value" + i, _root, 26f, TutorialTheme.HintAccentColor, TextAlignmentOptions.MidlineRight);
                TutorialUIFactory.Place(_rowValues[i].rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(170f, y), new Vector2(180f, 40f));
            }

            _empty = factory.CreateText("Empty", _root, 24f, TutorialTheme.RadioIdleColor, TextAlignmentOptions.Center);
            _empty.text = "가방이 비어 있습니다";
            TutorialUIFactory.Place(_empty.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -160f), new Vector2(560f, 40f));

            Image divider = factory.CreateImage("Divider", _root, TutorialTheme.PanelEdgeColor);
            TutorialUIFactory.Place(divider.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 92f), new Vector2(560f, 2f));

            _total = factory.CreateText("Total", _root, 34f, TutorialTheme.HintAccentColor, TextAlignmentOptions.MidlineRight);
            _total.fontStyle = FontStyles.Bold;
            TutorialUIFactory.Place(_total.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(560f, 50f));

            _stamp = factory.CreateText("Stamp", _root, 22f, TutorialTheme.CommandColor, TextAlignmentOptions.MidlineLeft);
            _stamp.text = "정산 완료";
            TutorialUIFactory.Place(_stamp.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(560f, 50f));
            _stamp.gameObject.SetActive(false);

            _root.gameObject.SetActive(false);
        }

        public void Open(List<Entry> entries)
        {
            _totalValue = 0;
            int count = entries == null ? 0 : Mathf.Min(entries.Count, MaxRows);

            for (int i = 0; i < MaxRows; i++)
            {
                bool used = i < count;
                _rowNames[i].gameObject.SetActive(used);
                _rowValues[i].gameObject.SetActive(used);

                if (!used)
                {
                    continue;
                }

                _rowNames[i].text = entries[i].Name;
                _rowValues[i].SetText("{0:0} G", entries[i].Value);
                _totalValue += entries[i].Value;
            }

            _empty.gameObject.SetActive(count == 0);
            _total.SetText("합계  {0:0} G", 0);
            _stamp.gameObject.SetActive(false);
            _counting = false;
            _lastShown = -1;

            _root.gameObject.SetActive(true);
            _targetAlpha = 1f;
        }

        /// <summary>합계를 0부터 세어 올린 뒤 onRevealed 를 부른다.</summary>
        public void Reveal(Action onRevealed)
        {
            _onRevealed = onRevealed;
            _counting = true;
            _countTime = 0f;
        }

        public void Close()
        {
            _targetAlpha = 0f;
            _counting = false;
        }

        private void Update()
        {
            float deltaTime = Time.unscaledDeltaTime;
            _group.alpha = Mathf.MoveTowards(_group.alpha, _targetAlpha, deltaTime / FadeSeconds);

            if (_targetAlpha <= 0f && _group.alpha <= 0f && _root.gameObject.activeSelf)
            {
                _root.gameObject.SetActive(false);
            }

            if (!_counting)
            {
                return;
            }

            _countTime += deltaTime;
            float t = Mathf.Clamp01(_countTime / CountSeconds);
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            int shown = Mathf.RoundToInt(_totalValue * eased);

            if (shown != _lastShown)
            {
                _lastShown = shown;
                _total.SetText("합계  {0:0} G", shown);
                _audio?.PlayBlip(Pawntom.Tutorial.Core.TutorialSpeaker.Kkamnyang);
            }

            if (t < 1f)
            {
                return;
            }

            _counting = false;
            _stamp.gameObject.SetActive(true);
            _audio?.PlaySquelch();

            Action callback = _onRevealed;
            _onRevealed = null;
            callback?.Invoke();
        }
    }
}

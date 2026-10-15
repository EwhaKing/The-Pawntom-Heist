using System.Collections.Generic;
using Pawntom.Tutorial.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Pawntom.TutorialBridge
{
    /// <summary>
    /// 키 안내 문구. 대화창이 사라진 뒤에만 보이고, 안내한 행동이 확인되면 초록으로 번쩍인 뒤 바로 사라진다.
    /// 최대 <see cref="TutorialSequencer.MaxHints"/>줄을 아래에서 위로 쌓는다.
    /// </summary>
    public sealed class TutorialHintView : MonoBehaviour
    {
        private const float FadeInSeconds = 0.22f;
        private const float FadeOutSeconds = 0.25f;
        private const float CompleteFlashSeconds = 0.32f;

        private sealed class Row
        {
            public RectTransform Rect;
            public CanvasGroup Group;
            public Image Background;
            public Image Accent;
            public TextMeshProUGUI Label;
            public TutorialHint Hint;
            public bool Leaving;
            public bool LeavingCompleted;
            public float LeaveTime;
            public float Alpha;
            public float TargetY;
            public int Order;
        }

        private int _orderCounter;

        private readonly List<Row> _rows = new List<Row>(TutorialSequencer.MaxHints + 1);
        private IReadOnlyDictionary<string, string> _tokens;
        private TutorialAudio _audio;
        private bool _dialogueIdle;

        public void Build(TutorialUIFactory factory, Transform parent, IReadOnlyDictionary<string, string> tokens, TutorialAudio audioPlayer)
        {
            _tokens = tokens;
            _audio = audioPlayer;

            // 사라지는 줄과 새 줄이 잠깐 겹칠 수 있어 한 칸 여유를 둔다.
            for (int i = 0; i < TutorialSequencer.MaxHints + 1; i++)
            {
                _rows.Add(CreateRow(factory, parent, i));
            }
        }

        public void SetDialogueIdle(bool idle)
        {
            _dialogueIdle = idle;
        }

        public void OnHintAdded(TutorialHint hint)
        {
            Row row = FindFreeRow();
            row.Hint = hint;
            row.Order = ++_orderCounter;
            row.Leaving = false;
            row.LeavingCompleted = false;
            row.Alpha = 0f;
            row.Label.text = TutorialHintFormatter.Format(hint.Text, _tokens, TutorialTheme.HighlightOpen, TutorialTheme.HighlightClose);
            row.Accent.color = TutorialTheme.HintAccentColor;
            row.Rect.localScale = Vector3.one;
            row.Rect.gameObject.SetActive(true);
            Relayout(snapNew: row);
        }

        /// <summary>상황 대사 뒤 이미 걸려 있던 안내를 다시 강조한다 — 처음 나타날 때처럼 깜빡이며 떠오른다.</summary>
        public void OnHintRecalled(TutorialHint hint)
        {
            for (int i = 0; i < _rows.Count; i++)
            {
                Row row = _rows[i];

                if (row.Hint == hint && !row.Leaving)
                {
                    row.Alpha = 0f;
                    row.Rect.anchoredPosition = new Vector2(0f, row.TargetY - 10f);
                }
            }
        }

        public void OnHintRemoved(TutorialHint hint, TutorialHintRemoval reason)
        {
            for (int i = 0; i < _rows.Count; i++)
            {
                Row row = _rows[i];

                if (row.Hint != hint || row.Leaving)
                {
                    continue;
                }

                row.Leaving = true;
                row.LeaveTime = 0f;
                row.LeavingCompleted = reason == TutorialHintRemoval.Completed && row.Alpha > 0.05f;

                if (row.LeavingCompleted)
                {
                    row.Accent.color = TutorialTheme.HintDoneColor;
                    _audio?.PlaySquelch();
                }
            }

            Relayout(null);
        }

        public void HideAll()
        {
            for (int i = 0; i < _rows.Count; i++)
            {
                _rows[i].Hint = null;
                _rows[i].Leaving = false;
                _rows[i].Rect.gameObject.SetActive(false);
            }
        }

        private void Update()
        {
            float deltaTime = Time.unscaledDeltaTime;

            for (int i = 0; i < _rows.Count; i++)
            {
                Row row = _rows[i];

                if (row.Hint == null)
                {
                    continue;
                }

                Vector2 position = row.Rect.anchoredPosition;
                position.y = Mathf.Lerp(position.y, row.TargetY, 1f - Mathf.Exp(-14f * deltaTime));
                row.Rect.anchoredPosition = position;

                if (row.Leaving)
                {
                    row.LeaveTime += deltaTime;

                    if (row.LeavingCompleted && row.LeaveTime < CompleteFlashSeconds)
                    {
                        // 완료: 살짝 커지며 초록 테두리로 번쩍.
                        float pulse = Mathf.Sin(row.LeaveTime / CompleteFlashSeconds * Mathf.PI);
                        row.Rect.localScale = Vector3.one * (1f + 0.05f * pulse);
                        row.Background.color = Color.Lerp(TutorialTheme.HintBackgroundColor, new Color(0.1f, 0.35f, 0.2f, 0.85f), pulse);
                    }
                    else
                    {
                        row.Alpha = Mathf.MoveTowards(row.Alpha, 0f, deltaTime / FadeOutSeconds);
                    }

                    row.Group.alpha = row.Alpha;

                    if (row.Alpha <= 0f || (!row.LeavingCompleted && row.Alpha <= 0.001f))
                    {
                        row.Hint = null;
                        row.Leaving = false;
                        row.Background.color = TutorialTheme.HintBackgroundColor;
                        row.Rect.localScale = Vector3.one;
                        row.Rect.gameObject.SetActive(false);
                    }

                    continue;
                }

                float target = _dialogueIdle ? 1f : 0f;
                row.Alpha = Mathf.MoveTowards(row.Alpha, target, deltaTime / FadeInSeconds);
                row.Group.alpha = row.Alpha;

                // 처음 나타날 때 약한 글리치 깜빡임.
                if (target > 0f && row.Alpha < 1f && Random.value < 0.25f)
                {
                    row.Group.alpha = row.Alpha * 0.4f;
                }
            }
        }

        private Row CreateRow(TutorialUIFactory factory, Transform parent, int index)
        {
            RectTransform rect = factory.CreateRect("HintRow" + index, parent);
            TutorialUIFactory.Place(rect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, TutorialTheme.HintBottom), TutorialTheme.HintRowSize);

            CanvasGroup group = rect.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            group.alpha = 0f;

            Image background = factory.CreateImage("Background", rect, TutorialTheme.HintBackgroundColor);
            TutorialUIFactory.Stretch(background.rectTransform);

            Image accent = factory.CreateImage("Accent", rect, TutorialTheme.HintAccentColor);
            accent.rectTransform.anchorMin = new Vector2(0f, 0f);
            accent.rectTransform.anchorMax = new Vector2(0f, 1f);
            accent.rectTransform.pivot = new Vector2(0f, 0.5f);
            accent.rectTransform.anchoredPosition = Vector2.zero;
            accent.rectTransform.sizeDelta = new Vector2(5f, 0f);

            TextMeshProUGUI label = factory.CreateText("Label", rect, 25f, TutorialTheme.BodyTextColor, TextAlignmentOptions.Center);
            label.richText = true;
            TutorialUIFactory.Inset(label.rectTransform, 22f, 16f, 6f, 6f);

            rect.gameObject.SetActive(false);

            return new Row
            {
                Rect = rect,
                Group = group,
                Background = background,
                Accent = accent,
                Label = label
            };
        }

        private Row FindFreeRow()
        {
            for (int i = 0; i < _rows.Count; i++)
            {
                if (_rows[i].Hint == null)
                {
                    return _rows[i];
                }
            }

            // 모두 차 있으면 가장 많이 사라진 줄을 재활용한다.
            Row oldest = _rows[0];

            for (int i = 1; i < _rows.Count; i++)
            {
                if (_rows[i].Leaving && (!oldest.Leaving || _rows[i].Alpha < oldest.Alpha))
                {
                    oldest = _rows[i];
                }
            }

            return oldest;
        }

        /// <summary>남아 있는 안내를 오래된 것이 아래가 되도록 쌓는다.</summary>
        private void Relayout(Row snapNew)
        {
            int slot = 0;
            int lastOrder = int.MinValue;

            // 줄이 3개 이하라 정렬 대신 "다음으로 작은 순번"을 반복해서 찾는다(할당 없음).
            while (true)
            {
                Row next = null;

                for (int i = 0; i < _rows.Count; i++)
                {
                    Row row = _rows[i];

                    if (row.Hint == null || row.Leaving || row.Order <= lastOrder)
                    {
                        continue;
                    }

                    if (next == null || row.Order < next.Order)
                    {
                        next = row;
                    }
                }

                if (next == null)
                {
                    return;
                }

                lastOrder = next.Order;
                next.TargetY = TutorialTheme.HintBottom + slot * TutorialTheme.HintRowSpacing;
                slot++;

                if (next == snapNew)
                {
                    next.Rect.anchoredPosition = new Vector2(0f, next.TargetY - 10f);
                }
            }
        }
    }
}

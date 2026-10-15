using System;
using Pawntom.Tutorial.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Pawntom.TutorialBridge
{
    /// <summary>
    /// 무전 대화창. 한 줄씩 받아 타자기 출력 → 머무름 → (다음 줄로 교체 | 닫힘)을 연출한다.
    /// <list type="bullet">
    /// <item>열림/닫힘: 세로로 펼쳐지며 깜빡이는 CRT 켜짐 느낌.</item>
    /// <item>타자기: 화자별 속도, 문장부호 뒤 쉼, 글자마다 출력음.</item>
    /// <item>노이즈: 방금 찍힌 글자가 잡음 기호로 보였다 풀리는 해독 꼬리, 지휘부(무전) 대사에는 주기적인 간섭
    ///       (글자 일부 잡음 + 글자 떨림 + 창 흔들림 + 지직 소리).</item>
    /// </list>
    /// Update 에서 할당하지 않는다 — 문자열 대신 char 버퍼와 TMP SetCharArray/maxVisibleCharacters 를 쓴다.
    /// </summary>
    public sealed class TutorialDialogueView : MonoBehaviour
    {
        private enum Phase
        {
            Hidden,
            Opening,
            Typing,
            Holding,
            Closing
        }

        private const float OpenSeconds = 0.22f;
        private const float CloseSeconds = 0.16f;
        private const float SwapSeconds = 0.1f;
        private const float BarRefreshSeconds = 0.08f;

        private float _commandCharactersPerSecond = 22f;
        private float _catCharactersPerSecond = 27f;

        private RectTransform _panel;
        private CanvasGroup _group;
        private Image _accent;
        private Image _edge;
        private RawImage _scanlines;
        private TextMeshProUGUI _speaker;
        private TextMeshProUGUI _channel;
        private TextMeshProUGUI _body;
        private Image[] _bars;
        private Vector2 _panelHome;

        private TutorialAudio _audio;
        private Action _onLineDone;

        private readonly TypewriterModel _typewriter = new TypewriterModel(22f);
        private readonly System.Random _random = new System.Random(20261011);
        private char[] _buffer = new char[128];

        private TutorialLine _line;
        private Phase _phase = Phase.Hidden;
        private float _phaseTime;
        private float _holdSeconds;
        private float _swapRemaining;
        private bool _doneNotified;

        private float _nextGlitchAt;
        private float _glitchUntil;
        private bool _wasGlitching;
        private float _nextBarRefresh;

        public bool IsVisible
        {
            get { return _phase != Phase.Hidden; }
        }

        public void Build(TutorialUIFactory factory, Transform parent, TutorialAudio audioPlayer, int maxTextLength, Action onLineDone)
        {
            _audio = audioPlayer;
            _onLineDone = onLineDone;
            _buffer = new char[Mathf.Max(16, maxTextLength)];

            _panel = factory.CreateRect("Dialogue", parent);
            TutorialUIFactory.Place(_panel, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, TutorialTheme.DialogueBottom), TutorialTheme.DialogueSize);
            _panelHome = _panel.anchoredPosition;
            _group = _panel.gameObject.AddComponent<CanvasGroup>();
            _group.blocksRaycasts = false;
            _group.interactable = false;

            Image background = factory.CreateImage("Background", _panel, TutorialTheme.PanelColor);
            TutorialUIFactory.Stretch(background.rectTransform);

            _scanlines = factory.CreateRawImage("Scanlines", _panel, factory.CreateScanlineTexture(), TutorialTheme.ScanlineColor);
            TutorialUIFactory.Stretch(_scanlines.rectTransform);
            _scanlines.uvRect = new Rect(0f, 0f, 1f, TutorialTheme.DialogueSize.y / 3f);

            _edge = factory.CreateImage("Edge", _panel, TutorialTheme.PanelEdgeColor);
            TutorialUIFactory.Place(_edge.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero,
                new Vector2(TutorialTheme.DialogueSize.x, 2f));

            _accent = factory.CreateImage("Accent", _panel, TutorialTheme.CommandColor);
            _accent.rectTransform.anchorMin = new Vector2(0f, 0f);
            _accent.rectTransform.anchorMax = new Vector2(0f, 1f);
            _accent.rectTransform.pivot = new Vector2(0f, 0.5f);
            _accent.rectTransform.anchoredPosition = Vector2.zero;
            _accent.rectTransform.sizeDelta = new Vector2(6f, 0f);

            _speaker = factory.CreateText("Speaker", _panel, 25f, TutorialTheme.CommandColor, TextAlignmentOptions.TopLeft);
            _speaker.fontStyle = FontStyles.Bold;
            TutorialUIFactory.Inset(_speaker.rectTransform, 30f, 300f, 12f, TutorialTheme.DialogueSize.y - 46f);

            _channel = factory.CreateText("Channel", _panel, 18f, TutorialTheme.CommandColor, TextAlignmentOptions.TopRight);
            TutorialUIFactory.Inset(_channel.rectTransform, TutorialTheme.DialogueSize.x - 230f, 92f, 16f, TutorialTheme.DialogueSize.y - 44f);

            _bars = new Image[4];

            for (int i = 0; i < _bars.Length; i++)
            {
                Image bar = factory.CreateImage("SignalBar" + i, _panel, TutorialTheme.CommandColor);
                RectTransform rect = bar.rectTransform;
                rect.anchorMin = new Vector2(1f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(0.5f, 0f);
                rect.anchoredPosition = new Vector2(-72f + i * 12f, -40f);
                rect.sizeDelta = new Vector2(7f, 6f + i * 5f);
                _bars[i] = bar;
            }

            _body = factory.CreateText("Body", _panel, TutorialTheme.DialogueFontSize, TutorialTheme.BodyTextColor, TextAlignmentOptions.TopLeft);
            _body.richText = false; // 잡음 기호 '<' '>' 가 태그로 읽히지 않도록
            _body.lineSpacing = 6f;
            TutorialUIFactory.Inset(_body.rectTransform, 30f, 30f, 52f, 14f);

            _panel.gameObject.SetActive(false);
        }

        public void SetSpeeds(float commandCharactersPerSecond, float catCharactersPerSecond)
        {
            _commandCharactersPerSecond = Mathf.Max(1f, commandCharactersPerSecond);
            _catCharactersPerSecond = Mathf.Max(1f, catCharactersPerSecond);
        }

        /// <summary>새 대사를 띄운다. 이미 떠 있으면 짧은 간섭과 함께 교체한다.</summary>
        public void Show(TutorialLine line)
        {
            if (line == null)
            {
                return;
            }

            bool alreadyOpen = _phase == Phase.Typing || _phase == Phase.Holding;
            bool isCommand = line.Speaker == TutorialSpeaker.Command;

            _line = line;
            _doneNotified = false;
            _typewriter.CharactersPerSecond = isCommand ? _commandCharactersPerSecond : _catCharactersPerSecond;
            _typewriter.Begin(line.Text);
            _holdSeconds = TypewriterModel.HoldSecondsFor(line.Text, 1.3f, 4.2f, 0.035f);

            if (_buffer.Length < line.Text.Length)
            {
                _buffer = new char[line.Text.Length];
            }

            Color speakerColor = isCommand ? TutorialTheme.CommandColor : TutorialTheme.CatColor;
            _speaker.text = isCommand ? TutorialTheme.CommandName : TutorialTheme.CatName;
            _speaker.color = speakerColor;
            _channel.text = isCommand ? "RX  수신" : "TX  발신";
            _channel.color = speakerColor;
            _accent.color = speakerColor;

            for (int i = 0; i < _bars.Length; i++)
            {
                _bars[i].color = speakerColor;
            }

            ComposeBody(0, 0, 0d);
            _nextGlitchAt = Time.unscaledTime + RandomRange(0.5f, 1.4f);
            _glitchUntil = 0f;

            if (alreadyOpen)
            {
                _swapRemaining = SwapSeconds;
                SetPhase(Phase.Typing);
                _audio?.PlayStatic(false);
                return;
            }

            _panel.gameObject.SetActive(true);
            _swapRemaining = 0f;
            SetPhase(Phase.Opening);
            _audio?.PlaySquelch();

            if (isCommand)
            {
                // "칙..."으로 시작하는 첫 교신은 잡음을 길게.
                _audio?.PlayStatic(line.Text.StartsWith("칙", StringComparison.Ordinal));
            }
        }

        /// <summary>대화창을 닫는다(다음 대사가 바로 이어지지 않을 때).</summary>
        public void Close()
        {
            if (_phase == Phase.Hidden || _phase == Phase.Closing)
            {
                return;
            }

            SetPhase(Phase.Closing);
            ResetPanelPose();
        }

        /// <summary>디버그: 타자 출력을 끝까지 건너뛴다. 이미 다 찍혔으면 머무름을 끝낸다.</summary>
        public void SkipTyping()
        {
            if (_phase == Phase.Typing || _phase == Phase.Opening)
            {
                _typewriter.Complete();
                _swapRemaining = 0f;
                ComposeBody(_typewriter.Length, 0, 0d);
                SetPhase(Phase.Holding);
                return;
            }

            if (_phase == Phase.Holding)
            {
                _phaseTime = _holdSeconds;
            }
        }

        private void Update()
        {
            if (_phase == Phase.Hidden)
            {
                return;
            }

            float deltaTime = Time.unscaledDeltaTime;
            _phaseTime += deltaTime;

            AnimateScanlines(deltaTime);
            AnimateBars();

            switch (_phase)
            {
                case Phase.Opening:
                    UpdateOpening();
                    break;
                case Phase.Typing:
                    UpdateTyping(deltaTime);
                    break;
                case Phase.Holding:
                    UpdateHolding();
                    break;
                case Phase.Closing:
                    UpdateClosing();
                    break;
            }
        }

        private void UpdateOpening()
        {
            float t = Mathf.Clamp01(_phaseTime / OpenSeconds);
            float eased = 1f - (1f - t) * (1f - t);
            _panel.localScale = new Vector3(1f, Mathf.Max(0.02f, eased), 1f);

            // 켜지는 동안 두세 번 깜빡인다.
            _group.alpha = t < 1f ? (RandomRange(0f, 1f) < 0.35f ? 0.3f : 1f) : 1f;

            if (t >= 1f)
            {
                _group.alpha = 1f;
                _panel.localScale = Vector3.one;
                SetPhase(Phase.Typing);
            }
        }

        private void UpdateTyping(float deltaTime)
        {
            if (_swapRemaining > 0f)
            {
                _swapRemaining -= deltaTime;
                _group.alpha = RandomRange(0f, 1f) < 0.5f ? 0.55f : 1f;
                _panel.anchoredPosition = _panelHome + new Vector2(RandomRange(-4f, 4f), 0f);

                if (_swapRemaining > 0f)
                {
                    return;
                }

                ResetPanelPose();
            }

            int before = _typewriter.Visible;
            int revealed = _typewriter.Advance(deltaTime);

            if (revealed > 0 && _audio != null)
            {
                string text = _typewriter.Text;

                for (int i = before; i < before + revealed; i++)
                {
                    if (TypewriterModel.IsAudible(text[i]))
                    {
                        _audio.PlayBlip(_line.Speaker);
                        break; // 한 프레임에 한 번이면 충분하다(간격 제한은 오디오 쪽에도 있다).
                    }
                }
            }

            bool glitching = UpdateGlitchWindow();
            int tail = _typewriter.IsComplete ? 0 : (_line.Speaker == TutorialSpeaker.Command ? 2 : 1);
            ComposeBody(_typewriter.Visible, tail, glitching ? 0.22d : 0d);

            if (glitching)
            {
                JitterGlyphs();
            }

            if (_typewriter.IsComplete)
            {
                ComposeBody(_typewriter.Length, 0, 0d);
                SetPhase(Phase.Holding);
            }
        }

        private void UpdateHolding()
        {
            bool glitching = UpdateGlitchWindow();

            if (glitching)
            {
                ComposeBody(_typewriter.Length, 0, 0.16d);
                JitterGlyphs();
            }
            else if (_wasGlitching)
            {
                ComposeBody(_typewriter.Length, 0, 0d);
                ResetPanelPose();
            }

            _wasGlitching = glitching;

            if (_doneNotified || _phaseTime < _holdSeconds)
            {
                return;
            }

            _doneNotified = true;
            TutorialLine finished = _line;
            _onLineDone?.Invoke();

            // 콜백 안에서 다음 대사가 Show 되지 않았다면 창을 닫는다.
            if (_line == finished && _phase == Phase.Holding)
            {
                Close();
            }
        }

        private void UpdateClosing()
        {
            float t = Mathf.Clamp01(_phaseTime / CloseSeconds);
            _panel.localScale = new Vector3(1f + t * 0.03f, Mathf.Max(0.02f, 1f - t), 1f);
            _group.alpha = 1f - t;

            if (t >= 1f)
            {
                _panel.gameObject.SetActive(false);
                _panel.localScale = Vector3.one;
                _group.alpha = 1f;
                SetPhase(Phase.Hidden);
            }
        }

        /// <summary>지휘부 대사에만 주기적 간섭을 건다. 지금 간섭 중인지 돌려준다.</summary>
        private bool UpdateGlitchWindow()
        {
            if (_line == null || _line.Speaker != TutorialSpeaker.Command)
            {
                return false;
            }

            float now = Time.unscaledTime;

            if (now >= _nextGlitchAt)
            {
                _glitchUntil = now + RandomRange(0.06f, 0.15f);
                _nextGlitchAt = now + RandomRange(1.1f, 2.8f);

                if (RandomRange(0f, 1f) < 0.6f)
                {
                    _audio?.PlayStatic(false);
                }
            }

            bool glitching = now < _glitchUntil;

            if (glitching)
            {
                _group.alpha = RandomRange(0.72f, 1f);
                _panel.anchoredPosition = _panelHome + new Vector2(RandomRange(-3f, 3f), RandomRange(-1.5f, 1.5f));
            }
            else if (_wasGlitching && _phase == Phase.Typing)
            {
                ResetPanelPose();
            }

            if (_phase == Phase.Typing)
            {
                _wasGlitching = glitching;
            }

            return glitching;
        }

        private void ComposeBody(int visible, int decodeTail, double glitchChance)
        {
            string text = _typewriter.Text;
            RadioNoiseText.Compose(text, _buffer, visible, decodeTail, glitchChance, _random);
            _body.SetCharArray(_buffer, 0, text.Length);
            _body.maxVisibleCharacters = visible;
        }

        /// <summary>보이는 글자 일부를 몇 픽셀씩 흔든다(전파 간섭).</summary>
        private void JitterGlyphs()
        {
            _body.ForceMeshUpdate();
            TMP_TextInfo info = _body.textInfo;

            for (int i = 0; i < info.characterCount; i++)
            {
                TMP_CharacterInfo character = info.characterInfo[i];

                if (!character.isVisible || RandomRange(0f, 1f) > 0.35f)
                {
                    continue;
                }

                Vector3 offset = new Vector3(RandomRange(-3f, 3f), RandomRange(-2f, 2f), 0f);
                Vector3[] vertices = info.meshInfo[character.materialReferenceIndex].vertices;
                int index = character.vertexIndex;

                vertices[index] += offset;
                vertices[index + 1] += offset;
                vertices[index + 2] += offset;
                vertices[index + 3] += offset;
            }

            _body.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
        }

        private void AnimateScanlines(float deltaTime)
        {
            Rect uv = _scanlines.uvRect;
            uv.y = Mathf.Repeat(uv.y + deltaTime * 0.8f, 1f);
            _scanlines.uvRect = uv;
        }

        private void AnimateBars()
        {
            float now = Time.unscaledTime;

            if (now < _nextBarRefresh || _line == null)
            {
                return;
            }

            _nextBarRefresh = now + BarRefreshSeconds;
            bool receiving = _line.Speaker == TutorialSpeaker.Command && (_phase == Phase.Typing || _phase == Phase.Opening);

            for (int i = 0; i < _bars.Length; i++)
            {
                float level = receiving ? RandomRange(0.35f, 1f) : 1f;
                Color color = _bars[i].color;
                color.a = receiving ? (RandomRange(0f, 1f) < 0.8f ? 1f : 0.35f) : 0.85f;
                _bars[i].color = color;
                _bars[i].rectTransform.localScale = new Vector3(1f, level, 1f);
            }
        }

        private void SetPhase(Phase phase)
        {
            _phase = phase;
            _phaseTime = 0f;
        }

        private void ResetPanelPose()
        {
            _panel.anchoredPosition = _panelHome;
            _group.alpha = 1f;
        }

        private float RandomRange(float min, float max)
        {
            return min + (float)_random.NextDouble() * (max - min);
        }
    }
}

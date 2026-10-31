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
    /// <item>넘기기: 메인 대사는 Space·좌클릭으로 넘긴다 — 출력 중이면 한 번에 다 찍고, 다 찍힌 뒤에는 오른쪽 아래 ▼ 가
    ///       깜빡이며 다음 입력을 기다린다. 상황 대사(추격·재촉 등)는 플레이 중에 끼어드므로 예전처럼 저절로 넘어간다.</item>
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
        private static readonly Vector2 NextArrowSize = new Vector2(18f, 12f);
        private static readonly Vector2 NextArrowHome = new Vector2(-30f, 22f);

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
        private RawImage _nextArrow;
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
        private bool _waitForConfirm;

        private float _nextGlitchAt;
        private float _glitchUntil;
        private bool _wasGlitching;
        private float _nextBarRefresh;

        public bool IsVisible
        {
            get { return _phase != Phase.Hidden; }
        }

        /// <summary>
        /// 넘기기 입력을 기다리는 메인 대사가 떠 있다(출력 중 포함). 이 동안 플레이어 이동·시점을 막는다.
        /// </summary>
        public bool IsHoldingPlayer
        {
            get { return _waitForConfirm && _phase != Phase.Hidden && _phase != Phase.Closing; }
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
            // 단말기·해킹 팝업처럼 커서가 풀린 화면에서는 대화창을 직접 클릭해 넘긴다.
            // 그 클릭이 창 아래 단말기 버튼까지 눌리지 않도록, 넘기기를 기다리는 동안에만 창이 클릭을 받는다(평소엔 통과).
            _panel.gameObject.AddComponent<Canvas>();
            _panel.gameObject.AddComponent<GraphicRaycaster>();
            _group = _panel.gameObject.AddComponent<CanvasGroup>();
            _group.blocksRaycasts = false;
            _group.interactable = false;

            Image background = factory.CreateImage("Background", _panel, TutorialTheme.PanelColor);
            background.raycastTarget = true;
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

            // 다 찍힌 뒤 "넘기려면 누르세요" 표시. 오른쪽 아래에서 위아래로 까딱인다.
            _nextArrow = factory.CreateRawImage("NextArrow", _panel,
                factory.CreateDownTriangleTexture((int)NextArrowSize.x * 2, (int)NextArrowSize.y * 2), TutorialTheme.CommandColor);
            TutorialUIFactory.Place(_nextArrow.rectTransform, new Vector2(1f, 0f), new Vector2(0.5f, 0.5f), NextArrowHome, NextArrowSize);
            _nextArrow.gameObject.SetActive(false);

            _panel.gameObject.SetActive(false);
        }

        public void SetSpeeds(float commandCharactersPerSecond, float catCharactersPerSecond)
        {
            _commandCharactersPerSecond = Mathf.Max(1f, commandCharactersPerSecond);
            _catCharactersPerSecond = Mathf.Max(1f, catCharactersPerSecond);
        }

        /// <summary>새 대사를 띄운다. 이미 떠 있으면 짧은 간섭과 함께 교체한다.</summary>
        /// <param name="waitForConfirm">
        /// true 면 Space·좌클릭으로 넘긴다(출력 중 누르면 즉시 다 찍고, 다 찍힌 뒤 누르면 다음 대사).
        /// false 면 머무름 시간이 지나면 저절로 넘어간다.
        /// </param>
        public void Show(TutorialLine line, bool waitForConfirm)
        {
            if (line == null)
            {
                return;
            }

            bool alreadyOpen = _phase == Phase.Typing || _phase == Phase.Holding;
            bool isCommand = line.Speaker == TutorialSpeaker.Command;

            _line = line;
            _doneNotified = false;
            _waitForConfirm = waitForConfirm;
            _nextArrow.gameObject.SetActive(false);
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
            _group.blocksRaycasts = false;
            _nextArrow.gameObject.SetActive(false);
            ResetPanelPose();
        }

        /// <summary>타자 출력을 끝까지 건너뛴다. 이미 다 찍혔으면 이 대사를 끝낸다(넘기기 입력·디버그 F9 공용).</summary>
        public void SkipTyping()
        {
            if (_phase == Phase.Typing || _phase == Phase.Opening)
            {
                CompleteTyping();
                return;
            }

            if (_phase == Phase.Holding)
            {
                FinishLine();
            }
        }

        private void CompleteTyping()
        {
            _typewriter.Complete();
            _swapRemaining = 0f;
            _group.alpha = 1f;
            _panel.localScale = Vector3.one;
            ResetPanelPose();
            ComposeBody(_typewriter.Length, 0, 0d);
            SetPhase(Phase.Holding);
        }

        /// <summary>
        /// Space·좌클릭이 이번 프레임에 눌렸는가.
        /// 1인칭 조작 중(커서 잠김)에는 화면 어디를 클릭해도 되고, 단말기·해킹 팝업 등 커서가 풀린 화면에서는 대화창을 클릭해야 한다
        /// (다른 곳 클릭은 그 화면의 버튼 조작이다). 해킹 미니게임은 메인 대사가 떠 있는 동안 디렉터가 멈춰 두므로 Space 가 게임으로 새지 않는다.
        /// </summary>
        private bool ConfirmPressedThisFrame()
        {
            UnityEngine.InputSystem.Keyboard keyboard = UnityEngine.InputSystem.Keyboard.current;

            if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
            {
                return true;
            }

            UnityEngine.InputSystem.Mouse mouse = UnityEngine.InputSystem.Mouse.current;

            if (mouse == null || !mouse.leftButton.wasPressedThisFrame)
            {
                return false;
            }

            if (Cursor.lockState == CursorLockMode.Locked)
            {
                return true;
            }

            return RectTransformUtility.RectangleContainsScreenPoint(_panel, mouse.position.ReadValue(), null);
        }

        /// <summary>넘기기를 기다리는 동안 커서가 풀려 있으면 대화창이 클릭을 받게 한다(아래 UI 로 새지 않게).</summary>
        private void UpdateClickBlocking()
        {
            bool clickable = IsHoldingPlayer && Cursor.lockState != CursorLockMode.Locked;

            if (_group.blocksRaycasts != clickable)
            {
                _group.blocksRaycasts = clickable;
            }
        }

        /// <summary>넘기기 키(Space·좌클릭)를 아직 누르고 있는가. 잠금을 풀 때 같은 Space 가 점프로 새지 않도록 확인한다.</summary>
        public static bool IsConfirmHeld()
        {
            UnityEngine.InputSystem.Keyboard keyboard = UnityEngine.InputSystem.Keyboard.current;
            UnityEngine.InputSystem.Mouse mouse = UnityEngine.InputSystem.Mouse.current;
            return (keyboard != null && keyboard.spaceKey.isPressed) || (mouse != null && mouse.leftButton.isPressed);
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
            UpdateClickBlocking();

            // 한 프레임에 한 단계만: 출력 중이면 다 찍기, 다 찍힌 뒤면 다음 대사.
            if (_waitForConfirm && (_phase == Phase.Opening || _phase == Phase.Typing || _phase == Phase.Holding) &&
                ConfirmPressedThisFrame())
            {
                SkipTyping();
                return;
            }

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

            if (_waitForConfirm)
            {
                AnimateNextArrow();
                return; // 넘기기 입력을 기다린다(Update 에서 처리)
            }

            if (_phaseTime < _holdSeconds)
            {
                return;
            }

            FinishLine();
        }

        private void AnimateNextArrow()
        {
            if (!_nextArrow.gameObject.activeSelf)
            {
                _nextArrow.gameObject.SetActive(true);
                _nextArrow.color = _accent.color;
            }

            // 위아래로 까딱이며 은은하게 깜빡인다(게임 대화창의 "다음" 표시).
            float wave = Mathf.Sin(_phaseTime * 6f);
            _nextArrow.rectTransform.anchoredPosition = NextArrowHome + new Vector2(0f, wave * 3f);
            Color color = _nextArrow.color;
            color.a = 0.65f + 0.35f * (0.5f + 0.5f * wave);
            _nextArrow.color = color;
        }

        private void FinishLine()
        {
            if (_doneNotified)
            {
                return;
            }

            _nextArrow.gameObject.SetActive(false);
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

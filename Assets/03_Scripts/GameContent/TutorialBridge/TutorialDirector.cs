using System;
using System.Collections;
using System.Collections.Generic;
using Hacking;
using Pawntom.Tutorial.Core;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Pawntom.TutorialBridge
{
    /// <summary>
    /// 튜토리얼 진행 총괄. Tutorial_Scene(<see cref="SceneNames.DemoTutorialMap"/>)이 로드되면 스스로 생성되어
    /// 눈 뜨기 오프닝 → 1~8단계 대사·키 안내 → 정산 → 교신 종료 엔딩까지 이끈다.
    ///
    /// <para><b>구성</b></para>
    /// <list type="bullet">
    /// <item><see cref="TutorialSequencer"/>(순수 C#): 무엇을 언제 말할지.</item>
    /// <item><see cref="TutorialSignalProbe"/>: 게임 상태 → 신호.</item>
    /// <item>뷰(대화창·키 안내·무전 아이콘·캐스팅 바·정산·눈꺼풀): 코드로 만든 UI.</item>
    /// <item><see cref="TutorialAudio"/>: 합성 효과음.</item>
    /// </list>
    ///
    /// <para>
    /// 씬을 고치지 않고 동작하도록 씬 로드 훅에서 만든다. 씬에 이 컴포넌트를 직접 배치해 두면 그쪽이 우선이다
    /// (폰트·대체 효과음·속도를 인스펙터로 바꾸고 싶을 때).
    /// </para>
    /// <para>
    /// 각 클라이언트가 자기 로컬 플레이어 기준으로 따로 진행한다(대사·안내는 개인 화면 연출이다).
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Pawntom/Tutorial/Tutorial Director")]
    public sealed class TutorialDirector : MonoBehaviour
    {
        private const string EndTitle = "교신 종료";
        private const string EndSubtitle = "TUTORIAL COMPLETE";

        [Header("시작")]
        [Tooltip("눈 뜨기 오프닝을 재생한다. 끄면 바로 1-1 부터 시작한다")]
        [SerializeField] private bool playOpening = true;

        [Tooltip("로컬 플레이어가 스폰되기를 이만큼 기다린다. 넘으면 플레이어 없이 진행한다(씬 단독 미리보기용)")]
        [SerializeField, Min(0f)] private float waitForPlayerSeconds = 12f;

        [Header("대사 속도 (글자/초)")]
        [SerializeField, Min(1f)] private float commandCharactersPerSecond = 22f;
        [SerializeField, Min(1f)] private float catCharactersPerSecond = 27f;

        [Header("표시")]
        [Tooltip("비우면 TMP 기본 폰트(NanumBarunpenR SDF)")]
        [SerializeField] private TMP_FontAsset fontOverride;

        [Header("정산")]
        [Tooltip("7-10 대사가 끝난 뒤 정산 창을 띄우기까지의 시간")]
        [SerializeField, Min(0f)] private float settlementDelay = 1.2f;

        private static bool _bootstrapRegistered;

        private TutorialSequencer _sequencer;
        private TutorialSignalProbe _probe;
        private TutorialAudio _audio;
        private TutorialUIFactory _factory;

        private TutorialDialogueView _dialogue;
        private TutorialHintView _hints;
        private TutorialRadioIndicatorView _radio;
        private TutorialCastBarView _castBar;
        private TutorialSettlementView _settlement;
        private TutorialEyelidView _eyelids;
        private TutorialGuideMarker _guide;
        private TutorialDeathFlow _deathFlow;
        private Func<TutorialSignal, bool> _canResolveGuide;
        private bool _controlEverEnabled;
        private float _nextGuideUpdate;

        private bool _directorLock;
        private bool _dialogueHold;
        private bool _settlementOpening;
        private bool _ending;
        private float _nextLockReassert;

        /// <summary>지금 씬에서 동작 중인 디렉터. 없으면 null.</summary>
        public static TutorialDirector Active { get; private set; }

        /// <summary>
        /// 엔딩 연출(교신 종료)이 끝났다. 8-7 "질의응답 단계로 전환"을 이 이벤트에 연결한다.
        /// </summary>
        public static event Action Completed;

        /// <summary>대사가 화면에 뜰 때. 자막 로그·음성 재생 등 외부 연동용.</summary>
        public static event Action<TutorialLine> LineShown;

        public TutorialSequencer Sequencer
        {
            get { return _sequencer; }
        }

        public Transform LocalPlayerTransform
        {
            get { return _probe != null ? _probe.LocalPlayerTransform : null; }
        }

        public InventoryManager LocalInventory
        {
            get { return _probe != null ? _probe.LocalInventory : null; }
        }

        /// <summary>디렉터 자신이 조작을 막고 있는가(오프닝·정산·엔딩). 분전반·금고 상호작용을 막는 데 쓴다.</summary>
        public bool IsControlLockedByDirector
        {
            get { return _directorLock; }
        }

        // ───────────── 자동 생성 ─────────────

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Active = null;
            Completed = null;
            LineShown = null;
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            _bootstrapRegistered = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterBootstrap()
        {
            if (_bootstrapRegistered)
            {
                return;
            }

            _bootstrapRegistered = true;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != SceneNames.DemoTutorialMap)
            {
                return;
            }

            if (FindAnyObjectByType<TutorialDirector>(FindObjectsInactive.Include) != null)
            {
                return; // 씬에 직접 배치된 디렉터가 우선
            }

            GameObject host = new GameObject(nameof(TutorialDirector));
            SceneManager.MoveGameObjectToScene(host, scene); // 씬을 떠나면 함께 사라지도록
            host.AddComponent<TutorialDirector>();

            Debug.Log("[TutorialDirector] " + scene.name + " 진입 — 튜토리얼을 시작합니다.");
        }

        // ───────────── 수명 ─────────────

        private void Awake()
        {
            if (Active != null && Active != this)
            {
                Destroy(gameObject);
                return;
            }

            Active = this;

            _audio = GetOrAdd<TutorialAudio>();
            _probe = GetOrAdd<TutorialSignalProbe>();

            TutorialScript script = TutorialScriptLibrary.CreateDefault();

            if (FindAnyObjectByType<TutorialSecurityNode>() == null)
            {
                Debug.LogWarning("[TutorialDirector] 씬에 TutorialSecurityNode(분전반)가 없습니다 — 3단계 3-1 에서 진행이 멈춥니다. " +
                                 "분전반을 배치하거나, 미리보기는 F10 으로 강제 진행하세요.");
            }
            BuildViews(script);

            _sequencer = new TutorialSequencer(script);
            _sequencer.LineStarted += HandleLineStarted;
            _sequencer.ActionRequested += HandleActionRequested;
            _sequencer.BeatStarted += HandleBeatStarted;
            _sequencer.BeatSkipped += HandleBeatSkipped;
            _sequencer.HintAdded += _hints.OnHintAdded;
            _sequencer.HintRemoved += _hints.OnHintRemoved;
            _sequencer.HintRecalled += _hints.OnHintRecalled;
            _sequencer.Finished += HandleFinished;

            _probe.SignalRaised += _sequencer.Raise;
            GameManager.OnStateChanged += HandleGameStateChanged;

            _eyelids.SetBlackout(playOpening ? 1f : 0f);
            _eyelids.SetOpenness(playOpening ? 0f : 1f, playOpening ? 1f : 0f);
        }

        private void Start()
        {
            StartCoroutine(RunOpening());
        }

        private void OnDestroy()
        {
            if (Active != this)
            {
                return;
            }

            Active = null;

            if (_probe != null && _sequencer != null)
            {
                _probe.SignalRaised -= _sequencer.Raise;
            }

            GameManager.OnStateChanged -= HandleGameStateChanged;
            HackingManager.PreferredGameType = null;
            TutorialInputLock.ForgetOwner(this);
            TutorialInputLock.ForgetOwner(_dialogue);
            Hacking.HackingGameBase.Paused = false;
            _factory?.DestroyOwnedTextures();
            _factory?.DestroyRuntimeFont();
        }

        private void Update()
        {
            if (_sequencer == null || (_deathFlow != null && _deathFlow.IsShowing))
            {
                return;
            }

            _sequencer.Tick(Time.unscaledDeltaTime);
            UpdateDialogueHold();
            _hints.SetDialogueIdle(_sequencer.IsDialogueIdle && !_ending);
            _probe.SetDialogueBusy(!_sequencer.IsDialogueIdle || _ending);

            // 데모 진입 경로 등이 입력을 다시 켰을 수 있다. 잠겨 있으면 가끔 다시 끈다.
            float now = Time.unscaledTime;

            if (TutorialInputLock.IsLocked && now >= _nextLockReassert)
            {
                _nextLockReassert = now + 0.5f;
                TutorialInputLock.Reassert();
            }

            if (now >= _nextGuideUpdate)
            {
                _nextGuideUpdate = now + 0.2f;
                UpdateGuide();
            }

            HandleDebugKeys();
        }

        // ───────────── 외부 API ─────────────

        /// <summary>씬 배치 컴포넌트(영역·분전반·금고)가 신호를 보낸다.</summary>
        public void Raise(TutorialSignal signal)
        {
            _sequencer?.Raise(signal);
        }

        public void ShowCastProgress(string label, float progress)
        {
            _castBar.Show(label, progress);
        }

        public void HideCastProgress()
        {
            _castBar.Hide();
        }

        // ───────────── 오프닝 ─────────────

        private IEnumerator RunOpening()
        {
            float waited = 0f;

            while (!_probe.HasLocalPlayer && waited < waitForPlayerSeconds)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            if (!_probe.HasLocalPlayer)
            {
                Debug.LogWarning("[TutorialDirector] 로컬 플레이어가 스폰되지 않아 플레이어 없이 진행합니다(위치 기반 신호는 나지 않습니다).");
            }

            LockControl(true);

            if (playOpening)
            {
                yield return PlayEyeOpening();
            }

            _radio.SetMode(TutorialRadioIndicatorView.Mode.Idle);
            _sequencer.Raise(TutorialSignal.EyesOpened);
        }

        /// <summary>
        /// 암전 → 심장 박동 → 눈꺼풀이 살짝 떴다 감김 → 반쯤 → 깜빡 → 완전히 뜸. 흐릿함은 비네트로 걷힌다.
        /// </summary>
        private IEnumerator PlayEyeOpening()
        {
            EyelidTimeline timeline = EyelidTimeline.CreateOpening();
            float end = timeline.Duration + 0.6f;
            float time = 0f;
            bool firstBeat = false;
            bool secondBeat = false;
            bool radioCrackle = false;

            _audio.SetAmbientStatic(0.7f);

            while (time < end)
            {
                time += Time.unscaledDeltaTime;

                if (!firstBeat && time >= 0.15f)
                {
                    firstBeat = true;
                    _audio.PlayHeartbeat(1f);
                }

                if (!secondBeat && time >= 1.15f)
                {
                    secondBeat = true;
                    _audio.PlayHeartbeat(0.65f);
                }

                if (!radioCrackle && time >= 2.4f)
                {
                    radioCrackle = true;
                    _audio.PlayStatic(false);
                }

                // 처음엔 완전 암전, 꺼풀이 움직이기 직전에 암전을 걷어 꺼풀에 넘긴다.
                _eyelids.SetBlackout(1f - Mathf.Clamp01((time - 0.75f) / 0.35f));
                _eyelids.SetOpenness(timeline.Evaluate(time), timeline.Haze(time));
                yield return null;
            }

            _eyelids.SetBlackout(0f);
            _eyelids.SetOpenness(1f, 0f);
            _audio.SetAmbientStatic(0f);
        }

        // ───────────── 시퀀서 이벤트 ─────────────

        private void HandleLineStarted(TutorialLine line)
        {
            // 메인 대사는 Space·클릭으로 넘기고, 떠 있는 동안 이동·시점을 막는다.
            // 상황 대사(추격 경고·재촉 등)는 플레이 도중에 끼어들므로 막지 않고 저절로 넘긴다 — 쫓기는 중에 멈춰 세우면 그대로 잡힌다.
            TutorialBeat beat = _sequencer.CurrentBeat;
            bool waitForConfirm = beat != null && beat.Kind == TutorialBeatKind.Main;
            _dialogue.Show(line, waitForConfirm);
            UpdateDialogueHold();
            _radio.SetMode(line.Speaker == TutorialSpeaker.Command
                ? TutorialRadioIndicatorView.Mode.Receiving
                : TutorialRadioIndicatorView.Mode.Transmitting);
            _hints.SetDialogueIdle(false);
            LineShown?.Invoke(line);
        }

        private void HandleDialogueLineDone()
        {
            if (_deathFlow != null && _deathFlow.IsShowing) return;
            _sequencer.CompleteCurrentLine();

            if (_sequencer.CurrentLine == null && !_ending)
            {
                _radio.SetMode(TutorialRadioIndicatorView.Mode.Idle);
            }
        }

        private void HandleBeatStarted(TutorialBeat beat)
        {
            if (beat.Kind != TutorialBeatKind.Main)
            {
                Debug.Log("[TutorialDirector] 상황 대사 " + beat.Id);
                return;
            }

            _probe.ResetLookReference();
            _deathFlow?.CaptureCheckpoint(beat.Stage);

            // 단계별 안내와 미니게임을 맞춘다: 4단계 격벽 = 방향키 순서 입력, 6단계 이후 금고 = Space 타이밍.
            if (beat.Stage >= 6)
            {
                HackingManager.PreferredGameType = HackingGameType.DigitalSync;
            }
            else if (beat.Stage >= 4)
            {
                HackingManager.PreferredGameType = HackingGameType.CommandOverride;
            }
            else
            {
                HackingManager.PreferredGameType = null;
            }

            Debug.Log("[TutorialDirector] 비트 시작 " + beat.Id + " (" + beat.Stage + "단계)");
        }

        private void HandleBeatSkipped(TutorialBeat beat)
        {
            Debug.Log("[TutorialDirector] 비트 건너뜀 " + beat.Id + " — 플레이어가 앞질렀거나 씬에 해당 지점이 없습니다.");
        }

        private void HandleActionRequested(TutorialLineAction action, TutorialLine line)
        {
            switch (action)
            {
                case TutorialLineAction.EnableControl:
                    _controlEverEnabled = true;
                    LockControl(false);
                    _probe.MarkControlEnabled();
                    break;
                case TutorialLineAction.OpenSettlement:
                    StartCoroutine(OpenSettlement(settlementDelay));
                    break;
                case TutorialLineAction.RevealSettlement:
                    _settlement.Reveal(HandleSettlementRevealed);
                    break;
                case TutorialLineAction.EndTransmission:
                    StartCoroutine(PlayEnding());
                    break;
            }
        }

        private void HandleFinished()
        {
            Debug.Log("[TutorialDirector] 대본 진행 완료");
        }

        private void HandleGameStateChanged(GameState previous, GameState next)
        {
            if (_deathFlow != null && _deathFlow.IsShowing) return;
            // 출발 버튼으로 정식 결과 상태가 되면 그 시점에 정산을 띄운다(7-10 경로보다 먼저 올 수 있다).
            if (next == GameState.Result)
            {
                StartCoroutine(OpenSettlement(0.3f));
            }
        }

        // ───────────── 정산 ─────────────

        private IEnumerator OpenSettlement(float delay)
        {
            if (_settlementOpening || _settlement.IsOpen)
            {
                yield break;
            }

            _settlementOpening = true;
            LockControl(true);

            yield return new WaitForSecondsRealtime(delay);

            _settlement.Open(BuildSettlementEntries());
            _audio.PlaySquelch();

            yield return new WaitForSecondsRealtime(0.6f);

            _sequencer.Raise(TutorialSignal.SettlementShown);
        }

        private void HandleSettlementRevealed()
        {
            _sequencer.Raise(TutorialSignal.SettlementRevealed);
        }

        private List<TutorialSettlementView.Entry> BuildSettlementEntries()
        {
            List<TutorialSettlementView.Entry> entries = new List<TutorialSettlementView.Entry>(InventoryManager.MaxSlotCount);
            InventoryManager inventory = _probe.LocalInventory;

            if (inventory == null || inventory.Object == null || !inventory.Object.IsValid)
            {
                return entries;
            }

            for (int i = 0; i < InventoryManager.MaxSlotCount; i++)
            {
                int itemId = inventory.Slots[i];

                if (itemId == InventoryManager.EmptySlot)
                {
                    continue;
                }

                ItemData data = ItemDatabase.Instance != null ? ItemDatabase.Instance.GetItemData(itemId) : null;

                entries.Add(new TutorialSettlementView.Entry
                {
                    Name = data != null && !string.IsNullOrEmpty(data.itemName) ? data.itemName : "아이템 #" + itemId,
                    Value = data != null ? data.sellPrice : 0
                });
            }

            return entries;
        }

        // ───────────── 엔딩 ─────────────

        /// <summary>
        /// 8-7 이후: 무전 아이콘 비활성화 → 교신 종료 잡음 → 눈이 감기며 암전 → "교신 종료" → <see cref="Completed"/>.
        /// </summary>
        private IEnumerator PlayEnding()
        {
            _ending = true;
            LockControl(true);

            _radio.SetMode(TutorialRadioIndicatorView.Mode.Disabled);
            _audio.PlaySquelch();
            _audio.PlayStatic(true);
            _hints.HideAll();

            yield return new WaitForSecondsRealtime(1.4f);

            _settlement.Close();

            EyelidTimeline closing = EyelidTimeline.CreateClosing();
            float time = 0f;
            bool beat = false;

            while (time < closing.Duration)
            {
                time += Time.unscaledDeltaTime;

                if (!beat && time >= 0.6f)
                {
                    beat = true;
                    _audio.PlayHeartbeat(0.5f);
                }

                float openness = closing.Evaluate(time);
                _eyelids.SetOpenness(openness, 1f - openness);
                yield return null;
            }

            float fade = 0f;

            while (fade < 1f)
            {
                fade += Time.unscaledDeltaTime / 0.6f;
                _eyelids.SetBlackout(fade);
                yield return null;
            }

            _radio.SetMode(TutorialRadioIndicatorView.Mode.Hidden);

            float card = 0f;

            while (card < 1f)
            {
                card += Time.unscaledDeltaTime / 1.2f;
                _eyelids.SetEndCard(card, EndTitle, EndSubtitle);
                yield return null;
            }

            Debug.Log("[TutorialDirector] 튜토리얼 완료 — 질의응답 단계로 전환합니다(Completed 이벤트).");
            Completed?.Invoke();
        }

        // ───────────── 내부 ─────────────

        private void BuildViews(TutorialScript script)
        {
            _factory = new TutorialUIFactory(fontOverride);

            Canvas hud = _factory.CreateCanvas("TutorialHudCanvas", TutorialTheme.HudSortingOrder, transform);
            Canvas overlay = _factory.CreateCanvas("TutorialOverlayCanvas", TutorialTheme.OverlaySortingOrder, transform);

            int maxTextLength = 16;
            Dictionary<string, string> tokens = TutorialKeyLabels.Build();

            // 튜토리얼에 나올 글자를 전용 아틀라스에 미리 굽는다(대사·안내·고정 문구·잡음 기호·숫자).
            System.Text.StringBuilder glyphs = new System.Text.StringBuilder(4096);
            glyphs.Append(TutorialTheme.CommandName).Append(TutorialTheme.CatName)
                .Append("RX 수신 TX 발신 RADIO 수신 중 발신 중 대기 교신 종료 TUTORIAL COMPLETE")
                .Append("정산 가져온 전리품 가방이 비어 있습니다 합계 G 정산 완료 아이템 # 분전반 접속 중 %")
                .Append("0123456789").Append(RadioNoiseText.Glyphs);

            foreach (TutorialLine line in script.AllLines())
            {
                maxTextLength = Mathf.Max(maxTextLength, line.Text.Length);
                glyphs.Append(line.Text);

                if (line.HasHint)
                {
                    glyphs.Append(TutorialHintFormatter.Plain(line.Hint, tokens));
                }
            }

            _factory.Prewarm(glyphs.ToString());

            _castBar = hud.gameObject.AddComponent<TutorialCastBarView>();
            _castBar.Build(_factory, hud.transform);

            _settlement = hud.gameObject.AddComponent<TutorialSettlementView>();
            _settlement.Build(_factory, hud.transform, _audio);

            _hints = hud.gameObject.AddComponent<TutorialHintView>();
            _hints.Build(_factory, hud.transform, tokens, _audio);

            _dialogue = hud.gameObject.AddComponent<TutorialDialogueView>();
            _dialogue.Build(_factory, hud.transform, _audio, maxTextLength, HandleDialogueLineDone);
            _dialogue.SetSpeeds(commandCharactersPerSecond, catCharactersPerSecond);

            _radio = hud.gameObject.AddComponent<TutorialRadioIndicatorView>();
            _radio.Build(_factory, hud.transform);

            _guide = GetOrAdd<TutorialGuideMarker>();
            _guide.Build();
            _canResolveGuide = _probe.CanResolveGuide; // 대리자는 한 번만 만든다(주기 호출마다 할당하지 않도록)

            _eyelids = overlay.gameObject.AddComponent<TutorialEyelidView>();
            _eyelids.Build(_factory, overlay.transform);

            _deathFlow = GetOrAdd<TutorialDeathFlow>();
            _deathFlow.Build(this, _factory);
        }

        public void SuspendForDeath()
        {
            StopAllCoroutines();
            LockControl(true);
            _dialogue.Close();
            _hints.HideAll();
            _castBar.Hide();
            _settlement.Close();
            _guide.Hide();
            _radio.SetMode(TutorialRadioIndicatorView.Mode.Hidden);
            _eyelids.SetBlackout(0f);
            _eyelids.SetOpenness(1f, 0f);
            _probe.enabled = false;
        }

        public void ResumeStage(int stage)
        {
            _ending = _settlementOpening = false;
            _dialogue.Close();
            _dialogueHold = false;
            TutorialInputLock.Release(_dialogue);
            Hacking.HackingGameBase.Paused = false;
            _probe.ResetForRetry();
            _probe.enabled = true;
            _controlEverEnabled = true;
            LockControl(false);
            _radio.SetMode(TutorialRadioIndicatorView.Mode.Idle);
            _sequencer.RestartStage(stage);
        }

        /// <summary>
        /// 다음 목적지에 연초록 안내 상자를 띄운다. 조작이 허용된 뒤부터, 엔딩·정산 전까지만.
        /// </summary>
        private void UpdateGuide()
        {
            Transform player = _probe.LocalPlayerTransform;

            if (!_controlEverEnabled || _ending || _directorLock || player == null)
            {
                _guide.Hide();
                return;
            }

            TutorialSignal target = TutorialGuidePlanner.Choose(_sequencer, _canResolveGuide);
            Bounds bounds;

            if (target != TutorialSignal.None && _probe.TryGetGuideBounds(target, out bounds))
            {
                _guide.Show(bounds, player.position);
            }
            else
            {
                _guide.Hide();
            }
        }

        /// <summary>
        /// 넘기기를 기다리는 메인 대사가 떠 있으면 이동·시점을 막는다. 대사 창이 소유자다(오프닝·정산 잠금과 별개).
        /// 풀 때는 넘기기에 쓴 Space 를 뗀 뒤에 푼다 — 누른 채로 풀면 다음 틱에 "새로 눌림"으로 읽혀 점프가 나간다.
        /// </summary>
        private void UpdateDialogueHold()
        {
            if (_dialogue.IsHoldingPlayer)
            {
                if (!_dialogueHold)
                {
                    _dialogueHold = true;
                    TutorialInputLock.Acquire(_dialogue);
                }
            }
            else if (_dialogueHold && !TutorialDialogueView.IsConfirmHeld())
            {
                _dialogueHold = false;
                TutorialInputLock.Release(_dialogue);
            }

            // 대사를 읽는 동안 해킹 미니게임의 제한 시간이 흐르지 않게 멈춘다. 넘기기에 쓴 Space 를 뗀 뒤에 다시 돌린다
            // (누른 채로 풀면 그 Space 가 타이밍 미니게임 입력으로 읽힌다).
            Hacking.HackingGameBase.Paused = _dialogueHold;
        }

        private void LockControl(bool locked)
        {
            if (_directorLock == locked)
            {
                return;
            }

            _directorLock = locked;

            if (locked)
            {
                TutorialInputLock.Acquire(this);
            }
            else
            {
                TutorialInputLock.Release(this);
            }
        }

        private T GetOrAdd<T>() where T : Component
        {
            T component = GetComponent<T>();
            return component != null ? component : gameObject.AddComponent<T>();
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void HandleDebugKeys()
        {
            Keyboard keyboard = Keyboard.current;

            if (keyboard == null)
            {
                return;
            }

            // F9: 타자 출력/머무름 건너뛰기. F10: 지금 기다리는 비트를 트리거 없이 강제 진행(씬에 지점이 없을 때 미리보기용).
            if (keyboard.f9Key.wasPressedThisFrame)
            {
                _dialogue.SkipTyping();
            }

            if (keyboard.f10Key.wasPressedThisFrame)
            {
                if (_sequencer.CurrentLine != null)
                {
                    _dialogue.SkipTyping();
                    _dialogue.SkipTyping();
                }
                else
                {
                    TutorialBeat pending = _sequencer.PendingMain;
                    Debug.Log("[TutorialDirector] (디버그) 강제 진행: " + (pending != null ? pending.Id : "없음"));
                    _sequencer.ForceAdvance();
                }
            }
        }
    }
}

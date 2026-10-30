using System;
using System.Collections.Generic;

namespace Pawntom.Tutorial.Core
{
    public enum TutorialBeatState
    {
        Pending = 0,
        Playing = 1,
        Done = 2,
        Skipped = 3
    }

    public enum TutorialHintRemoval
    {
        /// <summary>안내한 행동이 확인됐다.</summary>
        Completed = 0,

        /// <summary>표시 시간이 다 됐다.</summary>
        Expired = 1,

        /// <summary>표시 칸이 모자라 새 안내에 밀려났다.</summary>
        Superseded = 2,

        /// <summary>다음 단계로 넘어가 의미가 없어졌다.</summary>
        StageChanged = 3
    }

    /// <summary>
    /// 화면에 걸린(또는 걸릴) 키 안내 문구 하나.
    /// </summary>
    public sealed class TutorialHint
    {
        internal TutorialHint(TutorialLine line, int stage)
        {
            Line = line;
            Stage = stage;
        }

        public TutorialLine Line { get; }

        public int Stage { get; }

        public string Text
        {
            get { return Line.Hint; }
        }

        /// <summary>대화창이 비어 실제로 보인 누적 시간.</summary>
        public float VisibleSeconds { get; internal set; }

        internal bool CompletedEarly { get; set; }
    }

    /// <summary>
    /// 튜토리얼 진행 상태 머신. 씬·UI·시간을 모른다 — 신호(<see cref="Raise"/>)와 경과 시간(<see cref="Tick"/>),
    /// 대사 표시 완료 통지(<see cref="CompleteCurrentLine"/>)만 받아 다음에 무엇을 보여줄지 정한다.
    ///
    /// <para><b>규칙</b></para>
    /// <list type="bullet">
    /// <item>대사는 한 번에 한 줄. 연출 쪽이 타자 출력과 머무름을 끝내고 <see cref="CompleteCurrentLine"/>을 불러야 다음 줄로 간다.</item>
    /// <item>메인 비트는 순서대로 열린다. 커서 비트의 트리거가 들어오면 재생. 커서가 <see cref="TutorialBeat.Skippable"/>이면
    ///       뒤 비트의 트리거가 먼저 와도 그쪽으로 건너뛴다(다음 단계까지만, 닻 비트는 예외).</item>
    /// <item>트리거 인정 구간: 기본은 "직전에 시작된 메인 비트의 바로 앞 메인 비트가 시작된 뒤" 들어온 신호.
    ///       앞 비트 재생 중 함께 일어난 사건(예: 해킹 시작과 성공이 연달아)을 잃지 않기 위해서다.</item>
    /// <item>곁가지 비트는 창이 열려 있을 때 트리거가 오면 대기열 끝에 붙는다.</item>
    /// <item>키 안내 문구는 대사가 <b>끝난 뒤</b> 목록에 들어가고, 대화창이 빌 때만 보인다(<see cref="IsDialogueIdle"/>).
    ///       대사가 시작된 뒤 완료 신호가 오면 즉시 지워진다 — 대사 도중 이미 해냈다면 아예 걸리지 않는다.</item>
    /// </list>
    /// </summary>
    public sealed class TutorialSequencer
    {
        public const int MaxHints = 2;

        private struct QueuedLine
        {
            public TutorialLine Line;
            public TutorialBeat Owner;
            public int SideIndex;
            public bool IsLastOfBeat;
        }

        private readonly TutorialScript _script;
        private readonly TutorialBeatState[] _mainStates;
        private readonly int[] _sidePlayCount;
        private readonly float[] _sideLastPlayTime;
        private readonly bool[] _sideQueued;
        private readonly Dictionary<string, int> _sideIndex = new Dictionary<string, int>();

        // 긴급 대사가 맨 앞에 끼어들 수 있어야 해서 Queue 대신 List(용량 미리 확보, 앞에서 꺼냄)를 쓴다.
        private readonly List<QueuedLine> _queue = new List<QueuedLine>(16);

        // 상황 대사 뒤 다시 띄울 "아직 해내지 않은 행동" 안내. 시간제 안내는 대상이 아니다.
        private TutorialLine _recallLine;
        private int _recallStage;
        private readonly List<TutorialHint> _hints = new List<TutorialHint>(MaxHints + 1);

        private readonly bool[] _incoming;
        private readonly long[] _stamp;
        private long _stampCounter;
        private long _validFrom;
        private long _lastMainStart;

        private QueuedLine _current;
        private bool _hasCurrent;
        private TutorialHint _armedHint;

        private bool _delayArmed;
        private float _delayRemaining;

        private int _cursor;
        private float _time;
        private bool _finished;

        public TutorialSequencer(TutorialScript script)
        {
            _script = script ?? throw new ArgumentNullException(nameof(script));

            _mainStates = new TutorialBeatState[script.Mains.Count];

            int sideCount = script.Sides.Count;
            _sidePlayCount = new int[sideCount];
            _sideLastPlayTime = new float[sideCount];
            _sideQueued = new bool[sideCount];

            for (int i = 0; i < sideCount; i++)
            {
                _sideIndex.Add(script.Sides[i].Id, i);
            }

            int signalCount = 0;

            foreach (TutorialSignal value in Enum.GetValues(typeof(TutorialSignal)))
            {
                signalCount = Math.Max(signalCount, (int)value + 1);
            }

            _incoming = new bool[signalCount];
            _stamp = new long[signalCount];
        }

        /// <summary>새 대사를 띄워야 할 때.</summary>
        public event Action<TutorialLine> LineStarted;

        /// <summary>대사가 끝났을 때(<see cref="CompleteCurrentLine"/> 직후).</summary>
        public event Action<TutorialLine> LineFinished;

        /// <summary>대사의 후속 지시(조작 허용·정산·교신 종료).</summary>
        public event Action<TutorialLineAction, TutorialLine> ActionRequested;

        public event Action<TutorialBeat> BeatStarted;

        public event Action<TutorialBeat> BeatSkipped;

        public event Action<TutorialHint> HintAdded;

        public event Action<TutorialHint, TutorialHintRemoval> HintRemoved;

        /// <summary>이미 걸려 있던 안내를 상황 대사 뒤에 다시 강조할 때(새로 추가되면 <see cref="HintAdded"/>가 온다).</summary>
        public event Action<TutorialHint> HintRecalled;

        /// <summary>모든 메인 비트가 지나가고 대기열이 비었다.</summary>
        public event Action Finished;

        public TutorialScript Script
        {
            get { return _script; }
        }

        /// <summary>지금 표시 중인 대사. 없으면 null.</summary>
        public TutorialLine CurrentLine
        {
            get { return _hasCurrent ? _current.Line : null; }
        }

        /// <summary>지금 표시 중인 대사가 속한 비트. 없으면 null. 메인 대사인지(클릭으로 넘기는지) 가르는 데 쓴다.</summary>
        public TutorialBeat CurrentBeat
        {
            get { return _hasCurrent ? _current.Owner : null; }
        }

        /// <summary>표시 중인 대사도, 기다리는 대사도 없다. 키 안내 문구는 이때만 보인다.</summary>
        public bool IsDialogueIdle
        {
            get { return !_hasCurrent && _queue.Count == 0; }
        }

        /// <summary>화면에 걸린 키 안내 문구. 오래된 것이 앞.</summary>
        public IReadOnlyList<TutorialHint> Hints
        {
            get { return _hints; }
        }

        public bool IsFinished
        {
            get { return _finished; }
        }

        /// <summary>다음에 열릴 메인 비트의 순서 번호. 다 지나갔으면 -1.</summary>
        public int PendingMainIndex
        {
            get
            {
                for (int i = _cursor; i < _mainStates.Length; i++)
                {
                    if (_mainStates[i] == TutorialBeatState.Pending)
                    {
                        return i;
                    }
                }

                return -1;
            }
        }

        /// <summary>다음에 열릴 메인 비트. 디버그 표시용. 다 지나갔으면 null.</summary>
        public TutorialBeat PendingMain
        {
            get
            {
                for (int i = _cursor; i < _mainStates.Length; i++)
                {
                    if (_mainStates[i] == TutorialBeatState.Pending)
                    {
                        return _script.Mains[i];
                    }
                }

                return null;
            }
        }

        public TutorialBeatState GetMainState(string beatId)
        {
            int index = _script.IndexOfMain(beatId);

            if (index < 0)
            {
                throw new ArgumentException("메인 비트가 아니다: " + beatId, nameof(beatId));
            }

            return _mainStates[index];
        }

        public int GetSidePlayCount(string beatId)
        {
            int index;

            if (!_sideIndex.TryGetValue(beatId, out index))
            {
                throw new ArgumentException("곁가지 비트가 아니다: " + beatId, nameof(beatId));
            }

            return _sidePlayCount[index];
        }

        /// <summary>
        /// 신호를 받는다. 처리는 다음 <see cref="Tick"/>에서 한다(같은 프레임에 여러 번 와도 한 번으로 친다).
        /// </summary>
        public void Raise(TutorialSignal signal)
        {
            int index = (int)signal;

            if (signal == TutorialSignal.None || index < 0 || index >= _incoming.Length)
            {
                return;
            }

            _incoming[index] = true;
        }

        public void Tick(float deltaTime)
        {
            if (deltaTime < 0f)
            {
                deltaTime = 0f;
            }

            _time += deltaTime;

            ProcessIncoming();
            UpdateHintTimers(deltaTime);

            if (_delayArmed)
            {
                _delayRemaining -= deltaTime;
            }

            Advance();
        }

        /// <summary>
        /// 연출 쪽이 현재 대사의 출력과 머무름을 끝냈다. 다음 대사가 바로 이어지면 이 호출 안에서 <see cref="LineStarted"/>가 온다.
        /// </summary>
        public void CompleteCurrentLine()
        {
            if (!_hasCurrent)
            {
                return;
            }

            QueuedLine done = _current;
            _hasCurrent = false;

            if (_armedHint != null)
            {
                if (!_armedHint.CompletedEarly)
                {
                    AddHint(_armedHint);
                }

                _armedHint = null;
            }

            if (done.IsLastOfBeat)
            {
                if (done.Owner.Kind == TutorialBeatKind.Main)
                {
                    int index = _script.IndexOfMain(done.Owner.Id);

                    if (_mainStates[index] == TutorialBeatState.Playing)
                    {
                        _mainStates[index] = TutorialBeatState.Done;
                    }
                }
                else
                {
                    _sideQueued[done.SideIndex] = false;
                }

                if (done.Owner.RecallsHint)
                {
                    RecallHint();
                }
            }

            LineFinished?.Invoke(done.Line);

            if (done.Line.Action != TutorialLineAction.None)
            {
                ActionRequested?.Invoke(done.Line.Action, done.Line);
            }

            Advance();
        }

        /// <summary>
        /// 디버그용 강제 진행. 대사가 떠 있으면 끝내고, 대기 중이면 대기를 끊고, 아무것도 없으면 다음 메인 비트를 트리거 없이 시작한다.
        /// </summary>
        public void ForceAdvance()
        {
            if (_hasCurrent)
            {
                CompleteCurrentLine();
                return;
            }

            if (_delayArmed)
            {
                _delayRemaining = 0f;
                Advance();
                return;
            }

            if (_queue.Count > 0)
            {
                Advance();
                return;
            }

            NormalizeCursor();

            if (_cursor < _mainStates.Length)
            {
                StartMain(_cursor);
                Advance();
            }
        }

        private void ProcessIncoming()
        {
            for (int i = 1; i < _incoming.Length; i++)
            {
                if (!_incoming[i])
                {
                    continue;
                }

                _incoming[i] = false;
                _stamp[i] = ++_stampCounter;

                TutorialSignal signal = (TutorialSignal)i;
                CompleteHints(signal);
                TriggerSides(signal);
            }
        }

        private void CompleteHints(TutorialSignal signal)
        {
            if (_armedHint != null && Contains(_armedHint.Line.HintCompleteOn, signal))
            {
                _armedHint.CompletedEarly = true;
            }

            // 목록에서 밀려난 뒤에 해낸 행동도 "해냈다"로 친다 — 다시 띄우면 안 된다.
            if (_recallLine != null && Contains(_recallLine.HintCompleteOn, signal))
            {
                _recallLine = null;
            }

            for (int i = _hints.Count - 1; i >= 0; i--)
            {
                if (Contains(_hints[i].Line.HintCompleteOn, signal))
                {
                    RemoveHintAt(i, TutorialHintRemoval.Completed);
                }
            }
        }

        private void TriggerSides(TutorialSignal signal)
        {
            IReadOnlyList<TutorialBeat> sides = _script.Sides;

            for (int i = 0; i < sides.Count; i++)
            {
                TutorialBeat side = sides[i];

                if (!side.HasTrigger(signal) || _sideQueued[i] || !IsSideWindowOpen(side))
                {
                    continue;
                }

                if (_sidePlayCount[i] > 0)
                {
                    if (!side.Repeatable || _time - _sideLastPlayTime[i] < side.RepeatCooldownSeconds)
                    {
                        continue;
                    }
                }

                _sidePlayCount[i]++;
                _sideLastPlayTime[i] = _time;
                _sideQueued[i] = true;

                if (side.Rotating || side.Urgent)
                {
                    TutorialLine[] pool = side.Lines;
                    TutorialLine line = side.Rotating ? pool[(_sidePlayCount[i] - 1) % pool.Length] : pool[0];

                    if (side.Rotating || pool.Length == 1)
                    {
                        EnqueueSingle(side, line, i, side.Urgent);
                    }
                    else
                    {
                        // 긴급 + 여러 줄: 순서를 지켜 맨 앞에 끼운다.
                        for (int k = pool.Length - 1; k >= 0; k--)
                        {
                            EnqueueSingle(side, pool[k], i, true);
                        }

                        MarkOnlyLastOfBeat(side, pool.Length);
                    }
                }
                else
                {
                    Enqueue(side, side.Lines, i);
                }

                BeatStarted?.Invoke(side);

                if (side.Urgent && _hasCurrent && _current.Owner != side)
                {
                    // 떠 있는 대사를 끊고 바로 긴급 대사로. 끊긴 대사의 지시(조작 허용 등)는 그대로 실행된다.
                    CompleteCurrentLine();
                }
            }
        }

        private void MarkOnlyLastOfBeat(TutorialBeat owner, int count)
        {
            for (int k = 0; k < count && k < _queue.Count; k++)
            {
                QueuedLine entry = _queue[k];

                if (entry.Owner == owner)
                {
                    entry.IsLastOfBeat = k == count - 1;
                    _queue[k] = entry;
                }
            }
        }

        private bool IsSideWindowOpen(TutorialBeat side)
        {
            if (side.OpensAfter != null)
            {
                TutorialBeatState opens = _mainStates[_script.IndexOfMain(side.OpensAfter)];

                if (opens != TutorialBeatState.Playing && opens != TutorialBeatState.Done)
                {
                    return false;
                }
            }

            if (side.ClosesAt != null)
            {
                return _mainStates[_script.IndexOfMain(side.ClosesAt)] == TutorialBeatState.Pending;
            }

            return true;
        }

        private void UpdateHintTimers(float deltaTime)
        {
            if (!IsDialogueIdle)
            {
                return;
            }

            for (int i = _hints.Count - 1; i >= 0; i--)
            {
                TutorialHint hint = _hints[i];
                hint.VisibleSeconds += deltaTime;

                float limit = hint.Line.HintAutoHideSeconds;

                if (limit > 0f && hint.VisibleSeconds >= limit)
                {
                    RemoveHintAt(i, TutorialHintRemoval.Expired);
                }
            }
        }

        private void Advance()
        {
            while (!_hasCurrent)
            {
                if (_queue.Count > 0)
                {
                    QueuedLine next = _queue[0];

                    if (_delayArmed)
                    {
                        if (_delayRemaining > 0f)
                        {
                            return;
                        }

                        _delayArmed = false;
                    }
                    else if (next.Line.PreDelaySeconds > 0f)
                    {
                        _delayArmed = true;
                        _delayRemaining = next.Line.PreDelaySeconds;
                        return;
                    }

                    _queue.RemoveAt(0);
                    StartLine(next);
                    return;
                }

                if (!TryStartMain())
                {
                    CheckFinished();
                    return;
                }
            }
        }

        private void StartLine(QueuedLine entry)
        {
            _current = entry;
            _hasCurrent = true;
            _armedHint = entry.Line.HasHint ? new TutorialHint(entry.Line, entry.Owner.Stage) : null;
            LineStarted?.Invoke(entry.Line);
        }

        private void NormalizeCursor()
        {
            IReadOnlyList<TutorialBeat> mains = _script.Mains;

            while (_cursor < mains.Count)
            {
                if (_mainStates[_cursor] != TutorialBeatState.Pending)
                {
                    _cursor++;
                    continue;
                }

                TutorialBeat beat = mains[_cursor];

                // 요구한 비트가 이미 지나갔는데 재생되지 않았다면(건너뜀) 이 비트도 말이 안 된다.
                if (beat.RequiresPlayed != null && !WasPlayed(beat.RequiresPlayed))
                {
                    Skip(_cursor);
                    _cursor++;
                    continue;
                }

                return;
            }
        }

        private bool TryStartMain()
        {
            NormalizeCursor();

            IReadOnlyList<TutorialBeat> mains = _script.Mains;

            if (_cursor >= mains.Count)
            {
                return false;
            }

            int startStage = mains[_cursor].Stage;

            for (int j = _cursor; j < mains.Count; j++)
            {
                TutorialBeat beat = mains[j];

                bool reachable = j == _cursor || beat.Stage <= startStage + 1 || beat.IsAnchor;

                if (_mainStates[j] == TutorialBeatState.Pending &&
                    reachable &&
                    (beat.RequiresPlayed == null || WasPlayed(beat.RequiresPlayed)) &&
                    IsTriggered(beat))
                {
                    StartMain(j);
                    return true;
                }

                if (!beat.Skippable)
                {
                    return false;
                }
            }

            return false;
        }

        private bool IsTriggered(TutorialBeat beat)
        {
            TutorialSignal[] triggers = beat.Triggers;

            if (triggers.Length == 0)
            {
                return true;
            }

            long threshold = beat.FreshTriggerOnly ? _lastMainStart : _validFrom;

            for (int i = 0; i < triggers.Length; i++)
            {
                if (_stamp[(int)triggers[i]] > threshold)
                {
                    return true;
                }
            }

            return false;
        }

        private void StartMain(int index)
        {
            for (int k = _cursor; k < index; k++)
            {
                if (_mainStates[k] == TutorialBeatState.Pending)
                {
                    Skip(k);
                }
            }

            TutorialBeat beat = _script.Mains[index];
            _mainStates[index] = TutorialBeatState.Playing;
            _cursor = index + 1;

            _validFrom = _lastMainStart;
            _lastMainStart = _stampCounter;

            for (int i = _hints.Count - 1; i >= 0; i--)
            {
                if (_hints[i].Stage < beat.Stage)
                {
                    RemoveHintAt(i, TutorialHintRemoval.StageChanged);
                }
            }

            if (_recallLine != null && _recallStage < beat.Stage)
            {
                _recallLine = null;
            }

            TutorialLine[] lines = beat.Lines;

            if (beat.VariantWhenSidePlayed != null && _sidePlayCount[_sideIndex[beat.VariantWhenSidePlayed]] > 0)
            {
                lines = beat.VariantLines;
            }

            Enqueue(beat, lines, -1);
            BeatStarted?.Invoke(beat);
        }

        private void Skip(int index)
        {
            _mainStates[index] = TutorialBeatState.Skipped;
            BeatSkipped?.Invoke(_script.Mains[index]);
        }

        private void Enqueue(TutorialBeat owner, TutorialLine[] lines, int sideIndex)
        {
            for (int i = 0; i < lines.Length; i++)
            {
                _queue.Add(new QueuedLine
                {
                    Line = lines[i],
                    Owner = owner,
                    SideIndex = sideIndex,
                    IsLastOfBeat = i == lines.Length - 1
                });
            }
        }

        private void EnqueueSingle(TutorialBeat owner, TutorialLine line, int sideIndex, bool atFront)
        {
            QueuedLine entry = new QueuedLine
            {
                Line = line,
                Owner = owner,
                SideIndex = sideIndex,
                IsLastOfBeat = true
            };

            if (!atFront)
            {
                _queue.Add(entry);
                return;
            }

            _queue.Insert(0, entry);

            // 맨 앞 대사가 바뀌었으니 걸어 둔 대기 시간은 그 대사의 것이 아니다. 원래 대사 차례에 다시 건다.
            _delayArmed = false;
        }

        /// <summary>
        /// 그 구간에서 아직 해내지 않은 행동 안내를 다시 띄운다. 이미 걸려 있으면 처음 보인 것처럼 시간을 되돌린다.
        /// </summary>
        private void RecallHint()
        {
            if (_recallLine == null)
            {
                return;
            }

            for (int i = 0; i < _hints.Count; i++)
            {
                if (_hints[i].Line == _recallLine)
                {
                    _hints[i].VisibleSeconds = 0f;
                    HintRecalled?.Invoke(_hints[i]);
                    return;
                }
            }

            AddHint(new TutorialHint(_recallLine, _recallStage));
        }

        private bool WasPlayed(string mainBeatId)
        {
            TutorialBeatState state = _mainStates[_script.IndexOfMain(mainBeatId)];
            return state == TutorialBeatState.Playing || state == TutorialBeatState.Done;
        }

        private void AddHint(TutorialHint hint)
        {
            while (_hints.Count >= MaxHints)
            {
                RemoveHintAt(0, TutorialHintRemoval.Superseded);
            }

            _hints.Add(hint);

            // 행동으로 지워지는 안내만 다시 띄울 대상이다(시간제 안내는 확인할 행동이 없다).
            if (hint.Line.HintCompleteOn.Length > 0 && hint.Line.HintAutoHideSeconds <= 0f)
            {
                _recallLine = hint.Line;
                _recallStage = hint.Stage;
            }

            HintAdded?.Invoke(hint);
        }

        private void RemoveHintAt(int index, TutorialHintRemoval reason)
        {
            TutorialHint hint = _hints[index];
            _hints.RemoveAt(index);
            HintRemoved?.Invoke(hint, reason);
        }

        private void CheckFinished()
        {
            if (_finished || _hasCurrent || _queue.Count > 0 || _cursor < _mainStates.Length)
            {
                return;
            }

            _finished = true;
            Finished?.Invoke();
        }

        private static bool Contains(TutorialSignal[] signals, TutorialSignal signal)
        {
            for (int i = 0; i < signals.Length; i++)
            {
                if (signals[i] == signal)
                {
                    return true;
                }
            }

            return false;
        }
    }
}

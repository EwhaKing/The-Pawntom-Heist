using System;

namespace Pawntom.Tutorial.Core
{
    /// <summary>비트 종류.</summary>
    public enum TutorialBeatKind
    {
        /// <summary>
        /// 본 흐름. 대본 순서대로 하나씩 열리고, 트리거가 오면 재생된다.
        /// </summary>
        Main = 0,

        /// <summary>
        /// 곁가지. 정해진 구간(창) 안에서 트리거가 오면 끼어든다. 스태미나 경고·해킹 실패·추격 같은 상황 대사.
        /// </summary>
        Side = 1
    }

    /// <summary>
    /// 트리거 하나로 시작해 연달아 재생되는 대사 묶음.
    /// <para>
    /// 대본의 "직후" 체인은 한 비트로 묶는다. 예: 2-3 "뒤를 돌아본 후" → 2-4 → 2-5 → 2-6 은 트리거 하나(<see cref="TutorialSignal.LookedBack"/>)에
    /// 대사 네 줄이다.
    /// </para>
    /// </summary>
    public sealed class TutorialBeat
    {
        private static readonly TutorialSignal[] NoSignals = new TutorialSignal[0];

        private TutorialBeat(
            string id,
            int stage,
            TutorialBeatKind kind,
            TutorialSignal[] triggers,
            TutorialLine[] lines)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentException("비트 번호가 비었다", nameof(id));
            }

            if (lines == null || lines.Length == 0)
            {
                throw new ArgumentException("비트에는 대사가 하나 이상 있어야 한다: " + id, nameof(lines));
            }

            Id = id;
            Stage = stage;
            Kind = kind;
            Triggers = triggers ?? NoSignals;
            Lines = lines;
        }

        /// <summary>본 흐름 비트. 트리거가 비어 있으면 앞 비트가 끝나는 즉시 재생된다.</summary>
        public static TutorialBeat Main(string id, int stage, TutorialSignal trigger, params TutorialLine[] lines)
        {
            TutorialSignal[] triggers = trigger == TutorialSignal.None ? NoSignals : new[] { trigger };
            return new TutorialBeat(id, stage, TutorialBeatKind.Main, triggers, lines);
        }

        /// <summary>
        /// 곁가지 비트. <paramref name="opensAfter"/> 메인 비트가 지나간 뒤부터 <paramref name="closesAt"/> 메인 비트가 지나가기 전까지만 반응한다.
        /// </summary>
        public static TutorialBeat Side(
            string id,
            int stage,
            TutorialSignal trigger,
            string opensAfter,
            string closesAt,
            params TutorialLine[] lines)
        {
            if (trigger == TutorialSignal.None)
            {
                throw new ArgumentException("곁가지 비트에는 트리거가 있어야 한다: " + id, nameof(trigger));
            }

            TutorialBeat beat = new TutorialBeat(id, stage, TutorialBeatKind.Side, new[] { trigger }, lines);
            beat.OpensAfter = opensAfter;
            beat.ClosesAt = closesAt;
            return beat;
        }

        /// <summary>첫 대사 번호. 예: "2-3".</summary>
        public string Id { get; }

        /// <summary>대본 단계(1~8).</summary>
        public int Stage { get; }

        public TutorialBeatKind Kind { get; }

        /// <summary>이 중 하나가 오면 재생. 메인에서 비어 있으면 즉시.</summary>
        public TutorialSignal[] Triggers { get; }

        public TutorialLine[] Lines { get; }

        /// <summary>
        /// 메인 전용. 이 비트를 기다리는 동안 뒤 비트의 트리거가 먼저 오면 건너뛴다.
        /// 씬에 해당 지점이 아직 없거나 플레이어가 순서를 앞질렀을 때 진행이 멈추지 않게 한다.
        /// </summary>
        public bool Skippable { get; private set; }

        /// <summary>
        /// 메인 전용. 이 메인 비트가 실제로 <b>재생</b>돼야만 열린다. 건너뛰어졌으면 이 비트도 건너뛴다.
        /// 예: 3-6 "본부 안까지는 들어오지 못합니다"는 3-4 추격이 있었을 때만 말이 된다.
        /// </summary>
        public string RequiresPlayed { get; private set; }

        /// <summary>
        /// 메인 전용. 건너뛰기 탐색은 보통 다음 단계까지만 내다보지만, 닻 비트는 단계와 무관하게 건너뛰어 도달할 수 있다.
        /// 캣닢 획득처럼 "일어났으면 이야기를 거기로 옮겨야 하는" 사건에 붙인다.
        /// </summary>
        public bool IsAnchor { get; private set; }

        /// <summary>
        /// 메인 전용. 바로 앞 메인 비트가 <b>시작된 뒤</b> 들어온 트리거만 인정한다.
        /// 기본은 한 비트 더 앞에서부터 인정한다(앞 비트 재생 중 함께 일어난 사건을 잃지 않도록).
        /// 예: 2-3 "뒤를 돌아본 후"는 2-2 지시를 들은 뒤에 돌아봐야 한다.
        /// </summary>
        public bool FreshTriggerOnly { get; private set; }

        /// <summary>곁가지 전용. 이 메인 비트가 재생되기 시작한 뒤부터 반응. null 이면 처음부터.</summary>
        public string OpensAfter { get; private set; }

        /// <summary>곁가지 전용. 이 메인 비트가 지나가면 닫힘. null 이면 끝까지.</summary>
        public string ClosesAt { get; private set; }

        /// <summary>곁가지 전용. 여러 번 재생 가능.</summary>
        public bool Repeatable { get; private set; }

        /// <summary>곁가지 반복 재생 사이 최소 간격(초).</summary>
        public float RepeatCooldownSeconds { get; private set; }

        /// <summary>
        /// 곁가지 전용. <see cref="Lines"/>를 연달아 재생하지 않고, 발동할 때마다 <b>한 줄씩 번갈아</b> 재생한다(후보 풀).
        /// 같은 대사가 연달아 나오지 않게 하기 위함이다.
        /// </summary>
        public bool Rotating { get; private set; }

        /// <summary>
        /// 곁가지 전용. 대기열 맨 앞에 끼어들고, 지금 떠 있는 대사를 끊는다(사망처럼 미룰 수 없는 상황).
        /// </summary>
        public bool Urgent { get; private set; }

        /// <summary>
        /// 이 비트가 끝나면 그 구간의 키 안내 문구(아직 해내지 않은 행동 안내)를 다시 띄운다.
        /// </summary>
        public bool RecallsHint { get; private set; }

        /// <summary>메인 전용. 이 곁가지가 한 번이라도 재생됐으면 <see cref="VariantLines"/>로 대신 재생한다.</summary>
        public string VariantWhenSidePlayed { get; private set; }

        public TutorialLine[] VariantLines { get; private set; }

        public TutorialBeat AsSkippable()
        {
            EnsureKind(TutorialBeatKind.Main, nameof(AsSkippable));
            Skippable = true;
            return this;
        }

        public TutorialBeat RequiringPlayed(string mainBeatId)
        {
            EnsureKind(TutorialBeatKind.Main, nameof(RequiringPlayed));
            RequiresPlayed = mainBeatId;
            return this;
        }

        public TutorialBeat AsAnchor()
        {
            EnsureKind(TutorialBeatKind.Main, nameof(AsAnchor));
            IsAnchor = true;
            return this;
        }

        public TutorialBeat WithFreshTrigger()
        {
            EnsureKind(TutorialBeatKind.Main, nameof(WithFreshTrigger));
            FreshTriggerOnly = true;
            return this;
        }

        public TutorialBeat AsRepeatable(float cooldownSeconds)
        {
            EnsureKind(TutorialBeatKind.Side, nameof(AsRepeatable));
            Repeatable = true;
            RepeatCooldownSeconds = cooldownSeconds < 0f ? 0f : cooldownSeconds;
            return this;
        }

        public TutorialBeat AsRotating()
        {
            EnsureKind(TutorialBeatKind.Side, nameof(AsRotating));
            Rotating = true;
            return this;
        }

        public TutorialBeat AsUrgent()
        {
            EnsureKind(TutorialBeatKind.Side, nameof(AsUrgent));
            Urgent = true;
            return this;
        }

        public TutorialBeat WithHintRecall()
        {
            RecallsHint = true;
            return this;
        }

        public TutorialBeat WithVariant(string sideBeatId, params TutorialLine[] variantLines)
        {
            EnsureKind(TutorialBeatKind.Main, nameof(WithVariant));

            if (variantLines == null || variantLines.Length == 0)
            {
                throw new ArgumentException("대체 대사가 비었다: " + Id, nameof(variantLines));
            }

            VariantWhenSidePlayed = sideBeatId;
            VariantLines = variantLines;
            return this;
        }

        public bool HasTrigger(TutorialSignal signal)
        {
            for (int i = 0; i < Triggers.Length; i++)
            {
                if (Triggers[i] == signal)
                {
                    return true;
                }
            }

            return false;
        }

        public override string ToString()
        {
            return Id;
        }

        private void EnsureKind(TutorialBeatKind expected, string member)
        {
            if (Kind != expected)
            {
                throw new InvalidOperationException(member + " 은 " + expected + " 비트 전용이다: " + Id);
            }
        }
    }
}

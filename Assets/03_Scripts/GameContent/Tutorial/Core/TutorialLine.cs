using System;

namespace Pawntom.Tutorial.Core
{
    /// <summary>대사 화자. 표시 이름·색·출력음이 화자별로 갈린다.</summary>
    public enum TutorialSpeaker
    {
        /// <summary>지휘부. 무전으로 수신되므로 노이즈 연출이 붙는다.</summary>
        Command = 0,

        /// <summary>깜냥이(플레이어).</summary>
        Kkamnyang = 1
    }

    /// <summary>
    /// 대사가 끝난 직후 연출 쪽이 수행할 일. 대본의 "키 안내 문구" 열 중 문구가 아닌 지시를 옮긴 것이다.
    /// </summary>
    public enum TutorialLineAction
    {
        None = 0,

        /// <summary>1-7 "조작 가능 상태로 전환".</summary>
        EnableControl,

        /// <summary>7-10 이후 정산 화면을 띄운다(정식 정산 화면이 없어 튜토리얼이 대신 띄운다).</summary>
        OpenSettlement,

        /// <summary>8-1 "정산 결과창 표시" — 집계 연출을 시작한다.</summary>
        RevealSettlement,

        /// <summary>8-7 "무전 아이콘 비활성화, 엔딩 연출 후 질의응답 단계로 전환".</summary>
        EndTransmission
    }

    /// <summary>
    /// 대사 한 줄. 불변이다.
    /// </summary>
    public sealed class TutorialLine
    {
        private static readonly TutorialSignal[] NoSignals = new TutorialSignal[0];

        public TutorialLine(string id, TutorialSpeaker speaker, string text)
            : this(id, speaker, text, null, NoSignals, 0f, 0f, TutorialLineAction.None)
        {
        }

        private TutorialLine(
            string id,
            TutorialSpeaker speaker,
            string text,
            string hint,
            TutorialSignal[] hintCompleteOn,
            float hintAutoHideSeconds,
            float preDelaySeconds,
            TutorialLineAction action)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentException("대사 번호가 비었다", nameof(id));
            }

            Id = id;
            Speaker = speaker;
            Text = text ?? string.Empty;
            Hint = hint;
            HintCompleteOn = hintCompleteOn ?? NoSignals;
            HintAutoHideSeconds = hintAutoHideSeconds;
            PreDelaySeconds = preDelaySeconds;
            Action = action;
        }

        /// <summary>대본 번호. 예: "2-7".</summary>
        public string Id { get; }

        public TutorialSpeaker Speaker { get; }

        public string Text { get; }

        /// <summary>
        /// 키 안내 문구. 없으면 null. <c>[키]</c> 표기는 강조 대상, <c>{Interact}</c> 같은 중괄호는 실제 바인딩으로 치환된다.
        /// </summary>
        public string Hint { get; }

        /// <summary>이 중 하나라도 대사 시작 이후 들어오면 안내 문구를 즉시 지운다.</summary>
        public TutorialSignal[] HintCompleteOn { get; }

        /// <summary>
        /// 0보다 크면 표시된 뒤 이 시간이 지나면 지운다. "스태미나 바를 확인합시다"처럼 확인할 행동이 없는 안내용.
        /// </summary>
        public float HintAutoHideSeconds { get; }

        /// <summary>앞 대사가 끝난 뒤 이 줄을 띄우기 전 대기 시간. 6-3 "6-2 이후 3초 경과".</summary>
        public float PreDelaySeconds { get; }

        public TutorialLineAction Action { get; }

        public bool HasHint
        {
            get { return !string.IsNullOrEmpty(Hint); }
        }

        /// <summary>행동으로 지워지는 안내 문구를 붙인 사본.</summary>
        public TutorialLine WithHint(string hint, params TutorialSignal[] completeOn)
        {
            return new TutorialLine(Id, Speaker, Text, hint, completeOn, HintAutoHideSeconds, PreDelaySeconds, Action);
        }

        /// <summary>시간이 지나면 지워지는 안내 문구를 붙인 사본. completeOn 이 오면 더 일찍 지워진다.</summary>
        public TutorialLine WithTimedHint(string hint, float seconds, params TutorialSignal[] completeOn)
        {
            return new TutorialLine(Id, Speaker, Text, hint, completeOn, seconds, PreDelaySeconds, Action);
        }

        public TutorialLine WithPreDelay(float seconds)
        {
            return new TutorialLine(Id, Speaker, Text, Hint, HintCompleteOn, HintAutoHideSeconds, seconds, Action);
        }

        public TutorialLine WithAction(TutorialLineAction action)
        {
            return new TutorialLine(Id, Speaker, Text, Hint, HintCompleteOn, HintAutoHideSeconds, PreDelaySeconds, action);
        }

        public override string ToString()
        {
            return Id;
        }
    }
}

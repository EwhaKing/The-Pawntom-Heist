using System;

namespace Pawntom.Tutorial.Core
{
    /// <summary>
    /// 타자기 출력 타이밍. 글자당 기본 간격에 문장부호 뒤 쉼을 더한다.
    /// 문자열을 만들지 않는다 — 보이는 글자 수만 센다(UI는 TMP maxVisibleCharacters 로 반영).
    /// </summary>
    public sealed class TypewriterModel
    {
        private string _text = string.Empty;
        private int _visible;
        private float _budget;

        public TypewriterModel(float charactersPerSecond)
        {
            CharactersPerSecond = charactersPerSecond;
            SentencePauseSeconds = 0.32f;
            CommaPauseSeconds = 0.14f;
            EllipsisDotPauseSeconds = 0.17f;
        }

        public float CharactersPerSecond
        {
            get { return _charactersPerSecond; }
            set { _charactersPerSecond = value < 1f ? 1f : value; }
        }

        private float _charactersPerSecond;

        /// <summary>마침표·물음표·느낌표 뒤 쉼. 문장 끝(마지막 글자)에는 쉬지 않는다.</summary>
        public float SentencePauseSeconds { get; set; }

        public float CommaPauseSeconds { get; set; }

        /// <summary>"..." 안의 점 사이 쉼. 말줄임이 또박또박 찍히도록.</summary>
        public float EllipsisDotPauseSeconds { get; set; }

        public string Text
        {
            get { return _text; }
        }

        public int Visible
        {
            get { return _visible; }
        }

        public int Length
        {
            get { return _text.Length; }
        }

        public bool IsComplete
        {
            get { return _visible >= _text.Length; }
        }

        public void Begin(string text)
        {
            _text = text ?? string.Empty;
            _visible = 0;
            _budget = 0f;
        }

        /// <summary>시간을 흘려 새로 드러난 글자 수를 돌려준다.</summary>
        public int Advance(float deltaTime)
        {
            if (IsComplete || deltaTime <= 0f)
            {
                return 0;
            }

            _budget += deltaTime;

            int revealed = 0;
            float perCharacter = 1f / _charactersPerSecond;

            while (_visible < _text.Length)
            {
                float cost = perCharacter + (_visible > 0 ? PauseAfter(_visible - 1) : 0f);

                if (_budget < cost)
                {
                    break;
                }

                _budget -= cost;
                _visible++;
                revealed++;
            }

            if (IsComplete)
            {
                _budget = 0f;
            }

            return revealed;
        }

        /// <summary>남은 글자를 한 번에 모두 드러낸다.</summary>
        public void Complete()
        {
            _visible = _text.Length;
            _budget = 0f;
        }

        /// <summary>이 문장을 끝까지 출력하는 데 걸리는 시간.</summary>
        public float EstimateDuration(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0f;
            }

            string previous = _text;
            _text = text;

            float total = 0f;

            for (int i = 0; i < text.Length; i++)
            {
                total += 1f / _charactersPerSecond + (i > 0 ? PauseAfter(i - 1) : 0f);
            }

            _text = previous;
            return total;
        }

        /// <summary>index 글자를 찍은 뒤 다음 글자까지 추가로 쉴 시간.</summary>
        public float PauseAfter(int index)
        {
            if (index < 0 || index >= _text.Length - 1)
            {
                return 0f;
            }

            char c = _text[index];
            char next = _text[index + 1];

            switch (c)
            {
                case '.':
                case '…':
                    return next == '.' || next == '…' ? EllipsisDotPauseSeconds : SentencePauseSeconds;
                case '?':
                case '!':
                    return next == '?' || next == '!' ? 0f : SentencePauseSeconds;
                case ',':
                    return CommaPauseSeconds;
                default:
                    return 0f;
            }
        }

        /// <summary>출력음을 낼 글자인가. 공백·문장부호는 소리 없이 지나간다.</summary>
        public static bool IsAudible(char c)
        {
            return !char.IsWhiteSpace(c) && !char.IsPunctuation(c) && !char.IsSymbol(c);
        }

        /// <summary>
        /// 다 찍은 뒤 대화창을 유지할 시간. 긴 문장일수록 읽을 시간을 더 준다.
        /// </summary>
        public static float HoldSecondsFor(string text, float minSeconds, float maxSeconds, float perCharacterSeconds)
        {
            int length = text == null ? 0 : text.Length;
            float hold = minSeconds + length * perCharacterSeconds;
            return Math.Max(minSeconds, Math.Min(maxSeconds, hold));
        }
    }
}

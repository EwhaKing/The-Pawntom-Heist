using System;

namespace Pawntom.Tutorial.Core
{
    /// <summary>
    /// 눈꺼풀 개폐 곡선. 시간(초) → 열린 정도(0=감음, 1=완전히 뜸).
    /// 키 사이는 smoothstep 으로 잇는다(무겁게 떴다가 멈칫하는 느낌).
    /// </summary>
    public sealed class EyelidTimeline
    {
        private readonly float[] _times;
        private readonly float[] _values;

        public EyelidTimeline(float[] times, float[] values)
        {
            if (times == null || values == null || times.Length == 0 || times.Length != values.Length)
            {
                throw new ArgumentException("키 시간과 값의 개수가 맞지 않는다");
            }

            for (int i = 1; i < times.Length; i++)
            {
                if (times[i] < times[i - 1])
                {
                    throw new ArgumentException("키 시간은 오름차순이어야 한다");
                }
            }

            _times = (float[])times.Clone();
            _values = (float[])values.Clone();
        }

        /// <summary>
        /// 오프닝: 암전 → 살짝 떴다 감김 → 반쯤 → 깜빡 → 완전히 뜸. 3.6초.
        /// </summary>
        public static EyelidTimeline CreateOpening()
        {
            return new EyelidTimeline(
                new[] { 0f, 1.0f, 1.45f, 1.7f, 2.35f, 2.55f, 3.6f },
                new[] { 0f, 0f, 0.26f, 0.05f, 0.55f, 0.2f, 1f });
        }

        /// <summary>
        /// 엔딩: 무겁게 한 번 감겼다 떴다가 완전히 감김. 2초.
        /// </summary>
        public static EyelidTimeline CreateClosing()
        {
            return new EyelidTimeline(
                new[] { 0f, 0.55f, 0.85f, 2f },
                new[] { 1f, 0.35f, 0.6f, 0f });
        }

        public float Duration
        {
            get { return _times[_times.Length - 1]; }
        }

        public float Evaluate(float time)
        {
            if (time <= _times[0])
            {
                return _values[0];
            }

            int last = _times.Length - 1;

            if (time >= _times[last])
            {
                return _values[last];
            }

            for (int i = 1; i <= last; i++)
            {
                if (time > _times[i])
                {
                    continue;
                }

                float span = _times[i] - _times[i - 1];
                float t = span <= 0f ? 1f : (time - _times[i - 1]) / span;
                t = t * t * (3f - 2f * t);
                return _values[i - 1] + (_values[i] - _values[i - 1]) * t;
            }

            return _values[last];
        }

        /// <summary>
        /// 초점 흐림 정도(1=완전히 흐림, 0=선명). 열린 정도를 따라가되 늦게 맑아진다.
        /// 카메라 후처리가 꺼져 있어 화면 어둡기·비네트 세기로 표현한다.
        /// </summary>
        public float Haze(float time)
        {
            float progress = Duration <= 0f ? 1f : time / (Duration + 0.6f);
            progress = progress < 0f ? 0f : (progress > 1f ? 1f : progress);
            return 1f - progress * progress;
        }
    }
}

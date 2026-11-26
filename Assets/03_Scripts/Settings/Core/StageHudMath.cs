using System;

namespace Pawntom.Settings
{
    /// <summary>
    /// 게임 씬(Map_Scene) HUD 피드백의 순수 계산. 캐스팅 원형 게이지·캣닢 경보 점멸·안내 문구 수명.
    /// 엔진에 의존하지 않아 EditMode 테스트로 검증한다. UI 쪽은 UI/Stage/*.
    /// (UI 폴더에 asmdef 가 없어 테스트가 닿도록 이 어셈블리에 둔다 — CONTRACT REQUEST 참고.)
    /// </summary>
    public static class StageHudMath
    {
        public static float Clamp01(float value)
        {
            if (float.IsNaN(value) || value < 0f)
            {
                return 0f;
            }

            return value > 1f ? 1f : value;
        }

        /// <summary>진행도(0~1) → 시계방향 원호 각도(도, 12시 방향에서 시작).</summary>
        public static float ArcDegrees(float progress)
        {
            return Clamp01(progress) * 360f;
        }

        /// <summary>진행도(0~1) → 표시 퍼센트(내림, 0~100).</summary>
        public static int Percent(float progress)
        {
            return (int)Math.Floor(Clamp01(progress) * 100f + 0.0001f);
        }

        /// <summary>
        /// 경보 점멸 세기. 주기 시작에는 최소, 주기 절반에서 최대(1)인 부드러운 파형(사이렌 한 주기와 같은 모양).
        /// </summary>
        public static float AlarmPulse(float elapsedSeconds, float cycleSeconds, float minWeight)
        {
            if (cycleSeconds <= 0.0001f)
            {
                return 1f;
            }

            float min = Clamp01(minWeight);
            float wave = 0.5f - 0.5f * (float)Math.Cos(2.0 * Math.PI * elapsedSeconds / cycleSeconds);
            return min + (1f - min) * wave;
        }

        /// <summary>0→1 로 부드럽게 올라가는 진입 곡선(smoothstep).</summary>
        public static float SmoothFade(float t)
        {
            float k = Clamp01(t);
            return k * k * (3f - 2f * k);
        }

        /// <summary>발동 직후 1 에서 0 으로 줄어드는 번쩍임(제곱 감쇠).</summary>
        public static float BurstDecay(float elapsedSeconds, float durationSeconds)
        {
            if (durationSeconds <= 0.0001f || elapsedSeconds < 0f)
            {
                return 0f;
            }

            float k = 1f - Clamp01(elapsedSeconds / durationSeconds);
            return k * k;
        }
    }

    /// <summary>
    /// 잠깐 떴다 사라지는 안내 문구의 수명. 나타날 때·사라질 때 부드럽게 알파를 준다.
    /// 시간은 호출자가 넘긴다(보통 Time.unscaledTime).
    /// </summary>
    public sealed class TimedMessage
    {
        private float _shownAt;
        private float _expireAt;

        public string Text { get; private set; }

        public void Show(string text, float now, float holdSeconds)
        {
            Text = text;
            _shownAt = now;
            _expireAt = now + (holdSeconds < 0f ? 0f : holdSeconds);
        }

        public void Clear()
        {
            Text = null;
            _expireAt = 0f;
        }

        public bool IsVisible(float now)
        {
            return !string.IsNullOrEmpty(Text) && now < _expireAt;
        }

        /// <summary>0~1. 문구가 없거나 만료되면 0.</summary>
        public float Alpha(float now, float fadeSeconds)
        {
            if (!IsVisible(now))
            {
                return 0f;
            }

            if (fadeSeconds <= 0.0001f)
            {
                return 1f;
            }

            float fadeIn = StageHudMath.Clamp01((now - _shownAt) / fadeSeconds);
            float fadeOut = StageHudMath.Clamp01((_expireAt - now) / fadeSeconds);
            return Math.Min(fadeIn, fadeOut);
        }
    }
}

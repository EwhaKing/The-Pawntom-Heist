using System;

namespace Pawntom.Tutorial.Core
{
    /// <summary>
    /// 튜토리얼 효과음을 코드로 합성한다. 프로젝트에 무전·타자음 에셋이 없어 직렬화 에셋 추가 없이 소리를 내기 위함이다.
    /// 결과는 [-1, 1] 범위 모노 PCM. 같은 시드면 같은 파형이다.
    /// 나중에 사운드 에셋이 생기면 연출 쪽에서 교체할 수 있다(이 클래스는 대체 수단).
    /// </summary>
    public static class TutorialSfxSynth
    {
        private const double TwoPi = Math.PI * 2d;

        /// <summary>
        /// 글자 출력음. 짧은 사각/사인 혼합 톤에 빠른 감쇠, 끝으로 갈수록 살짝 내려가는 음정.
        /// </summary>
        /// <param name="squareMix">0=사인(부드러움) ~ 1=사각(무전기 삑삑)</param>
        /// <param name="noiseMix">0~1 잡음 섞임(무전 수신음)</param>
        public static float[] Blip(int sampleRate, double frequency, double durationSeconds, double squareMix, double noiseMix, int seed)
        {
            int count = SampleCount(sampleRate, durationSeconds);
            float[] data = new float[count];
            Random random = new Random(seed);

            double attack = 0.003d;
            double decayRate = 6d / Math.Max(durationSeconds, 0.001d);
            double phase = 0d;

            for (int i = 0; i < count; i++)
            {
                double t = (double)i / sampleRate;
                double progress = (double)i / count;
                double freq = frequency * (1d - 0.08d * progress);
                phase += TwoPi * freq / sampleRate;

                double sine = Math.Sin(phase);
                double square = sine >= 0d ? 1d : -1d;
                double tone = square * squareMix + sine * (1d - squareMix);
                double noise = random.NextDouble() * 2d - 1d;

                double envelope = (t < attack ? t / attack : 1d) * Math.Exp(-(t - attack) * decayRate);
                data[i] = (float)((tone * (1d - noiseMix) + noise * noiseMix) * envelope);
            }

            Normalize(data, 0.42f);
            return data;
        }

        /// <summary>
        /// 무전 잡음. 대역 제한한 백색잡음에 떨림(진폭 변조)과 지직거림(임펄스)을 얹는다.
        /// </summary>
        /// <param name="cracklesPerSecond">지직 임펄스 밀도</param>
        public static float[] Static(int sampleRate, double durationSeconds, int seed, double cracklesPerSecond)
        {
            int count = SampleCount(sampleRate, durationSeconds);
            float[] data = new float[count];
            Random random = new Random(seed);

            // 단극 고역 통과 계수는 RC/(RC+dt) = 1 - 저역 통과 계수.
            double highPassAlpha = 1d - OnePoleAlpha(sampleRate, 700d);
            double lowPassAlpha = OnePoleAlpha(sampleRate, 4200d);
            double previousInput = 0d;
            double highPassed = 0d;
            double lowPassed = 0d;

            double crackle = 0d;
            double crackleDecay = Math.Exp(-1d / (0.002d * sampleRate));
            double crackleProbability = cracklesPerSecond / sampleRate;

            double flutterPhase = random.NextDouble() * TwoPi;
            double fadeIn = 0.012d;
            double fadeOut = Math.Min(0.06d, durationSeconds * 0.4d);

            for (int i = 0; i < count; i++)
            {
                double t = (double)i / sampleRate;
                double white = random.NextDouble() * 2d - 1d;

                // 단극 고역 통과 → 저역 통과 = 무전기 특유의 좁은 대역.
                highPassed = highPassAlpha * (highPassed + white - previousInput);
                previousInput = white;
                lowPassed += lowPassAlpha * (highPassed - lowPassed);

                if (random.NextDouble() < crackleProbability)
                {
                    crackle = (random.NextDouble() < 0.5d ? -1d : 1d) * (0.6d + 0.4d * random.NextDouble());
                }

                crackle *= crackleDecay;

                double flutter = 0.65d + 0.35d * Math.Sin(TwoPi * 9d * t + flutterPhase);
                double envelope = Math.Min(1d, t / fadeIn) * Math.Min(1d, (durationSeconds - t) / fadeOut);
                data[i] = (float)((lowPassed * 2.4d * flutter + crackle) * Math.Max(0d, envelope));
            }

            Normalize(data, 0.4f);
            return data;
        }

        /// <summary>
        /// 무전 송수신 개시음(스켈치). 높은 짧은 클릭 + 잡음 꼬리.
        /// </summary>
        public static float[] Squelch(int sampleRate, int seed)
        {
            double duration = 0.11d;
            int count = SampleCount(sampleRate, duration);
            float[] data = new float[count];
            float[] noise = Static(sampleRate, duration, seed, 140d);
            double phase = 0d;

            for (int i = 0; i < count; i++)
            {
                double t = (double)i / sampleRate;
                phase += TwoPi * (2300d - 9000d * t) / sampleRate;
                double click = Math.Sin(phase) * Math.Exp(-t * 90d);
                data[i] = (float)(click * 0.8d + noise[i] * 0.7d * Math.Min(1d, t / 0.01d));
            }

            Normalize(data, 0.38f);
            return data;
        }

        /// <summary>
        /// 심장 박동(쿵-쿵). 눈을 뜨기 전 암전 구간에 쓴다.
        /// </summary>
        public static float[] Heartbeat(int sampleRate)
        {
            double duration = 0.9d;
            int count = SampleCount(sampleRate, duration);
            float[] data = new float[count];

            AddThump(data, sampleRate, 0d, 1d);
            AddThump(data, sampleRate, 0.27d, 0.7d);

            Normalize(data, 0.75f);
            return data;
        }

        public static int SampleCount(int sampleRate, double durationSeconds)
        {
            if (sampleRate <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sampleRate));
            }

            return Math.Max(1, (int)Math.Round(sampleRate * Math.Max(0d, durationSeconds)));
        }

        /// <summary>최대 절댓값이 peak 가 되도록 맞춘다. 무음이면 그대로 둔다.</summary>
        public static void Normalize(float[] data, float peak)
        {
            float max = 0f;

            for (int i = 0; i < data.Length; i++)
            {
                float abs = Math.Abs(data[i]);

                if (abs > max)
                {
                    max = abs;
                }
            }

            if (max <= 0f)
            {
                return;
            }

            float scale = peak / max;

            for (int i = 0; i < data.Length; i++)
            {
                data[i] *= scale;
            }
        }

        private static void AddThump(float[] data, int sampleRate, double startSeconds, double gain)
        {
            int start = (int)(startSeconds * sampleRate);
            double phase = 0d;

            for (int i = start; i < data.Length; i++)
            {
                double t = (double)(i - start) / sampleRate;
                double freq = 48d + 40d * Math.Exp(-t * 30d);
                phase += TwoPi * freq / sampleRate;

                double envelope = Math.Min(1d, t / 0.006d) * Math.Exp(-t * 14d);
                data[i] += (float)(Math.Sin(phase) * envelope * gain);
            }
        }

        private static double OnePoleAlpha(int sampleRate, double cutoffHz)
        {
            double rc = 1d / (TwoPi * cutoffHz);
            double dt = 1d / sampleRate;
            return dt / (rc + dt);
        }
    }
}

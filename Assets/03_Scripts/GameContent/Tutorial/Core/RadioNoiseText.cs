using System;

namespace Pawntom.Tutorial.Core
{
    /// <summary>
    /// 무전 노이즈 텍스트 합성. 원문을 버퍼에 복사하면서 일부 글자를 잡음 기호로 바꾼다.
    /// <list type="bullet">
    /// <item><b>해독 꼬리</b>: 방금 드러난 글자 몇 개가 잡음으로 보였다가 다음 프레임에 제 글자로 바뀐다(수신 중 해독되는 느낌).</item>
    /// <item><b>글리치</b>: 이미 드러난 글자가 확률적으로 잠깐 잡음이 된다(전파 간섭).</item>
    /// </list>
    /// 할당하지 않는다. 같은 시드면 같은 결과다.
    /// </summary>
    public static class RadioNoiseText
    {
        /// <summary>
        /// 잡음 기호. 기본 TMP 폰트(NanumBarunpenR, 동적)에 확실히 있는 ASCII 만 쓴다 — 없는 글자는 네모로 보인다.
        /// </summary>
        public const string Glyphs = "#%&*+=<>/?!@$~^|";

        public static bool IsScramblable(char c)
        {
            return !char.IsWhiteSpace(c);
        }

        public static char PickGlyph(Random random)
        {
            return Glyphs[random.Next(Glyphs.Length)];
        }

        /// <summary>
        /// source 를 buffer 에 옮기며 잡음을 섞는다. 바뀐 글자 수를 돌려준다.
        /// </summary>
        /// <param name="source">원문</param>
        /// <param name="buffer">원문 길이 이상</param>
        /// <param name="visible">현재 보이는 글자 수</param>
        /// <param name="decodeTail">보이는 끝에서부터 잡음으로 둘 글자 수</param>
        /// <param name="glitchChance">해독 꼬리 앞의 보이는 글자 하나가 잡음이 될 확률(0~1)</param>
        /// <param name="random">시드가 고정된 난수기</param>
        public static int Compose(string source, char[] buffer, int visible, int decodeTail, double glitchChance, Random random)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (buffer == null || buffer.Length < source.Length)
            {
                throw new ArgumentException("버퍼가 원문보다 짧다", nameof(buffer));
            }

            if (random == null)
            {
                throw new ArgumentNullException(nameof(random));
            }

            int length = source.Length;
            visible = Clamp(visible, 0, length);
            decodeTail = Clamp(decodeTail, 0, visible);

            int tailStart = visible - decodeTail;
            int changed = 0;

            for (int i = 0; i < length; i++)
            {
                char c = source[i];

                bool scramble = false;

                if (i < visible && IsScramblable(c))
                {
                    scramble = i >= tailStart || (glitchChance > 0d && random.NextDouble() < glitchChance);
                }

                if (scramble)
                {
                    buffer[i] = PickGlyph(random);
                    changed++;
                }
                else
                {
                    buffer[i] = c;
                }
            }

            return changed;
        }

        private static int Clamp(int value, int min, int max)
        {
            return value < min ? min : (value > max ? max : value);
        }
    }
}

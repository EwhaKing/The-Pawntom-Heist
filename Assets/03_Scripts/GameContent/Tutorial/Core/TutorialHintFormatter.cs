using System;
using System.Collections.Generic;
using System.Text;

namespace Pawntom.Tutorial.Core
{
    /// <summary>
    /// 키 안내 문구 표기를 화면용 문자열로 바꾼다.
    /// <list type="bullet">
    /// <item><c>{Token}</c> → 키 이름(실제 입력 바인딩). 모르는 토큰은 그대로 둔다.</item>
    /// <item><c>[내용]</c> → 강조 열기 + 내용 + 강조 닫기(예: TMP 색 태그).</item>
    /// </list>
    /// 안내 문구가 바뀔 때만 부른다(매 프레임 X).
    /// </summary>
    public static class TutorialHintFormatter
    {
        public static string Format(string hint, IReadOnlyDictionary<string, string> tokens, string highlightOpen, string highlightClose)
        {
            if (string.IsNullOrEmpty(hint))
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder(hint.Length + 32);

            for (int i = 0; i < hint.Length; i++)
            {
                char c = hint[i];

                if (c == '{')
                {
                    int close = hint.IndexOf('}', i + 1);

                    if (close > i)
                    {
                        string key = hint.Substring(i + 1, close - i - 1);
                        string value;

                        if (tokens != null && tokens.TryGetValue(key, out value))
                        {
                            builder.Append(value);
                            i = close;
                            continue;
                        }
                    }

                    builder.Append(c);
                    continue;
                }

                if (c == '[')
                {
                    builder.Append(highlightOpen ?? string.Empty);
                    continue;
                }

                if (c == ']')
                {
                    builder.Append(highlightClose ?? string.Empty);
                    continue;
                }

                builder.Append(c);
            }

            return builder.ToString();
        }

        /// <summary>강조·토큰 표기를 모두 벗긴 평문. 로그·테스트용.</summary>
        public static string Plain(string hint, IReadOnlyDictionary<string, string> tokens)
        {
            return Format(hint, tokens, string.Empty, string.Empty);
        }
    }
}

using System.Collections.Generic;

namespace Pawntom.Settings
{
    /// <summary>해상도 한 칸(주사율은 구분하지 않는다).</summary>
    public struct ResolutionOption
    {
        public readonly int Width;
        public readonly int Height;

        public ResolutionOption(int width, int height)
        {
            Width = width;
            Height = height;
        }

        public override string ToString()
        {
            return Width + " × " + Height;
        }
    }

    /// <summary>
    /// 모니터가 알려 준 해상도 목록을 옵션창용으로 정리한다.
    /// Screen.resolutions 는 같은 크기를 주사율마다 여러 번 돌려주므로 크기만 남기고, 작은 것부터 정렬한다.
    /// </summary>
    public static class ResolutionList
    {
        /// <summary>이보다 작은 해상도는 UI 가 깨지므로 목록에서 뺀다(기준 해상도 1800×950 의 절반 남짓).</summary>
        public const int MinWidth = 800;
        public const int MinHeight = 600;

        public static List<ResolutionOption> Build(IList<ResolutionOption> raw)
        {
            List<ResolutionOption> result = new List<ResolutionOption>();

            if (raw == null)
            {
                return result;
            }

            for (int i = 0; i < raw.Count; i++)
            {
                ResolutionOption option = raw[i];

                if (option.Width < MinWidth || option.Height < MinHeight || Contains(result, option))
                {
                    continue;
                }

                result.Add(option);
            }

            result.Sort(Compare);
            return result;
        }

        /// <summary>목록에서 (width, height) 에 가장 가까운 칸. 목록이 비면 -1.</summary>
        public static int IndexOfClosest(IReadOnlyList<ResolutionOption> options, int width, int height)
        {
            if (options == null || options.Count == 0)
            {
                return -1;
            }

            int best = 0;
            long bestDistance = long.MaxValue;

            for (int i = 0; i < options.Count; i++)
            {
                long dw = options[i].Width - width;
                long dh = options[i].Height - height;
                long distance = dw * dw + dh * dh;

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = i;
                }
            }

            return best;
        }

        private static bool Contains(List<ResolutionOption> list, ResolutionOption option)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Width == option.Width && list[i].Height == option.Height)
                {
                    return true;
                }
            }

            return false;
        }

        private static int Compare(ResolutionOption a, ResolutionOption b)
        {
            int byWidth = a.Width.CompareTo(b.Width);
            return byWidth != 0 ? byWidth : a.Height.CompareTo(b.Height);
        }
    }
}

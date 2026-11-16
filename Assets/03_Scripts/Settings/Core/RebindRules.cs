using System;
using System.Collections.Generic;

namespace Pawntom.Settings
{
    /// <summary>
    /// 키 바꾸기 규칙. 입력 시스템 경로 문자열(예: "&lt;Keyboard&gt;/space")만 다루므로 엔진 없이 검증한다.
    /// </summary>
    public static class RebindRules
    {
        /// <summary>
        /// 바꿀 수 없는 키. Esc 는 메뉴·취소용, 1~4 는 인벤토리 칸 선택(직접 읽음)이다.
        /// 마우스 위치·이동·휠은 "누르는 키"가 아니므로 InputManager 가 따로 걸러낸다.
        /// </summary>
        public static readonly string[] ReservedPaths =
        {
            "<Keyboard>/escape",
            "<Keyboard>/anyKey",
            "<Keyboard>/1",
            "<Keyboard>/2",
            "<Keyboard>/3",
            "<Keyboard>/4",
            "<Keyboard>/numpad1",
            "<Keyboard>/numpad2",
            "<Keyboard>/numpad3",
            "<Keyboard>/numpad4",
        };

        public static bool IsReserved(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return true;
            }

            for (int i = 0; i < ReservedPaths.Length; i++)
            {
                if (SamePath(ReservedPaths[i], path))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// targetIndex 칸에 newPath 를 넣으려 할 때 같은 키를 이미 쓰고 있는 다른 칸. 없으면 -1.
        /// 호출한 쪽은 그 칸에 targetIndex 의 이전 키를 넣어 두 칸을 맞바꾼다(한 키가 두 동작을 하지 않게).
        /// </summary>
        public static int FindConflict(IList<string> currentPaths, int targetIndex, string newPath)
        {
            if (currentPaths == null || string.IsNullOrEmpty(newPath))
            {
                return -1;
            }

            for (int i = 0; i < currentPaths.Count; i++)
            {
                if (i != targetIndex && SamePath(currentPaths[i], newPath))
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>입력 시스템 경로는 대소문자를 구분하지 않는다("&lt;Keyboard&gt;/Space" == "&lt;keyboard&gt;/space").</summary>
        public static bool SamePath(string a, string b)
        {
            return !string.IsNullOrEmpty(a) && !string.IsNullOrEmpty(b) &&
                   string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }
    }
}

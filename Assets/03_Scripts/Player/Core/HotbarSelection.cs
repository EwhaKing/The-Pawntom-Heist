namespace Pawntom.Player.Core
{
    /// <summary>
    /// 핫바 선택 규칙. -1 = 아무 칸도 고르지 않음(맨손).
    /// </summary>
    public static class HotbarSelection
    {
        /// <summary>숫자키: 다른 칸이면 그 칸을 고르고, 이미 고른 칸이면 해제한다. 범위 밖이면 그대로.</summary>
        public static int Toggle(int current, int pressed, int slotCount)
        {
            if (pressed < 0 || pressed >= slotCount)
            {
                return current;
            }

            return current == pressed ? -1 : pressed;
        }

        /// <summary>
        /// 휠: steps 칸만큼 순환 이동(양수 = 다음 칸). 아무것도 안 고른 상태에서는
        /// 첫 칸이 방향에 따라 1번(다음 방향) 또는 마지막 칸(이전 방향)이 된다.
        /// </summary>
        public static int Scroll(int current, int steps, int slotCount)
        {
            if (steps == 0 || slotCount <= 0)
            {
                return current;
            }

            int direction = steps > 0 ? 1 : -1;
            int count = steps > 0 ? steps : -steps;
            int index = current;

            for (int i = 0; i < count; i++)
            {
                if (index < 0 || index >= slotCount)
                {
                    index = direction > 0 ? 0 : slotCount - 1;
                    continue;
                }

                index = (index + direction + slotCount) % slotCount;
            }

            return index;
        }
    }
}

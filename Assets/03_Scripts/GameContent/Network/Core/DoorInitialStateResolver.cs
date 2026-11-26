namespace Pawntom.Network
{
    /// <summary>
    /// 격벽 초기 해금 상태의 출처 우선순위: 레벨 데이터 → 같은 오브젝트의 DoorLayoutMarker → 현재 기본값(적용하지 않음).
    /// </summary>
    public static class DoorInitialStateResolver
    {
        /// <returns>적용할 값이 있으면 true. false 면 기본값을 건드리지 않는다.</returns>
        public static bool TryResolve(bool hasLayoutEntry, bool layoutUnlocked, bool hasMarker, bool markerUnlocked, out bool unlocked)
        {
            if (hasLayoutEntry)
            {
                unlocked = layoutUnlocked;
                return true;
            }

            if (hasMarker)
            {
                unlocked = markerUnlocked;
                return true;
            }

            unlocked = false;
            return false;
        }
    }
}

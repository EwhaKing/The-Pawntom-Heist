#if UNITY_INCLUDE_TESTS
using NUnit.Framework;

namespace Pawntom.Network.Tests
{
    /// <summary>격벽 초기 상태 우선순위: 레벨 데이터 → 같은 오브젝트의 DoorLayoutMarker → 현재 기본값(건드리지 않음).</summary>
    public class DoorInitialStateResolverTests
    {
        // hasLayout, layoutUnlocked, hasMarker, markerUnlocked → applies, unlocked
        [TestCase(true, true, true, false, true, true)]
        [TestCase(true, false, true, true, true, false)]
        [TestCase(true, true, false, false, true, true)]
        [TestCase(false, false, true, true, true, true)]
        [TestCase(false, false, true, false, true, false)]
        [TestCase(false, true, false, true, false, false)]
        public void TryResolve_Sources_FollowsPriority(bool hasLayout, bool layoutUnlocked, bool hasMarker, bool markerUnlocked,
            bool expectedApplies, bool expectedUnlocked)
        {
            bool applies = DoorInitialStateResolver.TryResolve(hasLayout, layoutUnlocked, hasMarker, markerUnlocked, out bool unlocked);

            Assert.AreEqual(expectedApplies + ":" + expectedUnlocked, applies + ":" + unlocked);
        }
    }
}
#endif

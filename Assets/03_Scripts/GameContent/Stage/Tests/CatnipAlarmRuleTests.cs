using NUnit.Framework;

namespace Pawntom.Stage.Tests
{
    /// <summary>
    /// 캣닢 경보(GAME_DESIGN §3.7: 캣닢 획득 즉시 경보 → 브루투스 스폰). 첫 획득에만 발동하고 이후는 무시한다.
    /// </summary>
    public sealed class CatnipAlarmRuleTests
    {
        [Test]
        public void Observe_NobodyHoldsCatnip_DoesNotTrigger()
        {
            var rule = new CatnipAlarmRule();

            Assert.IsFalse(rule.Observe(false));
        }

        [Test]
        public void Observe_FirstAcquisition_Triggers()
        {
            var rule = new CatnipAlarmRule();
            rule.Observe(false);

            Assert.IsTrue(rule.Observe(true));
        }

        [Test]
        public void Observe_StillHoldingNextTick_DoesNotTriggerAgain()
        {
            var rule = new CatnipAlarmRule();
            rule.Observe(true);

            Assert.IsFalse(rule.Observe(true));
        }

        [Test]
        public void Observe_DroppedAndPickedUpAgain_DoesNotTriggerAgain()
        {
            // 팀원 인계·사망 드롭 후 재획득은 경보를 다시 울리지 않는다(§3.7 릴레이 전술).
            var rule = new CatnipAlarmRule();
            rule.Observe(true);
            rule.Observe(false);

            Assert.IsFalse(rule.Observe(true));
        }

        [Test]
        public void IsTriggered_AfterFirstAcquisition_IsTrue()
        {
            var rule = new CatnipAlarmRule();
            rule.Observe(true);

            Assert.IsTrue(rule.IsTriggered);
        }

        [Test]
        public void Restore_AlreadyTriggered_IgnoresNextAcquisition()
        {
            // 늦게 들어온 참가자·재스폰된 상태 권한은 네트워크 상태로 되돌린다.
            var rule = new CatnipAlarmRule();
            rule.Restore(true);

            Assert.IsFalse(rule.Observe(true));
        }
    }
}

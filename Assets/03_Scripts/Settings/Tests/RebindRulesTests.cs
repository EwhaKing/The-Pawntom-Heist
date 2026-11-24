using System.Collections.Generic;
using NUnit.Framework;
using Pawntom.Settings;

namespace Pawntom.Settings.Tests
{
    public sealed class RebindRulesTests
    {
        // 옵션창의 키 칸 순서: 앞·뒤·왼쪽·오른쪽·달리기·점프·상호작용·버리기 (InputManager 기본 바인딩)
        private static List<string> Defaults()
        {
            return new List<string>
            {
                "<Keyboard>/w", "<Keyboard>/s", "<Keyboard>/a", "<Keyboard>/d",
                "<Keyboard>/leftShift", "<Keyboard>/space", "<Keyboard>/e", "<Keyboard>/q",
            };
        }

        [Test]
        public void FindConflict_KeyUsedByAnotherSlot_ReturnsThatSlot()
        {
            // 점프(5)에 E 를 넣으려 하면 상호작용(6)과 겹친다.
            Assert.AreEqual(6, RebindRules.FindConflict(Defaults(), 5, "<Keyboard>/e"));
        }

        [Test]
        public void FindConflict_SameSlotSameKey_IsNotConflict()
        {
            Assert.AreEqual(-1, RebindRules.FindConflict(Defaults(), 6, "<Keyboard>/e"));
        }

        [Test]
        public void FindConflict_FreeKey_ReturnsMinusOne()
        {
            Assert.AreEqual(-1, RebindRules.FindConflict(Defaults(), 6, "<Keyboard>/f"));
        }

        [Test]
        public void FindConflict_IgnoresCase()
        {
            Assert.AreEqual(5, RebindRules.FindConflict(Defaults(), 0, "<keyboard>/SPACE"));
        }

        [TestCase("<Keyboard>/escape", true)]
        [TestCase("<Keyboard>/2", true)]
        [TestCase("<Keyboard>/numpad4", true)]
        [TestCase("", true)]
        [TestCase("<Keyboard>/f", false)]
        [TestCase("<Mouse>/rightButton", false)]
        public void IsReserved_ReservedKeys(string path, bool expected)
        {
            Assert.AreEqual(expected, RebindRules.IsReserved(path));
        }
    }
}

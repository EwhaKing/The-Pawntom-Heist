using NUnit.Framework;
using Pawntom.Player.Core;

namespace Pawntom.Player.Tests
{
    public sealed class HotbarSelectionTests
    {
        private const int Slots = 4; // InventoryManager.MaxSlotCount

        [TestCase(-1, 2, 2)]
        [TestCase(0, 3, 3)]
        [TestCase(2, 2, -1)]
        [TestCase(1, 9, 1)]
        public void Toggle_PressedSlot_SelectsOrClears(int current, int pressed, int expected)
        {
            Assert.AreEqual(expected, HotbarSelection.Toggle(current, pressed, Slots));
        }

        [TestCase(-1, 1, 0)]
        [TestCase(-1, -1, 3)]
        [TestCase(0, 1, 1)]
        [TestCase(3, 1, 0)]
        [TestCase(0, -1, 3)]
        [TestCase(1, 2, 3)]
        [TestCase(2, -3, 3)]
        [TestCase(-1, 3, 2)]
        [TestCase(2, 0, 2)]
        public void Scroll_Steps_MovesCyclically(int current, int steps, int expected)
        {
            Assert.AreEqual(expected, HotbarSelection.Scroll(current, steps, Slots));
        }
    }
}

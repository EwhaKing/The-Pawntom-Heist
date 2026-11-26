using NUnit.Framework;

namespace Pawntom.Stage.Tests
{
    /// <summary>
    /// 플레이어 스폰 지점 배정(호스트). 과거 결함: 입장 수를 인덱스로 써서 한 명이 나간 뒤 들어온 사람이
    /// 남아 있는 사람과 같은 지점을 받았다.
    /// </summary>
    public sealed class SpawnSlotAllocatorTests
    {
        [Test]
        public void Acquire_NewPlayers_GetLowestFreeSlotsInOrder()
        {
            var slots = new SpawnSlotAllocator();

            Assert.AreEqual(0, slots.Acquire(10, 4));
            Assert.AreEqual(1, slots.Acquire(11, 4));
            Assert.AreEqual(2, slots.Acquire(12, 4));
        }

        [Test]
        public void Acquire_SamePlayerTwice_ReturnsSameSlot()
        {
            var slots = new SpawnSlotAllocator();
            slots.Acquire(10, 4);
            int first = slots.Acquire(11, 4);

            Assert.AreEqual(first, slots.Acquire(11, 4));
            Assert.AreEqual(2, slots.UsedCount);
        }

        [Test]
        public void Release_MiddlePlayer_NextJoinerReusesThatSlotWithoutCollision()
        {
            var slots = new SpawnSlotAllocator();
            slots.Acquire(1, 4); // 0
            slots.Acquire(2, 4); // 1
            slots.Acquire(3, 4); // 2

            slots.Release(2);

            Assert.AreEqual(1, slots.Acquire(4, 4));
            Assert.AreEqual(2, slots.Acquire(3, 4), "남아 있는 사람의 지점은 그대로");
        }

        [Test]
        public void Acquire_AllSlotsUsed_ReturnsMinusOne()
        {
            var slots = new SpawnSlotAllocator();
            slots.Acquire(1, 2);
            slots.Acquire(2, 2);

            Assert.AreEqual(-1, slots.Acquire(3, 2));
            Assert.AreEqual(2, slots.UsedCount);
        }

        [Test]
        public void Acquire_ZeroSlots_ReturnsMinusOne()
        {
            Assert.AreEqual(-1, new SpawnSlotAllocator().Acquire(1, 0));
        }

        [Test]
        public void Acquire_HeldSlotBeyondNewCount_IsReassigned()
        {
            // 레벨 데이터 씬에서 지점 수가 줄어든 경우: 범위 밖 지점을 계속 쥐고 있지 않는다.
            var slots = new SpawnSlotAllocator();
            slots.Acquire(1, 4);
            slots.Acquire(2, 4);
            slots.Acquire(3, 4); // 2

            Assert.AreEqual(-1, slots.Acquire(3, 2));
            Assert.IsFalse(slots.TryGetSlot(3, out _));
        }

        [Test]
        public void Release_UnknownPlayer_IsNoOp()
        {
            var slots = new SpawnSlotAllocator();
            slots.Acquire(1, 4);

            slots.Release(99);

            Assert.AreEqual(1, slots.UsedCount);
            Assert.IsTrue(slots.TryGetSlot(1, out int slot));
            Assert.AreEqual(0, slot);
        }

        [Test]
        public void Clear_ReleasesEverything()
        {
            var slots = new SpawnSlotAllocator();
            slots.Acquire(1, 4);
            slots.Acquire(2, 4);

            slots.Clear();

            Assert.AreEqual(0, slots.UsedCount);
            Assert.AreEqual(0, slots.Acquire(2, 4));
        }
    }
}

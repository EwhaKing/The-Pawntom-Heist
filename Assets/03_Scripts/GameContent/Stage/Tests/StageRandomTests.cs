using System;
using NUnit.Framework;

namespace Pawntom.Stage.Tests
{
    /// <summary>
    /// 시드 RNG: 호스트가 정한 시드 하나만 공유하면 모든 피어가 같은 배치를 다시 계산할 수 있어야 한다
    /// (fusion-network 규칙 — 판정에 UnityEngine.Random 금지).
    /// </summary>
    public sealed class StageRandomTests
    {
        private const int SequenceLength = 64;

        [Test]
        public void NextUInt_SameSeed_ProducesSameSequence()
        {
            var a = new StageRandom(12345);
            var b = new StageRandom(12345);
            bool allEqual = true;

            for (int i = 0; i < SequenceLength; i++)
            {
                if (a.NextUInt() != b.NextUInt())
                {
                    allEqual = false;
                }
            }

            Assert.IsTrue(allEqual);
        }

        [Test]
        public void NextUInt_DifferentSeeds_ProduceDifferentSequences()
        {
            var a = new StageRandom(1);
            var b = new StageRandom(2);
            int sameCount = 0;

            for (int i = 0; i < SequenceLength; i++)
            {
                if (a.NextUInt() == b.NextUInt())
                {
                    sameCount++;
                }
            }

            Assert.Less(sameCount, SequenceLength);
        }

        [TestCase(0)]
        [TestCase(-1)]
        [TestCase(int.MinValue)]
        [TestCase(int.MaxValue)]
        public void NextUInt_EdgeSeed_IsNotStuckAtZero(int seed)
        {
            var rng = new StageRandom(seed);
            uint first = rng.NextUInt();
            uint second = rng.NextUInt();

            Assert.IsFalse(first == 0u && second == 0u);
        }

        [TestCase(1)]
        [TestCase(3)]
        [TestCase(7)]
        [TestCase(1000)]
        public void NextInt_AnySeed_StaysWithinRange(int max)
        {
            bool inRange = true;

            for (int seed = 0; seed < 50; seed++)
            {
                var rng = new StageRandom(seed);

                for (int i = 0; i < 20; i++)
                {
                    int value = rng.NextInt(max);
                    if (value < 0 || value >= max)
                    {
                        inRange = false;
                    }
                }
            }

            Assert.IsTrue(inRange);
        }

        [TestCase(0)]
        [TestCase(-5)]
        public void NextInt_NonPositiveMax_Throws(int max)
        {
            var rng = new StageRandom(1);

            Assert.Throws<ArgumentOutOfRangeException>(() => rng.NextInt(max));
        }

        [Test]
        public void NextFloat01_ManyDraws_StaysInHalfOpenUnitRange()
        {
            var rng = new StageRandom(99);
            bool inRange = true;

            for (int i = 0; i < 5000; i++)
            {
                float value = rng.NextFloat01();
                if (value < 0f || value >= 1f)
                {
                    inRange = false;
                }
            }

            Assert.IsTrue(inRange);
        }

        [Test]
        public void NextInt_ManyDraws_HitsEveryValue()
        {
            var rng = new StageRandom(7);
            var hits = new int[5];

            for (int i = 0; i < 500; i++)
            {
                hits[rng.NextInt(hits.Length)]++;
            }

            Assert.That(hits, Has.All.GreaterThan(0));
        }
    }
}

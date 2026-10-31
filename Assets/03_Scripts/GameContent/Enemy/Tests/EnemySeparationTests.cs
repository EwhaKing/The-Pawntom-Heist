using System.Collections.Generic;
using NUnit.Framework;
using Pawntom.Enemy.Core;
using UnityEngine;

namespace Pawntom.Enemy.Tests
{
    public sealed class EnemySeparationTests
    {
        [Test]
        public void AdjustDestination_NoOthers_KeepsDestination()
        {
            Vector3 result = EnemySeparation.AdjustDestination(Vector3.zero, new Vector3(5f, 0f, 0f), new List<Vector3>(), 1.2f);

            Assert.AreEqual(new Vector3(5f, 0f, 0f), result);
        }

        [Test]
        public void AdjustDestination_OtherFarFromDestination_KeepsDestination()
        {
            var others = new List<Vector3> { new Vector3(20f, 0f, 0f) };

            Vector3 result = EnemySeparation.AdjustDestination(Vector3.zero, new Vector3(5f, 0f, 0f), others, 1.2f);

            Assert.AreEqual(new Vector3(5f, 0f, 0f), result);
        }

        [Test]
        public void AdjustDestination_OtherStandingOnDestination_StopsMinSeparationAway()
        {
            // 다른 개가 웨이포인트(5,0,0)에 서 있으면, 그 개에서 내 쪽으로 1.2 떨어진 곳에서 멈춘다.
            var others = new List<Vector3> { new Vector3(5f, 0f, 0f) };

            Vector3 result = EnemySeparation.AdjustDestination(Vector3.zero, new Vector3(5f, 0f, 0f), others, 1.2f);

            Assert.AreEqual(3.8f, result.x, 0.0001f);
        }

        [Test]
        public void AdjustDestination_Adjusted_KeepsAtLeastMinSeparationFromOther()
        {
            var others = new List<Vector3> { new Vector3(5f, 0f, 0.3f) };

            Vector3 result = EnemySeparation.AdjustDestination(new Vector3(0f, 0f, 2f), new Vector3(5f, 0f, 0f), others, 1.2f);

            Assert.GreaterOrEqual(Vector3.Distance(result, others[0]), 1.2f - 0.0001f);
        }

        [Test]
        public void AdjustDestination_SelfOnTopOfOther_StillMovesAway()
        {
            var others = new List<Vector3> { new Vector3(5f, 0f, 0f) };

            Vector3 result = EnemySeparation.AdjustDestination(new Vector3(5f, 0f, 0f), new Vector3(5f, 0f, 0f), others, 1.2f);

            Assert.AreEqual(1.2f, Vector3.Distance(result, others[0]), 0.0001f);
        }

        [Test]
        public void AdjustDestination_ZeroSeparation_KeepsDestination()
        {
            var others = new List<Vector3> { new Vector3(5f, 0f, 0f) };

            Vector3 result = EnemySeparation.AdjustDestination(Vector3.zero, new Vector3(5f, 0f, 0f), others, 0f);

            Assert.AreEqual(new Vector3(5f, 0f, 0f), result);
        }
    }
}

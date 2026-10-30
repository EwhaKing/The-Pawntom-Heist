using System.Collections.Generic;
using NUnit.Framework;
using Pawntom.Player.Core;
using UnityEngine;

namespace Pawntom.Player.Tests
{
    public sealed class InteractAimSelectorTests
    {
        private static readonly Ray Forward = new Ray(Vector3.zero, Vector3.forward);

        [Test]
        public void Choose_AimedAtSecondItem_PicksItEvenIfFirstIsCloser()
        {
            // 0: 조준선 오른쪽에 있는 가까운 물건, 1: 정면의 먼 물건 → 정면(1)을 집는다.
            var visuals = new List<Bounds>
            {
                new Bounds(new Vector3(0.6f, 0f, 1.5f), Vector3.one * 0.5f),
                new Bounds(new Vector3(0f, 0f, 3f), Vector3.one * 0.5f)
            };

            Assert.AreEqual(1, InteractAimSelector.Choose(Forward, 4f, visuals, 0.1f, float.MaxValue));
        }

        [Test]
        public void Choose_TwoOverlapping_PicksCenterClosestToRay()
        {
            // 큰 그림(중심이 옆)과 작은 항아리(정면)가 둘 다 광선에 걸리면 항아리.
            var visuals = new List<Bounds>
            {
                new Bounds(new Vector3(1.2f, 0f, 2.5f), new Vector3(3f, 2f, 0.1f)),
                new Bounds(new Vector3(0.05f, 0f, 2f), Vector3.one * 0.6f)
            };

            Assert.AreEqual(1, InteractAimSelector.Choose(Forward, 4f, visuals, 0.1f, float.MaxValue));
        }

        [Test]
        public void Choose_NothingOnRay_ReturnsMinusOne()
        {
            var visuals = new List<Bounds> { new Bounds(new Vector3(2f, 0f, 2f), Vector3.one * 0.5f) };

            Assert.AreEqual(-1, InteractAimSelector.Choose(Forward, 4f, visuals, 0.1f, float.MaxValue));
        }

        [Test]
        public void Choose_BeyondMaxDistance_ReturnsMinusOne()
        {
            var visuals = new List<Bounds> { new Bounds(new Vector3(0f, 0f, 6f), Vector3.one * 0.5f) };

            Assert.AreEqual(-1, InteractAimSelector.Choose(Forward, 4f, visuals, 0.1f, float.MaxValue));
        }

        [Test]
        public void Choose_BehindWall_ReturnsMinusOne()
        {
            var visuals = new List<Bounds> { new Bounds(new Vector3(0f, 0f, 3f), Vector3.one * 0.5f) };

            Assert.AreEqual(-1, InteractAimSelector.Choose(Forward, 4f, visuals, 0.1f, 1.5f));
        }

        [Test]
        public void Choose_SlightlyOffSmallItem_PaddingStillPicksIt()
        {
            var visuals = new List<Bounds> { new Bounds(new Vector3(0.3f, 0f, 2f), Vector3.one * 0.4f) };

            Assert.AreEqual(0, InteractAimSelector.Choose(Forward, 4f, visuals, 0.15f, float.MaxValue));
        }
    }
}

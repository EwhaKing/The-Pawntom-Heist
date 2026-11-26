using System.Collections.Generic;
using NUnit.Framework;
using Pawntom.Settings;

namespace Pawntom.Settings.Tests
{
    public sealed class ResolutionListTests
    {
        private static List<ResolutionOption> Raw()
        {
            // Screen.resolutions 는 주사율마다 같은 크기를 반복해서 준다.
            return new List<ResolutionOption>
            {
                new ResolutionOption(1920, 1080),
                new ResolutionOption(1280, 720),
                new ResolutionOption(1920, 1080),
                new ResolutionOption(640, 480),
                new ResolutionOption(2560, 1440),
                new ResolutionOption(1280, 720),
            };
        }

        [Test]
        public void Build_RemovesDuplicateSizes()
        {
            Assert.AreEqual(3, ResolutionList.Build(Raw()).Count);
        }

        [Test]
        public void Build_DropsTooSmallSizes()
        {
            List<ResolutionOption> built = ResolutionList.Build(Raw());
            Assert.IsFalse(built.Exists(r => r.Width == 640));
        }

        [Test]
        public void Build_SortsAscending()
        {
            List<ResolutionOption> built = ResolutionList.Build(Raw());
            Assert.AreEqual(1280, built[0].Width);
        }

        [Test]
        public void Build_Null_ReturnsEmpty()
        {
            Assert.AreEqual(0, ResolutionList.Build(null).Count);
        }

        [TestCase(1920, 1080, 1)]
        [TestCase(1900, 1000, 1)]
        [TestCase(3840, 2160, 2)]
        [TestCase(100, 100, 0)]
        public void IndexOfClosest_PicksNearestSize(int width, int height, int expected)
        {
            List<ResolutionOption> built = ResolutionList.Build(Raw());
            Assert.AreEqual(expected, ResolutionList.IndexOfClosest(built, width, height));
        }

        [Test]
        public void IndexOfClosest_EmptyList_ReturnsMinusOne()
        {
            Assert.AreEqual(-1, ResolutionList.IndexOfClosest(new List<ResolutionOption>(), 1920, 1080));
        }
    }
}

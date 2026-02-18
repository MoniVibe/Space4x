#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
using NUnit.Framework;
using Space4x.Scenario;

namespace Space4X.Tests
{
    public class Space4XFleetcrawlBuildDeterminismTests
    {
        [Test]
        public void Fleetcrawl_AutoPickDigest_IsDeterministic()
        {
            const uint seed = 9017u;
            const int roomCount = 5;

            var first = Space4XFleetcrawlRoomDirectorSystem.Space4XFleetcrawlBuildDeterminism.SimulateDigest(seed, roomCount);
            var second = Space4XFleetcrawlRoomDirectorSystem.Space4XFleetcrawlBuildDeterminism.SimulateDigest(seed, roomCount);

            Assert.AreEqual(first, second, "Fleetcrawl gate auto-pick digest must be deterministic for fixed seed/room count.");
            Assert.AreEqual(754799518u, first, "Digest constant drifted for canonical fleetcrawl micro run.");
        }
    }
}
#endif

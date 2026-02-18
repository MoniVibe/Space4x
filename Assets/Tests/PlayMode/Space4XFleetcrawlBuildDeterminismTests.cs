#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
using NUnit.Framework;
using Space4x.Scenario;

public class Space4XFleetcrawlBuildDeterminismTests
{
    [Test]
    public void Fleetcrawl_AutoPickDigest_IsDeterministic()
    {
        const uint seed = 9017u;
        const int rooms = 5;
        const uint expectedDigest = 1377985494u;

        var digest = Space4XFleetcrawlRoomDirectorSystem.Space4XFleetcrawlBuildDeterminism.SimulateDigest(seed, rooms);

        Assert.AreEqual(expectedDigest, digest, "Fleetcrawl build digest changed for fixed seed/room progression.");
    }
}
#endif

#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
using NUnit.Framework;
using PureDOTS.Runtime.Math;
using Unity.Mathematics;

namespace Space4X.Tests
{
    public sealed class PureDotsSegmentGeometryTests
    {
        [Test]
        public void ResolveSegmentSphereOcclusion01_ZeroWhenLaneIsClear()
        {
            var occlusion = SegmentGeometry.ResolveSegmentSphereOcclusion01(
                new float3(0f, 0f, 0f),
                new float3(10f, 0f, 0f),
                new float3(0f, 5f, 0f),
                1f,
                0f);

            Assert.AreEqual(0f, occlusion, 1e-4f);
        }

        [Test]
        public void ResolveSegmentSphereOcclusion01_IncreasesWithPathRadius()
        {
            var narrow = SegmentGeometry.ResolveSegmentSphereOcclusion01(
                new float3(0f, 0f, 0f),
                new float3(10f, 0f, 0f),
                new float3(5f, 1f, 0f),
                1f,
                0f);
            var wide = SegmentGeometry.ResolveSegmentSphereOcclusion01(
                new float3(0f, 0f, 0f),
                new float3(10f, 0f, 0f),
                new float3(5f, 1f, 0f),
                1f,
                1f);

            Assert.Greater(wide, narrow);
        }
    }
}
#endif

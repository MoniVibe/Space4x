using Unity.Mathematics;

namespace PureDOTS.Runtime.Math
{
    public static class SegmentGeometry
    {
        public static float DistancePointToSegmentSq(float3 point, float3 start, float3 end, out float t)
        {
            var segment = end - start;
            var lengthSq = math.lengthsq(segment);
            if (lengthSq <= 1e-8f)
            {
                t = 0f;
                return math.lengthsq(point - start);
            }

            t = math.saturate(math.dot(point - start, segment) / lengthSq);
            var closest = start + segment * t;
            return math.lengthsq(point - closest);
        }

        public static float ResolveSegmentSphereOcclusion01(
            float3 start,
            float3 end,
            float3 sphereCenter,
            float sphereRadius,
            float pathRadius = 0f)
        {
            var effectiveRadius = math.max(0f, sphereRadius) + math.max(0f, pathRadius);
            if (effectiveRadius <= 1e-5f)
            {
                return 0f;
            }

            var distanceSq = DistancePointToSegmentSq(sphereCenter, start, end, out _);
            var radiusSq = effectiveRadius * effectiveRadius;
            if (distanceSq >= radiusSq)
            {
                return 0f;
            }

            var distance = math.sqrt(distanceSq);
            return math.saturate(1f - distance / effectiveRadius);
        }
    }
}

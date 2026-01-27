using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

namespace PureDOTS.Runtime.Physics
{
    public struct KinematicSweepResult
    {
        public float3 ResolvedDelta;
        public float3 HitPosition;
        public float3 HitNormal;
        public Entity HitEntity;
        public float HitFraction;
        public byte HasHit;
    }

    public static class KinematicSweepUtility
    {
        private struct ClosestBlockingHitCollector : ICollector<ColliderCastHit>
        {
            public bool EarlyOutOnFirstHit => false;
            public float MaxFraction { get; set; }
            public int NumHits { get; private set; }

            public ColliderCastHit ClosestHit;
            public Entity Self;
            public bool IgnoreTriggerHits;

            public static ClosestBlockingHitCollector Create(Entity self, bool ignoreTriggerHits)
            {
                return new ClosestBlockingHitCollector
                {
                    MaxFraction = 1f,
                    NumHits = 0,
                    ClosestHit = default,
                    Self = self,
                    IgnoreTriggerHits = ignoreTriggerHits
                };
            }

            public bool AddHit(ColliderCastHit hit)
            {
                if (hit.Entity == Self)
                {
                    return false;
                }

                if (IgnoreTriggerHits)
                {
                    var response = hit.Material.CollisionResponse;
                    if (response == CollisionResponsePolicy.RaiseTriggerEvents ||
                        response == CollisionResponsePolicy.None)
                    {
                        return false;
                    }
                }

                if (hit.Fraction < MaxFraction)
                {
                    MaxFraction = hit.Fraction;
                    ClosestHit = hit;
                    NumHits = 1;
                    return true;
                }

                return false;
            }
        }

        public static unsafe bool TryResolveSweep(
            in PhysicsWorldSingleton physicsWorld,
            in PhysicsCollider collider,
            in Entity self,
            in float3 startPosition,
            in quaternion rotation,
            in float3 desiredDelta,
            float skinDistance,
            bool allowSlide,
            bool ignoreTriggerHits,
            out KinematicSweepResult result)
        {
            result = default;
            result.ResolvedDelta = desiredDelta;

            var deltaSq = math.lengthsq(desiredDelta);
            if (deltaSq < 1e-8f)
            {
                return false;
            }

            var input = new ColliderCastInput
            {
                Collider = (Unity.Physics.Collider*)collider.Value.GetUnsafePtr(),
                Start = startPosition,
                End = startPosition + desiredDelta,
                Orientation = rotation
            };

            var collector = ClosestBlockingHitCollector.Create(self, ignoreTriggerHits);
            if (!physicsWorld.CastCollider(input, ref collector) || collector.NumHits == 0)
            {
                return false;
            }

            var hit = collector.ClosestHit;

            var travelDistance = math.sqrt(deltaSq);
            var travelDir = math.normalizesafe(desiredDelta);
            var hitDistance = travelDistance * hit.Fraction;
            var clampedDistance = math.max(0f, hitDistance - math.max(0f, skinDistance));
            var clampedDelta = travelDir * clampedDistance;
            var resolvedDelta = clampedDelta;

            if (allowSlide)
            {
                var remaining = desiredDelta - clampedDelta;
                var normal = hit.SurfaceNormal;
                var slide = remaining - math.dot(remaining, normal) * normal;
                resolvedDelta += slide;
            }

            result.ResolvedDelta = resolvedDelta;
            result.HitEntity = hit.Entity;
            result.HitNormal = hit.SurfaceNormal;
            result.HitFraction = hit.Fraction;
            result.HitPosition = startPosition + travelDir * hitDistance;
            result.HasHit = 1;

            return true;
        }
    }
}

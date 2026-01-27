using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Steering
{
    public struct SteeringContext
    {
        public float3 Position;
        public float3 Velocity;
        public float3 Forward;
        public float MaxSpeed;
        public float MaxAccel;
        public float DeltaTime;
    }

    public struct SteeringOutput
    {
        public float3 DesiredAccel;
        public float3 DesiredForward;
        public float ClosingSpeed;
        public float ConeDot;
        public float PnOmega;
    }

    [BurstCompile]
    public static class SteeringPrimitives
    {
        private const float InvUIntMax = 1f / uint.MaxValue;

        [BurstCompile]
        public static void Seek(ref SteeringContext context, in float3 targetPosition, out float3 result)
        {
            var desiredVelocity = math.normalizesafe(targetPosition - context.Position) * context.MaxSpeed;
            var accel = desiredVelocity - context.Velocity;
            LimitAccel(in accel, context.MaxAccel, out result);
        }

        [BurstCompile]
        public static void Pursuit(ref SteeringContext context, in float3 targetPosition, in float3 targetVelocity, out float3 result)
        {
            PredictTargetPosition(in context.Position, context.MaxSpeed, in targetPosition, in targetVelocity, out var predicted);
            var desiredVelocity = math.normalizesafe(predicted - context.Position) * context.MaxSpeed;
            var accel = desiredVelocity - context.Velocity;
            LimitAccel(in accel, context.MaxAccel, out result);
        }

        [BurstCompile]
        public static void Arrive(ref SteeringContext context, in float3 targetPosition, float slowingRadius, out float3 result)
        {
            var toTarget = targetPosition - context.Position;
            var distance = math.length(toTarget);
            if (distance <= 1e-4f)
            {
                result = float3.zero;
                return;
            }

            var speed = context.MaxSpeed;
            if (slowingRadius > 1e-4f && distance < slowingRadius)
            {
                speed *= math.saturate(distance / slowingRadius);
            }

            var desiredVelocity = (toTarget / distance) * speed;
            var accel = desiredVelocity - context.Velocity;
            LimitAccel(in accel, context.MaxAccel, out result);
        }

        [BurstCompile]
        public static void Evade(ref SteeringContext context, in float3 targetPosition, in float3 targetVelocity, out float3 result)
        {
            PredictTargetPosition(in context.Position, context.MaxSpeed, in targetPosition, in targetVelocity, out var predicted);
            var desiredVelocity = math.normalizesafe(context.Position - predicted) * context.MaxSpeed;
            var accel = desiredVelocity - context.Velocity;
            LimitAccel(in accel, context.MaxAccel, out result);
        }

        [BurstCompile]
        public static void OffsetPursuit(ref SteeringContext context, in float3 targetPosition, in float3 targetVelocity, in float3 offset,
            out float3 result)
        {
            PredictTargetPosition(in context.Position, context.MaxSpeed, in targetPosition, in targetVelocity, out var predicted);
            var desiredVelocity = math.normalizesafe((predicted + offset) - context.Position) * context.MaxSpeed;
            var accel = desiredVelocity - context.Velocity;
            LimitAccel(in accel, context.MaxAccel, out result);
        }

        [BurstCompile]
        public static void Separation(in float3 position, in NativeArray<float3> neighbors, float radius, float strength, out float3 result)
        {
            if (neighbors.Length == 0 || radius <= 0f)
            {
                result = float3.zero;
                return;
            }

            var separation = float3.zero;
            var radiusSq = radius * radius;
            for (int i = 0; i < neighbors.Length; i++)
            {
                var offset = position - neighbors[i];
                var distSq = math.lengthsq(offset);
                if (distSq < 1e-6f || distSq > radiusSq)
                {
                    continue;
                }

                var dist = math.sqrt(distSq);
                var weight = (radius - dist) / radius;
                separation += (offset / dist) * weight;
            }

            if (math.lengthsq(separation) > 1e-6f)
            {
                separation = math.normalizesafe(separation) * strength;
            }

            result = separation;
        }

        [BurstCompile]
        public static void DeterministicOffset2D(in Entity entity, out float2 result)
        {
            DeterministicOffset2D(in entity, 0u, out result);
        }

        [BurstCompile]
        public static void DeterministicOffset2D(in Entity entity, uint salt, out float2 result)
        {
            var seed = math.hash(new uint3((uint)entity.Index, (uint)entity.Version, salt));
            HashToSignedUnitFloat2(seed, out result);
        }

        [BurstCompile]
        public static bool LeadInterceptPoint(
            in float3 targetPosition,
            in float3 targetVelocity,
            in float3 origin,
            float projectileSpeed,
            out float3 interceptPoint,
            out float interceptTime)
        {
            interceptPoint = targetPosition;
            interceptTime = 0f;

            if (projectileSpeed <= 1e-4f)
            {
                return false;
            }

            var toTarget = targetPosition - origin;
            var a = math.dot(targetVelocity, targetVelocity) - projectileSpeed * projectileSpeed;
            var b = 2f * math.dot(toTarget, targetVelocity);
            var c = math.dot(toTarget, toTarget);

            var hasSolution = SolvePositiveQuadratic(a, b, c, out interceptTime);
            if (!hasSolution)
            {
                return false;
            }

            interceptPoint = targetPosition + targetVelocity * interceptTime;
            return true;
        }

        [BurstCompile]
        public static void PN_Accel(in float3 ownPosition, in float3 ownVelocity, in float3 targetPosition, in float3 targetVelocity,
            float navConstantN, out float3 result)
        {
            var toTarget = targetPosition - ownPosition;
            var distanceSq = math.lengthsq(toTarget);
            if (distanceSq <= 1e-6f)
            {
                result = float3.zero;
                return;
            }

            var relativeVelocity = targetVelocity - ownVelocity;
            var distance = math.sqrt(distanceSq);
            var lineOfSight = toTarget / distance;
            var closingSpeed = -math.dot(relativeVelocity, lineOfSight);

            var losRate = math.cross(toTarget, relativeVelocity) / distanceSq;
            var lateralAccel = math.cross(losRate, lineOfSight) * (navConstantN * closingSpeed);

            result = lateralAccel;
        }

        private static void PredictTargetPosition(in float3 origin, float maxSpeed, in float3 targetPosition, in float3 targetVelocity,
            out float3 result)
        {
            var toTarget = targetPosition - origin;
            var distance = math.length(toTarget);
            var speed = math.max(1e-4f, maxSpeed);
            var time = distance / speed;
            result = targetPosition + targetVelocity * time;
        }

        private static void LimitAccel(in float3 accel, float maxAccel, out float3 result)
        {
            if (maxAccel <= 0f)
            {
                result = float3.zero;
                return;
            }

            var magnitudeSq = math.lengthsq(accel);
            if (magnitudeSq <= maxAccel * maxAccel)
            {
                result = accel;
                return;
            }

            result = math.normalize(accel) * maxAccel;
        }

        private static void HashToSignedUnitFloat2(uint seed, out float2 result)
        {
            var hashes = new uint2(seed, seed ^ 0x9E3779B9u);
            var unit = new float2(hashes) * InvUIntMax;
            result = unit * 2f - 1f;
        }

        private static bool SolvePositiveQuadratic(float a, float b, float c, out float t)
        {
            t = 0f;
            if (math.abs(a) < 1e-6f)
            {
                if (math.abs(b) < 1e-6f)
                {
                    return false;
                }

                t = -c / b;
                return t > 0f;
            }

            var discriminant = b * b - 4f * a * c;
            if (discriminant < 0f)
            {
                return false;
            }

            var sqrtDisc = math.sqrt(discriminant);
            var denom = 2f * a;
            var t0 = (-b - sqrtDisc) / denom;
            var t1 = (-b + sqrtDisc) / denom;

            var minPositive = float.MaxValue;
            if (t0 > 0f)
            {
                minPositive = t0;
            }
            if (t1 > 0f && t1 < minPositive)
            {
                minPositive = t1;
            }

            if (minPositive == float.MaxValue)
            {
                return false;
            }

            t = minPositive;
            return true;
        }
    }
}

using PureDOTS.Runtime.Combat;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Telemetry;
using PureDOTS.Systems;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;

namespace PureDOTS.Systems.Combat
{
    /// <summary>
    /// Performs per-tick hazard ray / pseudo-sphere casts and writes avoidance vectors.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(CombatSystemGroup))]
    public partial struct HazardRaycastAvoidanceSystem : ISystem
    {
        private ComponentLookup<HazardDodgeTelemetry> _telemetryLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<PhysicsWorldSingleton>();
            state.RequireForUpdate<HazardRaycastProbe>();
            state.RequireForUpdate<HazardAvoidanceState>();

            _telemetryLookup = state.GetComponentLookup<HazardDodgeTelemetry>(false);
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state) { }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            var physicsWorldSingleton = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
            var collisionWorld = physicsWorldSingleton.PhysicsWorld.CollisionWorld;

            _telemetryLookup.Update(ref state);

            var job = new HazardRaycastJob
            {
                CollisionWorld = collisionWorld,
                CurrentTick = timeState.Tick,
                DeltaTime = timeState.DeltaTime,
                TelemetryLookup = _telemetryLookup
            };

            state.Dependency = job.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        public partial struct HazardRaycastJob : IJobEntity
        {
            [ReadOnly]
            public CollisionWorld CollisionWorld;
            public uint CurrentTick;
            public float DeltaTime;
            [NativeDisableParallelForRestriction]
            public ComponentLookup<HazardDodgeTelemetry> TelemetryLookup;

            void Execute(
                Entity entity,
                ref HazardAvoidanceState avoidanceState,
                ref HazardRaycastState raycastState,
                ref HazardRaycastProbe probe,
                in LocalTransform transform)
            {
                var hasTelemetry = TelemetryLookup.HasComponent(entity);
                RefRW<HazardDodgeTelemetry> telemetryRef = default;
                if (hasTelemetry)
                {
                    telemetryRef = TelemetryLookup.GetRefRW(entity);
                }

                var wasAvoiding = hasTelemetry && telemetryRef.ValueRO.WasAvoidingLastTick != 0;
                var isAvoiding = false;

                if (probe.SampleCount == 0)
                {
                    probe.SampleCount = 1;
                }

                if (probe.CooldownSeconds > 0f && probe.LastSampleTick != 0)
                {
                    var ticksSince = CurrentTick - probe.LastSampleTick;
                    var cooldownTicks = (uint)math.max(1f, probe.CooldownSeconds * 60f);
                    if (ticksSince < cooldownTicks)
                    {
                        DecayUrgency(ref avoidanceState, ref raycastState, ref probe);
                        return;
                    }
                }

                float3 forward = math.mul(transform.Rotation, new float3(0f, 0f, 1f));
                float3 up = math.mul(transform.Rotation, new float3(0f, 1f, 0f));
                float3 right = math.normalizesafe(math.cross(forward, up));

                float3 aggregated = float3.zero;
                float maxUrgency = 0f;
                byte hitCount = 0;
                Entity closestEntity = Entity.Null;
                float closestFraction = 1f;
                float closestDistance = probe.RayLength;

                for (int i = 0; i < probe.SampleCount; i++)
                {
                    float normalizedIndex = probe.SampleCount == 1
                        ? 0f
                        : (i / (float)(probe.SampleCount - 1) - 0.5f);

                    float angle = math.radians(probe.SpreadAngleDeg) * normalizedIndex;
                    float3 dir = math.normalize(math.mul(quaternion.AxisAngle(up, angle), forward));

                    float3 origin = transform.Position;
                    if (probe.SphereRadius > 1e-4f)
                    {
                        origin += right * normalizedIndex * probe.SphereRadius;
                    }

                    var input = new RaycastInput
                    {
                        Start = origin,
                        End = origin + dir * probe.RayLength,
                        Filter = probe.CollisionFilter
                    };

                    if (CollisionWorld.CastRay(input, out var hit))
                    {
                        float weight = 1f - hit.Fraction;
                        float3 away = math.normalizesafe(origin - hit.Position);
                        if (math.lengthsq(away) < 1e-5f)
                        {
                            away = -dir;
                        }

                        aggregated += away * weight;
                        maxUrgency = math.max(maxUrgency, weight);
                        hitCount++;

                        if (hit.Fraction < closestFraction)
                        {
                            closestFraction = hit.Fraction;
                            closestDistance = hit.Fraction * probe.RayLength;
                            closestEntity = hit.Entity;
                        }
                    }
                }

                if (hitCount > 0)
                {
                    isAvoiding = true;
                    avoidanceState.CurrentAdjustment = math.normalizesafe(aggregated);
                    avoidanceState.AvoidanceUrgency = math.clamp(maxUrgency, 0f, 1f);
                    avoidanceState.AvoidingEntity = closestEntity;

                    raycastState.LastAvoidanceDirection = avoidanceState.CurrentAdjustment;
                    raycastState.LastHitDistance = closestDistance;
                    raycastState.HitCount = hitCount;
                    raycastState.LastHitTick = CurrentTick;

                    probe.LastSampleTick = CurrentTick;

                    if (hasTelemetry)
                    {
                        ref var telemetryValue = ref telemetryRef.ValueRW;
                        telemetryValue.RaycastHitsInterval += hitCount;
                        telemetryValue.DodgeDistanceMmInterval += BehaviorTelemetryMath.ToMilli(math.max(0f, probe.RayLength - closestDistance));
                        if (avoidanceState.AvoidanceUrgency >= 0.75f)
                        {
                            telemetryValue.HighUrgencyTicksInterval++;
                        }
                    }
                }
                else
                {
                    DecayUrgency(ref avoidanceState, ref raycastState, ref probe);
                }

                if (hasTelemetry)
                {
                    ref var telemetryValue = ref telemetryRef.ValueRW;
                    if (isAvoiding != wasAvoiding)
                    {
                        telemetryValue.AvoidanceTransitionsInterval++;
                    }

                    telemetryValue.WasAvoidingLastTick = (byte)(isAvoiding ? 1 : 0);
                }
            }

            private void DecayUrgency(
                ref HazardAvoidanceState avoidanceState,
                ref HazardRaycastState raycastState,
                ref HazardRaycastProbe probe)
            {
                if (probe.UrgencyFalloff <= 0f)
                {
                    avoidanceState.CurrentAdjustment = float3.zero;
                    avoidanceState.AvoidanceUrgency = 0f;
                    avoidanceState.AvoidingEntity = Entity.Null;
                    raycastState.HitCount = 0;
                    return;
                }

                float decay = math.clamp(DeltaTime / math.max(1e-3f, probe.UrgencyFalloff), 0f, 1f);
                avoidanceState.AvoidanceUrgency = math.max(0f, avoidanceState.AvoidanceUrgency - decay);
                if (avoidanceState.AvoidanceUrgency <= 1e-4f)
                {
                    avoidanceState.CurrentAdjustment = float3.zero;
                    avoidanceState.AvoidingEntity = Entity.Null;
                }

                raycastState.HitCount = 0;
            }
        }
    }
}

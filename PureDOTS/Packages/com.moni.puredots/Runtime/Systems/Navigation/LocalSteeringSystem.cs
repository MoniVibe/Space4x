using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Navigation;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems.Navigation
{
    /// <summary>
    /// Computes local steering forces (separation, avoidance, cohesion) for agents.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(HotPathSystemGroup))]
    [UpdateAfter(typeof(FlowFieldFollowSystem))]
    public partial struct LocalSteeringSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<FlowFieldConfig>();
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var config = SystemAPI.GetSingleton<FlowFieldConfig>();

            var job = new ComputeSteeringJob
            {
                SeparationWeight = config.SeparationWeight,
                AvoidanceWeight = config.AvoidanceWeight,
                CohesionWeight = config.CohesionWeight
            };

            state.Dependency = job.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        public partial struct ComputeSteeringJob : IJobEntity
        {
            public float SeparationWeight;
            public float AvoidanceWeight;
            public float CohesionWeight;

            public void Execute(
                ref SteeringState steering,
                ref FlowFieldState flowState,
                in SpatialSensor sensor,
                in DynamicBuffer<SpatialSensorEntity> sensorEntities,
                in LocalTransform transform)
            {
                if (sensorEntities.Length == 0)
                {
                    steering.SeparationVector = float2.zero;
                    steering.AvoidanceVector = float2.zero;
                    steering.CohesionVector = float2.zero;
                    steering.BlendedHeading = float2.zero;
                    return;
                }

                var agentPos = new float2(transform.Position.xz);
                var separation = float2.zero;
                var avoidance = float2.zero;
                var cohesion = float2.zero;
                int neighborCount = 0;

                for (int i = 0; i < sensorEntities.Length; i++)
                {
                    var entity = sensorEntities[i];
                    var otherPos = new float2(entity.Position.xz);
                    var diff = agentPos - otherPos;
                    var distSq = math.lengthsq(diff);

                    if (distSq < 0.01f)
                    {
                        continue;
                    }

                    var dist = math.sqrt(distSq);
                    var direction = diff / dist;

                    // Separation (avoid neighbors)
                    if (entity.EntityType == 1) // Neighbor
                    {
                        var strength = 1f / (distSq + 0.1f);
                        separation += direction * strength;
                        neighborCount++;
                    }

                    // Avoidance (obstacles/threats)
                    if (entity.EntityType == 2) // Threat
                    {
                        var strength = 1f / (distSq + 0.1f);
                        avoidance += direction * strength;
                    }

                    // Cohesion (move toward center of neighbors)
                    if (entity.EntityType == 1) // Neighbor
                    {
                        cohesion += otherPos;
                    }
                }

                // Normalize separation
                if (math.lengthsq(separation) > 0.01f)
                {
                    separation = math.normalize(separation) * SeparationWeight;
                }

                // Normalize avoidance
                if (math.lengthsq(avoidance) > 0.01f)
                {
                    avoidance = math.normalize(avoidance) * AvoidanceWeight;
                }

                // Compute cohesion vector
                if (neighborCount > 0)
                {
                    var center = cohesion / neighborCount;
                    var cohesionDir = center - agentPos;
                    if (math.lengthsq(cohesionDir) > 0.01f)
                    {
                        cohesion = math.normalize(cohesionDir) * CohesionWeight;
                    }
                    else
                    {
                        cohesion = float2.zero;
                    }
                }

                // Blend flow field direction with steering
                var blended = separation + avoidance + cohesion;
                if (math.lengthsq(blended) > 0.01f)
                {
                    blended = math.normalize(blended);
                }
                else
                {
                    blended = float2.zero;
                }

                steering.SeparationVector = separation;
                steering.AvoidanceVector = avoidance;
                steering.CohesionVector = cohesion;
                steering.BlendedHeading = blended;
            }
        }
    }
}


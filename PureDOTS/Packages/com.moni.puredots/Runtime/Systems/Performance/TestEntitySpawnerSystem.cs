using PureDOTS.Runtime.Rendering;
using PureDOTS.Runtime.Telemetry;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace PureDOTS.Runtime.Systems.Performance
{
    /// <summary>
    /// Spawns test entities with LOD/aggregate components for sanity check scenarios.
    /// This is a temporary system for validating the performance infrastructure.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct TestEntitySpawnerSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            // Only run once
            state.Enabled = false;
        }

        public void OnUpdate(ref SystemState state)
        {
            // This system is disabled by default
            // Enable it manually or via command for test scenarios
        }

        /// <summary>
        /// Spawns test entities with LOD components for the mini LOD scenario.
        /// </summary>
        public static void SpawnLODTestEntities(ref SystemState state, int count, float3 center, float radius)
        {
            var random = new Unity.Mathematics.Random(12345u);
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            for (int i = 0; i < count; i++)
            {
                var entity = state.EntityManager.CreateEntity();

                // Random position in sphere
                var angle1 = random.NextFloat(0f, math.PI * 2f);
                var angle2 = random.NextFloat(0f, math.PI);
                var r = random.NextFloat(0f, radius);
                var pos = center + new float3(
                    r * math.sin(angle2) * math.cos(angle1),
                    r * math.sin(angle2) * math.sin(angle1),
                    r * math.cos(angle2)
                );

                // Add transform
                state.EntityManager.AddComponentData(entity, LocalTransform.FromPosition(pos));

                // Add LOD components
                state.EntityManager.AddComponentData(entity, new RenderLODData
                {
                    CameraDistance = 0f,  // Will be updated by camera system
                    ImportanceScore = random.NextFloat(0f, 1f),
                    RecommendedLOD = 0,
                    LastUpdateTick = 0
                });

                state.EntityManager.AddComponentData(entity, new RenderCullable
                {
                    CullDistance = 200f,
                    Priority = (byte)random.NextInt(0, 256)
                });

                // Add sample index (for density control)
                var sampleIndex = RenderLODHelpers.CalculateSampleIndex(entity.Index, 100);
                state.EntityManager.AddComponentData(entity, new RenderSampleIndex
                {
                    SampleIndex = sampleIndex,
                    SampleModulus = 100,
                    ShouldRender = 1
                });
            }

            Debug.Log($"[TestEntitySpawner] Spawned {count} LOD test entities");
        }

        /// <summary>
        /// Spawns aggregate entities with members for the mini aggregate scenario.
        /// </summary>
        public static void SpawnAggregateTestEntities(
            ref SystemState state, 
            int aggregateCount, 
            int membersPerAggregate,
            float3 center,
            float aggregateRadius)
        {
            var random = new Unity.Mathematics.Random(54321u);
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            for (int aggIdx = 0; aggIdx < aggregateCount; aggIdx++)
            {
                // Create aggregate entity
                var aggregateEntity = state.EntityManager.CreateEntity();

                // Random aggregate position
                var angle = random.NextFloat(0f, math.PI * 2f);
                var r = random.NextFloat(0f, aggregateRadius);
                var aggPos = center + new float3(
                    r * math.cos(angle),
                    0f,
                    r * math.sin(angle)
                );

                state.EntityManager.AddComponentData(aggregateEntity, LocalTransform.FromPosition(aggPos));

                // Add aggregate components
                state.EntityManager.AddComponent<AggregateTag>(aggregateEntity);
                state.EntityManager.AddComponentData(aggregateEntity, new AggregateState
                {
                    MemberCount = 0,
                    AveragePosition = aggPos,
                    BoundsMin = aggPos - 10f,
                    BoundsMax = aggPos + 10f,
                    TotalHealth = 0f,
                    AverageMorale = 0f,
                    TotalStrength = 0f,
                    LastAggregationTick = 0,
                    AggregationInterval = 20
                });

                state.EntityManager.AddComponentData(aggregateEntity, new AggregateRenderSummary
                {
                    MemberCount = 0,
                    AveragePosition = aggPos,
                    BoundsCenter = aggPos,
                    BoundsRadius = 10f,
                    TotalHealth = 0f,
                    AverageMorale = 0f,
                    TotalStrength = 0f,
                    DominantTypeIndex = (byte)random.NextInt(0, 10),
                    LastUpdateTick = 0
                });

                state.EntityManager.AddComponentData(aggregateEntity, new AggregateRenderConfig
                {
                    AggregateRenderDistance = 50f,
                    MinMembersForMarker = 10,
                    MaxIndividualRender = 50,
                    UpdateInterval = 20
                });

                // Create member buffer
                var memberBuffer = state.EntityManager.AddBuffer<AggregateMemberElement>(aggregateEntity);

                // Spawn members around aggregate
                for (int memIdx = 0; memIdx < membersPerAggregate; memIdx++)
                {
                    var memberEntity = state.EntityManager.CreateEntity();

                    // Random position around aggregate
                    var memAngle = random.NextFloat(0f, math.PI * 2f);
                    var memDist = random.NextFloat(0f, 10f);
                    var memPos = aggPos + new float3(
                        memDist * math.cos(memAngle),
                        0f,
                        memDist * math.sin(memAngle)
                    );

                    state.EntityManager.AddComponentData(memberEntity, LocalTransform.FromPosition(memPos));

                    // Add membership
                    state.EntityManager.AddComponentData(memberEntity, new AggregateMembership
                    {
                        AggregateEntity = aggregateEntity,
                        MemberIndex = (byte)memIdx,
                        Flags = AggregateMembership.FlagActive
                    });

                    // Add to aggregate buffer
                    memberBuffer.Add(new AggregateMemberElement
                    {
                        MemberEntity = memberEntity,
                        StrengthContribution = random.NextFloat(10f, 100f),
                        Health = random.NextFloat(50f, 100f)
                    });
                }

                // Update aggregate state with initial member count
                var aggState = state.EntityManager.GetComponentData<AggregateState>(aggregateEntity);
                aggState.MemberCount = membersPerAggregate;
                state.EntityManager.SetComponentData(aggregateEntity, aggState);

                var aggSummary = state.EntityManager.GetComponentData<AggregateRenderSummary>(aggregateEntity);
                aggSummary.MemberCount = membersPerAggregate;
                state.EntityManager.SetComponentData(aggregateEntity, aggSummary);
            }

            Debug.Log($"[TestEntitySpawner] Spawned {aggregateCount} aggregates with {membersPerAggregate} members each");
        }
    }
}

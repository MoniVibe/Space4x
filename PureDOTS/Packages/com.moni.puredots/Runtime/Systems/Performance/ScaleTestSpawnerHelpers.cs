using PureDOTS.Runtime.Rendering;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Runtime.Systems.Performance
{
    /// <summary>
    /// Reusable helpers for spawning test entities with LOD/aggregate components.
    /// Used by TestEntitySpawnerSystem and scenario-driven spawners.
    /// </summary>
    public static class ScaleTestSpawnerHelpers
    {
        /// <summary>
        /// Spawns a batch of test entities with LOD components.
        /// </summary>
        public static void SpawnLODTestEntities(
            EntityManager entityManager,
            int count,
            float3 center,
            float radius,
            uint seed = 12345u)
        {
            var random = new Unity.Mathematics.Random(seed);

            for (int i = 0; i < count; i++)
            {
                var entity = entityManager.CreateEntity();

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
                entityManager.AddComponentData(entity, LocalTransform.FromPosition(pos));

                // Add LOD components
                AddLODComponents(entityManager, entity, random);
            }
        }

        /// <summary>
        /// Adds LOD components to an existing entity.
        /// </summary>
        public static void AddLODComponents(EntityManager entityManager, Entity entity, Unity.Mathematics.Random random)
        {
            entityManager.AddComponentData(entity, new RenderLODData
            {
                CameraDistance = 0f,
                ImportanceScore = random.NextFloat(0f, 1f),
                RecommendedLOD = 0,
                LastUpdateTick = 0
            });

            entityManager.AddComponentData(entity, new RenderCullable
            {
                CullDistance = 200f,
                Priority = (byte)random.NextInt(0, 256)
            });

            var sampleIndex = RenderLODHelpers.CalculateSampleIndex(entity.Index, 100);
            entityManager.AddComponentData(entity, new RenderSampleIndex
            {
                SampleIndex = sampleIndex,
                SampleModulus = 100,
                ShouldRender = 1
            });
        }

        /// <summary>
        /// Adds LOD components to an existing entity using ECB.
        /// </summary>
        public static void AddLODComponents(EntityCommandBuffer ecb, Entity entity, ref Unity.Mathematics.Random random)
        {
            ecb.AddComponent(entity, new RenderLODData
            {
                CameraDistance = 0f,
                ImportanceScore = random.NextFloat(0f, 1f),
                RecommendedLOD = 0,
                LastUpdateTick = 0
            });

            ecb.AddComponent(entity, new RenderCullable
            {
                CullDistance = 200f,
                Priority = (byte)random.NextInt(0, 256)
            });

            var sampleIndex = RenderLODHelpers.CalculateSampleIndex(entity.Index, 100);
            ecb.AddComponent(entity, new RenderSampleIndex
            {
                SampleIndex = sampleIndex,
                SampleModulus = 100,
                ShouldRender = 1
            });
        }

        /// <summary>
        /// Spawns aggregate entities with members.
        /// </summary>
        public static void SpawnAggregateTestEntities(
            EntityManager entityManager,
            int aggregateCount,
            int membersPerAggregate,
            float3 center,
            float aggregateRadius,
            uint seed = 54321u)
        {
            var random = new Unity.Mathematics.Random(seed);

            for (int aggIdx = 0; aggIdx < aggregateCount; aggIdx++)
            {
                // Create aggregate entity
                var aggregateEntity = entityManager.CreateEntity();

                // Random aggregate position
                var angle = random.NextFloat(0f, math.PI * 2f);
                var r = random.NextFloat(0f, aggregateRadius);
                var aggPos = center + new float3(
                    r * math.cos(angle),
                    0f,
                    r * math.sin(angle)
                );

                entityManager.AddComponentData(aggregateEntity, LocalTransform.FromPosition(aggPos));

                // Add aggregate components
                AddAggregateComponents(entityManager, aggregateEntity, aggPos, membersPerAggregate, ref random);

                // Create member buffer
                var memberBuffer = entityManager.GetBuffer<AggregateMemberElement>(aggregateEntity);

                // Spawn members around aggregate
                for (int memIdx = 0; memIdx < membersPerAggregate; memIdx++)
                {
                    var memberEntity = entityManager.CreateEntity();

                    // Random position around aggregate
                    var memAngle = random.NextFloat(0f, math.PI * 2f);
                    var memDist = random.NextFloat(0f, 10f);
                    var memPos = aggPos + new float3(
                        memDist * math.cos(memAngle),
                        0f,
                        memDist * math.sin(memAngle)
                    );

                    entityManager.AddComponentData(memberEntity, LocalTransform.FromPosition(memPos));

                    // Add membership
                    entityManager.AddComponentData(memberEntity, new AggregateMembership
                    {
                        AggregateEntity = aggregateEntity,
                        MemberIndex = (byte)(memIdx % 256),
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
            }
        }

        /// <summary>
        /// Adds aggregate components to an entity.
        /// </summary>
        public static void AddAggregateComponents(
            EntityManager entityManager,
            Entity entity,
            float3 position,
            int memberCount,
            ref Unity.Mathematics.Random random)
        {
            entityManager.AddComponent<AggregateTag>(entity);

            entityManager.AddComponentData(entity, new AggregateState
            {
                MemberCount = memberCount,
                AveragePosition = position,
                BoundsMin = position - 10f,
                BoundsMax = position + 10f,
                TotalHealth = 0f,
                AverageMorale = 0f,
                TotalStrength = 0f,
                LastAggregationTick = 0,
                AggregationInterval = 20
            });

            entityManager.AddComponentData(entity, new AggregateRenderSummary
            {
                MemberCount = memberCount,
                AveragePosition = position,
                BoundsCenter = position,
                BoundsRadius = 10f,
                TotalHealth = 0f,
                AverageMorale = 0f,
                TotalStrength = 0f,
                DominantTypeIndex = (byte)random.NextInt(0, 10),
                LastUpdateTick = 0
            });

            entityManager.AddComponentData(entity, new AggregateRenderConfig
            {
                AggregateRenderDistance = 50f,
                MinMembersForMarker = 10,
                MaxIndividualRender = 50,
                UpdateInterval = 20
            });

            entityManager.AddBuffer<AggregateMemberElement>(entity);
        }

        /// <summary>
        /// Adds aggregate membership to an existing entity.
        /// </summary>
        public static void AddAggregateMembership(
            EntityManager entityManager,
            Entity memberEntity,
            Entity aggregateEntity,
            byte memberIndex)
        {
            entityManager.AddComponentData(memberEntity, new AggregateMembership
            {
                AggregateEntity = aggregateEntity,
                MemberIndex = memberIndex,
                Flags = AggregateMembership.FlagActive
            });
        }
    }
}


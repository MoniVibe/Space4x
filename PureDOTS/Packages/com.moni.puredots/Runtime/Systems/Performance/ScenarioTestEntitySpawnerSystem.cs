using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Modularity;
using PureDOTS.Runtime.Rendering;
using PureDOTS.Runtime.Scenarios;
using PureDOTS.Runtime.Telemetry;
using PureDOTS.Systems;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace PureDOTS.Runtime.Systems.Performance
{
    /// <summary>
    /// Spawns test entities based on ScenarioEntityCountElement for scale test scenarios.
    /// Handles special registry IDs like "registry.test_entity", "registry.aggregate", "registry.aggregate_member".
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(SceneSpawnSystem))]
    public partial struct ScenarioTestEntitySpawnerSystem : ISystem
    {
        private bool _hasProcessed;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ScenarioInfo>();
            _hasProcessed = false;
        }

        public void OnUpdate(ref SystemState state)
        {
            if (_hasProcessed)
            {
                return;
            }

            // Find scenario entity with counts
            Entity scenarioEntity = Entity.Null;
            foreach (var (info, entity) in SystemAPI.Query<RefRO<ScenarioInfo>>().WithEntityAccess())
            {
                scenarioEntity = entity;
                break;
            }

            if (scenarioEntity == Entity.Null)
            {
                return;
            }

            if (!state.EntityManager.HasBuffer<ScenarioEntityCountElement>(scenarioEntity))
            {
                _hasProcessed = true;
                return;
            }

            var counts = state.EntityManager.GetBuffer<ScenarioEntityCountElement>(scenarioEntity);
            var scenarioInfo = state.EntityManager.GetComponentData<ScenarioInfo>(scenarioEntity);

            // Get or create metrics config
            EnsureMetricsConfig(ref state);

            // Process each entity count
            var random = new Unity.Mathematics.Random(scenarioInfo.Seed);

            for (int i = 0; i < counts.Length; i++)
            {
                var entry = counts[i];
                var registryId = entry.RegistryId.ToString();

                if (registryId == "registry.test_entity")
                {
                    SpawnTestEntities(ref state, entry.Count, ref random);
                }
                else if (registryId == "registry.blank")
                {
                    SpawnBlankEntities(ref state, entry.Count, ref random, includeNeeds: false, includeRelations: false, includeProfile: false);
                }
                else if (registryId == "registry.blank_needs")
                {
                    SpawnBlankEntities(ref state, entry.Count, ref random, includeNeeds: true, includeRelations: false, includeProfile: false);
                }
                else if (registryId == "registry.blank_relations")
                {
                    SpawnBlankEntities(ref state, entry.Count, ref random, includeNeeds: false, includeRelations: true, includeProfile: false);
                }
                else if (registryId == "registry.blank_profile")
                {
                    SpawnBlankEntities(ref state, entry.Count, ref random, includeNeeds: false, includeRelations: false, includeProfile: true);
                }
                else if (registryId == "registry.blank_agency")
                {
                    SpawnBlankEntities(ref state, entry.Count, ref random, includeNeeds: false, includeRelations: false, includeProfile: false, includeAgency: true);
                }
                else if (registryId == "registry.blank_all")
                {
                    SpawnBlankEntities(ref state, entry.Count, ref random, includeNeeds: true, includeRelations: true, includeProfile: true, includeAgency: true);
                }
                else if (registryId == "registry.aggregate")
                {
                    SpawnAggregates(ref state, entry.Count, ref random);
                }
                else if (registryId == "registry.aggregate_member")
                {
                    // Members are spawned with aggregates, skip
                    continue;
                }
                // Other registry IDs are handled by game-specific spawners
            }

            _hasProcessed = true;
            Debug.Log($"[ScenarioTestEntitySpawner] Processed scenario: {scenarioInfo.ScenarioId}");
        }

        private void SpawnBlankEntities(
            ref SystemState state,
            int count,
            ref Unity.Mathematics.Random random,
            bool includeNeeds,
            bool includeRelations,
            bool includeProfile,
            bool includeAgency = false)
        {
            var center = float3.zero;
            var radius = 25f;

            for (int i = 0; i < count; i++)
            {
                var entity = state.EntityManager.CreateEntity();

                // Random position in a disk (XZ)
                var angle = random.NextFloat(0f, math.PI * 2f);
                var r = random.NextFloat(0f, radius);
                var pos = center + new float3(r * math.cos(angle), 0f, r * math.sin(angle));

                state.EntityManager.AddComponentData(entity, LocalTransform.FromPosition(pos));

                if (includeNeeds)
                {
                    state.EntityManager.AddComponent<NeedsModuleTag>(entity);
                }

                if (includeRelations)
                {
                    state.EntityManager.AddComponent<RelationsModuleTag>(entity);
                }

                if (includeProfile)
                {
                    state.EntityManager.AddComponent<ProfileModuleTag>(entity);
                }

                if (includeAgency)
                {
                    state.EntityManager.AddComponent<AgencyModuleTag>(entity);
                }
            }
        }

        private void SpawnTestEntities(ref SystemState state, int count, ref Unity.Mathematics.Random random)
        {
            Debug.Log($"[ScenarioTestEntitySpawner] Spawning {count} test entities with LOD components");

            var center = float3.zero;
            var radius = 100f;

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

                state.EntityManager.AddComponentData(entity, LocalTransform.FromPosition(pos));
                ScaleTestSpawnerHelpers.AddLODComponents(state.EntityManager, entity, random);
            }
        }

        private void SpawnAggregates(ref SystemState state, int count, ref Unity.Mathematics.Random random)
        {
            // Look for member count in scenario
            int membersPerAggregate = 40; // Default

            Debug.Log($"[ScenarioTestEntitySpawner] Spawning {count} aggregates with {membersPerAggregate} members each");

            var center = float3.zero;
            var aggregateRadius = 200f;

            for (int aggIdx = 0; aggIdx < count; aggIdx++)
            {
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
                ScaleTestSpawnerHelpers.AddAggregateComponents(
                    state.EntityManager, aggregateEntity, aggPos, membersPerAggregate, ref random);

                var memberBuffer = state.EntityManager.GetBuffer<AggregateMemberElement>(aggregateEntity);

                // Spawn members
                for (int memIdx = 0; memIdx < membersPerAggregate; memIdx++)
                {
                    var memberEntity = state.EntityManager.CreateEntity();

                    var memAngle = random.NextFloat(0f, math.PI * 2f);
                    var memDist = random.NextFloat(0f, 10f);
                    var memPos = aggPos + new float3(
                        memDist * math.cos(memAngle),
                        0f,
                        memDist * math.sin(memAngle)
                    );

                    state.EntityManager.AddComponentData(memberEntity, LocalTransform.FromPosition(memPos));

                    state.EntityManager.AddComponentData(memberEntity, new AggregateMembership
                    {
                        AggregateEntity = aggregateEntity,
                        MemberIndex = (byte)(memIdx % 256),
                        Flags = AggregateMembership.FlagActive
                    });

                    memberBuffer.Add(new AggregateMemberElement
                    {
                        MemberEntity = memberEntity,
                        StrengthContribution = random.NextFloat(10f, 100f),
                        Health = random.NextFloat(50f, 100f)
                    });
                }
            }
        }

        private void EnsureMetricsConfig(ref SystemState state)
        {
            // Check if ScaleTestMetricsConfig already exists
            if (SystemAPI.TryGetSingleton<ScaleTestMetricsConfig>(out _))
            {
                return;
            }

            // Create default config
            var configEntity = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponentData(configEntity, new ScaleTestMetricsConfig
            {
                SampleInterval = 10,
                LogInterval = 50,
                CollectSystemTimings = 1,
                CollectMemoryStats = 1,
                EnableLODDebug = 1,
                EnableAggregateDebug = 1,
                TargetTickTimeMs = 16.67f,
                TargetMemoryMB = 2048f
            });

            Debug.Log("[ScenarioTestEntitySpawner] Created default ScaleTestMetricsConfig");
        }
    }
}

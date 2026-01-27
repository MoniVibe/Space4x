using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Villagers;
using PureDOTS.Systems;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace PureDOTS.Runtime.Systems.Villagers
{
    /// <summary>
    /// Compatibility system that migrates old VillagerBelief components to VillagerBeliefOptimized.
    /// This runs once per entity during initialization to support transitional compatibility.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct VillagerBeliefMigrationSystem : ISystem
    {
        private EntityQuery _migrationQuery;

        public void OnCreate(ref SystemState state)
        {
            // Query for entities with old VillagerBelief but not new VillagerBeliefOptimized
            _migrationQuery = state.GetEntityQuery(new EntityQueryBuilder(Allocator.Temp)
                .WithAll<VillagerBelief>()
                .WithNone<VillagerBeliefOptimized>()
                .WithAll<VillagerId>());

            state.RequireForUpdate(_migrationQuery);
        }

        public void OnUpdate(ref SystemState state)
        {
            if (_migrationQuery.IsEmpty)
            {
                return;
            }

            var ecb = new EntityCommandBuffer(Allocator.Temp);
            int migratedCount = 0;

            foreach (var (belief, entity) in SystemAPI.Query<RefRO<VillagerBelief>>()
                .WithNone<VillagerBeliefOptimized>()
                .WithAll<VillagerId>()
                .WithEntityAccess())
            {
                // Convert FixedString deity ID to index
                // For now, use a simple hash-based index (actual implementation would use a catalog)
                var deityIndex = (byte)(belief.ValueRO.PrimaryDeityId.GetHashCode() % 256);

                var optimized = new VillagerBeliefOptimized
                {
                    PrimaryDeityIndex = deityIndex,
                    Faith = (byte)(belief.ValueRO.Faith * 255f),
                    WorshipProgress = (byte)(belief.ValueRO.WorshipProgress * 255f),
                    Flags = VillagerBeliefFlags.None,
                    LastUpdateTick = 0
                };

                ecb.AddComponent(entity, optimized);
                migratedCount++;
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();

            if (migratedCount > 0)
            {
                Debug.Log($"[VillagerBeliefMigration] Migrated {migratedCount} villagers to VillagerBeliefOptimized");
            }
        }
    }

    /// <summary>
    /// System that adds LOD components to villagers that don't have them.
    /// Runs during initialization to ensure all villagers have LOD support.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(VillagerBeliefMigrationSystem))]
    public partial struct VillagerLODInitializationSystem : ISystem
    {
        private EntityQuery _lodQuery;

        public void OnCreate(ref SystemState state)
        {
            // Query for villagers without LOD components
            _lodQuery = state.GetEntityQuery(new EntityQueryBuilder(Allocator.Temp)
                .WithAll<VillagerId>()
                .WithNone<PureDOTS.Runtime.Rendering.RenderLODData>());

            state.RequireForUpdate(_lodQuery);
        }

        public void OnUpdate(ref SystemState state)
        {
            if (_lodQuery.IsEmpty)
            {
                return;
            }

            int addedCount = 0;
            using var entities = _lodQuery.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < entities.Length; i++)
            {
                VillagerLODHelpers.AddLODComponents(state.EntityManager, entities[i]);
                addedCount++;
            }

            if (addedCount > 0)
            {
                Debug.Log($"[VillagerLODInit] Added LOD components to {addedCount} villagers");
            }
        }
    }
}


using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Ensures VillagerFlags exists on all villagers; legacy tag syncing can be enabled with VILLAGER_LEGACY_TAG_MIGRATION.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(VillagerSystemGroup), OrderFirst = true)]
    public partial struct VillagerFlagsMigrationSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<VillagerId>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.TempJob);

#if VILLAGER_LEGACY_TAG_MIGRATION
#pragma warning disable CS0618
            // Legacy path kept for backward compatibility; enable the define to mirror legacy tags onto the packed flags.
            foreach (var (id, entity) in SystemAPI.Query<RefRO<VillagerId>>()
                         .WithNone<VillagerFlags>()
                         .WithEntityAccess())
            {
                var flags = new VillagerFlags();

                if (state.EntityManager.HasComponent<VillagerDeadTag>(entity))
                {
                    flags.IsDead = true;
                }
                if (state.EntityManager.HasComponent<VillagerSelectedTag>(entity))
                {
                    flags.IsSelected = true;
                }
                if (state.EntityManager.HasComponent<VillagerHighlightedTag>(entity))
                {
                    flags.IsHighlighted = true;
                }
                if (state.EntityManager.HasComponent<VillagerInCombatTag>(entity))
                {
                    flags.IsInCombat = true;
                }
                if (state.EntityManager.HasComponent<VillagerCarryingTag>(entity))
                {
                    flags.IsCarrying = true;
                }

                ecb.AddComponent(entity, flags);
            }

            foreach (var (flags, entity) in SystemAPI.Query<RefRO<VillagerFlags>>().WithEntityAccess())
            {
                var flagsValue = flags.ValueRO;

                if (flagsValue.IsDead)
                {
                    if (!state.EntityManager.HasComponent<VillagerDeadTag>(entity))
                    {
                        ecb.AddComponent<VillagerDeadTag>(entity);
                    }
                }
                else if (state.EntityManager.HasComponent<VillagerDeadTag>(entity))
                {
                    ecb.RemoveComponent<VillagerDeadTag>(entity);
                }
            }
#pragma warning restore CS0618
#else
            // Default path: ensure the packed flags component exists so downstream systems rely solely on VillagerFlags.
            foreach (var (id, entity) in SystemAPI.Query<RefRO<VillagerId>>()
                         .WithNone<VillagerFlags>()
                         .WithEntityAccess())
            {
                ecb.AddComponent(entity, new VillagerFlags());
            }
#endif

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}

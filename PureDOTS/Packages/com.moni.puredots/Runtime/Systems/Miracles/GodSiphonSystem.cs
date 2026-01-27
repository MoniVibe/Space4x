using PureDOTS.Runtime;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Miracles;
using PureDOTS.Runtime.Resource;
using PureDOTS.Runtime.Time;
using PureDOTS.Runtime.Village;
using PureDOTS.Runtime.Platform;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Miracles
{
    /// <summary>
    /// Processes siphon actions and transfers resources from targets to GodPool.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(MiracleEffectSystemGroup))]
    [UpdateAfter(typeof(MiracleSystem))]
    public partial struct GodSiphonSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<GodPool>();
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

            // Find miracle cast events with Siphon miracle
            // Check if any entities have MiracleCastEvent buffer
            bool hasEvents = false;
            foreach (var _ in SystemAPI.Query<DynamicBuffer<MiracleCastEvent>>())
            {
                hasEvents = true;
                break;
            }
            if (!hasEvents)
            {
                return;
            }

            var godPoolEntity = SystemAPI.GetSingletonEntity<GodPool>();
            var godPool = SystemAPI.GetComponent<GodPool>(godPoolEntity);
            var ecb = new EntityCommandBuffer(Allocator.TempJob);

            foreach (var events in SystemAPI.Query<DynamicBuffer<MiracleCastEvent>>())
            {
                for (int i = 0; i < events.Length; i++)
                {
                    var castEvent = events[i];
                    // Process any non-standard miracle as Siphon (for now)
                    var miracleId = (MiracleId)castEvent.MiracleId;
                    if (miracleId != MiracleId.None && 
                        miracleId != MiracleId.Rain && 
                        miracleId != MiracleId.TemporalVeil && 
                        miracleId != MiracleId.Fire && 
                        miracleId != MiracleId.Heal)
                    {
                        ProcessSiphon(ref state, castEvent, ref godPool, ref ecb);
                    }
                }
            }

            ecb.SetComponent(godPoolEntity, godPool);
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        [BurstCompile]
        private void ProcessSiphon(ref SystemState state, MiracleCastEvent castEvent, ref GodPool godPool, ref EntityCommandBuffer ecb)
        {
            if (castEvent.TargetEntity == Entity.Null || !state.EntityManager.Exists(castEvent.TargetEntity))
            {
                return;
            }

            float siphonAmount = 10f; // Units to siphon per cast

            // Try to siphon from ResourceDeposit
            if (state.EntityManager.HasComponent<ResourceDeposit>(castEvent.TargetEntity))
            {
                var deposit = state.EntityManager.GetComponentData<ResourceDeposit>(castEvent.TargetEntity);
                float siphoned = math.min(siphonAmount, deposit.CurrentAmount);
                deposit.CurrentAmount -= siphoned;
                godPool.Essence += siphoned;
                ecb.SetComponent(castEvent.TargetEntity, deposit);
                return;
            }

            // Try to siphon from ResourceStack inventory
            if (state.EntityManager.HasBuffer<ResourceStack>(castEvent.TargetEntity))
            {
                var inventory = state.EntityManager.GetBuffer<ResourceStack>(castEvent.TargetEntity);
                // Siphon from first available resource stack
                for (int i = 0; i < inventory.Length; i++)
                {
                    var stack = inventory[i];
                    if (stack.Amount > 0f)
                    {
                        float siphoned = math.min(siphonAmount, stack.Amount);
                        stack.Amount -= siphoned;
                        godPool.Essence += siphoned;
                        inventory[i] = stack;
                        if (stack.Amount <= 0f)
                        {
                            inventory.RemoveAtSwapBack(i);
                        }
                        // Buffer modifications are applied directly (non-Burst context)
                        return;
                    }
                }
            }

            // Try to siphon from VillageResources (aggregate)
            if (state.EntityManager.HasComponent<VillageResources>(castEvent.TargetEntity))
            {
                var resources = state.EntityManager.GetComponentData<VillageResources>(castEvent.TargetEntity);
                // Siphon from wood first
                if (resources.Wood > 0f)
                {
                    float siphoned = math.min(siphonAmount, resources.Wood);
                    resources.Wood -= siphoned;
                    godPool.Essence += siphoned;
                    ecb.SetComponent(castEvent.TargetEntity, resources);
                    return;
                }
            }

            // Try to siphon from PlatformResources (Space4X)
            if (state.EntityManager.HasComponent<PlatformResources>(castEvent.TargetEntity))
            {
                var resources = state.EntityManager.GetComponentData<PlatformResources>(castEvent.TargetEntity);
                // Siphon from ore first
                if (resources.Ore > 0f)
                {
                    float siphoned = math.min(siphonAmount, resources.Ore);
                    resources.Ore -= siphoned;
                    godPool.Essence += siphoned;
                    ecb.SetComponent(castEvent.TargetEntity, resources);
                    return;
                }
            }
        }
    }
}


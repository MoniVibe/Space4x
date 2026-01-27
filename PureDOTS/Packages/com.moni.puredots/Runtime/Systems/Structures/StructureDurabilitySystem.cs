using Unity.Entities;
using Unity.Burst;
using PureDOTS.Runtime.Structures;
using PureDOTS.Runtime.Components;

namespace PureDOTS.Runtime.Systems.Structures
{
    /// <summary>
    /// System that updates structure durability states and penalties.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [BurstCompile]
    public partial struct StructureDurabilitySystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) || rewindState.Mode != RewindMode.Record)
                return;

            var timeState = SystemAPI.GetSingleton<TimeState>();
            uint currentTick = timeState.Tick;

            DurabilityConfig config = DurabilityHelpers.DefaultConfig;
            if (SystemAPI.TryGetSingleton<DurabilityConfig>(out var existingConfig))
            {
                config = existingConfig;
            }

            var ecbSingleton = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.ValueRW.CreateCommandBuffer(state.WorldUnmanaged);

            // Update durability states and apply decay
            foreach (var (durability, stateEvents, entity) in 
                SystemAPI.Query<RefRW<StructureDurability>, DynamicBuffer<DurabilityStateChangedEvent>>()
                    .WithEntityAccess())
            {
                var oldState = durability.ValueRO.State;
                float oldDurability = durability.ValueRO.CurrentDurability;
                
                // Apply natural decay
                uint ticksSinceLastDamage = currentTick - durability.ValueRO.LastDamageTick;
                DurabilityHelpers.ApplyDecay(ref durability.ValueRW, 1, config); // Apply per-tick decay
                
                // Check for state change
                if (durability.ValueRO.State != oldState)
                {
                    stateEvents.Add(new DurabilityStateChangedEvent
                    {
                        OldState = oldState,
                        NewState = durability.ValueRO.State,
                        OldDurability = oldDurability,
                        NewDurability = durability.ValueRO.CurrentDurability,
                        Tick = currentTick
                    });
                    
                    // Check for destruction
                    if (durability.ValueRO.State == DurabilityState.Destroyed)
                    {
                        if (!SystemAPI.HasComponent<StructureDestroyedEvent>(entity))
                        {
                            ecb.AddComponent(entity, new StructureDestroyedEvent
                            {
                                DurabilityAtDestruction = oldDurability,
                                FinalDamageSource = DamageSourceType.Decay,
                                DestroyerEntity = Entity.Null,
                                Tick = currentTick
                            });
                        }
                    }
                }
            }

            // Handle entities without event buffer
            foreach (var (durability, entity) in 
                SystemAPI.Query<RefRW<StructureDurability>>()
                    .WithNone<DurabilityStateChangedEvent>()
                    .WithEntityAccess())
            {
                var oldState = durability.ValueRO.State;
                
                DurabilityHelpers.ApplyDecay(ref durability.ValueRW, 1, config);
                
                if (durability.ValueRO.State == DurabilityState.Destroyed && oldState != DurabilityState.Destroyed)
                {
                    if (!SystemAPI.HasComponent<StructureDestroyedEvent>(entity))
                    {
                        ecb.AddComponent(entity, new StructureDestroyedEvent
                        {
                            DurabilityAtDestruction = durability.ValueRO.CurrentDurability,
                            FinalDamageSource = DamageSourceType.Decay,
                            DestroyerEntity = Entity.Null,
                            Tick = currentTick
                        });
                    }
                }
            }
        }
    }

    /// <summary>
    /// System that processes repair requests.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(StructureDurabilitySystem))]
    [BurstCompile]
    public partial struct RepairSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) || rewindState.Mode != RewindMode.Record)
                return;

            var timeState = SystemAPI.GetSingleton<TimeState>();
            uint currentTick = timeState.Tick;

            DurabilityConfig config = DurabilityHelpers.DefaultConfig;
            if (SystemAPI.TryGetSingleton<DurabilityConfig>(out var existingConfig))
            {
                config = existingConfig;
            }

            var ecbSingleton = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.ValueRW.CreateCommandBuffer(state.WorldUnmanaged);

            // Process repair requests
            foreach (var (request, durability, entity) in 
                SystemAPI.Query<RefRO<RepairRequest>, RefRW<StructureDurability>>()
                    .WithEntityAccess())
            {
                // Apply repair
                DurabilityHelpers.Repair(ref durability.ValueRW, request.ValueRO.RepairAmount, currentTick, config);
                
                // Remove request
                ecb.RemoveComponent<RepairRequest>(entity);
            }
        }
    }
}


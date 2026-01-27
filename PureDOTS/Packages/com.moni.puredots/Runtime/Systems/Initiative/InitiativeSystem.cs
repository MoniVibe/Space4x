using Unity.Entities;
using Unity.Burst;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Initiative;

namespace PureDOTS.Runtime.Systems.Initiative
{
    /// <summary>
    /// System that updates entity initiative and readiness.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [BurstCompile]
    public partial struct InitiativeUpdateSystem : ISystem
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
            var rewindState = SystemAPI.GetSingleton<RewindState>();
            if (rewindState.Mode != RewindMode.Record)
                return;

            var timeState = SystemAPI.GetSingleton<TimeState>();
            float currentTime = timeState.ElapsedTime;
            uint currentTick = timeState.Tick;

            InitiativeConfig config = InitiativeHelpers.DefaultConfig;
            if (SystemAPI.TryGetSingleton<InitiativeConfig>(out var existingConfig))
            {
                config = existingConfig;
            }

            // Update initiative readiness
            foreach (var (init, entity) in 
                SystemAPI.Query<RefRW<EntityInitiative>>()
                    .WithEntityAccess())
            {
                // Recalculate current initiative from modifiers
                init.ValueRW.CurrentInitiative = InitiativeHelpers.CalculateCurrentInitiative(
                    init.ValueRO.BaseInitiative,
                    init.ValueRO.SpeedModifier,
                    init.ValueRO.FatigueModifier,
                    init.ValueRO.MoraleModifier);
                
                // Update readiness
                InitiativeHelpers.UpdateReadiness(ref init.ValueRW, currentTime, currentTick);
            }

            // Emit ready events
            foreach (var (init, readyEvents, entity) in 
                SystemAPI.Query<RefRO<EntityInitiative>, DynamicBuffer<InitiativeReadyEvent>>()
                    .WithEntityAccess())
            {
                // Check if just became ready
                if (init.ValueRO.IsReady && init.ValueRO.LastReadyTick == currentTick)
                {
                    readyEvents.Add(new InitiativeReadyEvent
                    {
                        Tick = currentTick,
                        Initiative = init.ValueRO.CurrentInitiative
                    });
                }
            }
        }
    }

    /// <summary>
    /// System that processes action requests and applies cooldowns.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(InitiativeUpdateSystem))]
    [BurstCompile]
    public partial struct ActionRequestSystem : ISystem
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
            var rewindState = SystemAPI.GetSingleton<RewindState>();
            if (rewindState.Mode != RewindMode.Record)
                return;

            var timeState = SystemAPI.GetSingleton<TimeState>();
            float currentTime = timeState.ElapsedTime;

            InitiativeConfig config = InitiativeHelpers.DefaultConfig;
            if (SystemAPI.TryGetSingleton<InitiativeConfig>(out var existingConfig))
            {
                config = existingConfig;
            }

            var ecbSingleton = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.ValueRW.CreateCommandBuffer(state.WorldUnmanaged);

            // Process action requests
            foreach (var (request, init, entity) in 
                SystemAPI.Query<RefRO<ActionRequest>, RefRW<EntityInitiative>>()
                    .WithEntityAccess())
            {
                // Only process if ready
                if (init.ValueRO.IsReady)
                {
                    InitiativeHelpers.ApplyAction(ref init.ValueRW, request.ValueRO.ActionCost, currentTime, config);
                }
                
                // Remove request
                ecb.RemoveComponent<ActionRequest>(entity);
            }
        }
    }
}


using Unity.Entities;
using Unity.Burst;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Needs;
using PureDOTS.Runtime.Time;

namespace PureDOTS.Runtime.Systems.Needs
{
    /// <summary>
    /// System that decays entity needs over time based on activity state.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [BurstCompile]
    public partial struct NeedsDecaySystem : ISystem
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
            float deltaTime = timeState.DeltaTime;
            uint currentTick = timeState.Tick;

            // Get or create config singleton
            NeedsConfig config = NeedsHelpers.DefaultConfig;
            if (SystemAPI.TryGetSingleton<NeedsConfig>(out var existingConfig))
            {
                config = existingConfig;
            }

            // Process entities with simple EntityNeeds
            foreach (var (needs, activityState, entity) in 
                SystemAPI.Query<RefRW<EntityNeeds>, RefRO<NeedsActivityState>>()
                    .WithEntityAccess())
            {
                var activity = activityState.ValueRO.Current;
                
                // Apply decay
                NeedsHelpers.ApplyDecay(ref needs.ValueRW, deltaTime, activity, config);
                
                // Apply regen if in appropriate activity
                NeedsHelpers.ApplyRegen(ref needs.ValueRW, deltaTime, activity, config);
                
                needs.ValueRW.LastUpdateTick = currentTick;
            }

            // Process entities with detailed NeedEntry buffers
            foreach (var (needsBuffer, activityState, entity) in 
                SystemAPI.Query<DynamicBuffer<NeedEntry>, RefRO<NeedsActivityState>>()
                    .WithEntityAccess())
            {
                var needsEntries = needsBuffer;
                var activity = activityState.ValueRO.Current;
                float decayMult = NeedsHelpers.GetDecayMultiplier(activity, config);

                for (int i = 0; i < needsEntries.Length; i++)
                {
                    var entry = needsEntries[i];
                    
                    // Apply decay
                    float effectiveDecay = entry.DecayRate * decayMult * deltaTime;
                    entry.Current = Unity.Mathematics.math.max(0, entry.Current - effectiveDecay);
                    
                    // Apply regen if applicable
                    float regenMult = NeedsHelpers.GetRegenMultiplier(activity, entry.Type, config);
                    if (regenMult > 1f)
                    {
                        float effectiveRegen = entry.RegenRate * regenMult * deltaTime;
                        entry.Current = Unity.Mathematics.math.min(entry.Max, entry.Current + effectiveRegen);
                    }
                    
                    // Update urgency
                    entry.Urgency = NeedsHelpers.GetUrgency(entry.Current, entry.Max, config);
                    entry.LastUpdateTick = currentTick;
                    
                    if (entry.Urgency == NeedUrgency.Satisfied)
                        entry.LastSatisfiedTick = currentTick;
                    
                    needsEntries[i] = entry;
                }
            }
        }
    }

    /// <summary>
    /// System that updates need urgency levels and emits critical events.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(NeedsDecaySystem))]
    [BurstCompile]
    public partial struct NeedsUrgencySystem : ISystem
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
            uint currentTick = timeState.Tick;

            // Check for critical needs and emit events
            foreach (var (needs, criticalEvents, entity) in 
                SystemAPI.Query<RefRO<EntityNeeds>, DynamicBuffer<NeedCriticalEvent>>()
                    .WithEntityAccess())
            {
                var criticalEventsBuffer = criticalEvents;

                // Check health critical
                if (needs.ValueRO.HealthUrgency == NeedUrgency.Critical)
                {
                    EmitCriticalEvent(criticalEventsBuffer, NeedType.Health, needs.ValueRO.Health, currentTick);
                }
                
                // Check energy critical
                if (needs.ValueRO.EnergyUrgency == NeedUrgency.Critical)
                {
                    EmitCriticalEvent(criticalEventsBuffer, NeedType.Hunger, needs.ValueRO.Energy, currentTick);
                }
                
                // Check morale critical
                if (needs.ValueRO.MoraleUrgency == NeedUrgency.Critical)
                {
                    EmitCriticalEvent(criticalEventsBuffer, NeedType.Social, needs.ValueRO.Morale, currentTick);
                }
            }

            // Check detailed needs buffers
            foreach (var (needsBuffer, criticalEvents, entity) in 
                SystemAPI.Query<DynamicBuffer<NeedEntry>, DynamicBuffer<NeedCriticalEvent>>()
                    .WithEntityAccess())
            {
                var needsEntries = needsBuffer;
                var criticalEventsBuffer = criticalEvents;

                for (int i = 0; i < needsEntries.Length; i++)
                {
                    var entry = needsEntries[i];
                    if (entry.Urgency == NeedUrgency.Critical)
                    {
                        EmitCriticalEvent(criticalEventsBuffer, entry.Type, entry.Current, currentTick);
                    }
                }
            }
        }

        private static void EmitCriticalEvent(DynamicBuffer<NeedCriticalEvent> buffer, NeedType needType, float currentValue, uint tick)
        {
            // Check if we already emitted for this need recently (within 100 ticks)
            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i].NeedType == needType && tick - buffer[i].Tick < 100)
                {
                    return; // Already emitted recently
                }
            }

            // Remove old events if buffer is full
            if (buffer.Length >= buffer.Capacity)
            {
                buffer.RemoveAt(0);
            }

            buffer.Add(new NeedCriticalEvent
            {
                NeedType = needType,
                CurrentValue = currentValue,
                Tick = tick
            });
        }
    }

    /// <summary>
    /// System that processes satisfy need requests.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(NeedsUrgencySystem))]
    [BurstCompile]
    public partial struct SatisfyNeedSystem : ISystem
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

            NeedsConfig config = NeedsHelpers.DefaultConfig;
            if (SystemAPI.TryGetSingleton<NeedsConfig>(out var existingConfig))
            {
                config = existingConfig;
            }

            var ecbSingleton = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.ValueRW.CreateCommandBuffer(state.WorldUnmanaged);

            // Process satisfy requests
            foreach (var (request, needs, entity) in 
                SystemAPI.Query<RefRO<SatisfyNeedRequest>, RefRW<EntityNeeds>>()
                    .WithEntityAccess())
            {
                NeedsHelpers.SatisfyNeed(ref needs.ValueRW, request.ValueRO.NeedType, request.ValueRO.Amount, config);
                ecb.RemoveComponent<SatisfyNeedRequest>(entity);
            }
        }
    }
}


using Unity.Entities;
using Unity.Burst;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Social;
using PureDOTS.Runtime.Time;

namespace PureDOTS.Runtime.Systems.Social
{
    /// <summary>
    /// System that updates loyalty and checks for desertion/mutiny.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [BurstCompile]
    public partial struct LoyaltyUpdateSystem : ISystem
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

            LoyaltyConfig config = LoyaltyHelpers.DefaultConfig;
            if (SystemAPI.TryGetSingleton<LoyaltyConfig>(out var existingConfig))
            {
                config = existingConfig;
            }

            // Update loyalty state and modifiers
            foreach (var (loyalty, modifiers, entity) in 
                SystemAPI.Query<RefRW<EntityLoyalty>, RefRW<LoyaltyModifiers>>()
                    .WithEntityAccess())
            {
                // Apply natural decay
                uint ticksSinceChange = currentTick - loyalty.ValueRO.LastLoyaltyChangeTick;
                loyalty.ValueRW.Loyalty = LoyaltyHelpers.ApplyDecay(
                    loyalty.ValueRO.Loyalty,
                    loyalty.ValueRO.NaturalLoyalty,
                    ticksSinceChange,
                    config);
                
                // Update state
                loyalty.ValueRW.State = LoyaltyHelpers.GetState(loyalty.ValueRO.Loyalty);
                loyalty.ValueRW.DesertionRisk = LoyaltyHelpers.GetDesertionRisk(loyalty.ValueRO.Loyalty, config);
                
                // Update modifiers
                modifiers.ValueRW = LoyaltyHelpers.CalculateModifiers(loyalty.ValueRO.Loyalty, config);
            }

            // Handle entities without modifiers component
            foreach (var (loyalty, entity) in 
                SystemAPI.Query<RefRW<EntityLoyalty>>()
                    .WithNone<LoyaltyModifiers>()
                    .WithEntityAccess())
            {
                uint ticksSinceChange = currentTick - loyalty.ValueRO.LastLoyaltyChangeTick;
                loyalty.ValueRW.Loyalty = LoyaltyHelpers.ApplyDecay(
                    loyalty.ValueRO.Loyalty,
                    loyalty.ValueRO.NaturalLoyalty,
                    ticksSinceChange,
                    config);
                
                loyalty.ValueRW.State = LoyaltyHelpers.GetState(loyalty.ValueRO.Loyalty);
                loyalty.ValueRW.DesertionRisk = LoyaltyHelpers.GetDesertionRisk(loyalty.ValueRO.Loyalty, config);
            }
        }
    }

    /// <summary>
    /// System that processes loyalty events.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(LoyaltyUpdateSystem))]
    [BurstCompile]
    public partial struct LoyaltyEventSystem : ISystem
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

            LoyaltyConfig config = LoyaltyHelpers.DefaultConfig;
            if (SystemAPI.TryGetSingleton<LoyaltyConfig>(out var existingConfig))
            {
                config = existingConfig;
            }

            // Process loyalty events
            foreach (var (loyalty, events, entity) in 
                SystemAPI.Query<RefRW<EntityLoyalty>, DynamicBuffer<LoyaltyEvent>>()
                    .WithEntityAccess())
            {
                // Process recent events (within last 10 ticks)
                for (int i = events.Length - 1; i >= 0; i--)
                {
                    var evt = events[i];
                    
                    // Only process recent events
                    if (currentTick - evt.Tick > 10)
                    {
                        events.RemoveAt(i);
                        continue;
                    }
                    
                    // Apply event
                    LoyaltyHelpers.ApplyLoyaltyEvent(ref loyalty.ValueRW, evt.Type, evt.Magnitude, config);
                    loyalty.ValueRW.LastLoyaltyChangeTick = currentTick;
                    
                    events.RemoveAt(i);
                }
            }
        }
    }

    /// <summary>
    /// System that checks for desertion.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(LoyaltyUpdateSystem))]
    [BurstCompile]
    public partial struct DesertionCheckSystem : ISystem
    {
        private const uint CHECK_INTERVAL = 600; // Every 10 seconds at 60 ticks/sec

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

            // Only check periodically
            if (currentTick % CHECK_INTERVAL != 0)
                return;

            LoyaltyConfig config = LoyaltyHelpers.DefaultConfig;
            if (SystemAPI.TryGetSingleton<LoyaltyConfig>(out var existingConfig))
            {
                config = existingConfig;
            }

            var ecbSingleton = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.ValueRW.CreateCommandBuffer(state.WorldUnmanaged);

            // Check for desertion
            foreach (var (loyalty, entity) in 
                SystemAPI.Query<RefRO<EntityLoyalty>>()
                    .WithNone<DesertionEvent>()
                    .WithEntityAccess())
            {
                if (loyalty.ValueRO.DesertionRisk <= 0f)
                    continue;
                
                // Generate seed from entity and tick
                uint seed = (uint)(entity.Index ^ entity.Version ^ currentTick);
                
                // TODO: Get actual hardship level from needs/morale
                float hardshipLevel = 0.5f;
                
                if (LoyaltyHelpers.WillDesert(loyalty.ValueRO.Loyalty, hardshipLevel, seed, config))
                {
                    ecb.AddComponent(entity, new DesertionEvent
                    {
                        LoyaltyAtDesertion = loyalty.ValueRO.Loyalty,
                        HardshipLevel = hardshipLevel,
                        Tick = currentTick
                    });
                }
            }
        }
    }
}


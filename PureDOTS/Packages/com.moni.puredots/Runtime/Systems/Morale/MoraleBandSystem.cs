using Unity.Entities;
using Unity.Burst;
using Unity.Mathematics;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Morale;
using PureDOTS.Runtime.Time;

namespace PureDOTS.Runtime.Systems.Morale
{
    /// <summary>
    /// System that updates morale bands and applies modifiers.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [BurstCompile]
    public partial struct MoraleBandSystem : ISystem
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

            MoraleConfig config = MoraleHelpers.DefaultConfig;
            if (SystemAPI.TryGetSingleton<MoraleConfig>(out var existingConfig))
            {
                config = existingConfig;
            }

            // Update morale from modifiers and memories
            foreach (var (morale, modifiers, memories, bandEvents, entity) in 
                SystemAPI.Query<RefRW<EntityMorale>, DynamicBuffer<MoraleModifier>, DynamicBuffer<MoraleMemory>, DynamicBuffer<MoraleBandChangedEvent>>()
                    .WithEntityAccess())
            {
                var modifiersBuffer = modifiers;
                var memoriesBuffer = memories;
                var bandEventsBuffer = bandEvents;

                // Calculate total modifier contribution
                float modifierTotal = MoraleHelpers.CalculateTotalModifier(modifiersBuffer);
                float memoryTotal = MoraleHelpers.CalculateMemoryContribution(memoriesBuffer);
                
                // Base morale (500) + modifiers + memories
                float baseMorale = 500f;
                float newMorale = math.clamp(baseMorale + modifierTotal + memoryTotal, 0, config.MaxMorale);
                
                // Update band
                var newBand = MoraleHelpers.GetBand(newMorale, config);
                
                // Check for band change
                if (newBand != morale.ValueRO.Band)
                {
                    // Emit band change event
                    bandEventsBuffer.Add(new MoraleBandChangedEvent
                    {
                        OldBand = morale.ValueRO.Band,
                        NewBand = newBand,
                        OldMorale = morale.ValueRO.CurrentMorale,
                        NewMorale = newMorale,
                        Tick = currentTick
                    });
                    
                    morale.ValueRW.PreviousBand = morale.ValueRO.Band;
                    morale.ValueRW.LastBandChangeTick = currentTick;
                }
                
                // Update morale state
                morale.ValueRW.CurrentMorale = newMorale;
                morale.ValueRW.Band = newBand;
                morale.ValueRW.LastUpdateTick = currentTick;
                
                // Update risk factors
                morale.ValueRW.BreakdownRisk = MoraleHelpers.GetBreakdownRisk(newMorale, config);
                morale.ValueRW.BurnoutRisk = MoraleHelpers.GetBurnoutRisk(newMorale, config);
                
                // Update modifiers
                MoraleHelpers.UpdateModifiers(ref morale.ValueRW);
            }

            // Handle entities with just EntityMorale (no buffers)
            foreach (var (morale, entity) in 
                SystemAPI.Query<RefRW<EntityMorale>>()
                    .WithNone<MoraleModifier>()
                    .WithEntityAccess())
            {
                var newBand = MoraleHelpers.GetBand(morale.ValueRO.CurrentMorale, config);
                
                if (newBand != morale.ValueRO.Band)
                {
                    morale.ValueRW.PreviousBand = morale.ValueRO.Band;
                    morale.ValueRW.LastBandChangeTick = currentTick;
                }
                
                morale.ValueRW.Band = newBand;
                morale.ValueRW.LastUpdateTick = currentTick;
                morale.ValueRW.BreakdownRisk = MoraleHelpers.GetBreakdownRisk(morale.ValueRO.CurrentMorale, config);
                morale.ValueRW.BurnoutRisk = MoraleHelpers.GetBurnoutRisk(morale.ValueRO.CurrentMorale, config);
                MoraleHelpers.UpdateModifiers(ref morale.ValueRW);
            }
        }
    }

    /// <summary>
    /// System that decays morale modifiers over time.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(MoraleBandSystem))]
    [BurstCompile]
    public partial struct MoraleModifierDecaySystem : ISystem
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

            MoraleConfig config = MoraleHelpers.DefaultConfig;
            if (SystemAPI.TryGetSingleton<MoraleConfig>(out var existingConfig))
            {
                config = existingConfig;
            }

            // Decay modifiers
            foreach (var (modifiers, entity) in 
                SystemAPI.Query<DynamicBuffer<MoraleModifier>>()
                    .WithEntityAccess())
            {
                var modifiersBuffer = modifiers;

                for (int i = modifiersBuffer.Length - 1; i >= 0; i--)
                {
                    var mod = modifiersBuffer[i];
                    
                    // Check duration expiry
                    if (mod.RemainingTicks > 0)
                    {
                        mod.RemainingTicks--;
                        if (mod.RemainingTicks == 0)
                        {
                            modifiersBuffer.RemoveAt(i);
                            continue;
                        }
                    }
                    
                    // Apply half-life decay
                    if (mod.DecayHalfLife > 0)
                    {
                        uint ticksSinceApplied = currentTick - mod.AppliedTick;
                        float halfLives = ticksSinceApplied / (float)mod.DecayHalfLife;
                        float decayFactor = math.pow(0.5f, halfLives);
                        
                        // Calculate decayed magnitude
                        short originalMagnitude = mod.Magnitude;
                        mod.Magnitude = (short)(originalMagnitude * decayFactor);
                        
                        // Remove if decayed to near zero
                        if (math.abs(mod.Magnitude) < 1)
                        {
                            modifiersBuffer.RemoveAt(i);
                            continue;
                        }
                    }
                    
                    modifiersBuffer[i] = mod;
                }
            }
        }
    }

    /// <summary>
    /// System that decays morale memories over time.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(MoraleBandSystem))]
    [BurstCompile]
    public partial struct MoraleMemoryDecaySystem : ISystem
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

            // Decay memories
            foreach (var (memories, entity) in 
                SystemAPI.Query<DynamicBuffer<MoraleMemory>>()
                    .WithEntityAccess())
            {
                var memoriesBuffer = memories;

                for (int i = memoriesBuffer.Length - 1; i >= 0; i--)
                {
                    var memory = memoriesBuffer[i];
                    
                    // Skip permanent memories
                    if (memory.DecayHalfLife == 0)
                        continue;
                    
                    uint ticksSinceFormed = currentTick - memory.FormedTick;
                    memory.CurrentMagnitude = MoraleHelpers.DecayMemory(
                        memory.InitialMagnitude, 
                        ticksSinceFormed, 
                        memory.DecayHalfLife);
                    
                    // Remove if decayed to near zero
                    if (math.abs(memory.CurrentMagnitude) < 1)
                    {
                        memoriesBuffer.RemoveAt(i);
                        continue;
                    }
                    
                    memoriesBuffer[i] = memory;
                }
            }
        }
    }

    /// <summary>
    /// System that checks for breakdowns and burnouts.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(MoraleBandSystem))]
    [BurstCompile]
    public partial struct MoraleBreakdownSystem : ISystem
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

            MoraleConfig config = MoraleHelpers.DefaultConfig;
            if (SystemAPI.TryGetSingleton<MoraleConfig>(out var existingConfig))
            {
                config = existingConfig;
            }

            var ecbSingleton = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.ValueRW.CreateCommandBuffer(state.WorldUnmanaged);

            // Check for breakdowns (Despair band)
            foreach (var (morale, entity) in 
                SystemAPI.Query<RefRO<EntityMorale>>()
                    .WithNone<MoraleBreakdownEvent>()
                    .WithEntityAccess())
            {
                if (morale.ValueRO.Band != MoraleBand.Despair)
                    continue;
                
                // Check interval
                if (currentTick % config.BreakdownCheckInterval != 0)
                    continue;
                
                // Generate seed from entity and tick
                uint seed = (uint)(entity.Index ^ entity.Version ^ currentTick);
                
                if (MoraleHelpers.ShouldBreakdown(morale.ValueRO.BreakdownRisk, seed, config))
                {
                    ecb.AddComponent(entity, new MoraleBreakdownEvent
                    {
                        TriggerBand = morale.ValueRO.Band,
                        MoraleAtBreakdown = morale.ValueRO.CurrentMorale,
                        Tick = currentTick
                    });
                }
            }

            // Check for burnouts (Elated band)
            foreach (var (morale, entity) in 
                SystemAPI.Query<RefRO<EntityMorale>>()
                    .WithNone<MoraleBurnoutEvent>()
                    .WithEntityAccess())
            {
                if (morale.ValueRO.Band != MoraleBand.Elated)
                    continue;
                
                // Check interval
                if (currentTick % config.BurnoutCheckInterval != 0)
                    continue;
                
                // Generate seed from entity and tick
                uint seed = (uint)(entity.Index ^ entity.Version ^ currentTick);
                
                if (MoraleHelpers.ShouldBurnout(morale.ValueRO.BurnoutRisk, seed, config))
                {
                    ecb.AddComponent(entity, new MoraleBurnoutEvent
                    {
                        MoraleAtBurnout = morale.ValueRO.CurrentMorale,
                        Tick = currentTick
                    });
                }
            }
        }
    }
}


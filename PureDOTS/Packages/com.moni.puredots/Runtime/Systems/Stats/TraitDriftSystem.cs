using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Stats;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Stats
{
    /// <summary>
    /// System that applies trait drift: action footprints, decay over time, and resistance at extremes.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct TraitDriftSystem : ISystem
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
            {
                return;
            }

            var timeState = SystemAPI.GetSingleton<TimeState>();
            uint currentTick = timeState.Tick;

            // Apply decay to entities with TraitDriftState
            var decayJob = new ApplyDecayJob
            {
                CurrentTick = currentTick,
                DeltaTime = timeState.FixedDeltaTime
            };
            state.Dependency = decayJob.ScheduleParallel(state.Dependency);
        }

        /// <summary>
        /// Apply action footprint to an entity's trait axes.
        /// Called by action completion systems (not part of main update loop).
        /// </summary>
        public static void ApplyFootprint(
            BlobAssetReference<ActionFootprintBlob> footprint,
            BlobAssetReference<IntentModifierBlob> intentModifier,
            BlobAssetReference<ContextModifierBlob> contextModifier,
            ref DynamicBuffer<TraitAxisValue> traitBuffer)
        {
            if (!footprint.IsCreated)
            {
                return;
            }

            ref var footprintData = ref footprint.Value;

            // Apply base deltas
            for (int i = 0; i < footprintData.Deltas.Length; i++)
            {
                var delta = footprintData.Deltas[i];
                float finalDelta = delta.Delta;

                // Apply intent modifier
                if (intentModifier.IsCreated)
                {
                    ref var intentData = ref intentModifier.Value;
                    
                    // Find multiplier for this axis
                    for (int j = 0; j < intentData.Multipliers.Length; j++)
                    {
                        if (intentData.Multipliers[j].AxisId.Equals(delta.AxisId))
                        {
                            finalDelta *= intentData.Multipliers[j].Delta;
                            break;
                        }
                    }
                    
                    // Find offset for this axis
                    for (int j = 0; j < intentData.Offsets.Length; j++)
                    {
                        if (intentData.Offsets[j].AxisId.Equals(delta.AxisId))
                        {
                            finalDelta += intentData.Offsets[j].Delta;
                            break;
                        }
                    }
                }

                // Apply context modifier
                if (contextModifier.IsCreated)
                {
                    ref var contextData = ref contextModifier.Value;
                    
                    for (int j = 0; j < contextData.Deltas.Length; j++)
                    {
                        if (contextData.Deltas[j].AxisId.Equals(delta.AxisId))
                        {
                            finalDelta += contextData.Deltas[j].Delta;
                            break;
                        }
                    }
                }

                // Apply resistance (entities at extremes resist change)
                float currentValue = TraitAxisLookup.GetValueOrDefault(default, delta.AxisId, traitBuffer);
                float resistance = CalculateResistance(currentValue);
                finalDelta *= resistance;

                // Apply delta
                TraitAxisLookup.ApplyDelta(delta.AxisId, finalDelta, ref traitBuffer);
            }
        }

        /// <summary>
        /// Calculate resistance factor based on current value (entities at extremes resist change more).
        /// </summary>
        private static float CalculateResistance(float currentValue)
        {
            // Normalize to -1 to +1 range (assuming -100 to +100 input)
            float normalized = math.clamp(currentValue / 100f, -1f, 1f);
            
            // Resistance increases as we approach extremes
            // At 0: resistance = 1.0 (full impact)
            // At ±100: resistance = 0.1 (10% impact)
            float distanceFromCenter = math.abs(normalized);
            float resistance = 1.0f - (distanceFromCenter * 0.9f);
            
            return math.max(0.1f, resistance);
        }

        [BurstCompile]
        private partial struct ApplyDecayJob : IJobEntity
        {
            public uint CurrentTick;
            public float DeltaTime;

            public void Execute(
                ref DynamicBuffer<TraitAxisValue> traitBuffer,
                ref TraitDriftState driftState)
            {
                // Check if it's time to apply decay
                if (CurrentTick - driftState.LastDriftTick < driftState.DriftInterval)
                {
                    return;
                }

                float decayRate = driftState.DecayRatePerTick * driftState.DriftInterval;

                // Apply decay to all axes (drift toward neutral/default)
                for (int i = 0; i < traitBuffer.Length; i++)
                {
                    var axisValue = traitBuffer[i];
                    
                    // Decay toward 0 (neutral)
                    if (axisValue.Value > 0f)
                    {
                        axisValue.Value = math.max(0f, axisValue.Value - decayRate);
                    }
                    else if (axisValue.Value < 0f)
                    {
                        axisValue.Value = math.min(0f, axisValue.Value + decayRate);
                    }

                    traitBuffer[i] = axisValue;
                }

                driftState.LastDriftTick = CurrentTick;
            }
        }
    }
}


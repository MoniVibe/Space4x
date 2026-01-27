using PureDOTS.Runtime.Core;
using PureDOTS.Runtime.Infiltration;
using PureDOTS.Runtime.Interrupts;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Rewind;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Infiltration
{
    /// <summary>
    /// Gathers intelligence for infiltrating agents based on their infiltration level.
    /// Populates GatheredIntel buffer with intel types available at current level.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(InfiltrationProgressSystem))]
    public partial struct IntelGatheringSystem : ISystem
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
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            // Update every 50 ticks (throttled)
            if (timeState.Tick % 50 != 0)
            {
                return;
            }

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (infiltration, entity) in SystemAPI.Query<RefRO<InfiltrationState>>().WithEntityAccess())
            {
                if (infiltration.ValueRO.IsExposed != 0 || infiltration.ValueRO.IsExtracting != 0)
                {
                    continue;
                }

                if (infiltration.ValueRO.Level == InfiltrationLevel.None)
                {
                    continue;
                }

                // Ensure buffer exists
                if (!SystemAPI.HasBuffer<GatheredIntel>(entity))
                {
                    ecb.AddBuffer<GatheredIntel>(entity);
                    continue; // Will gather next tick after buffer is created
                }

                var intelBuffer = SystemAPI.GetBuffer<GatheredIntel>(entity);

                // Mark stale intel (older than 1000 ticks)
                for (int i = intelBuffer.Length - 1; i >= 0; i--)
                {
                    var intel = intelBuffer[i];
                    uint age = timeState.Tick > intel.GatheredTick ? timeState.Tick - intel.GatheredTick : 0;
                    if (age > 1000)
                    {
                        intel.IsStale = 1;
                        intelBuffer[i] = intel;
                    }
                }

                // Determine available intel types based on level
                int availableTypes = InfiltrationHelpers.GetAvailableIntelTypes(infiltration.ValueRO.Level);
                if (availableTypes == 0)
                {
                    continue;
                }

                // Random chance to gather new intel (based on level - higher levels gather more frequently)
                float gatherChance = (int)infiltration.ValueRO.Level * 0.15f; // 15% per level
                uint seed = (uint)(timeState.Tick + entity.Index);
                float roll = (DeterministicRandom(seed) % 1000) / 1000f;

                if (roll < gatherChance)
                {
                    // Select intel type based on level
                    IntelType intelType = SelectIntelType(infiltration.ValueRO.Level, seed);
                    
                    // Calculate intel value
                    float intelValue = InfiltrationHelpers.CalculateIntelValue(
                        infiltration.ValueRO.Level,
                        timeState.Tick,
                        timeState.Tick, // Fresh intel
                        0.0001f); // Staleness factor

                    // Add intel entry
                    intelBuffer.Add(new GatheredIntel
                    {
                        IntelId = (FixedString64Bytes)$"intel_{timeState.Tick}_{entity.Index}",
                        Type = intelType,
                        SourceEntity = infiltration.ValueRO.TargetOrganization,
                        RequiredLevel = infiltration.ValueRO.Level,
                        Value = intelValue,
                        GatheredTick = timeState.Tick,
                        IsVerified = 1,
                        IsStale = 0
                    });

                    // Emit interrupt for intel gathered
                    if (!SystemAPI.HasBuffer<Interrupt>(entity))
                    {
                        ecb.AddBuffer<Interrupt>(entity);
                    }
                    else
                    {
                        var interruptBuffer = SystemAPI.GetBuffer<Interrupt>(entity);
                        InterruptUtils.Emit(
                            ref interruptBuffer,
                            InterruptType.IntelGathered,
                            InterruptPriority.Low,
                            entity,
                            timeState.Tick,
                            targetEntity: infiltration.ValueRO.TargetOrganization,
                            payloadValue: intelValue,
                            payloadId: (FixedString32Bytes)$"intel.{intelType}");
                    }
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private static IntelType SelectIntelType(InfiltrationLevel level, uint seed)
        {
            // Determine available types based on level
            // Contact: Military only
            // Embedded: Military, Economic, Political
            // Trusted: + Social
            // Influential: + Technological
            // Subverted: All types

            int typeCount = level switch
            {
                InfiltrationLevel.Contact => 1,
                InfiltrationLevel.Embedded => 3,
                InfiltrationLevel.Trusted => 4,
                InfiltrationLevel.Influential => 5,
                InfiltrationLevel.Subverted => 5,
                _ => 1
            };

            int selected = (int)(DeterministicRandom(seed) % typeCount);

            return level switch
            {
                InfiltrationLevel.Contact => IntelType.Military,
                InfiltrationLevel.Embedded => selected switch
                {
                    0 => IntelType.Military,
                    1 => IntelType.Economic,
                    _ => IntelType.Political
                },
                InfiltrationLevel.Trusted => selected switch
                {
                    0 => IntelType.Military,
                    1 => IntelType.Economic,
                    2 => IntelType.Political,
                    _ => IntelType.Social
                },
                InfiltrationLevel.Influential => selected switch
                {
                    0 => IntelType.Military,
                    1 => IntelType.Economic,
                    2 => IntelType.Political,
                    3 => IntelType.Social,
                    _ => IntelType.Technological
                },
                InfiltrationLevel.Subverted => selected switch
                {
                    0 => IntelType.Military,
                    1 => IntelType.Economic,
                    2 => IntelType.Political,
                    3 => IntelType.Social,
                    _ => IntelType.Technological
                },
                _ => IntelType.Military
            };
        }

        private static uint DeterministicRandom(uint seed)
        {
            seed ^= seed << 13;
            seed ^= seed >> 17;
            seed ^= seed << 5;
            return seed;
        }
    }
}




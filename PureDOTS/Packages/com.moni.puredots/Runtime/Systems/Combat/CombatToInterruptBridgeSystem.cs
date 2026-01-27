using PureDOTS.Runtime.Combat;
using PureDOTS.Runtime.Combat.Targeting;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Interrupts;
using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Systems.Combat
{
    /// <summary>
    /// Bridges combat systems to Interrupts.
    /// Emits interrupts when targets are selected/lost, under attack, etc.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(WarmPathSystemGroup))]
    [UpdateAfter(typeof(TargetSelectionSystem))]
    public partial struct CombatToInterruptBridgeSystem : ISystem
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

            // Process entities with target priority
            foreach (var (targetPriority, threatSources, entity) in
                SystemAPI.Query<RefRO<TargetPriority>, DynamicBuffer<ThreatSource>>()
                .WithEntityAccess())
            {
                // Ensure entity has interrupt buffer
                if (!SystemAPI.HasBuffer<Interrupt>(entity))
                {
                    state.EntityManager.AddBuffer<Interrupt>(entity);
                }

                var interruptBuffer = SystemAPI.GetBuffer<Interrupt>(entity);
                var priority = targetPriority.ValueRO;

                // Emit UnderAttack interrupt if we have active threat sources
                if (threatSources.Length > 0)
                {
                    bool hasUnderAttackInterrupt = false;
                    for (int i = 0; i < interruptBuffer.Length; i++)
                    {
                        var interrupt = interruptBuffer[i];
                        if (interrupt.Type == InterruptType.UnderAttack && interrupt.IsProcessed == 0)
                        {
                            hasUnderAttackInterrupt = true;
                            break;
                        }
                    }

                    if (!hasUnderAttackInterrupt)
                    {
                        // Find highest threat source
                        Entity highestThreatSource = Entity.Null;
                        float highestThreat = 0f;
                        for (int i = 0; i < threatSources.Length; i++)
                        {
                            if (threatSources[i].ThreatAmount > highestThreat)
                            {
                                highestThreat = threatSources[i].ThreatAmount;
                                highestThreatSource = threatSources[i].Source;
                            }
                        }

                        if (highestThreatSource != Entity.Null)
                        {
                            InterruptUtils.EmitCombat(
                                ref interruptBuffer,
                                InterruptType.UnderAttack,
                                highestThreatSource,
                                entity,
                                timeState.Tick,
                                InterruptPriority.High);
                        }
                    }
                }

                // Emit LostTarget interrupt if we had a target but lost it
                if (priority.CurrentTarget == Entity.Null && priority.TargetSelectedTick > 0)
                {
                    // Check if we recently lost target (within last few ticks)
                    var ticksSinceLost = timeState.Tick - priority.TargetSelectedTick;
                    if (ticksSinceLost < 10) // Within last 10 ticks
                    {
                        bool hasLostTargetInterrupt = false;
                        for (int i = 0; i < interruptBuffer.Length; i++)
                        {
                            var interrupt = interruptBuffer[i];
                            if (interrupt.Type == InterruptType.LostTarget && interrupt.IsProcessed == 0)
                            {
                                hasLostTargetInterrupt = true;
                                break;
                            }
                        }

                        if (!hasLostTargetInterrupt)
                        {
                            InterruptUtils.Emit(
                                ref interruptBuffer,
                                InterruptType.LostTarget,
                                InterruptPriority.Normal,
                                Entity.Null,
                                timeState.Tick);
                        }
                    }
                }
            }
        }
    }
}


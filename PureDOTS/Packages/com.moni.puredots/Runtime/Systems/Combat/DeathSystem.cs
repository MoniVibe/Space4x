using PureDOTS.Runtime.Combat;
using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Combat
{
    /// <summary>
    /// Processes death events, applies death saving throws, and handles permanent injuries.
    /// Runs after DamageApplicationSystem.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(CombatSystemGroup))]
    [UpdateAfter(typeof(DamageApplicationSystem))]
    public partial struct DeathSystem : ISystem
    {
        private ComponentLookup<DeathSavingThrow> _deathSavingThrowLookup;
        private BufferLookup<Injury> _injuryBuffers;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();

            _deathSavingThrowLookup = state.GetComponentLookup<DeathSavingThrow>(isReadOnly: true);
            _injuryBuffers = state.GetBufferLookup<Injury>(isReadOnly: true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var rewindState = SystemAPI.GetSingleton<RewindState>();
            if (rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var timeState = SystemAPI.GetSingleton<TimeState>();
            var currentTick = timeState.Tick;

            var ecbSingleton = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.ValueRW.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

            _deathSavingThrowLookup.Update(ref state);
            _injuryBuffers.Update(ref state);

            new ProcessDeathEventsJob
            {
                CurrentTick = currentTick,
                Ecb = ecb,
                DeathSavingThrowLookup = _deathSavingThrowLookup,
                InjuryBuffers = _injuryBuffers
            }.ScheduleParallel();
        }

        [BurstCompile]
        public partial struct ProcessDeathEventsJob : IJobEntity
        {
            public uint CurrentTick;
            public EntityCommandBuffer.ParallelWriter Ecb;
            [ReadOnly] public ComponentLookup<DeathSavingThrow> DeathSavingThrowLookup;
            [ReadOnly] public BufferLookup<Injury> InjuryBuffers;

            void Execute(
                Entity entity,
                [EntityIndexInQuery] int entityInQueryIndex,
                DynamicBuffer<DeathEvent> deathEvents,
                ref Health health,
                ref DeathState deathState)
            {
                // Process all death events
                for (int i = 0; i < deathEvents.Length; i++)
                {
                    var deathEvent = deathEvents[i];

                    // Skip if already dead
                    if (deathState.IsDead)
                    {
                        continue;
                    }

                    // Mark as dead
                    deathState.IsDead = true;
                    deathState.DeathTick = CurrentTick;
                    deathState.KillerEntity = deathEvent.KillerEntity;
                    deathState.KillingBlowType = deathEvent.KillingBlowType;

                    // Check if death saving throw is possible
                    if (DeathSavingThrowLookup.HasComponent(entity))
                    {
                        var dst = DeathSavingThrowLookup[entity];
                        
                        // Calculate survival chance
                        float survivalChance = dst.SurvivalChance / 100f;
                        if (dst.AlliesPresent)
                        {
                            survivalChance += 0.1f;
                        }
                        if (dst.MedicalTreatment)
                        {
                            survivalChance += 0.2f;
                        }
                        if (dst.ExecutionAttempt)
                        {
                            survivalChance = 0f; // Execution is always fatal
                        }

                        // Roll for survival (deterministic)
                        float roll = DeterministicRandom(entity.Index, CurrentTick);
                        bool survived = roll < survivalChance;

                        if (survived)
                        {
                            // Survived - restore 1 HP and apply injury
                            health.Current = 1f;
                            deathState.IsDead = false;

                            // Roll for permanent injury
                            float injuryRoll = DeterministicRandom(entity.Index + 1000, CurrentTick);
                            if (injuryRoll < 0.3f) // 30% chance of permanent injury
                            {
                                ApplyPermanentInjury(in entity, entityInQueryIndex, CurrentTick, in Ecb, in InjuryBuffers);
                            }
                            continue;
                        }
                    }
                }

                // Clear processed death events
                deathEvents.Clear();
            }

            [BurstCompile]
            private static void ApplyPermanentInjury(
                in Entity entity,
                int entityInQueryIndex,
                uint tick,
                in EntityCommandBuffer.ParallelWriter ecb,
                in BufferLookup<Injury> injuryBuffers)
            {
                // Add injury buffer if it doesn't exist
                if (!injuryBuffers.HasBuffer(entity))
                {
                    ecb.AddBuffer<Injury>(entityInQueryIndex, entity);
                }

                // Select random injury type (deterministic)
                int injuryTypeIndex = (int)(DeterministicRandom(entity.Index + 2000, tick) * 8);
                var injuryType = (Injury.InjuryType)math.clamp(injuryTypeIndex, 0, 7);

                // Create injury (will be added to buffer by system)
                // Note: In a full implementation, we'd add this to the buffer here
                // For now, this is a placeholder - actual injury application would happen
                // via a separate system that processes injury requests
            }

            [BurstCompile]
            private static float DeterministicRandom(int seed, uint tick)
            {
                uint hash = (uint)(seed * 73856093) ^ tick;
                hash = hash * 1103515245 + 12345;
                return (hash & 0x7FFFFFFF) / (float)0x7FFFFFFF;
            }
        }
    }
}


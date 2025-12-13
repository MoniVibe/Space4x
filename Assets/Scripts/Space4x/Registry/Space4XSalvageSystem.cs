using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Registry
{
    using Debug = UnityEngine.Debug;

    
    /// <summary>
    /// Manages derelict state and decay over time.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct Space4XDerelictSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<DerelictState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var currentTick = (uint)SystemAPI.Time.ElapsedTime;

            foreach (var (derelict, yield, entity) in
                SystemAPI.Query<RefRW<DerelictState>, RefRW<SalvageYield>>()
                    .WithEntityAccess())
            {
                uint age = currentTick - derelict.ValueRO.DerelictionTick;

                // Decay condition over time
                if (age % 500 == 0 && derelict.ValueRO.Condition < DerelictCondition.Decaying)
                {
                    derelict.ValueRW.Condition = (DerelictCondition)((int)derelict.ValueRO.Condition + 1);

                    // Update yield based on new condition
                    yield.ValueRW = SalvageYield.FromCondition(derelict.ValueRO.Condition, derelict.ValueRO.OriginalClass);
                }

                // Hull degrades
                if ((float)derelict.ValueRO.HullRemaining > 0.01f)
                {
                    derelict.ValueRW.HullRemaining = (half)math.max(0f, (float)derelict.ValueRO.HullRemaining - 0.0001f);
                }

                // Spawn new hazards over time
                if (age % 1000 == 0)
                {
                    uint hazardSeed = (uint)(entity.Index * 54321) + currentTick;
                    DerelictHazard newHazards = DerelictUtility.GenerateHazards(derelict.ValueRO.Cause, age, hazardSeed);
                    derelict.ValueRW.Hazards |= newHazards;
                }
            }
        }
    }

    /// <summary>
    /// Handles salvage operation state machine.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XDerelictSystem))]
    public partial struct Space4XSalvageOperationSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SalvageOperation>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var currentTick = (uint)SystemAPI.Time.ElapsedTime;

            foreach (var (operation, capability, events, transform, entity) in
                SystemAPI.Query<RefRW<SalvageOperation>, RefRO<SalvageCapable>, DynamicBuffer<SalvageEvent>, RefRO<LocalTransform>>()
                    .WithEntityAccess())
            {
                if (operation.ValueRO.Phase == SalvagePhase.None ||
                    operation.ValueRO.Phase == SalvagePhase.Complete ||
                    operation.ValueRO.Phase == SalvagePhase.Aborted)
                {
                    continue;
                }

                // Validate target still exists
                if (!SystemAPI.Exists(operation.ValueRO.Target))
                {
                    operation.ValueRW.Phase = SalvagePhase.Aborted;
                    continue;
                }

                // Get derelict state
                if (!SystemAPI.HasComponent<DerelictState>(operation.ValueRO.Target))
                {
                    operation.ValueRW.Phase = SalvagePhase.Aborted;
                    continue;
                }

                var derelict = SystemAPI.GetComponent<DerelictState>(operation.ValueRO.Target);

                ProcessSalvagePhase(
                    ref operation.ValueRW,
                    capability.ValueRO,
                    events,
                    derelict,
                    transform.ValueRO.Position,
                    currentTick,
                    entity,
                    ref state
                );
            }
        }

        private void ProcessSalvagePhase(
            ref SalvageOperation operation,
            in SalvageCapable capability,
            DynamicBuffer<SalvageEvent> events,
            in DerelictState derelict,
            float3 salvagerPosition,
            uint currentTick,
            Entity salvager,
            ref SystemState systemState)
        {
            float speedMod = (float)operation.SpeedModifier * (1f + (float)capability.SpeedBonus);
            float progressIncrement = 0.01f * speedMod;

            switch (operation.Phase)
            {
                case SalvagePhase.Approaching:
                    // Check if close enough
                    if (SystemAPI.HasComponent<LocalTransform>(operation.Target))
                    {
                        var targetPos = SystemAPI.GetComponent<LocalTransform>(operation.Target).Position;
                        float distance = math.distance(salvagerPosition, targetPos);

                        if (distance < 50f)
                        {
                            operation.Phase = SalvagePhase.Scanning;
                            operation.Progress = (half)0f;
                            AddEvent(events, SalvageEventType.Started, currentTick, 0, 0f);
                        }
                    }
                    break;

                case SalvagePhase.Scanning:
                    operation.Progress = (half)math.min(1f, (float)operation.Progress + progressIncrement);

                    if ((float)operation.Progress >= 1f)
                    {
                        operation.Phase = SalvagePhase.HazardAssessment;
                        operation.Progress = (half)0f;
                        operation.RiskLevel = (half)DerelictUtility.CalculateHazardRisk(derelict.Hazards);
                        AddEvent(events, SalvageEventType.ScanComplete, currentTick, 1, 0f);
                    }
                    break;

                case SalvagePhase.HazardAssessment:
                    operation.Progress = (half)math.min(1f, (float)operation.Progress + progressIncrement);

                    if ((float)operation.Progress >= 1f)
                    {
                        if (derelict.Hazards != DerelictHazard.None)
                        {
                            operation.Phase = SalvagePhase.HazardMitigation;
                            AddEvent(events, SalvageEventType.HazardDetected, currentTick, 0, (float)operation.RiskLevel);
                        }
                        else
                        {
                            operation.Phase = SalvagePhase.Extraction;
                        }
                        operation.Progress = (half)0f;
                    }
                    break;

                case SalvagePhase.HazardMitigation:
                    operation.Progress = (half)math.min(1f, (float)operation.Progress + progressIncrement * 0.5f);

                    // Roll for hazard encounter
                    uint hazardSeed = (uint)(salvager.Index * 98765) + currentTick;
                    if (DerelictUtility.RollHazardEncounter((float)operation.RiskLevel, (float)capability.RiskReduction, hazardSeed))
                    {
                        AddEvent(events, SalvageEventType.HazardTriggered, currentTick, 2, 10f);
                        // Could apply damage to salvager here
                    }

                    if ((float)operation.Progress >= 1f)
                    {
                        operation.Phase = SalvagePhase.Extraction;
                        operation.Progress = (half)0f;
                        operation.RiskLevel = (half)((float)operation.RiskLevel * 0.5f); // Reduced after mitigation
                        AddEvent(events, SalvageEventType.HazardMitigated, currentTick, 1, 0f);
                    }
                    break;

                case SalvagePhase.Extraction:
                    operation.Progress = (half)math.min(1f, (float)operation.Progress + progressIncrement);

                    // Extract resources incrementally
                    if (SystemAPI.HasComponent<SalvageYield>(operation.Target))
                    {
                        var yield = SystemAPI.GetComponent<SalvageYield>(operation.Target);
                        float extractRate = 0.02f * (1f + (float)capability.YieldBonus);

                        operation.ExtractedMetal += yield.ScrapMetal * extractRate;
                        operation.ExtractedFuel += yield.Fuel * extractRate;
                        operation.ExtractedAmmo += yield.Ammunition * extractRate;
                    }

                    if ((float)operation.Progress >= 1f)
                    {
                        // Check for reactivation attempt
                        if (operation.AttemptingReactivation == 1 && capability.CanReactivate == 1)
                        {
                            operation.Phase = SalvagePhase.Reactivation;
                        }
                        else
                        {
                            operation.Phase = SalvagePhase.Complete;
                            AddEvent(events, SalvageEventType.OperationComplete, currentTick, 1, operation.ExtractedMetal);
                        }
                        operation.Progress = (half)0f;
                    }
                    break;

                case SalvagePhase.Reactivation:
                    operation.Progress = (half)math.min(1f, (float)operation.Progress + progressIncrement * 0.3f);

                    if ((float)operation.Progress >= 1f)
                    {
                        // Roll for reactivation success
                        uint reactivateSeed = (uint)(salvager.Index * 13579) + currentTick;
                        var random = new Unity.Mathematics.Random(reactivateSeed);

                        float successChance = derelict.Condition switch
                        {
                            DerelictCondition.Pristine => 0.8f,
                            DerelictCondition.Damaged => 0.4f,
                            _ => 0.1f
                        };

                        if (random.NextFloat() < successChance)
                        {
                            AddEvent(events, SalvageEventType.ReactivationSuccess, currentTick, 1, 0f);
                            // Would remove DerelictTag and restore ship here
                        }
                        else
                        {
                            AddEvent(events, SalvageEventType.ReactivationFailed, currentTick, 2, 0f);
                        }

                        operation.Phase = SalvagePhase.Complete;
                    }
                    break;
            }
        }

        private void AddEvent(DynamicBuffer<SalvageEvent> events, SalvageEventType type, uint tick, byte outcome, float value)
        {
            if (events.Length < events.Capacity)
            {
                events.Add(new SalvageEvent
                {
                    Type = type,
                    Tick = tick,
                    Outcome = outcome,
                    Value = value
                });
            }
        }
    }

    /// <summary>
    /// Creates derelicts from destroyed or abandoned ships.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct Space4XDerelictSpawnSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<HullIntegrity>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var currentTick = (uint)SystemAPI.Time.ElapsedTime;

            foreach (var (hull, transform, entity) in
                SystemAPI.Query<RefRO<HullIntegrity>, RefRO<LocalTransform>>()
                    .WithNone<DerelictTag>()
                    .WithEntityAccess())
            {
                // Check if hull is destroyed (0%)
                if ((float)hull.ValueRO.Current <= 0f)
                {
                    // Convert to derelict
                    ecb.AddComponent(entity, new DerelictTag { });
                    ecb.AddComponent(entity, DerelictState.FromCombat(currentTick, 0, 1));
                    ecb.AddComponent(entity, SalvageYield.FromCondition(DerelictCondition.Damaged, 1));
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }

    /// <summary>
    /// Transfers extracted resources to salvager's supply.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XSalvageOperationSystem))]
    public partial struct Space4XSalvageTransferSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SalvageOperation>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (operation, supply, entity) in
                SystemAPI.Query<RefRW<SalvageOperation>, RefRW<SupplyStatus>>()
                    .WithEntityAccess())
            {
                if (operation.ValueRO.Phase != SalvagePhase.Complete)
                {
                    continue;
                }

                // Transfer extracted resources to supply
                supply.ValueRW.Fuel = math.min(
                    supply.ValueRO.FuelCapacity,
                    supply.ValueRO.Fuel + operation.ValueRO.ExtractedFuel
                );

                supply.ValueRW.Ammunition = math.min(
                    supply.ValueRO.AmmunitionCapacity,
                    supply.ValueRO.Ammunition + operation.ValueRO.ExtractedAmmo
                );

                // Reset operation
                operation.ValueRW.ExtractedMetal = 0f;
                operation.ValueRW.ExtractedFuel = 0f;
                operation.ValueRW.ExtractedAmmo = 0f;
                operation.ValueRW.Phase = SalvagePhase.None;
            }
        }
    }

    /// <summary>
    /// Telemetry for derelict and salvage systems.
    /// </summary>
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    public partial struct Space4XSalvageTelemetrySystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<DerelictState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            int totalDerelicts = 0;
            int claimedDerelicts = 0;
            int pristineDerelicts = 0;
            int activeOperations = 0;
            int completedOperations = 0;
            float totalExtractedValue = 0f;

            foreach (var derelict in SystemAPI.Query<RefRO<DerelictState>>())
            {
                totalDerelicts++;

                if (derelict.ValueRO.IsClaimed == 1)
                {
                    claimedDerelicts++;
                }

                if (derelict.ValueRO.Condition == DerelictCondition.Pristine)
                {
                    pristineDerelicts++;
                }
            }

            foreach (var operation in SystemAPI.Query<RefRO<SalvageOperation>>())
            {
                if (operation.ValueRO.Phase != SalvagePhase.None &&
                    operation.ValueRO.Phase != SalvagePhase.Complete &&
                    operation.ValueRO.Phase != SalvagePhase.Aborted)
                {
                    activeOperations++;
                }

                if (operation.ValueRO.Phase == SalvagePhase.Complete)
                {
                    completedOperations++;
                    totalExtractedValue += operation.ValueRO.ExtractedMetal +
                                          operation.ValueRO.ExtractedFuel +
                                          operation.ValueRO.ExtractedAmmo;
                }
            }

            // Would emit to telemetry stream
            // UnityEngine.Debug.Log($"[Salvage] Derelicts: {totalDerelicts}, Claimed: {claimedDerelicts}, Pristine: {pristineDerelicts}, ActiveOps: {activeOperations}, ExtractedValue: {totalExtractedValue:F0}");
        }
    }
}


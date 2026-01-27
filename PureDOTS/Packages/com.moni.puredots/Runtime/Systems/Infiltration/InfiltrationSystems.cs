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
    /// Updates infiltration progress for active infiltrations.
    /// Activities increase infiltration level over time.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct InfiltrationProgressSystem : ISystem
    {
        private ComponentLookup<CounterIntelligence> _counterIntelLookup;
        private ComponentLookup<CoverIdentity> _coverLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();

            _counterIntelLookup = state.GetComponentLookup<CounterIntelligence>(true);
            _coverLookup = state.GetComponentLookup<CoverIdentity>(true);
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

            // Update every 10 ticks (throttled)
            if (timeState.Tick % 10 != 0)
            {
                return;
            }

            _counterIntelLookup.Update(ref state);
            _coverLookup.Update(ref state);

            foreach (var (infiltration, entity) in SystemAPI.Query<RefRW<InfiltrationState>>().WithEntityAccess())
            {
                if (infiltration.ValueRO.IsExposed != 0 || infiltration.ValueRO.IsExtracting != 0)
                {
                    continue;
                }

                var targetOrg = infiltration.ValueRO.TargetOrganization;
                if (targetOrg == Entity.Null || !_counterIntelLookup.HasComponent(targetOrg))
                {
                    continue;
                }

                var counterIntel = _counterIntelLookup[targetOrg];
                float coverStrength = infiltration.ValueRO.CoverStrength;

                // Get cover credibility if available
                if (_coverLookup.HasComponent(entity))
                {
                    var cover = _coverLookup[entity];
                    coverStrength = math.max(coverStrength, cover.Credibility);
                }

                // Calculate progress rate
                float progressRate = InfiltrationHelpers.CalculateProgressRate(
                    infiltration.ValueRO.Level,
                    infiltration.ValueRO.Method,
                    coverStrength,
                    counterIntel.SecurityLevel);

                // Apply progress (scaled by tick delta)
                float progressDelta = progressRate * 0.01f; // Small increment per update
                infiltration.ValueRW.Progress = math.saturate(infiltration.ValueRO.Progress + progressDelta);
                infiltration.ValueRW.LastActivityTick = timeState.Tick;

                // Check for level up
                if (InfiltrationHelpers.ShouldLevelUp(infiltration.ValueRO, 1.0f))
                {
                    infiltration.ValueRW = InfiltrationHelpers.LevelUp(infiltration.ValueRO);
                }
            }
        }
    }

    /// <summary>
    /// Tracks suspicion levels for infiltrating agents.
    /// Suspicion increases from suspicious activities and decays over time.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(InfiltrationProgressSystem))]
    public partial struct SuspicionTrackingSystem : ISystem
    {
        private ComponentLookup<CounterIntelligence> _counterIntelLookup;
        private ComponentLookup<CoverIdentity> _coverLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();

            _counterIntelLookup = state.GetComponentLookup<CounterIntelligence>(true);
            _coverLookup = state.GetComponentLookup<CoverIdentity>(true);
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

            // Update every 5 ticks
            if (timeState.Tick % 5 != 0)
            {
                return;
            }

            _counterIntelLookup.Update(ref state);
            _coverLookup.Update(ref state);

            foreach (var (infiltration, entity) in SystemAPI.Query<RefRW<InfiltrationState>>().WithEntityAccess())
            {
                if (infiltration.ValueRO.IsExposed != 0)
                {
                    continue;
                }

                var targetOrg = infiltration.ValueRO.TargetOrganization;
                if (targetOrg == Entity.Null || !_counterIntelLookup.HasComponent(targetOrg))
                {
                    continue;
                }

                var counterIntel = _counterIntelLookup[targetOrg];
                float coverStrength = infiltration.ValueRO.CoverStrength;

                // Get cover credibility if available (on spy entity, not target org)
                if (_coverLookup.HasComponent(entity))
                {
                    var cover = _coverLookup[entity];
                    coverStrength = math.max(coverStrength, cover.Credibility);
                }

                // Calculate suspicion decay
                uint timeSinceActivity = timeState.Tick > infiltration.ValueRO.LastActivityTick
                    ? timeState.Tick - infiltration.ValueRO.LastActivityTick
                    : 0;

                float suspicionDecay = InfiltrationHelpers.CalculateSuspicionDecay(
                    infiltration.ValueRO.SuspicionLevel,
                    timeSinceActivity,
                    coverStrength,
                    counterIntel.SuspicionDecayRate);

                infiltration.ValueRW.SuspicionLevel = math.max(0f, infiltration.ValueRO.SuspicionLevel - suspicionDecay);
            }
        }
    }

    /// <summary>
    /// Performs detection checks against infiltrating agents.
    /// Counter-intelligence detects infiltration based on suspicion.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(SuspicionTrackingSystem))]
    public partial struct InfiltrationDetectionSystem : ISystem
    {
        private ComponentLookup<CounterIntelligence> _counterIntelLookup;
        private ComponentLookup<CoverIdentity> _coverLookup;
        private ComponentLookup<Investigation> _investigationLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();

            _counterIntelLookup = state.GetComponentLookup<CounterIntelligence>(true);
            _coverLookup = state.GetComponentLookup<CoverIdentity>(true);
            _investigationLookup = state.GetComponentLookup<Investigation>(true);
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

            // Check every 20 ticks (less frequent)
            if (timeState.Tick % 20 != 0)
            {
                return;
            }

            _counterIntelLookup.Update(ref state);
            _coverLookup.Update(ref state);
            _investigationLookup.Update(ref state);

            // Phase A: Collect entities that need interrupt buffers
            var entitiesNeedingBuffers = new NativeParallelHashMap<Entity, byte>(64, Allocator.Temp);
            var detectedEntities = new NativeList<(Entity entity, Entity targetOrg, float suspicion)>(64, Allocator.Temp);
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (infiltration, entity) in SystemAPI.Query<RefRW<InfiltrationState>>().WithEntityAccess())
            {
                if (infiltration.ValueRO.IsExposed != 0 || infiltration.ValueRO.IsExtracting != 0)
                {
                    continue;
                }

                var targetOrg = infiltration.ValueRO.TargetOrganization;
                if (targetOrg == Entity.Null || !_counterIntelLookup.HasComponent(targetOrg))
                {
                    continue;
                }

                var counterIntel = _counterIntelLookup[targetOrg];
                float coverStrength = infiltration.ValueRO.CoverStrength;

                // Get cover credibility if available
                if (_coverLookup.HasComponent(entity))
                {
                    var cover = _coverLookup[entity];
                    coverStrength = math.max(coverStrength, cover.Credibility);
                }

                // Check for active investigation
                bool hasActiveInvestigation = _investigationLookup.HasComponent(targetOrg) &&
                                             _investigationLookup[targetOrg].IsActive != 0;
                float investigationPower = hasActiveInvestigation ? counterIntel.InvestigationPower : 0f;

                // Perform detection check
                uint seed = (uint)(timeState.Tick + entity.Index);
                bool detected = InfiltrationHelpers.PerformDetectionCheck(
                    infiltration.ValueRO.SuspicionLevel,
                    coverStrength,
                    counterIntel.DetectionRate,
                    investigationPower,
                    seed);

                if (detected)
                {
                    // Expose the agent
                    infiltration.ValueRW.IsExposed = 1;

                    // Collect for buffer creation and interrupt emission
                    if (!SystemAPI.HasBuffer<Interrupt>(entity))
                    {
                        entitiesNeedingBuffers.TryAdd(entity, 1);
                    }

                    detectedEntities.Add((entity, targetOrg, infiltration.ValueRO.SuspicionLevel));

                    // Trigger extraction if plan exists
                    if (SystemAPI.HasComponent<ExtractionPlan>(entity))
                    {
                        var extractionPlan = SystemAPI.GetComponentRW<ExtractionPlan>(entity);
                        extractionPlan.ValueRW.Status = ExtractionStatus.InProgress;
                        extractionPlan.ValueRW.IsActivated = 1;
                    }
                }
            }

            // Ensure buffers exist via EntityManager after completing current dependencies
            state.CompleteDependency();
            foreach (var entry in entitiesNeedingBuffers)
            {
                ecb.AddBuffer<Interrupt>(entry.Key);
            }

            entitiesNeedingBuffers.Dispose();

            ecb.Playback(state.EntityManager);
            ecb.Dispose();

            // Phase B: Emit interrupts (buffers now guaranteed to exist)
            foreach (var (entity, targetOrg, suspicion) in detectedEntities)
            {
                var interruptBuffer = SystemAPI.GetBuffer<Interrupt>(entity);
                InterruptUtils.Emit(
                    ref interruptBuffer,
                    InterruptType.InfiltrationExposed,
                    InterruptPriority.High,
                    entity,
                    timeState.Tick,
                    targetEntity: targetOrg,
                    payloadValue: suspicion,
                    payloadId: (FixedString32Bytes)"infiltration.exposed");
            }

            detectedEntities.Dispose();
        }
    }

    /// <summary>
    /// Executes extraction plans for exposed agents.
    /// Handles emergency and planned extractions.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(InfiltrationDetectionSystem))]
    public partial struct ExtractionExecutionSystem : ISystem
    {
        private ComponentLookup<CounterIntelligence> _counterIntelLookup;
        private ComponentLookup<Investigation> _investigationLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();

            _counterIntelLookup = state.GetComponentLookup<CounterIntelligence>(true);
            _investigationLookup = state.GetComponentLookup<Investigation>(true);
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

            // Update every tick for active extractions
            _counterIntelLookup.Update(ref state);
            _investigationLookup.Update(ref state);

            // Phase A: Collect entities that need interrupt buffers
            var entitiesNeedingBuffers = new NativeParallelHashMap<Entity, byte>(64, Allocator.Temp);
            var extractionStartedEvents = new NativeList<(Entity entity, float3 position, float successChance)>(64, Allocator.Temp);
            var extractionCompletedEvents = new NativeList<(Entity entity, float3 position, bool success, Entity targetOrg)>(64, Allocator.Temp);
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (infiltration, extractionPlan, entity) in
                SystemAPI.Query<RefRO<InfiltrationState>, RefRW<ExtractionPlan>>().WithEntityAccess())
            {
                if (extractionPlan.ValueRO.Status != ExtractionStatus.InProgress)
                {
                    continue;
                }

                if (infiltration.ValueRO.IsExtracting == 0)
                {
                    extractionPlan.ValueRW.IsActivated = 1;
                    var infiltrationRW = SystemAPI.GetComponentRW<InfiltrationState>(entity);
                    infiltrationRW.ValueRW.IsExtracting = 1;

                    // Collect for buffer creation and interrupt emission
                    if (!SystemAPI.HasBuffer<Interrupt>(entity))
                    {
                        entitiesNeedingBuffers.TryAdd(entity, 1);
                    }

                    extractionStartedEvents.Add((entity, extractionPlan.ValueRO.ExtractionPoint, extractionPlan.ValueRO.SuccessChance));
                    continue;
                }

                // Calculate extraction success chance
                var targetOrg = infiltration.ValueRO.TargetOrganization;
                bool hasActiveInvestigation = targetOrg != Entity.Null &&
                                             _investigationLookup.HasComponent(targetOrg) &&
                                             _investigationLookup[targetOrg].IsActive != 0;

                float counterIntelPower = targetOrg != Entity.Null && _counterIntelLookup.HasComponent(targetOrg)
                    ? _counterIntelLookup[targetOrg].InvestigationPower
                    : 0f;

                float successChance = InfiltrationHelpers.CalculateExtractionChance(
                    extractionPlan.ValueRO,
                    infiltration.ValueRO.SuspicionLevel,
                    counterIntelPower,
                    hasActiveInvestigation);

                extractionPlan.ValueRW.SuccessChance = successChance;

                // Check if extraction should complete (simplified - in full implementation would check position/distance)
                // For now, complete after a delay based on success chance
                uint extractionDuration = (uint)(100 / math.max(0.1f, successChance));
                if (timeState.Tick >= extractionPlan.ValueRO.PlannedExtractionTick + extractionDuration)
                {
                    // Roll for success
                    uint seed = (uint)(timeState.Tick + entity.Index);
                    float roll = (DeterministicRandom(seed) % 1000) / 1000f;

                    // Collect for buffer creation and interrupt emission
                    if (!SystemAPI.HasBuffer<Interrupt>(entity))
                    {
                        entitiesNeedingBuffers.TryAdd(entity, 1);
                    }

                    if (roll < successChance)
                    {
                        extractionPlan.ValueRW.Status = ExtractionStatus.Completed;
                        var infiltrationRW = SystemAPI.GetComponentRW<InfiltrationState>(entity);
                        infiltrationRW.ValueRW.IsExposed = 0;
                        infiltrationRW.ValueRW.IsExtracting = 0;
                        infiltrationRW.ValueRW.SuspicionLevel = 0f;
                        infiltrationRW.ValueRW.Level = InfiltrationLevel.None;

                        extractionCompletedEvents.Add((entity, extractionPlan.ValueRO.ExtractionPoint, true, targetOrg));
                    }
                    else
                    {
                        extractionPlan.ValueRW.Status = ExtractionStatus.Failed;
                        extractionCompletedEvents.Add((entity, extractionPlan.ValueRO.ExtractionPoint, false, targetOrg));
                        // Agent captured - would trigger consequences in full implementation
                    }
                }
            }

            // Ensure buffers exist via EntityCommandBuffer after completing current dependencies
            state.CompleteDependency();
            foreach (var entry in entitiesNeedingBuffers)
            {
                ecb.AddBuffer<Interrupt>(entry.Key);
            }

            entitiesNeedingBuffers.Dispose();

            ecb.Playback(state.EntityManager);
            ecb.Dispose();

            // Phase B: Emit interrupts (buffers now guaranteed to exist)
            foreach (var (entity, position, successChance) in extractionStartedEvents)
            {
                var interruptBuffer = SystemAPI.GetBuffer<Interrupt>(entity);
                InterruptUtils.Emit(
                    ref interruptBuffer,
                    InterruptType.InfiltrationExtractionStarted,
                    InterruptPriority.Urgent,
                    entity,
                    timeState.Tick,
                    targetPosition: position,
                    payloadValue: successChance,
                    payloadId: (FixedString32Bytes)"extraction.started");
            }

            foreach (var (entity, position, success, targetOrg) in extractionCompletedEvents)
            {
                var interruptBuffer = SystemAPI.GetBuffer<Interrupt>(entity);
                if (success)
                {
                    InterruptUtils.Emit(
                        ref interruptBuffer,
                        InterruptType.InfiltrationExtractionCompleted,
                        InterruptPriority.Normal,
                        entity,
                        timeState.Tick,
                        targetPosition: position,
                        payloadValue: 1f,
                        payloadId: (FixedString32Bytes)"extraction.completed");
                }
                else
                {
                    InterruptUtils.Emit(
                        ref interruptBuffer,
                        InterruptType.InfiltrationExtractionFailed,
                        InterruptPriority.Critical,
                        entity,
                        timeState.Tick,
                        targetEntity: targetOrg,
                        payloadValue: 0f,
                        payloadId: (FixedString32Bytes)"extraction.failed");
                }
            }

            extractionStartedEvents.Dispose();
            extractionCompletedEvents.Dispose();
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


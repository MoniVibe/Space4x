using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Telemetry;
using PureDOTS.Systems;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    /// <summary>
    /// Advances situation phases based on timers and conditions.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SpatialSystemGroup))]
    public partial struct Space4XSituationSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<SituationState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            var rewindState = SystemAPI.GetSingleton<RewindState>();
            if (rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var deltaTime = timeState.FixedDeltaTime;
            var currentTick = timeState.Tick;

            // Update situation timers and phase progression
            foreach (var (situationRef, timerRef, entity) in SystemAPI.Query<RefRW<SituationState>, RefRW<SituationTimer>>().WithEntityAccess())
            {
                UpdateSituation(ref situationRef.ValueRW, ref timerRef.ValueRW, deltaTime, currentTick);
            }
        }

        [BurstCompile]
        private static void UpdateSituation(ref SituationState situation, ref SituationTimer timer, float deltaTime, uint currentTick)
        {
            // Skip resolved situations
            if (situation.Phase == SituationPhase.Resolved)
            {
                return;
            }

            // Update elapsed time
            timer.ElapsedTime += deltaTime;

            // Get current phase duration
            float phaseDuration = timer.GetPhaseDuration(situation.Phase);
            if (phaseDuration <= 0f)
            {
                phaseDuration = 30f; // Default fallback
            }

            // Update phase progress
            situation.PhaseProgress = (half)math.clamp(timer.ElapsedTime / phaseDuration, 0f, 1f);

            // Check for phase transition
            if (timer.AutoEscalate == 1 && timer.ElapsedTime >= phaseDuration)
            {
                AdvancePhase(ref situation, ref timer, currentTick);
            }
        }

        [BurstCompile]
        private static void AdvancePhase(ref SituationState situation, ref SituationTimer timer, uint currentTick)
        {
            // Reset timer for new phase
            timer.ElapsedTime = 0f;
            situation.PhaseProgress = (half)0f;
            situation.PhaseStartTick = currentTick;

            // Advance to next phase
            switch (situation.Phase)
            {
                case SituationPhase.Detection:
                    situation.Phase = SituationPhase.Escalation;
                    break;

                case SituationPhase.Escalation:
                    situation.Phase = SituationPhase.Climax;
                    break;

                case SituationPhase.Climax:
                    // Auto-resolve to partial success if no intervention
                    situation.Phase = SituationPhase.Aftermath;
                    situation.Outcome = SituationOutcome.PartialSuccess;
                    break;

                case SituationPhase.Aftermath:
                    situation.Phase = SituationPhase.Resolved;
                    break;
            }
        }
    }

    /// <summary>
    /// Applies situation effects to affected entities.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SpatialSystemGroup))]
    [UpdateAfter(typeof(Space4XSituationSystem))]
    public partial struct Space4XSituationEffectSystem : ISystem
    {
        private ComponentLookup<MoraleState> _moraleLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<SituationState>();

            _moraleLookup = state.GetComponentLookup<MoraleState>(false);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            _moraleLookup.Update(ref state);

            var deltaTime = timeState.FixedDeltaTime;

            // Apply effects from active situations
            foreach (var (situation, effects, entity) in SystemAPI.Query<RefRO<SituationState>, DynamicBuffer<SituationEffect>>().WithEntityAccess())
            {
                if (situation.ValueRO.Phase == SituationPhase.Resolved)
                {
                    continue;
                }

                ApplyEffects(in situation.ValueRO, in effects, ref _moraleLookup, deltaTime);
            }
        }

        [BurstCompile]
        private static void ApplyEffects(
            in SituationState situation,
            in DynamicBuffer<SituationEffect> effects,
            ref ComponentLookup<MoraleState> moraleLookup,
            float deltaTime)
        {
            if (situation.AffectedEntity == Entity.Null)
            {
                return;
            }

            for (int i = 0; i < effects.Length; i++)
            {
                var effect = effects[i];

                // Only apply effects for current phase
                if (effect.Phase != situation.Phase)
                {
                    continue;
                }

                // Apply effect based on type
                switch (effect.EffectType)
                {
                    case SituationEffectType.MoralePenalty:
                        if (moraleLookup.HasComponent(situation.AffectedEntity))
                        {
                            var morale = moraleLookup[situation.AffectedEntity];
                            float newMorale = (float)morale.Current - ((float)effect.Magnitude * deltaTime);
                            morale.Current = (half)math.clamp(newMorale, -1f, 1f);
                            moraleLookup[situation.AffectedEntity] = morale;
                        }
                        break;

                    case SituationEffectType.MoraleBonus:
                        if (moraleLookup.HasComponent(situation.AffectedEntity))
                        {
                            var morale = moraleLookup[situation.AffectedEntity];
                            float newMorale = (float)morale.Current + ((float)effect.Magnitude * deltaTime);
                            morale.Current = (half)math.clamp(newMorale, -1f, 1f);
                            moraleLookup[situation.AffectedEntity] = morale;
                        }
                        break;

                    // Other effects would be handled similarly
                }
            }
        }
    }

    /// <summary>
    /// Detects conditions that should spawn new situations.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SpatialSystemGroup))]
    [UpdateBefore(typeof(Space4XSituationSystem))]
    public partial struct Space4XSituationTriggerSystem : ISystem
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

            var rewindState = SystemAPI.GetSingleton<RewindState>();
            if (rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var currentTick = timeState.Tick;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // Check for morale collapse situations
            foreach (var (morale, entity) in SystemAPI.Query<RefRO<MoraleState>>()
                .WithNone<SituationState>()
                .WithEntityAccess())
            {
                if ((float)morale.ValueRO.Current <= MoraleThresholds.CriticalLow)
                {
                    // Spawn morale collapse situation
                    var situationEntity = ecb.CreateEntity();
                    ecb.AddComponent(situationEntity, SituationState.Create(
                        SituationType.MoraleCollapse,
                        entity,
                        0.8f,
                        currentTick));
                    ecb.AddComponent(situationEntity, SituationTimer.Default);
                    ecb.AddBuffer<SituationEffect>(situationEntity);
                    ecb.AddBuffer<SituationResolutionOption>(situationEntity);
                }
            }

            // Check for supply shortage situations on colonies
            foreach (var (colony, entity) in SystemAPI.Query<RefRO<Space4XColony>>()
                .WithNone<SituationState>()
                .WithEntityAccess())
            {
                float demand = Space4XColonySupply.ComputeDemand(colony.ValueRO.Population);
                float ratio = Space4XColonySupply.ComputeSupplyRatio(colony.ValueRO.StoredResources, demand);

                if (ratio < Space4XColonySupply.CriticalThreshold)
                {
                    // Spawn supply shortage situation
                    var situationEntity = ecb.CreateEntity();
                    ecb.AddComponent(situationEntity, SituationState.Create(
                        SituationType.SupplyShortage,
                        entity,
                        1f - ratio,
                        currentTick));
                    ecb.AddComponent(situationEntity, SituationTimer.Default);
                    ecb.AddBuffer<SituationEffect>(situationEntity);
                    ecb.AddBuffer<SituationResolutionOption>(situationEntity);
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }

    /// <summary>
    /// Emits situation telemetry metrics.
    /// </summary>
    [UpdateInGroup(typeof(SpatialSystemGroup))]
    [UpdateAfter(typeof(Space4XSituationSystem))]
    public partial struct Space4XSituationTelemetrySystem : ISystem
    {
        private EntityQuery _situationQuery;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TelemetryStream>();

            _situationQuery = SystemAPI.QueryBuilder()
                .WithAll<SituationState>()
                .Build();
        }

        public void OnUpdate(ref SystemState state)
        {
            var telemetryEntity = SystemAPI.GetSingletonEntity<TelemetryStream>();
            var buffer = state.EntityManager.GetBuffer<TelemetryMetric>(telemetryEntity);

            int activeCount = 0;
            int crisisCount = 0;
            int opportunityCount = 0;
            int detectionCount = 0;
            int escalationCount = 0;
            int climaxCount = 0;

            foreach (var situation in SystemAPI.Query<RefRO<SituationState>>())
            {
                if (situation.ValueRO.Phase == SituationPhase.Resolved)
                {
                    continue;
                }

                activeCount++;

                if (SituationUtility.IsCrisis(situation.ValueRO.Type))
                {
                    crisisCount++;
                }
                else if (SituationUtility.IsOpportunity(situation.ValueRO.Type))
                {
                    opportunityCount++;
                }

                switch (situation.ValueRO.Phase)
                {
                    case SituationPhase.Detection:
                        detectionCount++;
                        break;
                    case SituationPhase.Escalation:
                        escalationCount++;
                        break;
                    case SituationPhase.Climax:
                        climaxCount++;
                        break;
                }
            }

            buffer.AddMetric("space4x.situations.active", activeCount);
            buffer.AddMetric("space4x.situations.crises", crisisCount);
            buffer.AddMetric("space4x.situations.opportunities", opportunityCount);
            buffer.AddMetric("space4x.situations.detection", detectionCount);
            buffer.AddMetric("space4x.situations.escalation", escalationCount);
            buffer.AddMetric("space4x.situations.climax", climaxCount);
        }
    }
}


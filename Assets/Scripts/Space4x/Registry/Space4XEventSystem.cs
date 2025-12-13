using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    using Debug = UnityEngine.Debug;

    
    /// <summary>
    /// Checks trigger conditions and spawns events.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct Space4XEventTriggerSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EventDefinition>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var currentTick = (uint)SystemAPI.Time.ElapsedTime;

            // Only check triggers periodically
            if (currentTick % 100 != 0)
            {
                return;
            }

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (definition, conditions, entity) in
                SystemAPI.Query<RefRW<EventDefinition>, DynamicBuffer<EventTriggerCondition>>()
                    .WithEntityAccess())
            {
                // Check cooldown
                if (definition.ValueRO.CooldownTicks > 0 &&
                    currentTick < definition.ValueRO.LastTriggeredTick + definition.ValueRO.CooldownTicks)
                {
                    continue;
                }

                // Check if can repeat
                if (definition.ValueRO.CanRepeat == 0 && definition.ValueRO.TriggerCount > 0)
                {
                    continue;
                }

                // Evaluate conditions (simplified - would need faction-specific checks)
                bool allConditionsMet = true;
                foreach (var (faction, territory, resources) in
                    SystemAPI.Query<RefRO<Space4XFaction>, RefRO<Space4XTerritoryControl>, RefRO<FactionResources>>())
                {
                    for (int c = 0; c < conditions.Length; c++)
                    {
                        var condition = conditions[c];
                        float value = GetConditionValue(condition.Type, faction.ValueRO, territory.ValueRO, resources.ValueRO);

                        if (!EventMath.EvaluateCondition(value, condition.Operator, condition.ThresholdValue))
                        {
                            allConditionsMet = false;
                            break;
                        }
                    }

                    if (!allConditionsMet) break;

                    // Check probability
                    float probability = EventMath.CalculateTriggerProbability(
                        (float)definition.ValueRO.BaseProbability,
                        1f,
                        currentTick - definition.ValueRO.LastTriggeredTick,
                        definition.ValueRO.CooldownTicks
                    );

                    if (EventMath.RollSuccess(probability, currentTick + (uint)entity.Index))
                    {
                        // Spawn event
                        SpawnEvent(ecb, definition.ValueRO, faction.ValueRO.FactionId, currentTick);
                        definition.ValueRW.LastTriggeredTick = currentTick;
                        definition.ValueRW.TriggerCount++;
                    }

                    break; // Only check first faction for now
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private float GetConditionValue(
            EventConditionType type,
            in Space4XFaction faction,
            in Space4XTerritoryControl territory,
            in FactionResources resources)
        {
            return type switch
            {
                EventConditionType.Credits => resources.Credits,
                EventConditionType.ColonyCount => territory.ColonyCount,
                EventConditionType.FleetStrength => territory.FleetStrength,
                EventConditionType.PopulationTotal => territory.Population,
                _ => 0f
            };
        }

        private void SpawnEvent(
            EntityCommandBuffer ecb,
            in EventDefinition definition,
            ushort factionId,
            uint currentTick)
        {
            var eventEntity = ecb.CreateEntity();

            ecb.AddComponent(eventEntity, new Space4XEvent
            {
                EventTypeId = definition.EventTypeId,
                Category = definition.Category,
                Severity = definition.BaseSeverity,
                Phase = EventPhase.Triggered,
                AffectedFactionId = factionId,
                TriggeredTick = currentTick,
                ExpirationTick = currentTick + definition.Duration,
                DecisionTimer = definition.DecisionTime,
                SelectedChoice = -1,
                IsAcknowledged = 0,
                RandomSeed = currentTick * 12345
            });

            ecb.AddBuffer<EventChoice>(eventEntity);
            ecb.AddBuffer<EventOutcome>(eventEntity);
        }
    }

    /// <summary>
    /// Manages event lifecycle and phase transitions.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XEventTriggerSystem))]
    public partial struct Space4XEventLifecycleSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XEvent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var currentTick = (uint)SystemAPI.Time.ElapsedTime;

            foreach (var evt in SystemAPI.Query<RefRW<Space4XEvent>>())
            {
                switch (evt.ValueRO.Phase)
                {
                    case EventPhase.Triggered:
                        // Advance to announced for player notification
                        evt.ValueRW.Phase = EventPhase.Announced;
                        break;

                    case EventPhase.Announced:
                        // Wait for acknowledgement or auto-advance
                        if (evt.ValueRO.IsAcknowledged != 0)
                        {
                            evt.ValueRW.Phase = EventPhase.Active;
                        }
                        else if (currentTick > evt.ValueRO.TriggeredTick + 100)
                        {
                            // Auto-acknowledge after delay
                            evt.ValueRW.Phase = EventPhase.Active;
                        }
                        break;

                    case EventPhase.Active:
                        // Move to awaiting choice if choices exist
                        evt.ValueRW.Phase = EventPhase.AwaitingChoice;
                        break;

                    case EventPhase.AwaitingChoice:
                        // Decrement decision timer
                        if (evt.ValueRO.DecisionTimer > 0)
                        {
                            evt.ValueRW.DecisionTimer--;
                        }
                        else if (evt.ValueRO.SelectedChoice < 0)
                        {
                            // Time expired, auto-select default
                            evt.ValueRW.SelectedChoice = 0; // Default choice
                            evt.ValueRW.Phase = EventPhase.Resolving;
                        }

                        // Check if choice made
                        if (evt.ValueRO.SelectedChoice >= 0)
                        {
                            evt.ValueRW.Phase = EventPhase.Resolving;
                        }
                        break;

                    case EventPhase.Resolving:
                        // Handled by outcome system
                        break;

                    case EventPhase.Completed:
                    case EventPhase.Failed:
                        // Terminal states - cleanup handled elsewhere
                        break;
                }

                // Check expiration
                if (evt.ValueRO.Phase != EventPhase.Completed &&
                    evt.ValueRO.Phase != EventPhase.Failed &&
                    evt.ValueRO.ExpirationTick > 0 &&
                    currentTick > evt.ValueRO.ExpirationTick)
                {
                    evt.ValueRW.Phase = EventPhase.Failed;
                }
            }
        }
    }

    /// <summary>
    /// Resolves event outcomes based on choices.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XEventLifecycleSystem))]
    public partial struct Space4XEventOutcomeSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XEvent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var currentTick = (uint)SystemAPI.Time.ElapsedTime;

            foreach (var (evt, choices, outcomes, entity) in
                SystemAPI.Query<RefRW<Space4XEvent>, DynamicBuffer<EventChoice>, DynamicBuffer<EventOutcome>>()
                    .WithEntityAccess())
            {
                if (evt.ValueRO.Phase != EventPhase.Resolving)
                {
                    continue;
                }

                // Find selected choice
                EventChoice selectedChoice = default;
                bool choiceFound = false;
                for (int c = 0; c < choices.Length; c++)
                {
                    if (choices[c].ChoiceIndex == evt.ValueRO.SelectedChoice)
                    {
                        selectedChoice = choices[c];
                        choiceFound = true;
                        break;
                    }
                }

                // Roll for success
                float successChance = choiceFound
                    ? EventMath.CalculateChoiceSuccess((float)selectedChoice.SuccessChance, 0.5f, evt.ValueRO.Severity)
                    : 0.5f;

                bool isSuccess = EventMath.RollSuccess(successChance, evt.ValueRO.RandomSeed);

                // Apply relevant outcomes
                for (int o = 0; o < outcomes.Length; o++)
                {
                    var outcome = outcomes[o];

                    // Check if this outcome applies
                    if (outcome.IsSuccessOutcome != 0 && !isSuccess) continue;
                    if (outcome.IsSuccessOutcome == 0 && isSuccess) continue;

                    // Roll probability
                    if (!EventMath.RollSuccess((float)outcome.Probability, evt.ValueRO.RandomSeed + (uint)o))
                    {
                        continue;
                    }

                    // Apply outcome
                    float finalValue = EventMath.CalculateOutcomeVariance(outcome.Value, evt.ValueRO.RandomSeed + (uint)o + 1000, evt.ValueRO.Severity);

                    ApplyOutcome(outcome, finalValue, evt.ValueRO.AffectedFactionId, ref state);
                }

                evt.ValueRW.Phase = isSuccess ? EventPhase.Completed : EventPhase.Failed;
            }
        }

        private void ApplyOutcome(
            in EventOutcome outcome,
            float value,
            ushort factionId,
            ref SystemState state)
        {
            // Find affected faction
            foreach (var (faction, resources, modifiers) in
                SystemAPI.Query<RefRO<Space4XFaction>, RefRW<FactionResources>, DynamicBuffer<RelationModifier>>())
            {
                if (faction.ValueRO.FactionId != factionId)
                {
                    continue;
                }

                switch (outcome.Type)
                {
                    case EventOutcomeType.GainCredits:
                        resources.ValueRW.Credits += value;
                        break;

                    case EventOutcomeType.LoseCredits:
                        resources.ValueRW.Credits = math.max(0, resources.ValueRO.Credits - value);
                        break;

                    case EventOutcomeType.GainResearchPoints:
                        resources.ValueRW.Research += value;
                        break;

                    case EventOutcomeType.GainRelation:
                        if (modifiers.Length < modifiers.Capacity)
                        {
                            modifiers.Add(new RelationModifier
                            {
                                Type = RelationModifierType.GiftReceived,
                                ScoreChange = (sbyte)math.min(127, (int)value),
                                DecayRate = (half)0.1f
                            });
                        }
                        break;

                    case EventOutcomeType.LoseRelation:
                        if (modifiers.Length < modifiers.Capacity)
                        {
                            modifiers.Add(new RelationModifier
                            {
                                Type = RelationModifierType.InsultReceived,
                                ScoreChange = (sbyte)math.max(-128, -(int)value),
                                DecayRate = (half)0.1f
                            });
                        }
                        break;
                }

                break;
            }
        }
    }

    /// <summary>
    /// Records completed events to history.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    public partial struct Space4XEventHistorySystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XEvent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var currentTick = (uint)SystemAPI.Time.ElapsedTime;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (evt, entity) in SystemAPI.Query<RefRO<Space4XEvent>>().WithEntityAccess())
            {
                if (evt.ValueRO.Phase != EventPhase.Completed && evt.ValueRO.Phase != EventPhase.Failed)
                {
                    continue;
                }

                // Find faction and add to history
                foreach (var (faction, history) in SystemAPI.Query<RefRO<Space4XFaction>, DynamicBuffer<EventHistoryEntry>>())
                {
                    if (faction.ValueRO.FactionId != evt.ValueRO.AffectedFactionId)
                    {
                        continue;
                    }

                    if (history.Length < history.Capacity)
                    {
                        history.Add(new EventHistoryEntry
                        {
                            EventTypeId = evt.ValueRO.EventTypeId,
                            Category = evt.ValueRO.Category,
                            Severity = evt.ValueRO.Severity,
                            ChoiceMade = evt.ValueRO.SelectedChoice,
                            WasSuccessful = (byte)(evt.ValueRO.Phase == EventPhase.Completed ? 1 : 0),
                            CompletedTick = currentTick
                        });
                    }

                    break;
                }

                // Destroy completed event entity
                ecb.DestroyEntity(entity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }

    /// <summary>
    /// Creates event definitions for common event types.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct Space4XEventDefinitionSetupSystem : ISystem
    {
        private bool _initialized;

        public void OnCreate(ref SystemState state)
        {
        }

        public void OnUpdate(ref SystemState state)
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;

            // Create some default event definitions
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // Resource windfall event
            CreateEventDefinition(ecb, 1, EventCategory.Economic, EventSeverity.Minor, 0.001f, 10000, 5000, 3000);

            // Pirate raid event
            CreateEventDefinition(ecb, 2, EventCategory.Crisis, EventSeverity.Moderate, 0.002f, 5000, 3000, 2000);

            // Discovery event
            CreateEventDefinition(ecb, 3, EventCategory.Discovery, EventSeverity.Minor, 0.003f, 8000, 4000, 5000);

            // Political unrest event
            CreateEventDefinition(ecb, 4, EventCategory.Political, EventSeverity.Major, 0.0005f, 20000, 8000, 4000);

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private void CreateEventDefinition(
            EntityCommandBuffer ecb,
            ushort typeId,
            EventCategory category,
            EventSeverity severity,
            float probability,
            uint cooldown,
            uint duration,
            uint decisionTime)
        {
            var entity = ecb.CreateEntity();

            ecb.AddComponent(entity, new EventDefinition
            {
                EventTypeId = typeId,
                Category = category,
                BaseSeverity = severity,
                BaseProbability = (half)probability,
                CooldownTicks = cooldown,
                Duration = duration,
                DecisionTime = decisionTime,
                CanRepeat = 1,
                AutoResolves = 1,
                DefaultChoice = 0
            });

            ecb.AddBuffer<EventTriggerCondition>(entity);
        }
    }

    /// <summary>
    /// Telemetry for event system.
    /// </summary>
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    public partial struct Space4XEventTelemetrySystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XEvent>();
        }

        public void OnUpdate(ref SystemState state)
        {
            int activeEvents = 0;
            int awaitingChoice = 0;
            int completedThisTick = 0;
            int failedThisTick = 0;

            foreach (var evt in SystemAPI.Query<RefRO<Space4XEvent>>())
            {
                switch (evt.ValueRO.Phase)
                {
                    case EventPhase.Active:
                    case EventPhase.Announced:
                    case EventPhase.Resolving:
                        activeEvents++;
                        break;
                    case EventPhase.AwaitingChoice:
                        awaitingChoice++;
                        activeEvents++;
                        break;
                    case EventPhase.Completed:
                        completedThisTick++;
                        break;
                    case EventPhase.Failed:
                        failedThisTick++;
                        break;
                }
            }

            // Would emit to telemetry stream
            // UnityEngine.Debug.Log($"[Events] Active: {activeEvents}, AwaitingChoice: {awaitingChoice}, Completed: {completedThisTick}, Failed: {failedThisTick}");
        }
    }
}


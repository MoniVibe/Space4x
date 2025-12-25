using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Intent;
using PureDOTS.Runtime.Interrupts;
using Space4X.Registry;
using Space4X.Runtime;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Systems
{
    /// <summary>
    /// Bridges PureDOTS EntityIntent to Space4X VesselAIState.
    /// Consumes interrupt-driven intents and maps them to vessel goals.
    /// Runs after InterruptSystemGroup, before Space4XVesselAICommandBridgeSystem.
    /// 
    /// Note: QueuedIntent buffer support is optional. Entities can opt-in by adding
    /// DynamicBuffer&lt;QueuedIntent&gt; to enable queued intent promotion via IntentProcessingSystem.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PureDOTS.Systems.ResourceSystemGroup))]
    [UpdateAfter(typeof(PureDOTS.Systems.InterruptSystemGroup))]
    [UpdateBefore(typeof(Space4XVesselAICommandBridgeSystem))]
    public partial struct Space4XVesselIntentBridgeSystem : ISystem
    {
        private ComponentLookup<VesselAIState> _aiStateLookup;
        private ComponentLookup<MiningVessel> _miningVesselLookup;
        private EntityStorageInfoLookup _entityStorageInfoLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            _aiStateLookup = state.GetComponentLookup<VesselAIState>(false);
            _miningVesselLookup = state.GetComponentLookup<MiningVessel>(true);
            _entityStorageInfoLookup = state.GetEntityStorageInfoLookup();
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

            _aiStateLookup.Update(ref state);
            _miningVesselLookup.Update(ref state);
            _entityStorageInfoLookup.Update(ref state);

            // Process all entities with EntityIntent and VesselAIState
            foreach (var (intent, entity) in
                SystemAPI.Query<RefRW<EntityIntent>>()
                .WithAll<VesselAIState>()
                .WithEntityAccess())
            {
                // Skip if intent is invalid or idle
                if (intent.ValueRO.IsValid == 0 || intent.ValueRO.Mode == IntentMode.Idle)
                {
                    // If intent is invalid and AI state is idle, clear intent
                    if (intent.ValueRO.IsValid == 0)
                    {
                        var existingAiState = _aiStateLookup[entity];
                        if (existingAiState.CurrentGoal == VesselAIState.Goal.None && existingAiState.CurrentState == VesselAIState.State.Idle)
                        {
                            IntentService.ClearIntent(ref intent.ValueRW);
                        }
                    }
                    continue;
                }

                if (!_aiStateLookup.HasComponent(entity))
                {
                    continue;
                }

                // Only process mining vessels for now
                if (!_miningVesselLookup.HasComponent(entity))
                {
                    continue;
                }

                var aiState = _aiStateLookup[entity];
                var newGoal = MapIntentToGoal(intent.ValueRO, entity);

                // Only update if goal changed or if we need to update target
                bool shouldUpdate = false;

                if (newGoal != VesselAIState.Goal.None && newGoal != aiState.CurrentGoal)
                {
                    aiState.CurrentGoal = newGoal;
                    aiState.CurrentState = GoalToState(newGoal);
                    aiState.StateTimer = 0f;
                    aiState.StateStartTick = timeState.Tick;
                    shouldUpdate = true;
                }

                // Update target if provided
                if (intent.ValueRO.TargetEntity != Entity.Null && intent.ValueRO.TargetEntity != aiState.TargetEntity)
                {
                    aiState.TargetEntity = intent.ValueRO.TargetEntity;
                    shouldUpdate = true;
                }

                if (math.any(intent.ValueRO.TargetPosition != float3.zero) && math.distance(intent.ValueRO.TargetPosition, aiState.TargetPosition) > 0.1f)
                {
                    aiState.TargetPosition = intent.ValueRO.TargetPosition;
                    shouldUpdate = true;
                }

                if (shouldUpdate)
                {
                    _aiStateLookup[entity] = aiState;
                }

                // Check if intent should be cleared (goal completed or invalid)
                if (ShouldClearIntent(intent.ValueRO, aiState, ref _entityStorageInfoLookup))
                {
                    IntentService.ClearIntent(ref intent.ValueRW);
                }
            }
        }

        /// <summary>
        /// Maps IntentMode to VesselAIState.Goal.
        /// </summary>
        [BurstCompile]
        private VesselAIState.Goal MapIntentToGoal(in EntityIntent intent, Entity vesselEntity)
        {
            // Check if target is an asteroid (for mining vessels)
            bool isMiningTarget = intent.TargetEntity != Entity.Null && _miningVesselLookup.HasComponent(vesselEntity);
            bool isMiningVessel = _miningVesselLookup.HasComponent(vesselEntity);

            return intent.Mode switch
            {
                IntentMode.Idle => VesselAIState.Goal.None,
                IntentMode.MoveTo => isMiningTarget 
                    ? VesselAIState.Goal.Mining 
                    : VesselAIState.Goal.Idle, // Use Idle as fallback for MoveTo
                IntentMode.Attack => VesselAIState.Goal.None, // Combat not yet implemented for vessels (may need VesselAIState.Goal.Combat extension)
                IntentMode.Flee => VesselAIState.Goal.Returning, // Retreat to carrier
                IntentMode.Gather => VesselAIState.Goal.Mining, // Mining vessels gather resources
                IntentMode.ExecuteOrder => VesselAIState.Goal.Mining, // Assume mining order
                IntentMode.Patrol => VesselAIState.Goal.Patrol,
                IntentMode.Follow => VesselAIState.Goal.Formation, // Follow as formation
                IntentMode.Defend => VesselAIState.Goal.Escort, // Defend as escort
                IntentMode.UseAbility => isMiningVessel 
                    ? VesselAIState.Goal.Mining 
                    : VesselAIState.Goal.None, // Map to Mining if mining vessel, otherwise game-specific
                IntentMode.Build => VesselAIState.Goal.None, // Not applicable to vessels
                // Custom modes are intentionally mapped to None - game-specific systems should handle these
                IntentMode.Custom0 => VesselAIState.Goal.None, // Game-specific handling required
                IntentMode.Custom1 => VesselAIState.Goal.None, // Game-specific handling required
                IntentMode.Custom2 => VesselAIState.Goal.None, // Game-specific handling required
                IntentMode.Custom3 => VesselAIState.Goal.None, // Game-specific handling required
                _ => VesselAIState.Goal.None
            };
        }

        /// <summary>
        /// Maps VesselAIState.Goal to VesselAIState.State.
        /// </summary>
        [BurstCompile]
        private static VesselAIState.State GoalToState(VesselAIState.Goal goal)
        {
            return goal switch
            {
                VesselAIState.Goal.Mining => VesselAIState.State.Mining,
                VesselAIState.Goal.Returning => VesselAIState.State.Returning,
                VesselAIState.Goal.Formation => VesselAIState.State.MovingToTarget,
                VesselAIState.Goal.Patrol => VesselAIState.State.MovingToTarget,
                VesselAIState.Goal.Escort => VesselAIState.State.MovingToTarget,
                _ => VesselAIState.State.Idle
            };
        }

        /// <summary>
        /// Determines if intent should be cleared (goal completed or invalid).
        /// </summary>
        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool ShouldClearIntent(in EntityIntent intent, in VesselAIState aiState, ref EntityStorageInfoLookup entityStorageInfoLookup)
        {
            // Clear if goal is None and state is Idle (goal completed)
            if (aiState.CurrentGoal == VesselAIState.Goal.None && aiState.CurrentState == VesselAIState.State.Idle)
            {
                return true;
            }

            // Clear if target entity was destroyed
            if (intent.TargetEntity != Entity.Null && !entityStorageInfoLookup.Exists(intent.TargetEntity))
            {
                return true; // Target destroyed
            }

            // Don't clear if intent is still valid and goal matches
            return false;
        }
    }
}


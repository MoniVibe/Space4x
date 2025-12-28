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
        private ComponentLookup<Asteroid> _asteroidLookup;
        private ComponentLookup<AttackMoveIntent> _attackMoveLookup;
        private ComponentLookup<AttackMoveOrigin> _attackMoveOriginLookup;
        private ComponentLookup<AttackMoveSourceHint> _attackMoveSourceHintLookup;
        private ComponentLookup<Space4XEngagement> _engagementLookup;
        private ComponentLookup<VesselStanceComponent> _stanceLookup;
        private ComponentLookup<PatrolBehavior> _patrolBehaviorLookup;
        private ComponentLookup<WaypointPath> _waypointPathLookup;
        private EntityStorageInfoLookup _entityStorageInfoLookup;
        private const uint AttackMoveHintMaxAgeTicks = 30;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            _aiStateLookup = state.GetComponentLookup<VesselAIState>(false);
            _miningVesselLookup = state.GetComponentLookup<MiningVessel>(true);
            _asteroidLookup = state.GetComponentLookup<Asteroid>(true);
            _attackMoveLookup = state.GetComponentLookup<AttackMoveIntent>(false);
            _attackMoveOriginLookup = state.GetComponentLookup<AttackMoveOrigin>(false);
            _attackMoveSourceHintLookup = state.GetComponentLookup<AttackMoveSourceHint>(true);
            _engagementLookup = state.GetComponentLookup<Space4XEngagement>(true);
            _stanceLookup = state.GetComponentLookup<VesselStanceComponent>(false);
            _patrolBehaviorLookup = state.GetComponentLookup<PatrolBehavior>(true);
            _waypointPathLookup = state.GetComponentLookup<WaypointPath>(true);
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
            _asteroidLookup.Update(ref state);
            _attackMoveLookup.Update(ref state);
            _attackMoveOriginLookup.Update(ref state);
            _attackMoveSourceHintLookup.Update(ref state);
            _engagementLookup.Update(ref state);
            _stanceLookup.Update(ref state);
            _patrolBehaviorLookup.Update(ref state);
            _waypointPathLookup.Update(ref state);
            _entityStorageInfoLookup.Update(ref state);

            var stanceConfig = Space4XStanceTuningConfig.Default;
            if (SystemAPI.TryGetSingleton<Space4XStanceTuningConfig>(out var stanceConfigSingleton))
            {
                stanceConfig = stanceConfigSingleton;
            }

            var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);
            var hasStructuralChanges = false;

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

                TryApplyCommandOverrideNeutral(entity, intent.ValueRO, timeState.Tick, stanceConfig);

                if (!_miningVesselLookup.HasComponent(entity))
                {
                    TryApplyAttackMoveIntent(entity, intent.ValueRO, timeState.Tick, ref ecb, ref hasStructuralChanges);
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

            if (hasStructuralChanges)
            {
                ecb.Playback(state.EntityManager);
            }
        }

        /// <summary>
        /// Maps IntentMode to VesselAIState.Goal.
        /// </summary>
        [BurstCompile]
        private VesselAIState.Goal MapIntentToGoal(in EntityIntent intent, Entity vesselEntity)
        {
            // Check if target is an asteroid (for mining vessels)
            bool isMiningVessel = _miningVesselLookup.HasComponent(vesselEntity);
            bool isMiningTarget = intent.TargetEntity != Entity.Null && _asteroidLookup.HasComponent(intent.TargetEntity);

            return intent.Mode switch
            {
                IntentMode.Idle => VesselAIState.Goal.None,
                IntentMode.MoveTo => isMiningTarget && isMiningVessel
                    ? VesselAIState.Goal.Mining
                    : VesselAIState.Goal.Patrol,
                IntentMode.Attack => VesselAIState.Goal.None, // Combat not yet implemented for vessels (may need VesselAIState.Goal.Combat extension)
                IntentMode.Flee => VesselAIState.Goal.Returning, // Retreat to carrier
                IntentMode.Gather => VesselAIState.Goal.Mining, // Mining vessels gather resources
                IntentMode.ExecuteOrder => isMiningVessel
                    ? VesselAIState.Goal.Mining
                    : VesselAIState.Goal.Patrol,
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

        private void TryApplyAttackMoveIntent(
            Entity vesselEntity,
            in EntityIntent intent,
            uint currentTick,
            ref EntityCommandBuffer ecb,
            ref bool hasStructuralChanges)
        {
            if (intent.IsValid == 0)
            {
                return;
            }

            var hasTargetPosition = math.any(intent.TargetPosition != float3.zero);
            if (!hasTargetPosition)
            {
                return;
            }

            if (intent.Mode == IntentMode.MoveTo)
            {
                var engageTarget = Entity.Null;
                if (_engagementLookup.HasComponent(vesselEntity))
                {
                    var engagement = _engagementLookup[vesselEntity];
                    if (engagement.PrimaryTarget != Entity.Null)
                    {
                        engageTarget = engagement.PrimaryTarget;
                    }
                }

                if (engageTarget == Entity.Null)
                {
                    return;
                }

                var wasPatrolling = ResolveWasPatrolling(vesselEntity);
                UpsertAttackMoveIntent(vesselEntity, intent.TargetPosition, engageTarget, 0, wasPatrolling,
                    AttackMoveSource.MoveWhileEngaged, currentTick, ref ecb, ref hasStructuralChanges);
                return;
            }

            if (intent.Mode == IntentMode.Attack)
            {
                var engageTarget = intent.TargetEntity;
                var acquireAlongRoute = engageTarget == Entity.Null ? (byte)1 : (byte)0;
                var wasPatrolling = ResolveWasPatrolling(vesselEntity);
                var source = ResolveAttackMoveSource(vesselEntity, intent, currentTick, ref ecb, ref hasStructuralChanges);
                UpsertAttackMoveIntent(vesselEntity, intent.TargetPosition, engageTarget, acquireAlongRoute, wasPatrolling,
                    source, currentTick, ref ecb, ref hasStructuralChanges);
            }
        }

        private void UpsertAttackMoveIntent(
            Entity vesselEntity,
            float3 destination,
            Entity engageTarget,
            byte acquireAlongRoute,
            byte wasPatrolling,
            AttackMoveSource source,
            uint currentTick,
            ref EntityCommandBuffer ecb,
            ref bool hasStructuralChanges)
        {
            var intent = new AttackMoveIntent
            {
                Destination = destination,
                DestinationRadius = 0f,
                EngageTarget = engageTarget,
                AcquireTargetsAlongRoute = acquireAlongRoute,
                KeepFiringWhileInRange = 1,
                StartTick = currentTick,
                Source = source
            };

            if (_attackMoveLookup.HasComponent(vesselEntity))
            {
                _attackMoveLookup[vesselEntity] = intent;
            }
            else
            {
                ecb.AddComponent(vesselEntity, intent);
                hasStructuralChanges = true;
            }

            if (!_attackMoveOriginLookup.HasComponent(vesselEntity))
            {
                ecb.AddComponent(vesselEntity, new AttackMoveOrigin
                {
                    WasPatrolling = wasPatrolling
                });
                hasStructuralChanges = true;
            }
        }

        private byte ResolveWasPatrolling(Entity vesselEntity)
        {
            if (_patrolBehaviorLookup.HasComponent(vesselEntity) || _waypointPathLookup.HasComponent(vesselEntity))
            {
                return 1;
            }

            return 0;
        }

        private AttackMoveSource ResolveAttackMoveSource(
            Entity vesselEntity,
            in EntityIntent intent,
            uint currentTick,
            ref EntityCommandBuffer ecb,
            ref bool hasStructuralChanges)
        {
            if (_attackMoveSourceHintLookup.HasComponent(vesselEntity))
            {
                var hint = _attackMoveSourceHintLookup[vesselEntity];
                var isFresh = hint.IssuedTick == 0
                    ? currentTick <= AttackMoveHintMaxAgeTicks
                    : currentTick >= hint.IssuedTick && currentTick - hint.IssuedTick <= AttackMoveHintMaxAgeTicks;

                ecb.RemoveComponent<AttackMoveSourceHint>(vesselEntity);
                hasStructuralChanges = true;

                if (intent.TargetEntity == Entity.Null && isFresh)
                {
                    return hint.Source;
                }
            }

            return AttackMoveSource.AttackTerrain;
        }

        private void TryApplyCommandOverrideNeutral(
            Entity entity,
            in EntityIntent intent,
            uint currentTick,
            in Space4XStanceTuningConfig stanceConfig)
        {
            if (intent.IntentSetTick != currentTick)
            {
                return;
            }

            if (intent.Mode == IntentMode.Attack)
            {
                return;
            }

            if (!_stanceLookup.HasComponent(entity) || !_engagementLookup.HasComponent(entity))
            {
                return;
            }

            var engagement = _engagementLookup[entity];
            if (engagement.PrimaryTarget == Entity.Null)
            {
                return;
            }

            var stance = _stanceLookup[entity];
            var tuning = stanceConfig.Resolve(stance.CurrentStance);
            if (tuning.CommandOverrideDropsToNeutral == 0)
            {
                return;
            }

            stance.CurrentStance = VesselStanceMode.Neutral;
            stance.DesiredStance = VesselStanceMode.Neutral;
            stance.StanceChangeTick = currentTick;
            _stanceLookup[entity] = stance;
        }
    }
}

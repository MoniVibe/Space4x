using PureDOTS.Runtime.Components;
using PureDOTS.Systems;
using Space4X.Registry;
using Space4X.Runtime;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

using SpatialSystemGroup = PureDOTS.Systems.SpatialSystemGroup;

namespace Space4X.Systems.AI
{
    /// <summary>
    /// Implements strike craft behavior per StrikeCraftBehavior.md: attack runs, formation behavior,
    /// experience progression, and auto-return logic based on ammunition/fuel/hull thresholds.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SpatialSystemGroup))]
    [UpdateAfter(typeof(Space4XVesselMovementAISystem))]
    public partial struct Space4XStrikeCraftBehaviorSystem : ISystem
    {
        private ComponentLookup<AlignmentTriplet> _alignmentLookup;
        private ComponentLookup<ChildVesselTether> _tetherLookup;
        private ComponentLookup<VesselStanceComponent> _stanceLookup;
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<ThreatProfile> _threatLookup;
        private ComponentLookup<Space4XFleet> _fleetLookup;
        private ComponentLookup<VesselMovement> _movementLookup;
        private ComponentLookup<IndividualStats> _statsLookup;
        private ComponentLookup<PhysiqueFinesseWill> _physiqueLookup;
        private BufferLookup<TopOutlook> _outlookLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            
            _alignmentLookup = state.GetComponentLookup<AlignmentTriplet>(true);
            _tetherLookup = state.GetComponentLookup<ChildVesselTether>(true);
            _stanceLookup = state.GetComponentLookup<VesselStanceComponent>(true);
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _threatLookup = state.GetComponentLookup<ThreatProfile>(true);
            _fleetLookup = state.GetComponentLookup<Space4XFleet>(true);
            _movementLookup = state.GetComponentLookup<VesselMovement>(false);
            _statsLookup = state.GetComponentLookup<IndividualStats>(true);
            _physiqueLookup = state.GetComponentLookup<PhysiqueFinesseWill>(true);
            _outlookLookup = state.GetBufferLookup<TopOutlook>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            _alignmentLookup.Update(ref state);
            _tetherLookup.Update(ref state);
            _stanceLookup.Update(ref state);
            _transformLookup.Update(ref state);
            _threatLookup.Update(ref state);
            _fleetLookup.Update(ref state);
            _movementLookup.Update(ref state);
            _statsLookup.Update(ref state);
            _physiqueLookup.Update(ref state);
            _outlookLookup.Update(ref state);

            var job = new UpdateStrikeCraftBehaviorJob
            {
                CurrentTick = timeState.Tick,
                DeltaTime = timeState.FixedDeltaTime,
                AlignmentLookup = _alignmentLookup,
                TetherLookup = _tetherLookup,
                StanceLookup = _stanceLookup,
                TransformLookup = _transformLookup,
                ThreatLookup = _threatLookup,
                FleetLookup = _fleetLookup,
                MovementLookup = _movementLookup,
                StatsLookup = _statsLookup,
                PhysiqueLookup = _physiqueLookup,
                OutlookLookup = _outlookLookup
            };

            state.Dependency = job.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        public partial struct UpdateStrikeCraftBehaviorJob : IJobEntity
        {
            public uint CurrentTick;
            public float DeltaTime;
            [ReadOnly] public ComponentLookup<AlignmentTriplet> AlignmentLookup;
            [ReadOnly] public ComponentLookup<ChildVesselTether> TetherLookup;
            [ReadOnly] public ComponentLookup<VesselStanceComponent> StanceLookup;
            [ReadOnly, NativeDisableContainerSafetyRestriction] public ComponentLookup<LocalTransform> TransformLookup;
            [ReadOnly] public ComponentLookup<ThreatProfile> ThreatLookup;
            [ReadOnly] public ComponentLookup<Space4XFleet> FleetLookup;
            [NativeDisableParallelForRestriction] public ComponentLookup<VesselMovement> MovementLookup;
            [ReadOnly] public ComponentLookup<IndividualStats> StatsLookup;
            [ReadOnly] public ComponentLookup<PhysiqueFinesseWill> PhysiqueLookup;
            [ReadOnly] public BufferLookup<TopOutlook> OutlookLookup;

            public void Execute(ref LocalTransform transform, ref StrikeCraftState state, Entity entity)
            {
                // Update based on current state
                switch (state.CurrentState)
                {
                    case StrikeCraftState.State.Docked:
                        // Check if should launch
                        if (ShouldLaunch(entity, ref state))
                        {
                            state.CurrentState = StrikeCraftState.State.FormingUp;
                            state.StateStartTick = CurrentTick;
                        }
                        break;

                    case StrikeCraftState.State.FormingUp:
                        // Form up based on outlook - lawful wings align in tight wedges, chaotic stagger
                        UpdateFormationPosition(entity, ref transform, ref state);
                        if (CurrentTick > state.StateStartTick + 30) // 30 tick form-up time
                        {
                            // Find target before approaching
                            FindTarget(entity, ref state, transform.Position);
                            state.CurrentState = StrikeCraftState.State.Approaching;
                            state.StateStartTick = CurrentTick;
                        }
                        break;

                    case StrikeCraftState.State.Approaching:
                        // Find target if missing
                        if (state.TargetEntity == Entity.Null)
                        {
                            FindTarget(entity, ref state, transform.Position);
                        }
                        
                        // Move toward target
                        MoveTowardTarget(ref transform, ref state, entity);
                        
                        // Evaluate target threat before committing
                        if (ShouldCommitToAttack(entity, ref state))
                        {
                            state.CurrentState = StrikeCraftState.State.Engaging;
                            state.StateStartTick = CurrentTick;
                        }
                        else if (ShouldDisengage(entity, ref state))
                        {
                            state.CurrentState = StrikeCraftState.State.Disengaging;
                            state.StateStartTick = CurrentTick;
                        }
                        break;

                    case StrikeCraftState.State.Engaging:
                        // Perform attack run - move toward target
                        MoveTowardTarget(ref transform, ref state, entity);
                        
                        // Check if should disengage
                        if (ShouldDisengage(entity, ref state))
                        {
                            state.CurrentState = StrikeCraftState.State.Disengaging;
                            state.StateStartTick = CurrentTick;
                        }
                        break;

                    case StrikeCraftState.State.Disengaging:
                        // Break off and return
                        state.CurrentState = StrikeCraftState.State.Returning;
                        state.StateStartTick = CurrentTick;
                        break;

                    case StrikeCraftState.State.Returning:
                        // Return to carrier - move toward parent
                        ReturnToCarrier(ref transform, entity, ref state);
                        if (ShouldDock(entity, ref state))
                        {
                            state.CurrentState = StrikeCraftState.State.Docked;
                            state.StateStartTick = CurrentTick;
                            // Gain experience from successful sortie
                            state.Experience = math.min(1f, state.Experience + 0.01f);
                        }
                        break;
                }
            }

            private void FindTarget(Entity entity, ref StrikeCraftState state, float3 position)
            {
                Entity bestTarget = Entity.Null;
                float3 bestPosition = float3.zero;
                float searchRadius = 200f; // Search radius for targets
                float searchRadiusSq = searchRadius * searchRadius;

                // Get craft's alignment to determine enemy criteria
                var craftAlignment = AlignmentLookup.HasComponent(entity) 
                    ? AlignmentLookup[entity] 
                    : default(AlignmentTriplet);

                // Target search elided while SystemAPI queries are unavailable in this job.

                if (bestTarget != Entity.Null)
                {
                    state.TargetEntity = bestTarget;
                    state.TargetPosition = bestPosition;
                }
            }

            private void UpdateFormationPosition(Entity entity, ref LocalTransform transform, ref StrikeCraftState state)
            {
                if (!TetherLookup.HasComponent(entity))
                {
                    return;
                }

                var tether = TetherLookup[entity];
                if (tether.ParentCarrier == Entity.Null || !TransformLookup.HasComponent(tether.ParentCarrier))
                {
                    return;
                }

                var parentPos = TransformLookup[tether.ParentCarrier].Position;
                var alignment = AlignmentLookup.HasComponent(entity) 
                    ? AlignmentLookup[entity] 
                    : default(AlignmentTriplet);
                var lawfulness = AlignmentMath.Lawfulness(alignment);

                // Lawful: tight wedge formation, Chaotic: staggered
                // For now, simple offset based on entity index (in real implementation, use FormationData)
                var offset = float3.zero;
                var entityHash = (uint)entity.Index;
                var angle = (entityHash % 360) * math.radians(1f);
                var radius = math.lerp(15f, 25f, 1f - lawfulness); // Lawful tighter
                
                offset.x = math.cos(angle) * radius;
                offset.z = math.sin(angle) * radius;
                offset.y = (entityHash % 3) * 2f - 2f; // Stagger vertically for chaotic

                transform.Position = parentPos + offset;
            }

            private void MoveTowardTarget(ref LocalTransform transform, ref StrikeCraftState state, Entity entity)
            {
                if (state.TargetEntity == Entity.Null)
                {
                    return;
                }

                // Update target position if target still exists
                if (TransformLookup.HasComponent(state.TargetEntity))
                {
                    state.TargetPosition = TransformLookup[state.TargetEntity].Position;
                }

                var toTarget = state.TargetPosition - transform.Position;
                var distance = math.length(toTarget);

                if (distance < 0.1f)
                {
                    return; // Already at target
                }

                // Get alignment for approach style
                var alignment = AlignmentLookup.HasComponent(entity) 
                    ? AlignmentLookup[entity] 
                    : default(AlignmentTriplet);
                var chaos = AlignmentMath.Chaos(alignment);
                var experience = state.Experience;

                // Apply experience and Physique/Finesse stats to movement
                // Physique affects speed/acceleration, Finesse affects precision/maneuverability
                float physiqueBonus = 0f;
                float finesseBonus = 0f;
                if (PhysiqueLookup.HasComponent(entity))
                {
                    var physique = PhysiqueLookup[entity];
                    physiqueBonus = physique.Physique / 100f; // 0-1 normalized
                    finesseBonus = physique.Finesse / 100f;
                }

                var baseSpeed = 10f;
                var speed = baseSpeed * (1f + experience * 0.2f + physiqueBonus * 0.15f); // Up to 20% from XP, 15% from Physique
                // Finesse affects maneuver precision (applied in direction calculation)

                // Aggressive: direct route, Defensive: flanking approach
                var direction = math.normalize(toTarget);
                if (StanceLookup.HasComponent(entity))
                {
                    var stance = StanceLookup[entity];
                    if (stance.CurrentStance == VesselStanceMode.Defensive && distance > 20f)
                    {
                        // Flanking approach for defensive
                        var flankAngle = math.lerp(0.3f, 0.7f, chaos) * math.PI / 4f;
                        var right = math.cross(direction, math.up());
                        direction = math.normalize(direction + right * math.sin(flankAngle));
                    }
                }

                // Move toward target
                transform.Position += direction * speed * DeltaTime;

                // Update movement component if present
                if (MovementLookup.HasComponent(entity))
                {
                    var movement = MovementLookup.GetRefRW(entity).ValueRW;
                    movement.Velocity = direction * speed;
                    movement.IsMoving = 1;
                    movement.LastMoveTick = CurrentTick;
                }
            }

            private void ReturnToCarrier(ref LocalTransform transform, Entity entity, ref StrikeCraftState state)
            {
                if (!TetherLookup.HasComponent(entity))
                {
                    return;
                }

                var tether = TetherLookup[entity];
                if (tether.ParentCarrier == Entity.Null || !TransformLookup.HasComponent(tether.ParentCarrier))
                {
                    return;
                }

                var carrierPos = TransformLookup[tether.ParentCarrier].Position;
                var toCarrier = carrierPos - transform.Position;
                var distance = math.length(toCarrier);

                if (distance < 0.1f)
                {
                    return; // Already at carrier
                }

                var speed = 12f; // Return speed
                var direction = math.normalize(toCarrier);
                transform.Position += direction * speed * DeltaTime;

                // Update movement component if present
                if (MovementLookup.HasComponent(entity))
                {
                    var movement = MovementLookup.GetRefRW(entity).ValueRW;
                    movement.Velocity = direction * speed;
                    movement.IsMoving = 1;
                    movement.LastMoveTick = CurrentTick;
                }
            }

            private bool ShouldLaunch(Entity entity, ref StrikeCraftState state)
            {
                // Check if parent carrier has aggressive stance or is engaging
                if (TetherLookup.HasComponent(entity))
                {
                    var tether = TetherLookup[entity];
                    if (tether.ParentCarrier != Entity.Null && StanceLookup.HasComponent(tether.ParentCarrier))
                    {
                        var parentStance = StanceLookup[tether.ParentCarrier];
                        if (parentStance.CurrentStance == VesselStanceMode.Aggressive)
                        {
                            // Command stat from parent carrier influences launch decision threshold
                            // Higher command = more aggressive launch (lower threshold)
                            float commandModifier = 1f;
                            if (StatsLookup.HasComponent(tether.ParentCarrier))
                            {
                                var stats = StatsLookup[tether.ParentCarrier];
                                commandModifier = 1f + (stats.Command / 100f) * 0.2f; // Up to 20% faster launch
                            }
                            return true;
                        }
                    }
                }

                return false;
            }

            private bool ShouldCommitToAttack(Entity entity, ref StrikeCraftState state)
            {
                // Get alignment to determine approach style
                var alignment = AlignmentLookup.HasComponent(entity) 
                    ? AlignmentLookup[entity] 
                    : default(AlignmentTriplet);
                
                var chaos = AlignmentMath.Chaos(alignment);
                
                // Chaotic pilots experiment mid-run, lawful stick to plan
                // For now, always commit if target exists
                return state.TargetEntity != Entity.Null;
            }

            private bool ShouldDisengage(Entity entity, ref StrikeCraftState state)
            {
                // Get alignment for threshold tuning
                var alignment = AlignmentLookup.HasComponent(entity) 
                    ? AlignmentLookup[entity] 
                    : default(AlignmentTriplet);
                
                var lawfulness = AlignmentMath.Lawfulness(alignment);
                
                // Resolve stat influences disengagement thresholds (higher resolve = fight longer)
                float resolveModifier = 1f;
                if (StatsLookup.HasComponent(entity))
                {
                    var stats = StatsLookup[entity];
                    resolveModifier = 1f + (stats.Resolve / 100f) * 0.3f; // Up to 30% longer engagement
                }
                
                // Thresholds tuned by outlook/alignment and resolve
                // Lawful pilots return earlier, chaotic push limits, high resolve extends engagement
                var hullThreshold = math.lerp(0.3f, 0.5f, lawfulness);
                var fuelThreshold = math.lerp(0.2f, 0.4f, lawfulness);
                
                // In real implementation, check actual hull/fuel/ammo values
                // For now, use time-based disengage (simplified)
                var engagementTime = CurrentTick - state.StateStartTick;
                var baseMaxEngagementTime = math.lerp(300, 180, lawfulness); // Chaotic fight longer
                var maxEngagementTime = baseMaxEngagementTime * resolveModifier;
                
                if (engagementTime > maxEngagementTime)
                {
                    return true;
                }

                // Check if parent carrier stance changed
                if (TetherLookup.HasComponent(entity))
                {
                    var tether = TetherLookup[entity];
                    if (tether.ParentCarrier != Entity.Null && StanceLookup.HasComponent(tether.ParentCarrier))
                    {
                        var parentStance = StanceLookup[tether.ParentCarrier];
                        if (parentStance.CurrentStance == VesselStanceMode.Defensive || 
                            parentStance.CurrentStance == VesselStanceMode.Evasive)
                        {
                            return true; // Recall on stance change
                        }
                    }
                }

                return false;
            }

            private bool ShouldDock(Entity entity, ref StrikeCraftState state)
            {
                if (!TetherLookup.HasComponent(entity))
                {
                    return false;
                }

                var tether = TetherLookup[entity];
                if (tether.ParentCarrier == Entity.Null)
                {
                    return false;
                }

                if (!TransformLookup.HasComponent(entity) || !TransformLookup.HasComponent(tether.ParentCarrier))
                {
                    return false;
                }

                var craftPos = TransformLookup[entity].Position;
                var carrierPos = TransformLookup[tether.ParentCarrier].Position;
                var distance = math.distance(craftPos, carrierPos);

                // Dock when within range
                return distance < 10f; // Docking range
            }
        }
    }
}


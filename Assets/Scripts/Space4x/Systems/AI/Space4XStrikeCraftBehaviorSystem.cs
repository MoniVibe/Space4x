using PureDOTS.Runtime.Agency;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Core;
using PureDOTS.Runtime.Authority;
using PureDOTS.Runtime.Formation;
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
        private ComponentLookup<HullIntegrity> _hullLookup;
        private ComponentLookup<TechLevel> _techLevelLookup;
        private ComponentLookup<VesselQuality> _vesselQualityLookup;
        private ComponentLookup<VesselMobilityProfile> _mobilityProfileLookup;
        private ComponentLookup<StrikeCraftProfile> _profileLookup;
        private ComponentLookup<StrikeCraftState> _strikeCraftStateLookup;
        private ComponentLookup<StrikeCraftPilotLink> _pilotLinkLookup;
        private ComponentLookup<StrikeCraftMaintenanceQuality> _maintenanceQualityLookup;
        private ComponentLookup<StrikeCraftWingDirective> _wingDirectiveLookup;
        private ComponentLookup<StrikeCraftOrderDecision> _orderDecisionLookup;
        private ComponentLookup<FormationMember> _formationMemberLookup;
        private ComponentLookup<Carrier> _carrierLookup;
        private ComponentLookup<CarrierTag> _carrierTagLookup;
        private ComponentLookup<StationId> _stationIdLookup;
        private ComponentLookup<ModuleHealth> _moduleHealthLookup;
        private ComponentLookup<DireTacticsPolicy> _direTacticsPolicyLookup;
        private ComponentLookup<CultureId> _cultureIdLookup;
        private BufferLookup<CultureDireTacticsPolicy> _culturePolicyLookup;
        private BufferLookup<ResolvedControl> _resolvedControlLookup;
        private BufferLookup<AuthoritySeatRef> _seatRefLookup;
        private ComponentLookup<AuthoritySeat> _seatLookup;
        private ComponentLookup<AuthoritySeatOccupant> _seatOccupantLookup;
        private BufferLookup<TopOutlook> _outlookLookup;
        private FixedString64Bytes _roleCaptain;

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
            _hullLookup = state.GetComponentLookup<HullIntegrity>(true);
            _techLevelLookup = state.GetComponentLookup<TechLevel>(true);
            _vesselQualityLookup = state.GetComponentLookup<VesselQuality>(true);
            _mobilityProfileLookup = state.GetComponentLookup<VesselMobilityProfile>(true);
            _profileLookup = state.GetComponentLookup<StrikeCraftProfile>(true);
            _strikeCraftStateLookup = state.GetComponentLookup<StrikeCraftState>(true);
            _pilotLinkLookup = state.GetComponentLookup<StrikeCraftPilotLink>(true);
            _maintenanceQualityLookup = state.GetComponentLookup<StrikeCraftMaintenanceQuality>(true);
            _wingDirectiveLookup = state.GetComponentLookup<StrikeCraftWingDirective>(true);
            _orderDecisionLookup = state.GetComponentLookup<StrikeCraftOrderDecision>(false);
            _formationMemberLookup = state.GetComponentLookup<FormationMember>(true);
            _carrierLookup = state.GetComponentLookup<Carrier>(true);
            _carrierTagLookup = state.GetComponentLookup<CarrierTag>(true);
            _stationIdLookup = state.GetComponentLookup<StationId>(true);
            _moduleHealthLookup = state.GetComponentLookup<ModuleHealth>(true);
            _direTacticsPolicyLookup = state.GetComponentLookup<DireTacticsPolicy>(true);
            _cultureIdLookup = state.GetComponentLookup<CultureId>(true);
            _culturePolicyLookup = state.GetBufferLookup<CultureDireTacticsPolicy>(true);
            _resolvedControlLookup = state.GetBufferLookup<ResolvedControl>(true);
            _seatRefLookup = state.GetBufferLookup<AuthoritySeatRef>(true);
            _seatLookup = state.GetComponentLookup<AuthoritySeat>(true);
            _seatOccupantLookup = state.GetComponentLookup<AuthoritySeatOccupant>(true);
            _outlookLookup = state.GetBufferLookup<TopOutlook>(true);
            _roleCaptain = new FixedString64Bytes("ship.captain");
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
            _hullLookup.Update(ref state);
            _techLevelLookup.Update(ref state);
            _vesselQualityLookup.Update(ref state);
            _mobilityProfileLookup.Update(ref state);
            _profileLookup.Update(ref state);
            _strikeCraftStateLookup.Update(ref state);
            _pilotLinkLookup.Update(ref state);
            _maintenanceQualityLookup.Update(ref state);
            _wingDirectiveLookup.Update(ref state);
            _orderDecisionLookup.Update(ref state);
            _formationMemberLookup.Update(ref state);
            _carrierLookup.Update(ref state);
            _carrierTagLookup.Update(ref state);
            _stationIdLookup.Update(ref state);
            _moduleHealthLookup.Update(ref state);
            _direTacticsPolicyLookup.Update(ref state);
            _cultureIdLookup.Update(ref state);
            _culturePolicyLookup.Update(ref state);
            _resolvedControlLookup.Update(ref state);
            _seatRefLookup.Update(ref state);
            _seatLookup.Update(ref state);
            _seatOccupantLookup.Update(ref state);
            _outlookLookup.Update(ref state);

            var behaviorConfig = StrikeCraftBehaviorProfileConfig.Default;
            if (SystemAPI.TryGetSingleton<StrikeCraftBehaviorProfileConfig>(out var behaviorConfigSingleton))
            {
                behaviorConfig = behaviorConfigSingleton;
            }

            var rewindEnabled = true;
            if (SystemAPI.TryGetSingleton<SimulationFeatureFlags>(out var features))
            {
                rewindEnabled = (features.Flags & SimulationFeatureFlags.RewindEnabled) != 0;
            }

            var culturePolicyEntity = Entity.Null;
            var hasCulturePolicy = SystemAPI.TryGetSingletonEntity<CultureDireTacticsPolicyCatalog>(out culturePolicyEntity) &&
                                   _culturePolicyLookup.HasBuffer(culturePolicyEntity);

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
                HullLookup = _hullLookup,
                TechLevelLookup = _techLevelLookup,
                VesselQualityLookup = _vesselQualityLookup,
                MobilityProfileLookup = _mobilityProfileLookup,
                ProfileLookup = _profileLookup,
                StrikeCraftStateLookup = _strikeCraftStateLookup,
                PilotLinkLookup = _pilotLinkLookup,
                MaintenanceQualityLookup = _maintenanceQualityLookup,
                WingDirectiveLookup = _wingDirectiveLookup,
                OrderDecisionLookup = _orderDecisionLookup,
                FormationMemberLookup = _formationMemberLookup,
                CarrierLookup = _carrierLookup,
                CarrierTagLookup = _carrierTagLookup,
                StationIdLookup = _stationIdLookup,
                ModuleHealthLookup = _moduleHealthLookup,
                DireTacticsPolicyLookup = _direTacticsPolicyLookup,
                CultureIdLookup = _cultureIdLookup,
                CulturePolicyLookup = _culturePolicyLookup,
                ResolvedControlLookup = _resolvedControlLookup,
                SeatRefLookup = _seatRefLookup,
                SeatLookup = _seatLookup,
                SeatOccupantLookup = _seatOccupantLookup,
                OutlookLookup = _outlookLookup,
                BehaviorConfig = behaviorConfig,
                RewindEnabled = rewindEnabled,
                CulturePolicyEntity = culturePolicyEntity,
                HasCulturePolicy = (byte)(hasCulturePolicy ? 1 : 0),
                RoleCaptain = _roleCaptain
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
            [ReadOnly] public ComponentLookup<HullIntegrity> HullLookup;
            [ReadOnly] public ComponentLookup<TechLevel> TechLevelLookup;
            [ReadOnly] public ComponentLookup<VesselQuality> VesselQualityLookup;
            [ReadOnly] public ComponentLookup<VesselMobilityProfile> MobilityProfileLookup;
            [ReadOnly] public ComponentLookup<StrikeCraftProfile> ProfileLookup;
            [ReadOnly] public ComponentLookup<StrikeCraftState> StrikeCraftStateLookup;
            [ReadOnly] public ComponentLookup<StrikeCraftPilotLink> PilotLinkLookup;
            [ReadOnly] public ComponentLookup<StrikeCraftMaintenanceQuality> MaintenanceQualityLookup;
            [ReadOnly] public ComponentLookup<StrikeCraftWingDirective> WingDirectiveLookup;
            [NativeDisableParallelForRestriction] public ComponentLookup<StrikeCraftOrderDecision> OrderDecisionLookup;
            [ReadOnly] public ComponentLookup<FormationMember> FormationMemberLookup;
            [ReadOnly] public ComponentLookup<Carrier> CarrierLookup;
            [ReadOnly] public ComponentLookup<CarrierTag> CarrierTagLookup;
            [ReadOnly] public ComponentLookup<StationId> StationIdLookup;
            [ReadOnly] public ComponentLookup<ModuleHealth> ModuleHealthLookup;
            [ReadOnly] public ComponentLookup<DireTacticsPolicy> DireTacticsPolicyLookup;
            [ReadOnly] public ComponentLookup<CultureId> CultureIdLookup;
            [ReadOnly] public BufferLookup<CultureDireTacticsPolicy> CulturePolicyLookup;
            [ReadOnly] public BufferLookup<ResolvedControl> ResolvedControlLookup;
            [ReadOnly] public BufferLookup<AuthoritySeatRef> SeatRefLookup;
            [ReadOnly] public ComponentLookup<AuthoritySeat> SeatLookup;
            [ReadOnly] public ComponentLookup<AuthoritySeatOccupant> SeatOccupantLookup;
            [ReadOnly] public BufferLookup<TopOutlook> OutlookLookup;
            public StrikeCraftBehaviorProfileConfig BehaviorConfig;
            public bool RewindEnabled;
            public Entity CulturePolicyEntity;
            public byte HasCulturePolicy;
            public FixedString64Bytes RoleCaptain;

            public void Execute(ref LocalTransform transform, ref StrikeCraftState state, Entity entity)
            {
                if (BehaviorConfig.AllowKamikaze == 0 && state.KamikazeActive == 1)
                {
                    state.KamikazeActive = 0;
                    state.KamikazeStartTick = 0;
                }

                // Update based on current state
                switch (state.CurrentState)
                {
                    case StrikeCraftState.State.Docked:
                        state.KamikazeActive = 0;
                        state.KamikazeStartTick = 0;
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
                        state.KamikazeActive = 0;
                        state.KamikazeStartTick = 0;
                        break;

                    case StrikeCraftState.State.Returning:
                        // Return to carrier - move toward parent
                        ReturnToCarrier(ref transform, entity, ref state);
                        if (ShouldDock(entity, ref state))
                        {
                            state.CurrentState = StrikeCraftState.State.Docked;
                            state.StateStartTick = CurrentTick;
                            state.KamikazeActive = 0;
                            state.KamikazeStartTick = 0;
                            // Gain experience from successful sortie
                            state.Experience = math.min(1f, state.Experience + 0.01f);
                        }
                        break;
                }
            }

            private void FindTarget(Entity entity, ref StrikeCraftState state, float3 position)
            {
                if (ProfileLookup.HasComponent(entity))
                {
                    var profile = ProfileLookup[entity];
                    if (profile.WingLeader != Entity.Null && StrikeCraftStateLookup.HasComponent(profile.WingLeader))
                    {
                        var leaderState = StrikeCraftStateLookup[profile.WingLeader];
                        if (leaderState.TargetEntity != Entity.Null)
                        {
                            state.TargetEntity = leaderState.TargetEntity;
                            state.TargetPosition = leaderState.TargetPosition;
                            return;
                        }
                    }
                }

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

                var anchorPos = TransformLookup[tether.ParentCarrier].Position;
                Entity leader = Entity.Null;
                if (ProfileLookup.HasComponent(entity))
                {
                    var profile = ProfileLookup[entity];
                    leader = profile.WingLeader;
                    if (leader != Entity.Null && TransformLookup.HasComponent(leader))
                    {
                        anchorPos = TransformLookup[leader].Position;
                    }
                }

                var profileEntity = ResolveProfileEntity(entity);
                var alignment = AlignmentLookup.HasComponent(profileEntity)
                    ? AlignmentLookup[profileEntity]
                    : default(AlignmentTriplet);
                var lawfulness = AlignmentMath.Lawfulness(alignment);
                var chaos = AlignmentMath.Chaos(alignment);
                var discipline = GetOutlookDiscipline(profileEntity);
                var maintenanceQuality = GetMaintenanceQuality(entity);

                var spacingScale = math.lerp(1.25f, 0.85f, discipline);
                spacingScale *= math.lerp(1.15f, 0.85f, maintenanceQuality);

                var wingOffset = ComputeWingOffset(entity, lawfulness, profileEntity);
                var hasDirective = leader != Entity.Null && TryResolveWingDirective(entity, leader, out var directiveMode, out var shouldObey);
                if (hasDirective)
                {
                    if (shouldObey && directiveMode == 1)
                    {
                        wingOffset = DefaultOrbitOffset(entity, lawfulness, profileEntity);
                        spacingScale *= math.lerp(1.4f, 2.2f, chaos);
                    }
                    else if (!shouldObey)
                    {
                        spacingScale *= math.lerp(1.1f, 1.6f, chaos);
                    }
                }

                var shouldUseFormation = !hasDirective || (shouldObey && directiveMode == 0);
                if (shouldUseFormation && FormationMemberLookup.HasComponent(entity))
                {
                    var member = FormationMemberLookup[entity];
                    if (member.FormationEntity != Entity.Null)
                    {
                        var target = member.TargetPosition;
                        var toTarget = target - transform.Position;
                        var distance = math.length(toTarget);
                        if (distance > 0.05f)
                        {
                            var moveSpeed = 10f;
                            moveSpeed *= math.lerp(0.85f, 1.2f, maintenanceQuality);
                            moveSpeed *= math.lerp(0.9f, 1.15f, GetMobilityQuality(entity));
                            var step = math.min(distance, moveSpeed * DeltaTime);
                            transform.Position += math.normalize(toTarget) * step;
                        }
                        else
                        {
                            transform.Position = target;
                        }
                        return;
                    }
                }

                transform.Position = anchorPos + wingOffset * spacingScale;
            }

            private void MoveTowardTarget(ref LocalTransform transform, ref StrikeCraftState state, Entity entity)
            {
                // Get alignment for approach style
                var profileEntity = ResolveProfileEntity(entity);
                var alignment = AlignmentLookup.HasComponent(profileEntity) 
                    ? AlignmentLookup[profileEntity] 
                    : default(AlignmentTriplet);
                var lawfulness = AlignmentMath.Lawfulness(alignment);
                var chaos = AlignmentMath.Chaos(alignment);
                var integrity = AlignmentMath.IntegrityNormalized(alignment);
                var experience = state.Experience;
                var maintenanceQuality = GetMaintenanceQuality(entity);
                var mobilityQuality = GetMobilityQuality(entity);
                var mobilityProfile = GetMobilityProfile(entity);
                var targetOffset = float3.zero;

                if (ProfileLookup.HasComponent(entity))
                {
                    var profile = ProfileLookup[entity];
                    if (profile.WingLeader != Entity.Null && StrikeCraftStateLookup.HasComponent(profile.WingLeader))
                    {
                        var leaderState = StrikeCraftStateLookup[profile.WingLeader];
                        if (leaderState.TargetEntity != Entity.Null)
                        {
                            var shouldObey = true;
                            byte directiveMode = 0;
                            var hasDirective = TryResolveWingDirective(entity, profile.WingLeader, out directiveMode, out shouldObey);
                            if (!hasDirective)
                            {
                                shouldObey = true;
                                directiveMode = 0;
                            }

                            if (shouldObey)
                            {
                                state.TargetEntity = leaderState.TargetEntity;
                                state.TargetPosition = leaderState.TargetPosition;
                                var wingOffset = ComputeWingOffset(entity, lawfulness, profileEntity);
                                if (directiveMode == 1)
                                {
                                    wingOffset = DefaultOrbitOffset(entity, lawfulness, profileEntity);
                                    wingOffset *= math.lerp(1.2f, 1.8f, chaos);
                                }
                                targetOffset = wingOffset;
                            }
                        }
                    }
                }

                if (state.TargetEntity == Entity.Null)
                {
                    return;
                }

                // Update target position if target still exists
                if (!TransformLookup.HasComponent(state.TargetEntity))
                {
                    return;
                }

                state.TargetPosition = TransformLookup[state.TargetEntity].Position + targetOffset;

                var toTarget = state.TargetPosition - transform.Position;
                var distance = math.length(toTarget);

                if (distance < 0.1f)
                {
                    return; // Already at target
                }

                // Apply experience and Physique/Finesse stats to movement
                // Physique affects speed/acceleration, Finesse affects precision/maneuverability
                float physiqueBonus = 0f;
                float finesseBonus = 0f;
                if (PhysiqueLookup.HasComponent(profileEntity))
                {
                    var physique = PhysiqueLookup[profileEntity];
                    physiqueBonus = physique.Physique / 100f; // 0-1 normalized
                    finesseBonus = physique.Finesse / 100f;
                }

                var baseSpeed = 10f;
                var speed = baseSpeed * (1f + experience * 0.2f + physiqueBonus * 0.15f);
                speed *= math.lerp(0.85f, 1.1f, maintenanceQuality);
                speed *= math.lerp(0.85f, 1.2f, mobilityQuality);
                // Finesse affects maneuver precision (applied in direction calculation)

                // Aggressive: direct route, Defensive: flanking approach
                var direction = math.normalize(toTarget);
                if (state.KamikazeActive == 0)
                {
                    TryActivateKamikaze(entity, profileEntity, ref state, lawfulness, chaos, integrity);
                }

                if (StanceLookup.HasComponent(entity) && state.KamikazeActive == 0)
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

                if (state.KamikazeActive == 1)
                {
                    speed *= BehaviorConfig.KamikazeSpeedMultiplier;
                    speed *= math.lerp(1f, BehaviorConfig.KamikazeTurnMultiplier, 0.25f);
                }
                else if (finesseBonus > 0f)
                {
                    speed *= math.lerp(0.95f, 1.05f, finesseBonus);
                }

                if (state.KamikazeActive == 0)
                {
                    TryApplyKiting(entity, ref state, ref direction, ref speed, distance, experience, finesseBonus, mobilityQuality, mobilityProfile);
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

            private float3 ComputeWingOffset(Entity entity, float lawfulness, Entity profileEntity)
            {
                if (!ProfileLookup.HasComponent(entity))
                {
                    return DefaultOrbitOffset(entity, lawfulness, profileEntity);
                }

                var profile = ProfileLookup[entity];
                if (profile.WingPosition == 0 || profile.WingLeader == Entity.Null)
                {
                    return DefaultOrbitOffset(entity, lawfulness, profileEntity);
                }

                var slot = profile.WingPosition;
                var row = (byte)((slot + 1) / 2);
                var side = (slot % 2 == 0) ? -1f : 1f;
                var discipline = GetOutlookDiscipline(profileEntity);
                var spacing = math.lerp(2f, 5f, 1f - lawfulness);
                spacing = math.lerp(spacing * 1.3f, spacing * 0.85f, discipline);
                return new float3(side * spacing * row, 0f, -spacing * row);
            }

            private float3 DefaultOrbitOffset(Entity entity, float lawfulness, Entity profileEntity)
            {
                var offset = float3.zero;
                var entityHash = (uint)entity.Index;
                var angle = (entityHash % 360) * math.radians(1f);
                var discipline = GetOutlookDiscipline(profileEntity);
                var radius = math.lerp(15f, 25f, 1f - lawfulness);
                radius = math.lerp(radius * 1.3f, radius * 0.85f, discipline);

                offset.x = math.cos(angle) * radius;
                offset.z = math.sin(angle) * radius;
                offset.y = (entityHash % 3) * 2f - 2f;
                return offset;
            }

            private float GetOutlookDiscipline(Entity profileEntity)
            {
                if (!OutlookLookup.HasBuffer(profileEntity))
                {
                    return 0.5f;
                }

                var buffer = OutlookLookup[profileEntity];
                var discipline = 0.5f;
                for (var i = 0; i < buffer.Length; i++)
                {
                    var entry = buffer[i];
                    var weight = math.clamp((float)entry.Weight, 0f, 1f);
                    switch (entry.OutlookId)
                    {
                        case OutlookId.Loyalist:
                            discipline += 0.2f * weight;
                            break;
                        case OutlookId.Fanatic:
                            discipline += 0.25f * weight;
                            break;
                        case OutlookId.Opportunist:
                            discipline -= 0.15f * weight;
                            break;
                        case OutlookId.Mutinous:
                            discipline -= 0.3f * weight;
                            break;
                    }
                }

                return math.saturate(discipline);
            }

            private float GetMaintenanceQuality(Entity craftEntity)
            {
                if (!MaintenanceQualityLookup.HasComponent(craftEntity))
                {
                    return 0.75f;
                }

                return math.saturate(MaintenanceQualityLookup[craftEntity].Value);
            }

            private float GetMobilityQuality(Entity craftEntity)
            {
                if (!VesselQualityLookup.HasComponent(craftEntity))
                {
                    return 0.5f;
                }

                var quality = VesselQualityLookup[craftEntity];
                var average = (quality.HullQuality + quality.SystemsQuality + quality.MobilityQuality + quality.IntegrationQuality) * 0.25f;
                return math.saturate(average);
            }

            private VesselMobilityProfile GetMobilityProfile(Entity craftEntity)
            {
                if (MobilityProfileLookup.HasComponent(craftEntity))
                {
                    return MobilityProfileLookup[craftEntity];
                }

                return VesselMobilityProfile.Default;
            }

            private void TryApplyKiting(
                Entity craftEntity,
                ref StrikeCraftState state,
                ref float3 direction,
                ref float speed,
                float distance,
                float experience,
                float finesseBonus,
                float mobilityQuality,
                VesselMobilityProfile mobilityProfile)
            {
                if (state.CurrentState != StrikeCraftState.State.Engaging)
                {
                    return;
                }

                if (mobilityProfile.AllowKiting == 0 || mobilityProfile.ReverseSpeedMultiplier <= 0f)
                {
                    return;
                }

                if (experience < BehaviorConfig.KitingMinExperience)
                {
                    return;
                }

                if (distance < BehaviorConfig.KitingMinDistance || distance > BehaviorConfig.KitingMaxDistance)
                {
                    return;
                }

                var roll = DeterministicRoll(craftEntity, state.TargetEntity, state.StateStartTick, 7);
                var chance = BehaviorConfig.KitingChance + (experience - BehaviorConfig.KitingMinExperience) * 0.5f;
                chance *= math.lerp(0.8f, 1.15f, mobilityQuality);
                if (finesseBonus > 0f)
                {
                    chance *= math.lerp(0.85f, 1.1f, finesseBonus);
                }

                chance = math.saturate(chance);
                if (roll > chance)
                {
                    return;
                }

                direction = -direction;
                speed *= math.max(0.05f, mobilityProfile.ReverseSpeedMultiplier);

                if (mobilityProfile.ThrustMode != VesselThrustMode.ForwardOnly && mobilityProfile.StrafeSpeedMultiplier > 0f)
                {
                    var right = math.cross(direction, math.up());
                    if (math.lengthsq(right) > 0f)
                    {
                        var strafeSign = ((craftEntity.Index & 1) == 0) ? 1f : -1f;
                        var strafeStrength = mobilityProfile.StrafeSpeedMultiplier * BehaviorConfig.KitingStrafeStrength;
                        direction = math.normalize(direction + right * strafeStrength * strafeSign);
                    }
                }
            }

            private Entity ResolveCarrierEntity(Entity craftEntity)
            {
                if (ProfileLookup.HasComponent(craftEntity))
                {
                    var profile = ProfileLookup[craftEntity];
                    if (profile.Carrier != Entity.Null)
                    {
                        return profile.Carrier;
                    }
                }

                if (TetherLookup.HasComponent(craftEntity))
                {
                    var tether = TetherLookup[craftEntity];
                    if (tether.ParentCarrier != Entity.Null)
                    {
                        return tether.ParentCarrier;
                    }
                }

                return Entity.Null;
            }

            private bool CaptainAllowsDireTactics(Entity craftEntity)
            {
                var carrier = ResolveCarrierEntity(craftEntity);
                if (carrier == Entity.Null || !SeatRefLookup.HasBuffer(carrier))
                {
                    return BehaviorConfig.DefaultCaptainAllowsDireTactics != 0;
                }

                var seatRefs = SeatRefLookup[carrier];
                for (var i = 0; i < seatRefs.Length; i++)
                {
                    var seatEntity = seatRefs[i].SeatEntity;
                    if (!SeatLookup.HasComponent(seatEntity))
                    {
                        continue;
                    }

                    var seat = SeatLookup[seatEntity];
                    if (!seat.RoleId.Equals(RoleCaptain))
                    {
                        continue;
                    }

                    if (!SeatOccupantLookup.HasComponent(seatEntity))
                    {
                        return BehaviorConfig.DefaultCaptainAllowsDireTactics != 0;
                    }

                    var occupant = SeatOccupantLookup[seatEntity].OccupantEntity;
                    if (occupant == Entity.Null)
                    {
                        return BehaviorConfig.DefaultCaptainAllowsDireTactics != 0;
                    }

                    if (!DireTacticsPolicyLookup.HasComponent(occupant))
                    {
                        return BehaviorConfig.DefaultCaptainAllowsDireTactics != 0;
                    }

                    return DireTacticsPolicyLookup[occupant].AllowKamikaze != 0;
                }

                return BehaviorConfig.DefaultCaptainAllowsDireTactics != 0;
            }

            private bool CultureAllowsDireTactics(Entity profileEntity, Entity craftEntity)
            {
                ushort cultureId = 0;
                if (CultureIdLookup.HasComponent(profileEntity))
                {
                    cultureId = CultureIdLookup[profileEntity].Value;
                }
                else if (CultureIdLookup.HasComponent(craftEntity))
                {
                    cultureId = CultureIdLookup[craftEntity].Value;
                }

                if (cultureId == 0)
                {
                    return BehaviorConfig.DefaultCultureAllowsDireTactics != 0;
                }

                if (HasCulturePolicy == 0 || CulturePolicyEntity == Entity.Null)
                {
                    return BehaviorConfig.DefaultCultureAllowsDireTactics != 0;
                }

                var buffer = CulturePolicyLookup[CulturePolicyEntity];
                for (var i = 0; i < buffer.Length; i++)
                {
                    var entry = buffer[i];
                    if (entry.CultureId == cultureId)
                    {
                        return entry.AllowKamikaze != 0;
                    }
                }

                return BehaviorConfig.DefaultCultureAllowsDireTactics != 0;
            }

            private bool TryResolveWingDirective(Entity craftEntity, Entity leader, out byte directiveMode, out bool shouldObey)
            {
                directiveMode = 0;
                shouldObey = true;

                if (leader == Entity.Null || !WingDirectiveLookup.HasComponent(leader))
                {
                    return false;
                }

                var directive = WingDirectiveLookup[leader];
                directiveMode = directive.Mode;

                if (BehaviorConfig.AllowDirectiveDisobedience == 0)
                {
                    shouldObey = true;
                    if (OrderDecisionLookup.HasComponent(craftEntity) && directive.LastDecisionTick > 0)
                    {
                        var forcedDecision = OrderDecisionLookup[craftEntity];
                        forcedDecision.LastDirectiveTick = directive.LastDecisionTick;
                        forcedDecision.LastDirectiveMode = directive.Mode;
                        forcedDecision.LastDecision = 1;
                        OrderDecisionLookup[craftEntity] = forcedDecision;
                    }
                    return true;
                }

                if (!OrderDecisionLookup.HasComponent(craftEntity) || directive.LastDecisionTick == 0)
                {
                    return true;
                }

                var decision = OrderDecisionLookup[craftEntity];
                if (decision.LastDirectiveTick == directive.LastDecisionTick &&
                    decision.LastDirectiveMode == directive.Mode &&
                    decision.LastDecision != 0)
                {
                    shouldObey = decision.LastDecision == 1;
                    return true;
                }

                var profileEntity = ResolveProfileEntity(craftEntity);
                var alignment = AlignmentLookup.HasComponent(profileEntity)
                    ? AlignmentLookup[profileEntity]
                    : default(AlignmentTriplet);
                var lawfulness = AlignmentMath.Lawfulness(alignment);
                var chaos = AlignmentMath.Chaos(alignment);
                var discipline = GetOutlookDiscipline(profileEntity);

                shouldObey = EvaluateObedience(craftEntity, leader, directive, lawfulness, chaos, discipline);
                decision.LastDirectiveTick = directive.LastDecisionTick;
                decision.LastDirectiveMode = directive.Mode;
                decision.LastDecision = (byte)(shouldObey ? 1 : 2);
                OrderDecisionLookup[craftEntity] = decision;
                return true;
            }

            private bool EvaluateObedience(Entity craftEntity, Entity leader, StrikeCraftWingDirective directive, float lawfulness, float chaos, float discipline)
            {
                var obedienceScore = 0.5f;
                obedienceScore += (lawfulness - 0.5f) * BehaviorConfig.LawfulnessWeight;
                obedienceScore += (discipline - 0.5f) * BehaviorConfig.DisciplineWeight;
                obedienceScore = math.saturate(obedienceScore);

                var disobeyChance = BehaviorConfig.BaseDisobeyChance;
                disobeyChance += BehaviorConfig.ChaosDisobeyBonus * chaos;
                disobeyChance += BehaviorConfig.MutinyDisobeyBonus * (1f - discipline);
                disobeyChance = math.saturate(disobeyChance);

                var roll = DeterministicRoll(craftEntity, leader, directive.LastDecisionTick, directive.Mode);
                return obedienceScore >= BehaviorConfig.ObedienceThreshold && roll > disobeyChance;
            }

            private float DeterministicRoll(Entity craftEntity, Entity leader, uint tick, byte mode)
            {
                var hash = math.hash(new uint4((uint)craftEntity.Index, (uint)leader.Index, tick, mode));
                return (hash & 0xFFFF) / 65535f;
            }

            private void TryActivateKamikaze(
                Entity craftEntity,
                Entity profileEntity,
                ref StrikeCraftState state,
                float lawfulness,
                float chaos,
                float integrity)
            {
                if (BehaviorConfig.AllowKamikaze == 0)
                {
                    return;
                }

                if (BehaviorConfig.RequireRewindEnabled != 0 && !RewindEnabled)
                {
                    return;
                }

                if (!MeetsCombatTechRequirement(craftEntity, BehaviorConfig.RequireCombatTechTier))
                {
                    return;
                }

                if (BehaviorConfig.RequireCaptainConsent != 0 && !CaptainAllowsDireTactics(craftEntity))
                {
                    return;
                }

                if (BehaviorConfig.RequireCultureConsent != 0 && !CultureAllowsDireTactics(profileEntity, craftEntity))
                {
                    return;
                }

                if (state.KamikazeActive == 1 || state.TargetEntity == Entity.Null)
                {
                    return;
                }

                if (!IsCriticalTarget(state.TargetEntity))
                {
                    return;
                }

                var hullRatio = GetHullRatio(craftEntity);
                if (hullRatio > BehaviorConfig.KamikazeHullThreshold)
                {
                    return;
                }

                if (integrity < BehaviorConfig.KamikazePurityThreshold)
                {
                    return;
                }

                if (lawfulness < BehaviorConfig.KamikazeLawfulnessThreshold &&
                    chaos < BehaviorConfig.KamikazeChaosThreshold)
                {
                    return;
                }

                var roll = DeterministicRoll(craftEntity, state.TargetEntity, state.StateStartTick, 3);
                if (roll > BehaviorConfig.KamikazeChance)
                {
                    return;
                }

                state.KamikazeActive = 1;
                state.KamikazeStartTick = CurrentTick;
            }

            private bool MeetsCombatTechRequirement(Entity craftEntity, byte requiredTier)
            {
                if (requiredTier == 0)
                {
                    return true;
                }

                if (TechLevelLookup.HasComponent(craftEntity))
                {
                    var tech = TechLevelLookup[craftEntity];
                    if (tech.CombatTech >= requiredTier)
                    {
                        return true;
                    }
                }

                if (ProfileLookup.HasComponent(craftEntity))
                {
                    var profile = ProfileLookup[craftEntity];
                    if (profile.Carrier != Entity.Null && TechLevelLookup.HasComponent(profile.Carrier))
                    {
                        if (TechLevelLookup[profile.Carrier].CombatTech >= requiredTier)
                        {
                            return true;
                        }
                    }
                }

                if (TetherLookup.HasComponent(craftEntity))
                {
                    var tether = TetherLookup[craftEntity];
                    if (tether.ParentCarrier != Entity.Null && TechLevelLookup.HasComponent(tether.ParentCarrier))
                    {
                        if (TechLevelLookup[tether.ParentCarrier].CombatTech >= requiredTier)
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            private float GetHullRatio(Entity craftEntity)
            {
                if (!HullLookup.HasComponent(craftEntity))
                {
                    return 1f;
                }

                var hull = HullLookup[craftEntity];
                if (hull.Max <= 0f)
                {
                    return 1f;
                }

                return math.saturate(hull.Current / hull.Max);
            }

            private bool IsCriticalTarget(Entity target)
            {
                return CarrierLookup.HasComponent(target) ||
                       CarrierTagLookup.HasComponent(target) ||
                       StationIdLookup.HasComponent(target) ||
                       ModuleHealthLookup.HasComponent(target);
            }

            private Entity ResolveProfileEntity(Entity craftEntity)
            {
                if (TryResolveController(craftEntity, AgencyDomain.Combat, out var controller))
                {
                    return controller != Entity.Null ? controller : craftEntity;
                }

                if (PilotLinkLookup.HasComponent(craftEntity))
                {
                    var link = PilotLinkLookup[craftEntity];
                    if (link.Pilot != Entity.Null)
                    {
                        return link.Pilot;
                    }
                }

                return craftEntity;
            }

            private bool TryResolveController(Entity craftEntity, AgencyDomain domain, out Entity controller)
            {
                controller = Entity.Null;
                if (!ResolvedControlLookup.HasBuffer(craftEntity))
                {
                    return false;
                }

                var resolved = ResolvedControlLookup[craftEntity];
                for (int i = 0; i < resolved.Length; i++)
                {
                    if (resolved[i].Domain == domain)
                    {
                        controller = resolved[i].Controller;
                        return true;
                    }
                }

                return false;
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
                // Chaotic pilots experiment mid-run, lawful stick to plan
                // For now, always commit if target exists
                if (state.KamikazeActive == 1)
                {
                    return true;
                }

                return state.TargetEntity != Entity.Null;
            }

            private bool ShouldDisengage(Entity entity, ref StrikeCraftState state)
            {
                // Get alignment for threshold tuning
                var profileEntity = ResolveProfileEntity(entity);
                var alignment = AlignmentLookup.HasComponent(profileEntity) 
                    ? AlignmentLookup[profileEntity] 
                    : default(AlignmentTriplet);
                
                var lawfulness = AlignmentMath.Lawfulness(alignment);
                
                // Resolve stat influences disengagement thresholds (higher resolve = fight longer)
                float resolveModifier = 1f;
                if (StatsLookup.HasComponent(profileEntity))
                {
                    var stats = StatsLookup[profileEntity];
                    resolveModifier = 1f + (stats.Resolve / 100f) * 0.3f; // Up to 30% longer engagement
                }

                if (state.KamikazeActive == 1)
                {
                    if (state.TargetEntity == Entity.Null || !TransformLookup.HasComponent(state.TargetEntity))
                    {
                        return true;
                    }

                    return false;
                }
                
                // Thresholds tuned by outlook/alignment and resolve
                // Lawful pilots return earlier, chaotic push limits, high resolve extends engagement
                var hullThreshold = math.lerp(0.3f, 0.5f, lawfulness);
                var fuelThreshold = math.lerp(0.2f, 0.4f, lawfulness);
                
                if (HullLookup.HasComponent(entity))
                {
                    var hull = HullLookup[entity];
                    if (hull.Max > 0f)
                    {
                        var hullRatio = hull.Current / hull.Max;
                        if (hullRatio <= hullThreshold)
                        {
                            return true;
                        }
                    }
                }

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

using PureDOTS.Runtime.Agency;
using PureDOTS.Runtime.Authority;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Combat;
using PureDOTS.Runtime.Modules;
using PureDOTS.Runtime.Movement;
using PureDOTS.Runtime.Power;
using PureDOTS.Runtime.Physics;
using PureDOTS.Runtime.Profile;
using PureDOTS.Runtime.Interaction;
using PureDOTS.Systems;
using Space4X.Runtime;
using Space4X.Registry;
using Space4X.Physics;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using UnityDebug = UnityEngine.Debug;
using WeaponMountBuffer = Space4X.Registry.WeaponMount;

namespace Space4X.Systems.AI
{
    using Debug = UnityEngine.Debug;

    
    /// <summary>
    /// Moves vessels toward their current target positions with simple steering.
    /// Similar to VillagerMovementSystem but designed for vessels.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PureDOTS.Systems.ResourceSystemGroup))]
    public partial struct VesselMovementSystem : ISystem
    {
        private ComponentLookup<ThreatProfile> _threatLookup;
        private ComponentLookup<VesselStanceComponent> _stanceLookup;
        private ComponentLookup<CapabilityState> _capabilityStateLookup;
        private ComponentLookup<CapabilityEffectiveness> _effectivenessLookup;
        private ComponentLookup<AlignmentTriplet> _alignmentLookup;
        private BufferLookup<StanceEntry> _outlookLookup;
        private ComponentLookup<IndividualStats> _statsLookup;
        private ComponentLookup<VesselPilotLink> _pilotLookup;
        private ComponentLookup<PilotProficiency> _pilotProficiencyLookup;
        private BufferLookup<AuthoritySeatRef> _seatRefLookup;
        private ComponentLookup<AuthoritySeat> _seatLookup;
        private ComponentLookup<AuthoritySeatOccupant> _seatOccupantLookup;
        private ComponentLookup<Carrier> _carrierLookup;
        private ComponentLookup<MiningVessel> _miningVesselLookup;
        private ComponentLookup<MiningState> _miningStateLookup;
        private ComponentLookup<ModuleStatAggregate> _moduleAggregateLookup;
        private ComponentLookup<ModuleCapabilityOutput> _moduleCapabilityLookup;
        private ComponentLookup<EnginePerformanceOutput> _engineOutputLookup;
        private BufferLookup<ShipPowerConsumer> _shipPowerConsumerLookup;
        private ComponentLookup<PowerConsumer> _powerConsumerLookup;
        private ComponentLookup<PowerEffectiveness> _powerEffectivenessLookup;
        private ComponentLookup<VesselQuality> _qualityLookup;
        private ComponentLookup<VesselMobilityProfile> _mobilityProfileLookup;
        private ComponentLookup<VesselPhysicalProperties> _physicalLookup;
        private ComponentLookup<NavigationCohesion> _navigationLookup;
        private ComponentLookup<Space4XFocusModifiers> _focusModifiersLookup;
        private BufferLookup<ResolvedControl> _resolvedControlLookup;
        private ComponentLookup<CrewSkills> _crewSkillsLookup;
        private ComponentLookup<BehaviorDisposition> _behaviorDispositionLookup;
        private BufferLookup<DepartmentStatsBuffer> _departmentStatsLookup;
        private ComponentLookup<TargetPriority> _priorityLookup;
        private ComponentLookup<LocalTransform> _targetTransformLookup;
        private ComponentLookup<Space4XEngagement> _engagementLookup;
        private BufferLookup<WeaponMountBuffer> _weaponLookup;
        private ComponentLookup<Asteroid> _asteroidLookup;
        private ComponentLookup<Space4XAsteroidVolumeConfig> _asteroidVolumeLookup;
        private ComponentLookup<Space4XAsteroidCenter> _asteroidCenterLookup;
        private ComponentLookup<MoveIntent> _moveIntentLookup;
        private ComponentLookup<MovePlan> _movePlanLookup;
        private ComponentLookup<DecisionTrace> _decisionTraceLookup;
        private ComponentLookup<MovementDebugState> _movementDebugLookup;
        private BufferLookup<MoveTraceEvent> _traceEventLookup;
        private ComponentLookup<AttackMoveIntent> _attackMoveLookup;
        private ComponentLookup<VesselAimDirective> _aimDirectiveLookup;
        private BufferLookup<SubsystemHealth> _subsystemLookup;
        private BufferLookup<SubsystemDisabled> _subsystemDisabledLookup;
        private ComponentLookup<PhysicsCollider> _physicsColliderLookup;
        private ComponentLookup<SpacePhysicsBody> _spacePhysicsBodyLookup;
        private ComponentLookup<PhysicsColliderSpec> _colliderSpecLookup;
        private ComponentLookup<MovementSuppressed> _movementSuppressedLookup;
        private FixedString64Bytes _roleNavigationOfficer;
        private FixedString64Bytes _roleShipmaster;
        private FixedString64Bytes _roleCaptain;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<VesselMovement>();
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();

            _threatLookup = state.GetComponentLookup<ThreatProfile>(true);
            _stanceLookup = state.GetComponentLookup<VesselStanceComponent>(true);
            _capabilityStateLookup = state.GetComponentLookup<CapabilityState>(true);
            _effectivenessLookup = state.GetComponentLookup<CapabilityEffectiveness>(true);
            _alignmentLookup = state.GetComponentLookup<AlignmentTriplet>(true);
            _outlookLookup = state.GetBufferLookup<StanceEntry>(true);
            _statsLookup = state.GetComponentLookup<IndividualStats>(true);
            _pilotLookup = state.GetComponentLookup<VesselPilotLink>(true);
            _pilotProficiencyLookup = state.GetComponentLookup<PilotProficiency>(true);
            _seatRefLookup = state.GetBufferLookup<AuthoritySeatRef>(true);
            _seatLookup = state.GetComponentLookup<AuthoritySeat>(true);
            _seatOccupantLookup = state.GetComponentLookup<AuthoritySeatOccupant>(true);
            _carrierLookup = state.GetComponentLookup<Carrier>(true);
            _miningVesselLookup = state.GetComponentLookup<MiningVessel>(true);
            _miningStateLookup = state.GetComponentLookup<MiningState>(true);
            _moduleAggregateLookup = state.GetComponentLookup<ModuleStatAggregate>(true);
            _moduleCapabilityLookup = state.GetComponentLookup<ModuleCapabilityOutput>(true);
            _engineOutputLookup = state.GetComponentLookup<EnginePerformanceOutput>(true);
            _shipPowerConsumerLookup = state.GetBufferLookup<ShipPowerConsumer>(true);
            _powerConsumerLookup = state.GetComponentLookup<PowerConsumer>(true);
            _powerEffectivenessLookup = state.GetComponentLookup<PowerEffectiveness>(true);
            _qualityLookup = state.GetComponentLookup<VesselQuality>(true);
            _mobilityProfileLookup = state.GetComponentLookup<VesselMobilityProfile>(true);
            _physicalLookup = state.GetComponentLookup<VesselPhysicalProperties>(true);
            _navigationLookup = state.GetComponentLookup<NavigationCohesion>(true);
            _focusModifiersLookup = state.GetComponentLookup<Space4XFocusModifiers>(true);
            _resolvedControlLookup = state.GetBufferLookup<ResolvedControl>(true);
            _crewSkillsLookup = state.GetComponentLookup<CrewSkills>(true);
            _behaviorDispositionLookup = state.GetComponentLookup<BehaviorDisposition>(true);
            _departmentStatsLookup = state.GetBufferLookup<DepartmentStatsBuffer>(true);
            _priorityLookup = state.GetComponentLookup<TargetPriority>(true);
            _targetTransformLookup = state.GetComponentLookup<LocalTransform>(true);
            _engagementLookup = state.GetComponentLookup<Space4XEngagement>(true);
            _weaponLookup = state.GetBufferLookup<WeaponMountBuffer>(true);
            _asteroidLookup = state.GetComponentLookup<Asteroid>(true);
            _asteroidVolumeLookup = state.GetComponentLookup<Space4XAsteroidVolumeConfig>(true);
            _asteroidCenterLookup = state.GetComponentLookup<Space4XAsteroidCenter>(true);
            _moveIntentLookup = state.GetComponentLookup<MoveIntent>(false);
            _movePlanLookup = state.GetComponentLookup<MovePlan>(false);
            _decisionTraceLookup = state.GetComponentLookup<DecisionTrace>(false);
            _movementDebugLookup = state.GetComponentLookup<MovementDebugState>(false);
            _traceEventLookup = state.GetBufferLookup<MoveTraceEvent>(false);
            _attackMoveLookup = state.GetComponentLookup<AttackMoveIntent>(true);
            _aimDirectiveLookup = state.GetComponentLookup<VesselAimDirective>(true);
            _subsystemLookup = state.GetBufferLookup<SubsystemHealth>(true);
            _subsystemDisabledLookup = state.GetBufferLookup<SubsystemDisabled>(true);
            _physicsColliderLookup = state.GetComponentLookup<PhysicsCollider>(true);
            _spacePhysicsBodyLookup = state.GetComponentLookup<SpacePhysicsBody>(true);
            _colliderSpecLookup = state.GetComponentLookup<PhysicsColliderSpec>(true);
            _movementSuppressedLookup = state.GetComponentLookup<MovementSuppressed>(true);
            _roleNavigationOfficer = default;
            _roleNavigationOfficer.Append('s');
            _roleNavigationOfficer.Append('h');
            _roleNavigationOfficer.Append('i');
            _roleNavigationOfficer.Append('p');
            _roleNavigationOfficer.Append('.');
            _roleNavigationOfficer.Append('n');
            _roleNavigationOfficer.Append('a');
            _roleNavigationOfficer.Append('v');
            _roleNavigationOfficer.Append('i');
            _roleNavigationOfficer.Append('g');
            _roleNavigationOfficer.Append('a');
            _roleNavigationOfficer.Append('t');
            _roleNavigationOfficer.Append('i');
            _roleNavigationOfficer.Append('o');
            _roleNavigationOfficer.Append('n');
            _roleNavigationOfficer.Append('_');
            _roleNavigationOfficer.Append('o');
            _roleNavigationOfficer.Append('f');
            _roleNavigationOfficer.Append('f');
            _roleNavigationOfficer.Append('i');
            _roleNavigationOfficer.Append('c');
            _roleNavigationOfficer.Append('e');
            _roleNavigationOfficer.Append('r');

            _roleShipmaster = default;
            _roleShipmaster.Append('s');
            _roleShipmaster.Append('h');
            _roleShipmaster.Append('i');
            _roleShipmaster.Append('p');
            _roleShipmaster.Append('.');
            _roleShipmaster.Append('s');
            _roleShipmaster.Append('h');
            _roleShipmaster.Append('i');
            _roleShipmaster.Append('p');
            _roleShipmaster.Append('m');
            _roleShipmaster.Append('a');
            _roleShipmaster.Append('s');
            _roleShipmaster.Append('t');
            _roleShipmaster.Append('e');
            _roleShipmaster.Append('r');

            _roleCaptain = default;
            _roleCaptain.Append('s');
            _roleCaptain.Append('h');
            _roleCaptain.Append('i');
            _roleCaptain.Append('p');
            _roleCaptain.Append('.');
            _roleCaptain.Append('c');
            _roleCaptain.Append('a');
            _roleCaptain.Append('p');
            _roleCaptain.Append('t');
            _roleCaptain.Append('a');
            _roleCaptain.Append('i');
            _roleCaptain.Append('n');
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

            // Debug logging (only first frame)
#if UNITY_EDITOR
            if (currentTick == 1)
            {
                var vesselCount = SystemAPI.QueryBuilder().WithAll<VesselMovement>().Build().CalculateEntityCount();
                UnityDebug.Log($"[VesselMovementSystem] Found {vesselCount} vessels, DeltaTime={deltaTime}, Tick={currentTick}");
            }
#endif

            EnsureMovementDebugSurfaces(ref state);

            _threatLookup.Update(ref state);
            _stanceLookup.Update(ref state);
            _capabilityStateLookup.Update(ref state);
            _effectivenessLookup.Update(ref state);
            _alignmentLookup.Update(ref state);
            _outlookLookup.Update(ref state);
            _statsLookup.Update(ref state);
            _pilotLookup.Update(ref state);
            _pilotProficiencyLookup.Update(ref state);
            _seatRefLookup.Update(ref state);
            _seatLookup.Update(ref state);
            _seatOccupantLookup.Update(ref state);
            _carrierLookup.Update(ref state);
            _miningVesselLookup.Update(ref state);
            _miningStateLookup.Update(ref state);
            _moduleAggregateLookup.Update(ref state);
            _moduleCapabilityLookup.Update(ref state);
            _engineOutputLookup.Update(ref state);
            _shipPowerConsumerLookup.Update(ref state);
            _powerConsumerLookup.Update(ref state);
            _powerEffectivenessLookup.Update(ref state);
            _qualityLookup.Update(ref state);
            _mobilityProfileLookup.Update(ref state);
            _physicalLookup.Update(ref state);
            _navigationLookup.Update(ref state);
            _focusModifiersLookup.Update(ref state);
            _resolvedControlLookup.Update(ref state);
            _crewSkillsLookup.Update(ref state);
            _behaviorDispositionLookup.Update(ref state);
            _departmentStatsLookup.Update(ref state);
            _priorityLookup.Update(ref state);
            _targetTransformLookup.Update(ref state);
            _engagementLookup.Update(ref state);
            _weaponLookup.Update(ref state);
            _asteroidLookup.Update(ref state);
            _asteroidVolumeLookup.Update(ref state);
            _asteroidCenterLookup.Update(ref state);
            _moveIntentLookup.Update(ref state);
            _movePlanLookup.Update(ref state);
            _decisionTraceLookup.Update(ref state);
            _movementDebugLookup.Update(ref state);
            _traceEventLookup.Update(ref state);
            _attackMoveLookup.Update(ref state);
            _aimDirectiveLookup.Update(ref state);
            _subsystemLookup.Update(ref state);
            _subsystemDisabledLookup.Update(ref state);
            _physicsColliderLookup.Update(ref state);
            _spacePhysicsBodyLookup.Update(ref state);
            _colliderSpecLookup.Update(ref state);
            _movementSuppressedLookup.Update(ref state);

            var hasPhysicsWorld = SystemAPI.TryGetSingleton<PhysicsWorldSingleton>(out var physicsWorld);

            var motionConfig = VesselMotionProfileConfig.Default;
            if (SystemAPI.TryGetSingleton<VesselMotionProfileConfig>(out var motionConfigSingleton))
            {
                motionConfig = motionConfigSingleton;
            }
            var stanceConfig = Space4XStanceTuningConfig.Default;
            if (SystemAPI.TryGetSingleton<Space4XStanceTuningConfig>(out var stanceConfigSingleton))
            {
                stanceConfig = stanceConfigSingleton;
            }
            var inertiaConfig = Space4XMovementInertiaConfig.Default;
            if (SystemAPI.TryGetSingleton<Space4XMovementInertiaConfig>(out var inertiaConfigSingleton))
            {
                inertiaConfig = inertiaConfigSingleton;
            }
            var movementTuning = Space4XMovementTuningConfig.Default;
            if (SystemAPI.TryGetSingleton<Space4XMovementTuningConfig>(out var movementTuningSingleton))
            {
                movementTuning = movementTuningSingleton;
            }

            var job = new UpdateVesselMovementJob
            {
                DeltaTime = deltaTime,
                CurrentTick = currentTick,
                ArrivalDistance = 2f, // Vessels stop 2 units away from target
                BaseRotationSpeed = 2f, // Base rotate speed in radians per second
                MotionConfig = motionConfig,
                StanceConfig = stanceConfig,
                InertiaConfig = inertiaConfig,
                MovementTuning = movementTuning,
                RoleNavigationOfficer = _roleNavigationOfficer,
                RoleShipmaster = _roleShipmaster,
                RoleCaptain = _roleCaptain,
                ThreatLookup = _threatLookup,
                StanceLookup = _stanceLookup,
                CapabilityStateLookup = _capabilityStateLookup,
                EffectivenessLookup = _effectivenessLookup,
                AlignmentLookup = _alignmentLookup,
                OutlookLookup = _outlookLookup,
                StatsLookup = _statsLookup,
                PilotLookup = _pilotLookup,
                PilotProficiencyLookup = _pilotProficiencyLookup,
                SeatRefLookup = _seatRefLookup,
                SeatLookup = _seatLookup,
                SeatOccupantLookup = _seatOccupantLookup,
                CarrierLookup = _carrierLookup,
                MiningVesselLookup = _miningVesselLookup,
                MiningStateLookup = _miningStateLookup,
                ModuleAggregateLookup = _moduleAggregateLookup,
                ModuleCapabilityLookup = _moduleCapabilityLookup,
                EngineOutputLookup = _engineOutputLookup,
                ShipPowerConsumerLookup = _shipPowerConsumerLookup,
                PowerConsumerLookup = _powerConsumerLookup,
                PowerEffectivenessLookup = _powerEffectivenessLookup,
                QualityLookup = _qualityLookup,
                MobilityProfileLookup = _mobilityProfileLookup,
                PhysicalLookup = _physicalLookup,
                NavigationLookup = _navigationLookup,
                FocusModifiersLookup = _focusModifiersLookup,
                ResolvedControlLookup = _resolvedControlLookup,
                CrewSkillsLookup = _crewSkillsLookup,
                BehaviorDispositionLookup = _behaviorDispositionLookup,
                DepartmentStatsLookup = _departmentStatsLookup,
                PriorityLookup = _priorityLookup,
                TargetTransformLookup = _targetTransformLookup,
                EngagementLookup = _engagementLookup,
                WeaponLookup = _weaponLookup,
                AsteroidLookup = _asteroidLookup,
                AsteroidVolumeLookup = _asteroidVolumeLookup,
                AsteroidCenterLookup = _asteroidCenterLookup,
                MoveIntentLookup = _moveIntentLookup,
                MovePlanLookup = _movePlanLookup,
                DecisionTraceLookup = _decisionTraceLookup,
                MovementDebugLookup = _movementDebugLookup,
                TraceEventLookup = _traceEventLookup,
                AttackMoveLookup = _attackMoveLookup,
                AimDirectiveLookup = _aimDirectiveLookup,
                SubsystemLookup = _subsystemLookup,
                SubsystemDisabledLookup = _subsystemDisabledLookup,
                PhysicsColliderLookup = _physicsColliderLookup,
                SpacePhysicsBodyLookup = _spacePhysicsBodyLookup,
                ColliderSpecLookup = _colliderSpecLookup,
                MovementSuppressedLookup = _movementSuppressedLookup,
                HasPhysicsWorld = hasPhysicsWorld,
                PhysicsWorld = physicsWorld,
                SweepSkin = 0.05f,
                AllowSlide = 1
            };

            state.Dependency = job.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        [WithNone(typeof(StrikeCraftDogfightTag), typeof(SimulationDisabledTag))]
        public partial struct UpdateVesselMovementJob : IJobEntity
        {
            public float DeltaTime;
            public uint CurrentTick;
            public float ArrivalDistance;
            public float BaseRotationSpeed;
            public VesselMotionProfileConfig MotionConfig;
            public Space4XStanceTuningConfig StanceConfig;
            public Space4XMovementInertiaConfig InertiaConfig;
            public Space4XMovementTuningConfig MovementTuning;
            public FixedString64Bytes RoleNavigationOfficer;
            public FixedString64Bytes RoleShipmaster;
            public FixedString64Bytes RoleCaptain;
            [ReadOnly] public ComponentLookup<ThreatProfile> ThreatLookup;
            [ReadOnly] public ComponentLookup<VesselStanceComponent> StanceLookup;
            [ReadOnly] public ComponentLookup<CapabilityState> CapabilityStateLookup;
            [ReadOnly] public ComponentLookup<CapabilityEffectiveness> EffectivenessLookup;
            [ReadOnly] public ComponentLookup<AlignmentTriplet> AlignmentLookup;
            [ReadOnly] public BufferLookup<StanceEntry> OutlookLookup;
            [ReadOnly] public ComponentLookup<IndividualStats> StatsLookup;
            [ReadOnly] public ComponentLookup<VesselPilotLink> PilotLookup;
            [ReadOnly] public ComponentLookup<PilotProficiency> PilotProficiencyLookup;
            [ReadOnly] public BufferLookup<AuthoritySeatRef> SeatRefLookup;
            [ReadOnly] public ComponentLookup<AuthoritySeat> SeatLookup;
            [ReadOnly] public ComponentLookup<AuthoritySeatOccupant> SeatOccupantLookup;
            [ReadOnly] public ComponentLookup<Carrier> CarrierLookup;
            [ReadOnly] public ComponentLookup<MiningVessel> MiningVesselLookup;
            [ReadOnly] public ComponentLookup<MiningState> MiningStateLookup;
            [ReadOnly] public ComponentLookup<ModuleStatAggregate> ModuleAggregateLookup;
            [ReadOnly] public ComponentLookup<ModuleCapabilityOutput> ModuleCapabilityLookup;
            [ReadOnly] public ComponentLookup<EnginePerformanceOutput> EngineOutputLookup;
            [ReadOnly] public BufferLookup<ShipPowerConsumer> ShipPowerConsumerLookup;
            [ReadOnly] public ComponentLookup<PowerConsumer> PowerConsumerLookup;
            [ReadOnly] public ComponentLookup<PowerEffectiveness> PowerEffectivenessLookup;
            [ReadOnly] public ComponentLookup<VesselQuality> QualityLookup;
            [ReadOnly] public ComponentLookup<VesselMobilityProfile> MobilityProfileLookup;
            [ReadOnly] public ComponentLookup<VesselPhysicalProperties> PhysicalLookup;
            [ReadOnly] public ComponentLookup<NavigationCohesion> NavigationLookup;
            [ReadOnly] public ComponentLookup<Space4XFocusModifiers> FocusModifiersLookup;
            [ReadOnly] public BufferLookup<ResolvedControl> ResolvedControlLookup;
            [ReadOnly] public ComponentLookup<CrewSkills> CrewSkillsLookup;
            [ReadOnly] public ComponentLookup<BehaviorDisposition> BehaviorDispositionLookup;
            [ReadOnly] public BufferLookup<DepartmentStatsBuffer> DepartmentStatsLookup;
            [ReadOnly] public ComponentLookup<TargetPriority> PriorityLookup;
            [ReadOnly] public ComponentLookup<LocalTransform> TargetTransformLookup;
            [ReadOnly] public ComponentLookup<Space4XEngagement> EngagementLookup;
            [ReadOnly] public BufferLookup<WeaponMountBuffer> WeaponLookup;
            [ReadOnly] public ComponentLookup<Asteroid> AsteroidLookup;
            [ReadOnly] public ComponentLookup<Space4XAsteroidVolumeConfig> AsteroidVolumeLookup;
            [ReadOnly] public ComponentLookup<Space4XAsteroidCenter> AsteroidCenterLookup;
            [NativeDisableParallelForRestriction] public ComponentLookup<MoveIntent> MoveIntentLookup;
            [NativeDisableParallelForRestriction] public ComponentLookup<MovePlan> MovePlanLookup;
            [NativeDisableParallelForRestriction] public ComponentLookup<DecisionTrace> DecisionTraceLookup;
            [NativeDisableParallelForRestriction] public ComponentLookup<MovementDebugState> MovementDebugLookup;
            [NativeDisableParallelForRestriction] public BufferLookup<MoveTraceEvent> TraceEventLookup;
            [ReadOnly] public ComponentLookup<AttackMoveIntent> AttackMoveLookup;
            [ReadOnly] public ComponentLookup<VesselAimDirective> AimDirectiveLookup;
            [ReadOnly] public BufferLookup<SubsystemHealth> SubsystemLookup;
            [ReadOnly] public BufferLookup<SubsystemDisabled> SubsystemDisabledLookup;
            [ReadOnly] public ComponentLookup<PhysicsCollider> PhysicsColliderLookup;
            [ReadOnly] public ComponentLookup<SpacePhysicsBody> SpacePhysicsBodyLookup;
            [ReadOnly] public ComponentLookup<PhysicsColliderSpec> ColliderSpecLookup;
            [ReadOnly] public ComponentLookup<MovementSuppressed> MovementSuppressedLookup;
            [ReadOnly] public PhysicsWorldSingleton PhysicsWorld;
            public bool HasPhysicsWorld;
            public float SweepSkin;
            public byte AllowSlide;

            public void Execute(Entity entity, ref VesselMovement movement, ref LocalTransform transform, ref VesselTurnRateState turnRateState, ref VesselThrottleState throttleState, in VesselAIState aiState)
            {
                var debugState = new MovementDebugState();
                var hasDebug = MovementDebugLookup.HasComponent(entity);
                if (hasDebug)
                {
                    debugState = MovementDebugLookup[entity];
                }

                if (turnRateState.Initialized == 0)
                {
                    turnRateState.Initialized = 1;
                    turnRateState.LastAngularSpeed = 0f;
                    turnRateState.SmoothedDirection = float3.zero;
                    turnRateState.LastDesiredDirection = float3.zero;
                    turnRateState.AttackRunDirection = float3.zero;
                    turnRateState.CombatBlend = 0f;
                    turnRateState.LastDesiredTick = 0;
                    turnRateState.HeadingHoldUntilTick = 0;
                    turnRateState.AttackRunCommitUntilTick = 0;
                    turnRateState.AttackRunCooldownUntilTick = 0;
                    turnRateState.AttackRunTarget = Entity.Null;
                }

                if (!IsFinite(transform.Position) || !IsFinite(transform.Rotation.value) || !IsFinite(movement.Velocity))
                {
                    movement.Velocity = float3.zero;
                    movement.CurrentSpeed = 0f;
                    movement.IsMoving = 0;
                    turnRateState.LastAngularSpeed = 0f;
                    if (hasDebug)
                    {
                        debugState.NaNInfCount += 1;
                        MovementDebugLookup[entity] = debugState;
                    }
                    return;
                }

                if (MovementSuppressedLookup.HasComponent(entity) &&
                    MovementSuppressedLookup.IsComponentEnabled(entity))
                {
                    movement.Velocity = float3.zero;
                    movement.CurrentSpeed = 0f;
                    movement.IsMoving = 0;
                    turnRateState.LastAngularSpeed = 0f;
                    throttleState.RampTicks = 0;
                    return;
                }

                // Check Movement capability - if disabled, stop movement
                if (CapabilityStateLookup.HasComponent(entity))
                {
                    var capabilityState = CapabilityStateLookup[entity];
                    if ((capabilityState.EnabledCapabilities & CapabilityFlags.Movement) == 0)
                    {
                        movement.Velocity = float3.zero;
                        movement.CurrentSpeed = 0f;
                        movement.IsMoving = 0;
                        turnRateState.LastAngularSpeed = 0f;
                        return;
                    }
                }

                var inertialEnabled = InertiaConfig.InertialMovementV1 != 0;
                var hasAttackMove = AttackMoveLookup.HasComponent(entity);
                var attackMove = hasAttackMove ? AttackMoveLookup[entity] : default;
                var forceHold = aiState.CurrentState == VesselAIState.State.Mining;
                var priorityTarget = Entity.Null;
                var hasPriorityTarget = false;
                if (PriorityLookup.HasComponent(entity))
                {
                    var priority = PriorityLookup[entity];
                    if (priority.CurrentTarget != Entity.Null && TargetTransformLookup.HasComponent(priority.CurrentTarget))
                    {
                        priorityTarget = priority.CurrentTarget;
                        hasPriorityTarget = true;
                    }
                }
                var noTarget = aiState.TargetEntity == Entity.Null && !hasAttackMove && !hasPriorityTarget;

                // Don't move if mining - stay in place to gather resources
                if (forceHold && !inertialEnabled)
                {
                    movement.Velocity = float3.zero;
                    movement.CurrentSpeed = 0f;
                    movement.IsMoving = 0;
                    turnRateState.LastAngularSpeed = 0f;
                    throttleState.RampTicks = 0;
                    UpdateDecisionTrace(entity, DecisionReasonCode.MiningHold, Entity.Null, 0f, Entity.Null, ref debugState, hasDebug);
                    return;
                }

                // Only check TargetEntity unless attack-move intent is present
                if (noTarget && !inertialEnabled)
                {
                    movement.Velocity = float3.zero;
                    movement.CurrentSpeed = 0f;
                    movement.IsMoving = 0;
                    turnRateState.LastAngularSpeed = 0f;
                    throttleState.RampTicks = 0;
                    UpdateDecisionTrace(entity, DecisionReasonCode.NoTarget, Entity.Null, 0f, Entity.Null, ref debugState, hasDebug);
                    return;
                }

                var engineScale = 1f;
                if (SubsystemLookup.HasBuffer(entity))
                {
                    var subsystems = SubsystemLookup[entity];
                    if (SubsystemDisabledLookup.HasBuffer(entity))
                    {
                        var disabledSubsystems = SubsystemDisabledLookup[entity];
                        engineScale = Space4XSubsystemUtility.ResolveEngineScale(subsystems, disabledSubsystems);
                    }
                    else if (Space4XSubsystemUtility.IsSubsystemDisabled(subsystems, SubsystemType.Engines))
                    {
                        engineScale = Space4XSubsystemUtility.EngineDisabledScale;
                    }
                }

                var thrustAuthority = 1f;
                var turnAuthority = 1f;
                var hasCapability = ModuleCapabilityLookup.HasComponent(entity);
                if (hasCapability)
                {
                    var capability = ModuleCapabilityLookup[entity];
                    thrustAuthority = math.saturate(capability.ThrustAuthority);
                    turnAuthority = math.saturate(capability.TurnAuthority);
                }

                var engineResponse = 0.5f;
                var engineEfficiency = 0.5f;
                var engineBoost = 0.5f;
                var engineTech = 0.5f;
                var engineQuality = 0.5f;
                var engineVectoring = 0.5f;
                if (EngineOutputLookup.HasComponent(entity))
                {
                    var engineOutput = EngineOutputLookup[entity];
                    engineResponse = math.saturate(engineOutput.Response);
                    engineEfficiency = math.saturate(engineOutput.Efficiency);
                    engineBoost = math.saturate(engineOutput.Boost);
                    engineTech = math.saturate(engineOutput.TechLevel);
                    engineQuality = math.saturate(engineOutput.Quality);
                    engineVectoring = math.saturate(engineOutput.Vectoring);

                    if (!hasCapability)
                    {
                        thrustAuthority = math.saturate(engineOutput.ThrustAuthority);
                        turnAuthority = math.saturate(engineOutput.TurnAuthority);
                    }
                }
                var powerAuthority = ResolvePowerAuthority(entity);
                thrustAuthority *= powerAuthority;
                turnAuthority *= powerAuthority;

                var baseSpeed = math.max(0.1f, movement.BaseSpeed * engineScale);

                // TargetPosition should be resolved by VesselTargetingSystem (runs earlier in Space4XTransportAISystemGroup).
                var targetPosition = hasAttackMove ? attackMove.Destination : aiState.TargetPosition;
                if (!hasAttackMove && aiState.TargetEntity == Entity.Null && hasPriorityTarget)
                {
                    targetPosition = TargetTransformLookup[priorityTarget].Position;
                }
                if (forceHold || noTarget)
                {
                    targetPosition = transform.Position;
                }

                var decisionReason = DecisionReasonCode.Moving;
                if (forceHold)
                {
                    decisionReason = DecisionReasonCode.MiningHold;
                }
                else if (noTarget)
                {
                    decisionReason = DecisionReasonCode.NoTarget;
                }
                var forceStop = forceHold || noTarget;
                var isAsteroidTarget = false;
                var asteroidRadius = 0f;
                var asteroidStandoff = 0f;
                if (AsteroidLookup.HasComponent(aiState.TargetEntity) &&
                    AsteroidVolumeLookup.HasComponent(aiState.TargetEntity))
                {
                    var volume = AsteroidVolumeLookup[aiState.TargetEntity];
                    asteroidRadius = math.max(0.5f, volume.Radius);
                    asteroidStandoff = MiningVesselLookup.HasComponent(entity) ? 1.4f : 6.5f;
                    isAsteroidTarget = true;
                }

                var physical = PhysicalLookup.HasComponent(entity)
                    ? PhysicalLookup[entity]
                    : VesselPhysicalProperties.Default;
                var vesselRadius = math.max(0.1f, physical.Radius);
                var baseMass = math.max(0.1f, physical.BaseMass);
                var cargoMass = 0f;
                if (MiningVesselLookup.HasComponent(entity))
                {
                    cargoMass = math.max(0f, MiningVesselLookup[entity].CurrentCargo) * math.max(0f, physical.CargoMassPerUnit);
                }
                var massFactor = math.saturate(1f / (1f + cargoMass / baseMass));
                var totalMass = baseMass + cargoMass;
                var massScale = math.max(1f, totalMass / 60f);
                var massAccelPenalty = math.max(0.2f, math.rsqrt(massScale));
                var massDecelPenalty = math.max(0.2f, math.rsqrt(massScale * 0.85f));
                var massTurnPenalty = math.max(0.15f, math.rsqrt(massScale * 1.15f));
                var massSlowdownMultiplier = math.lerp(1f, 1.5f, math.saturate((massScale - 1f) / 6f));
                var restitution = math.clamp(physical.Restitution * massFactor, 0f, 1f);
                var tangentialDamping = math.clamp(physical.TangentialDamping, 0f, 1f);
                var toTarget = targetPosition - transform.Position;
                var distance = math.length(toTarget);
                var blockerEntity = Entity.Null;

                var arrivalDistance = movement.ArrivalDistance > 0f ? movement.ArrivalDistance : ArrivalDistance;
                if (hasAttackMove && attackMove.DestinationRadius > 0f)
                {
                    arrivalDistance = math.max(arrivalDistance, attackMove.DestinationRadius);
                }
                var profileEntity = ResolveProfileEntity(entity);
                var disposition = ResolveBehaviorDisposition(profileEntity, entity);
                var compliance = disposition.Compliance;
                var caution = disposition.Caution;
                var riskTolerance = disposition.RiskTolerance;
                var aggression = disposition.Aggression;
                var patience = disposition.Patience;

                arrivalDistance *= math.lerp(0.95f, 1.15f, caution);
                arrivalDistance *= math.lerp(0.95f, 1.1f, patience);
                if (isAsteroidTarget)
                {
                    arrivalDistance = math.max(arrivalDistance, MiningVesselLookup.HasComponent(entity) ? 1.2f : 4.5f);
                }

                var alignment = AlignmentLookup.HasComponent(profileEntity)
                    ? AlignmentLookup[profileEntity]
                    : default;
                var lawfulness = AlignmentMath.Lawfulness(alignment);
                var chaos = AlignmentMath.Chaos(alignment);
                var integrity = AlignmentMath.IntegrityNormalized(alignment);
                var discipline = GetOutlookDiscipline(profileEntity);

                var command = 0.5f;
                var tactics = 0.5f;
                var engineering = 0.5f;
                if (StatsLookup.HasComponent(profileEntity))
                {
                    var stats = StatsLookup[profileEntity];
                    command = math.saturate((float)stats.Command / 100f);
                    tactics = math.saturate((float)stats.Tactics / 100f);
                    engineering = math.saturate((float)stats.Engineering / 100f);
                }

                var navigationCohesion = NavigationLookup.HasComponent(entity)
                    ? math.saturate(NavigationLookup[entity].Value)
                    : 0.5f;
                var crewExploration = 0.5f;
                if (CrewSkillsLookup.HasComponent(entity))
                {
                    crewExploration = math.saturate(CrewSkillsLookup[entity].ExplorationSkill);
                }
                var crewNavigation = math.lerp(0.85f, 1.15f, crewExploration);
                navigationCohesion = math.saturate(navigationCohesion * crewNavigation);
                var navStability = math.lerp(0.9f, 1.1f, navigationCohesion);
                discipline = math.saturate(discipline * navStability);

                var pilotSkill = math.saturate(command * 0.45f + tactics * 0.35f + engineering * 0.2f);
                var intelligence = math.saturate((command + tactics) * 0.5f * navStability);
                var pilotProficiency = new PilotProficiency
                {
                    ControlMult = 1f,
                    TurnRateMult = 1f,
                    EnergyMult = 1f,
                    Jitter = 0f,
                    ReactionSec = 0.5f
                };
                if (PilotProficiencyLookup.HasComponent(profileEntity))
                {
                    pilotProficiency = PilotProficiencyLookup[profileEntity];
                }
                else if (PilotProficiencyLookup.HasComponent(entity))
                {
                    pilotProficiency = PilotProficiencyLookup[entity];
                }

                var controlMult = math.clamp(pilotProficiency.ControlMult, 0.5f, 1.6f);
                var turnMult = math.clamp(pilotProficiency.TurnRateMult, 0.6f, 1.4f);
                var energyMult = math.clamp(pilotProficiency.EnergyMult, 0.6f, 1.6f);
                var reactionSec = math.clamp(pilotProficiency.ReactionSec, 0.1f, 1.2f);
                var jitter = math.clamp(pilotProficiency.Jitter, 0f, 0.2f);

                var controlNorm = math.saturate((controlMult - 0.5f) / 1.0f);
                var turnNorm = math.saturate((turnMult - 0.7f) / 0.6f);
                var energyNorm = math.saturate((1.5f - energyMult) / 0.8f);
                var reactionNorm = math.saturate((1.0f - reactionSec) / 0.9f);
                var jitterNorm = math.saturate(jitter / 0.1f);

                var pilotMastery = math.saturate(pilotSkill * 0.55f + controlNorm * 0.25f + crewExploration * 0.2f);
                var pilotControlMultiplier = math.lerp(0.85f, 1.15f, controlNorm);
                var pilotTurnMultiplier = math.lerp(0.85f, 1.2f, turnNorm);
                var pilotEnergyMultiplier = math.lerp(0.9f, 1.1f, energyNorm);
                var pilotResponseMultiplier = math.lerp(0.75f, 1.2f, reactionNorm);
                var pilotStabilityBias = math.lerp(0.85f, 1.2f, math.saturate(pilotSkill * 0.6f + crewExploration * 0.25f + controlNorm * 0.15f));
                var pilotJitter = jitterNorm;
                var throttleRampScale = math.lerp(1.35f, 0.85f, reactionNorm);
                throttleRampScale *= math.lerp(1.15f, 0.9f, pilotMastery);
                var focusEvasion = 0f;
                var focusFormation = 0f;
                var focusEntity = Entity.Null;
                if (FocusModifiersLookup.HasComponent(entity))
                {
                    focusEntity = entity;
                }
                else if (FocusModifiersLookup.HasComponent(profileEntity))
                {
                    focusEntity = profileEntity;
                }
                if (focusEntity != Entity.Null)
                {
                    var focusMods = FocusModifiersLookup[focusEntity];
                    focusEvasion = math.max(0f, (float)focusMods.EvasionBonus);
                    focusFormation = math.max(0f, (float)focusMods.FormationCohesionBonus);
                }
                var deliberate = math.saturate(lawfulness * (0.35f + integrity * 0.65f));
                var economic = math.saturate(integrity * (0.4f + lawfulness * 0.6f));
                var chaotic = math.saturate(chaos * (1f - discipline * 0.35f));
                var risk = math.saturate(chaos * 0.6f + (1f - lawfulness) * 0.2f + (1f - discipline) * 0.2f);

                deliberate = math.saturate(math.lerp(deliberate, compliance, 0.4f));
                chaotic = math.saturate(math.lerp(chaotic, 1f - compliance, 0.35f));
                risk = math.saturate(math.lerp(risk, riskTolerance, 0.5f));

                var pushIntent = math.saturate(
                    math.lerp(0.35f, 0.95f, aggression)
                    * math.lerp(1.05f, 0.85f, caution)
                    * math.lerp(1f, 1.1f, risk));
                var pushControl = math.saturate(
                    math.lerp(0.75f, 1.1f, pilotSkill)
                    * math.lerp(0.9f, 1.05f, navigationCohesion));
                pushIntent = math.saturate(pushIntent * math.lerp(0.9f, 1.1f, pilotMastery));
                pushControl = math.saturate(
                    pushControl
                    * math.lerp(0.85f, 1.15f, pilotMastery)
                    * math.lerp(1f, 0.9f, pilotJitter));

                var suppressArrival = false;
                if (!forceHold && !noTarget)
                {
                    if (AimDirectiveLookup.HasComponent(entity))
                    {
                        var aim = AimDirectiveLookup[entity];
                        suppressArrival = aim.AimTarget != Entity.Null;
                    }

                    if (!suppressArrival && EngagementLookup.HasComponent(entity))
                    {
                        var engagement = EngagementLookup[entity];
                        if (engagement.PrimaryTarget != Entity.Null &&
                            engagement.Phase != EngagementPhase.None &&
                            engagement.Phase != EngagementPhase.Destroyed &&
                            engagement.Phase != EngagementPhase.Disabled)
                        {
                            suppressArrival = true;
                        }
                    }
                }

                var currentSpeed = math.length(movement.Velocity);
                var currentSpeedSq = currentSpeed * currentSpeed;
                movement.CurrentSpeed = currentSpeed;
                var startingMove = movement.IsMoving == 0;
                if (startingMove || movement.MoveStartTick == 0)
                {
                    movement.MoveStartTick = CurrentTick;
                }

                var stopSpeed = math.max(0.05f, baseSpeed * 0.1f);
                var arrivedAndSlow = distance <= arrivalDistance && currentSpeed <= stopSpeed;
                if (suppressArrival)
                {
                    arrivedAndSlow = false;
                }
                if (arrivedAndSlow && !inertialEnabled)
                {
                    movement.Velocity = float3.zero;
                    movement.CurrentSpeed = 0f;
                    movement.IsMoving = 0;
                    turnRateState.LastAngularSpeed = 0f;
                    UpdateMoveIntent(entity, aiState.TargetEntity, targetPosition, MoveIntentType.Hold, ref debugState, hasDebug);
                    UpdateMovePlan(entity, MovePlanMode.Arrive, float3.zero, 0f, 0f, ref debugState, hasDebug);
                    UpdateDecisionTrace(entity, DecisionReasonCode.Arrived, aiState.TargetEntity, 1f, Entity.Null, ref debugState, hasDebug);
                    // VesselGatheringSystem will transition to Mining state when close enough
                    return;
                }
                if (arrivedAndSlow)
                {
                    forceStop = true;
                    if (!noTarget && !forceHold)
                    {
                        decisionReason = DecisionReasonCode.Arrived;
                    }
                }

                var direction = math.normalizesafe(toTarget, new float3(0f, 0f, 1f));
                var baseDirection = direction;
                
                // Get stance parameters (default to Balanced if no stance component)
                var stanceType = VesselStanceMode.Balanced;
                if (StanceLookup.HasComponent(entity))
                {
                    stanceType = StanceLookup[entity].CurrentStance;
                }
                
                var stanceConfig = StanceConfig.Resolve(stanceType);
                var avoidanceRadius = stanceConfig.AvoidanceRadius;
                var avoidanceStrength = stanceConfig.AvoidanceStrength;
                var speedMultiplier = stanceConfig.SpeedMultiplier;
                var rotationMultiplier = stanceConfig.RotationMultiplier;

                var combatSpeedScale = 1f;
                var combatManeuver = false;
                var commandBias = ResolveCommandApproachBias(entity);
                var combatDirection = direction;
                var maneuverSpeedScale = 1f;
                if (!forceHold && !noTarget)
                {
                    if (TryResolveCombatManeuver(entity, aiState, hasAttackMove, attackMove, transform, stanceConfig, ref turnRateState,
                            pilotMastery, navigationCohesion, commandBias, out combatDirection, out maneuverSpeedScale))
                    {
                        combatManeuver = true;
                    }
                }

                var combatIntent = combatManeuver;
                if (!combatIntent && !forceHold && !noTarget)
                {
                    if (AimDirectiveLookup.HasComponent(entity))
                    {
                        var aim = AimDirectiveLookup[entity];
                        combatIntent = aim.AimTarget != Entity.Null;
                    }

                    if (!combatIntent && EngagementLookup.HasComponent(entity))
                    {
                        var engagement = EngagementLookup[entity];
                        if (engagement.PrimaryTarget != Entity.Null &&
                            engagement.Phase != EngagementPhase.None &&
                            engagement.Phase != EngagementPhase.Destroyed &&
                            engagement.Phase != EngagementPhase.Disabled)
                        {
                            combatIntent = true;
                        }
                    }
                }

                var combatBlend = UpdateCombatBlend(combatIntent, pilotMastery, navigationCohesion, ref turnRateState);
                if (combatManeuver)
                {
                    direction = math.normalizesafe(math.lerp(baseDirection, combatDirection, combatBlend), combatDirection);
                    combatSpeedScale = math.lerp(1f, maneuverSpeedScale, combatBlend);
                }

                speedMultiplier *= math.lerp(1f, MotionConfig.DeliberateSpeedMultiplier, deliberate);
                speedMultiplier *= math.lerp(1f, MotionConfig.ChaoticSpeedMultiplier, chaotic);

                var accelerationMultiplier = math.lerp(1f, MotionConfig.EconomyAccelerationMultiplier, economic);
                accelerationMultiplier *= math.lerp(1f, MotionConfig.ChaoticAccelerationMultiplier, chaotic);

                var decelerationMultiplier = math.lerp(1f, MotionConfig.EconomyDecelerationMultiplier, economic);
                decelerationMultiplier *= math.lerp(1f, MotionConfig.ChaoticDecelerationMultiplier, chaotic);

                rotationMultiplier *= math.lerp(1f, MotionConfig.DeliberateTurnMultiplier, deliberate);
                rotationMultiplier *= math.lerp(1f, MotionConfig.ChaoticTurnMultiplier, chaotic);
                rotationMultiplier *= math.lerp(1f, MotionConfig.IntelligentTurnMultiplier, intelligence);

                var slowdownMultiplier = math.lerp(1f, MotionConfig.DeliberateSlowdownMultiplier, deliberate);
                slowdownMultiplier *= math.lerp(1f, MotionConfig.ChaoticSlowdownMultiplier, chaotic);
                slowdownMultiplier *= math.lerp(1f, MotionConfig.IntelligentSlowdownMultiplier, intelligence);

                var cruiseSpeedMultiplier = math.max(0f, MovementTuning.CruiseSpeedMultiplier);
                var combatSpeedMultiplier = math.max(0f, MovementTuning.CombatSpeedMultiplier);
                var cruiseTurnMultiplier = math.max(0f, MovementTuning.CruiseTurnMultiplier);
                var combatTurnMultiplier = math.max(0f, MovementTuning.CombatTurnMultiplier);
                var combatAccelMultiplier = math.max(0f, MovementTuning.CombatAccelMultiplier);
                speedMultiplier *= math.lerp(cruiseSpeedMultiplier, combatSpeedMultiplier, combatBlend);
                rotationMultiplier *= math.lerp(cruiseTurnMultiplier, combatTurnMultiplier, combatBlend);
                accelerationMultiplier *= math.lerp(1f, combatAccelMultiplier, combatBlend);
                decelerationMultiplier *= math.lerp(1f, math.max(0.75f, combatAccelMultiplier), combatBlend);
                slowdownMultiplier *= math.lerp(1f, 0.9f, combatBlend);

                speedMultiplier *= math.lerp(0.95f, 1.1f, aggression);
                rotationMultiplier *= math.lerp(0.9f, 1.1f, aggression);
                speedMultiplier *= math.lerp(1.05f, 0.85f, caution);
                slowdownMultiplier *= math.lerp(0.85f, 1.25f, caution);
                slowdownMultiplier *= math.lerp(0.95f, 1.2f, patience);
                accelerationMultiplier *= math.lerp(1.1f, 0.85f, patience);
                decelerationMultiplier *= math.lerp(0.95f, 1.15f, patience);

                speedMultiplier *= pilotEnergyMultiplier;
                accelerationMultiplier *= pilotControlMultiplier * math.lerp(1f, 0.9f, pilotJitter);
                decelerationMultiplier *= math.lerp(0.9f, 1.1f, controlNorm);
                rotationMultiplier *= pilotTurnMultiplier * math.lerp(1f, 0.92f, pilotJitter);
                slowdownMultiplier *= math.lerp(1.15f, 0.9f, pilotMastery);
                slowdownMultiplier *= math.lerp(1f, 1.2f, pilotJitter);

                var engineResponseMultiplier = math.lerp(0.8f, 1.2f, engineResponse);
                var engineBoostSpeedMultiplier = math.lerp(1f, 1.25f, engineBoost);
                var engineBoostAccelMultiplier = math.lerp(1f, 1.2f, engineBoost);
                var engineEfficiencyMultiplier = math.lerp(0.9f, 1.1f, engineEfficiency);
                var engineVectoringMultiplier = math.lerp(0.7f, 1.25f, engineVectoring);
                var engineTechMultiplier = math.lerp(0.9f, 1.1f, engineTech);
                var engineQualityMultiplier = math.lerp(0.9f, 1.1f, engineQuality);

                speedMultiplier *= engineBoostSpeedMultiplier * engineEfficiencyMultiplier * engineTechMultiplier;
                accelerationMultiplier *= engineResponseMultiplier * engineBoostAccelMultiplier * engineQualityMultiplier;
                decelerationMultiplier *= engineResponseMultiplier * engineQualityMultiplier;
                rotationMultiplier *= engineVectoringMultiplier * engineResponseMultiplier * engineTechMultiplier;

                speedMultiplier *= math.lerp(1f, 1.12f, pushIntent);
                accelerationMultiplier *= math.lerp(0.9f, 1.2f, pushIntent) * math.lerp(0.9f, 1.1f, pushControl);
                rotationMultiplier *= math.lerp(0.95f, 1.12f, pushIntent) * math.lerp(0.9f, 1.1f, pushControl);
                var brakePenalty = math.lerp(1.05f, 0.75f, pushIntent);
                decelerationMultiplier *= math.lerp(brakePenalty, 1f, pushControl);

                var navigationMultiplier = math.lerp(0.9f, 1.1f, navigationCohesion);
                accelerationMultiplier *= math.lerp(0.95f, 1.05f, navigationCohesion);
                decelerationMultiplier *= math.lerp(0.95f, 1.05f, navigationCohesion);
                rotationMultiplier *= navigationMultiplier;
                slowdownMultiplier *= math.lerp(1.1f, 0.9f, navigationCohesion);
                slowdownMultiplier *= math.lerp(1.15f, 0.9f, engineResponse);
                slowdownMultiplier *= math.lerp(1.1f, 0.85f, pushIntent);
                slowdownMultiplier *= massSlowdownMultiplier;
                if (focusEvasion > 0f || focusFormation > 0f)
                {
                    var evasionMult = math.lerp(1f, 1.25f, math.saturate(focusEvasion));
                    rotationMultiplier *= evasionMult;
                    accelerationMultiplier *= math.lerp(1f, 1.15f, focusEvasion);
                    decelerationMultiplier *= math.lerp(1f, 1.1f, focusEvasion);
                    slowdownMultiplier *= math.lerp(1.05f, 0.85f, focusEvasion);
                    rotationMultiplier *= math.lerp(1f, 1.1f, math.saturate(focusFormation));
                }

                var isCarrier = CarrierLookup.HasComponent(entity);
                if (isCarrier)
                {
                    speedMultiplier *= MotionConfig.CapitalShipSpeedMultiplier;
                    rotationMultiplier *= MotionConfig.CapitalShipTurnMultiplier;
                    accelerationMultiplier *= MotionConfig.CapitalShipAccelerationMultiplier;
                    decelerationMultiplier *= MotionConfig.CapitalShipDecelerationMultiplier;
                }

                var mobilityQuality = GetMobilityQuality(entity);
                speedMultiplier *= math.lerp(0.85f, 1.25f, mobilityQuality);
                accelerationMultiplier *= math.lerp(0.85f, 1.2f, mobilityQuality);
                decelerationMultiplier *= math.lerp(0.85f, 1.2f, mobilityQuality);
                rotationMultiplier *= math.lerp(0.9f, 1.15f, mobilityQuality);

                var moduleSpeedMultiplier = GetModuleSpeedMultiplier(entity);
                speedMultiplier *= moduleSpeedMultiplier;
                accelerationMultiplier *= math.lerp(1f, moduleSpeedMultiplier, 0.6f);
                decelerationMultiplier *= math.lerp(1f, moduleSpeedMultiplier, 0.4f);

                speedMultiplier *= thrustAuthority;
                accelerationMultiplier *= thrustAuthority;
                decelerationMultiplier *= thrustAuthority;
                rotationMultiplier *= turnAuthority;
                accelerationMultiplier *= massAccelPenalty;
                decelerationMultiplier *= massDecelPenalty;
                rotationMultiplier *= massTurnPenalty;
                speedMultiplier *= combatSpeedScale;

                var mobilitySpeedMultiplier = 1f;
                if (MobilityProfileLookup.HasComponent(entity))
                {
                    var mobilityProfile = MobilityProfileLookup[entity];
                    ApplyMobilityConstraints(ref direction, transform.Rotation, mobilityProfile, out mobilitySpeedMultiplier);
                    rotationMultiplier *= math.max(0.1f, mobilityProfile.TurnMultiplier);
                }
                
                // Apply stance-based threat avoidance
                direction = AvoidThreats(direction, transform.Position, avoidanceRadius, avoidanceStrength);

                var deviationStrength = MotionConfig.ChaoticDeviationStrength * chaotic;
                if (MiningVesselLookup.HasComponent(entity) && aiState.CurrentGoal == VesselAIState.Goal.Returning)
                {
                    speedMultiplier *= math.lerp(1f, MotionConfig.MinerRiskSpeedMultiplier, risk);
                    deviationStrength *= math.lerp(1f, MotionConfig.MinerRiskDeviationMultiplier, risk);
                    slowdownMultiplier *= math.lerp(1f, MotionConfig.MinerRiskSlowdownMultiplier, risk);
                    arrivalDistance *= math.lerp(1f, MotionConfig.MinerRiskArrivalMultiplier, risk);
                }

                if (deviationStrength > 0.001f && distance > MotionConfig.ChaoticDeviationMinDistance)
                {
                    direction = ApplyDeviation(direction, entity, aiState.TargetEntity, deviationStrength);
                }

                if (currentSpeedSq > 1e-4f)
                {
                    var currentDir = math.normalize(movement.Velocity);
                    var turnSpeed = (movement.TurnSpeed > 0f ? movement.TurnSpeed : BaseRotationSpeed) * engineScale;
                    var focusSteer = 1f + focusEvasion * 0.35f;
                    var steer = math.saturate(DeltaTime * turnSpeed * rotationMultiplier * 0.35f * focusSteer * pilotResponseMultiplier);
                    direction = math.normalizesafe(math.lerp(currentDir, direction, steer), direction);
                }
                if (forceStop && currentSpeedSq > 1e-4f)
                {
                    direction = math.normalizesafe(movement.Velocity, direction);
                }

                if (MiningStateLookup.HasComponent(entity))
                {
                    var miningState = MiningStateLookup[entity];
                    var phase = miningState.Phase;
                    var phaseSpeedMultiplier = phase switch
                    {
                        MiningPhase.Undocking => MotionConfig.MiningUndockSpeedMultiplier,
                        MiningPhase.ApproachTarget => MotionConfig.MiningApproachSpeedMultiplier,
                        MiningPhase.Latching => MotionConfig.MiningLatchSpeedMultiplier,
                        MiningPhase.Detaching => MotionConfig.MiningDetachSpeedMultiplier,
                        MiningPhase.ReturnApproach => MotionConfig.MiningReturnSpeedMultiplier,
                        MiningPhase.Docking => MotionConfig.MiningDockSpeedMultiplier,
                        _ => 1f
                    };

                    speedMultiplier *= phaseSpeedMultiplier;
                    var turnPhaseMultiplier = math.lerp(1f, phaseSpeedMultiplier, 0.5f);
                    if (phase == MiningPhase.Undocking)
                    {
                        // Slow turn-in during undock to avoid angular accel spikes on low-speed drift.
                        turnPhaseMultiplier *= math.max(0.1f, phaseSpeedMultiplier * phaseSpeedMultiplier);
                    }
                    rotationMultiplier *= turnPhaseMultiplier;

                    if ((phase == MiningPhase.Latching || phase == MiningPhase.Mining) && miningState.LatchSettleUntilTick > CurrentTick)
                    {
                        // Dampen latch settling to reduce surface jitter while attaching.
                        speedMultiplier *= 0.35f;
                        accelerationMultiplier *= 0.6f;
                        decelerationMultiplier *= 1.4f;
                        rotationMultiplier *= 0.75f;
                    }
                }

                // Apply capability effectiveness to speed (damaged engines reduce speed)
                float effectivenessMultiplier = 1f;
                if (EffectivenessLookup.HasComponent(entity))
                {
                    var effectiveness = EffectivenessLookup[entity];
                    effectivenessMultiplier = math.max(0f, effectiveness.MovementEffectiveness);
                }

                var desiredSpeed = baseSpeed * speedMultiplier * effectivenessMultiplier * mobilitySpeedMultiplier;
                var maxSpeed = math.max(0.1f, baseSpeed * 4f);
                if (desiredSpeed > maxSpeed)
                {
                    desiredSpeed = maxSpeed;
                    if (hasDebug)
                    {
                        debugState.SpeedClampCount += 1;
                    }
                }
                var slowdownDistance = movement.SlowdownDistance > 0f ? movement.SlowdownDistance : arrivalDistance * 4f;
                slowdownDistance = math.max(arrivalDistance * 1.5f, slowdownDistance * slowdownMultiplier);
                if (isAsteroidTarget && asteroidRadius > 0f)
                {
                    slowdownDistance = math.max(slowdownDistance, asteroidRadius + asteroidStandoff + arrivalDistance * 2f);
                }
                if (distance < slowdownDistance)
                {
                    desiredSpeed *= math.saturate(distance / slowdownDistance);
                }
                var overshoot = currentSpeedSq > 1e-4f && math.dot(movement.Velocity, toTarget) < 0f;
                if (overshoot)
                {
                    if (hasDebug)
                    {
                        debugState.OvershootCount += 1;
                    }
                    desiredSpeed *= 0.55f;
                    rotationMultiplier *= 0.7f;
                }
                if (distance <= arrivalDistance)
                {
                    desiredSpeed = math.min(desiredSpeed, math.max(0.05f, baseSpeed * 0.2f));
                }
                if (forceStop)
                {
                    desiredSpeed = 0f;
                }

                var acceleration = movement.Acceleration > 0f ? movement.Acceleration * engineScale : math.max(0.1f, baseSpeed * 2f);
                var deceleration = movement.Deceleration > 0f ? movement.Deceleration * engineScale : math.max(0.1f, baseSpeed * 2.5f);
                acceleration = math.max(0.01f, acceleration * accelerationMultiplier);
                deceleration = math.max(0.01f, deceleration * decelerationMultiplier);
                var maxAccel = math.max(0.1f, baseSpeed * 6f);
                if (desiredSpeed > currentSpeed + 0.01f && MotionConfig.AccelSpoolDurationSec > 0f)
                {
                    // Ease in acceleration for the first few ticks after movement starts.
                    var minMultiplier = math.clamp(MotionConfig.AccelSpoolMinMultiplier, 0.05f, 1f);
                    var spoolSeconds = math.max(1e-4f, MotionConfig.AccelSpoolDurationSec);
                    var ticksSinceStart = CurrentTick >= movement.MoveStartTick ? CurrentTick - movement.MoveStartTick : 0u;
                    var spoolT = math.saturate((ticksSinceStart * DeltaTime) / spoolSeconds);
                    var spoolThrottle = math.lerp(minMultiplier, 1f, spoolT);
                    maxAccel *= spoolThrottle;
                }
                if (acceleration > maxAccel)
                {
                    acceleration = maxAccel;
                    if (hasDebug)
                    {
                        debugState.AccelClampCount += 1;
                    }
                }
                var speedRatio = math.saturate(currentSpeed / math.max(0.1f, desiredSpeed));
                acceleration *= math.lerp(0.35f, 1f, speedRatio);
                if (overshoot)
                {
                    deceleration *= 1.5f;
                }
                if (isAsteroidTarget && asteroidRadius > 0f && distance < asteroidRadius + asteroidStandoff * 0.8f)
                {
                    desiredSpeed = math.min(desiredSpeed, math.max(0.2f, baseSpeed * 0.35f));
                    deceleration *= 1.6f;
                }
                var desiredVelocity = direction * desiredSpeed;
                if (currentSpeedSq > 1e-4f)
                {
                    var retrogradeWeight = 0f;
                    if (overshoot)
                    {
                        retrogradeWeight = 1f;
                    }
                    else if (distance < slowdownDistance)
                    {
                        retrogradeWeight = math.saturate((slowdownDistance - distance) / math.max(1e-4f, slowdownDistance));
                    }

                    if (retrogradeWeight > 0f)
                    {
                        var retrogradeBoost = MotionConfig.RetrogradeBoost;
                        if (retrogradeBoost > 0f)
                        {
                            retrogradeWeight = math.saturate(retrogradeWeight * (1f + retrogradeBoost));
                        }
                        var retroDir = math.normalizesafe(-movement.Velocity, direction);
                        var retroVelocity = retroDir * desiredSpeed;
                        desiredVelocity = math.lerp(desiredVelocity, retroVelocity, retrogradeWeight);
                        direction = math.normalizesafe(math.lerp(direction, retroDir, retrogradeWeight), retroDir);
                    }
                }

                direction = ApplyHeadingHold(
                    direction,
                    math.forward(transform.Rotation),
                    ref turnRateState,
                    CurrentTick,
                    hasDebug ? debugState.LastIntentChangeTick : 0u,
                    hasDebug ? debugState.LastPlanChangeTick : 0u,
                    currentSpeed,
                    baseSpeed,
                    combatManeuver,
                    discipline,
                    intelligence,
                    pilotStabilityBias,
                    pilotResponseMultiplier);

                var stabilizedDirection = StabilizeDirection(
                    direction,
                    math.forward(transform.Rotation),
                    ref turnRateState,
                    discipline,
                    intelligence,
                    chaotic,
                    currentSpeed,
                    baseSpeed,
                    DeltaTime,
                    forceStop,
                    pilotStabilityBias,
                    pilotResponseMultiplier,
                    pilotJitter);
                if (math.lengthsq(stabilizedDirection - direction) > 1e-6f)
                {
                    var desiredSpeedMag = math.length(desiredVelocity);
                    desiredVelocity = stabilizedDirection * desiredSpeedMag;
                    direction = stabilizedDirection;
                }

                var accelLimit = desiredSpeed > currentSpeed ? acceleration : deceleration;
                var maxDelta = accelLimit * DeltaTime;
                var throttle = 1f;
                if (inertialEnabled)
                {
                    if (desiredSpeed > currentSpeed + 0.01f)
                    {
                        var rampTicks = InertiaConfig.ThrottleRampTicks;
                        if (rampTicks > 0)
                        {
                            var adjustedRamp = (int)math.round(rampTicks * throttleRampScale);
                            rampTicks = (ushort)math.clamp(adjustedRamp, 1, 120);
                            throttleState.RampTicks = (ushort)math.min((uint)rampTicks, (uint)throttleState.RampTicks + 1u);
                            throttle = (float)throttleState.RampTicks / rampTicks;
                            maxDelta *= throttle;
                        }
                    }
                    else if (throttleState.RampTicks != 0)
                    {
                        throttleState.RampTicks = 0;
                    }
                }
                else if (throttleState.RampTicks != 0)
                {
                    throttleState.RampTicks = 0;
                }

                var deltaV = desiredVelocity - movement.Velocity;
                var deltaSq = math.lengthsq(deltaV);
                var maxDeltaSq = maxDelta * maxDelta;
                if (maxDelta > 0f && deltaSq > maxDeltaSq)
                {
                    if (hasDebug && inertialEnabled && throttle < 0.9f)
                    {
                        debugState.SharpStartCount += 1;
                    }
                    deltaV = math.normalizesafe(deltaV) * maxDelta;
                    if (hasDebug)
                    {
                        debugState.AccelClampCount += 1;
                    }
                }

                movement.Velocity += deltaV;
                movement.CurrentSpeed = math.length(movement.Velocity);

                var desiredDelta = movement.Velocity * DeltaTime;
                var resolvedDelta = desiredDelta;
                if (HasPhysicsWorld && PhysicsColliderLookup.HasComponent(entity))
                {
                    var collider = PhysicsColliderLookup[entity];
                    if (KinematicSweepUtility.TryResolveSweep(
                        PhysicsWorld,
                        collider,
                        entity,
                        transform.Position,
                        transform.Rotation,
                        desiredDelta,
                        SweepSkin,
                        AllowSlide != 0,
                        true,
                        out var sweepResult))
                    {
                        var hitEntity = sweepResult.HitEntity;
                        var isNonBlocking = sweepResult.HasHit != 0 && IsNonBlockingHit(hitEntity);
                        resolvedDelta = isNonBlocking ? desiredDelta : sweepResult.ResolvedDelta;

                        if (sweepResult.HasHit != 0 && blockerEntity == Entity.Null && !isNonBlocking)
                        {
                            blockerEntity = sweepResult.HitEntity;
                        }
                    }
                }

                transform.Position += resolvedDelta;
                if (DeltaTime > 1e-4f)
                {
                    movement.Velocity = resolvedDelta / DeltaTime;
                    movement.CurrentSpeed = math.length(movement.Velocity);
                }

                if (isAsteroidTarget && AsteroidCenterLookup.HasComponent(aiState.TargetEntity))
                {
                    var asteroidCenter = AsteroidCenterLookup[aiState.TargetEntity].Position;
                    var toVessel = transform.Position - asteroidCenter;
                    var centerDistance = math.length(toVessel);
                    var surfaceRadius = math.max(0.1f, asteroidRadius + vesselRadius);
                    if (centerDistance < surfaceRadius)
                    {
                        blockerEntity = aiState.TargetEntity;
                        var normal = centerDistance > 1e-4f ? toVessel / centerDistance : math.up();
                        var penetration = surfaceRadius - centerDistance;
                        transform.Position += normal * penetration;

                        var velocity = movement.Velocity;
                        var normalSpeed = math.dot(velocity, normal);
                        if (normalSpeed < 0f)
                        {
                            velocity -= (1f + restitution) * normalSpeed * normal;
                        }

                        var tangent = velocity - math.dot(velocity, normal) * normal;
                        velocity -= tangent * tangentialDamping;
                        movement.Velocity = velocity;
                        movement.CurrentSpeed = math.length(velocity);
                    }
                }

                if (math.lengthsq(movement.Velocity) > 0.001f)
                {
                    var rotationDirection = direction;
                    if (AimDirectiveLookup.HasComponent(entity))
                    {
                        var aim = AimDirectiveLookup[entity];
                        if (aim.AimWeight > 0f && math.lengthsq(aim.AimDirection) > 0.001f)
                        {
                            rotationDirection = math.normalizesafe(math.lerp(direction, aim.AimDirection, aim.AimWeight), direction);
                        }
                    }

                    var forward = math.forward(transform.Rotation);
                    var angle = math.acos(math.clamp(math.dot(forward, rotationDirection), -1f, 1f));
                    const float headingDeadbandRadians = 0.026f;
                    if (angle > headingDeadbandRadians)
                    {
                        movement.DesiredRotation = quaternion.LookRotationSafe(rotationDirection, math.up());
                        var turnSpeed = (movement.TurnSpeed > 0f ? movement.TurnSpeed : BaseRotationSpeed) * engineScale;
                        var dt = math.max(DeltaTime, 1e-4f);
                        var maxAngularSpeed = math.PI * 4f * math.lerp(0.9f, 1.1f, turnNorm);
                        var maxAngularAccel = math.PI * 8f * math.lerp(0.9f, 1.1f, controlNorm);
                        if (isCarrier)
                        {
                            maxAngularAccel *= MotionConfig.CapitalShipTurnMultiplier;
                        }
                        var desiredAngularSpeed = math.min(maxAngularSpeed, angle * turnSpeed * rotationMultiplier);
                        desiredAngularSpeed = math.min(desiredAngularSpeed, angle / dt);
                        var maxDeltaSpeed = maxAngularAccel * dt;
                        var angularSpeed = math.clamp(desiredAngularSpeed, turnRateState.LastAngularSpeed - maxDeltaSpeed, turnRateState.LastAngularSpeed + maxDeltaSpeed);
                        var stepAngle = angularSpeed * dt;
                        var stepT = stepAngle >= angle ? 1f : math.saturate(stepAngle / angle);
                        transform.Rotation = math.slerp(transform.Rotation, movement.DesiredRotation, stepT);
                        turnRateState.LastAngularSpeed = angularSpeed;
                    }
                    else
                    {
                        turnRateState.LastAngularSpeed = 0f;
                    }
                }
                else
                {
                    turnRateState.LastAngularSpeed = 0f;
                }

                if (forceStop)
                {
                    movement.IsMoving = movement.CurrentSpeed > 0.01f ? (byte)1 : (byte)0;
                }
                else
                {
                    movement.IsMoving = 1;
                }
                movement.LastMoveTick = CurrentTick;

                var intentType = ResolveIntentType(entity, aiState);
                if (arrivedAndSlow && !noTarget)
                {
                    intentType = MoveIntentType.Hold;
                }
                else if (combatManeuver && !forceHold && !noTarget)
                {
                    intentType = MoveIntentType.Orbit;
                }
                var planMode = ResolvePlanMode(entity, aiState, distance, arrivalDistance, isAsteroidTarget);
                if (combatManeuver && !forceHold && !noTarget)
                {
                    planMode = MovePlanMode.Orbit;
                }
                var planSpeed = math.length(desiredVelocity);
                var eta = planSpeed > 0.01f ? distance / planSpeed : 0f;
                var planAccel = DeltaTime > 1e-4f ? maxDelta / DeltaTime : accelLimit;
                var decisionTarget = noTarget ? Entity.Null : aiState.TargetEntity;
                if (decisionTarget == Entity.Null && hasPriorityTarget)
                {
                    decisionTarget = priorityTarget;
                }
                UpdateMoveIntent(entity, decisionTarget, targetPosition, intentType, ref debugState, hasDebug);
                UpdateMovePlan(entity, planMode, desiredVelocity, planAccel, eta, ref debugState, hasDebug);
                UpdateDecisionTrace(entity, decisionReason, decisionTarget, 1f, blockerEntity, ref debugState, hasDebug);
                UpdateMovementInvariants(entity, transform.Position, distance, movement.CurrentSpeed, baseSpeed, ref debugState, hasDebug);
            }

            private BehaviorDisposition ResolveBehaviorDisposition(Entity profileEntity, Entity vesselEntity)
            {
                if (BehaviorDispositionLookup.HasComponent(profileEntity))
                {
                    return BehaviorDispositionLookup[profileEntity];
                }

                if (BehaviorDispositionLookup.HasComponent(vesselEntity))
                {
                    return BehaviorDispositionLookup[vesselEntity];
                }

                return BehaviorDisposition.Default;
            }

            private float ResolveCommandApproachBias(Entity vesselEntity)
            {
                var cohesion = ResolveDepartmentCohesion(vesselEntity, DepartmentType.Command);
                var captain = ResolveSeatOccupant(vesselEntity, RoleCaptain);
                var command = 0.5f;
                var tactics = 0.5f;
                if (captain != Entity.Null && StatsLookup.HasComponent(captain))
                {
                    var stats = StatsLookup[captain];
                    command = math.saturate((float)stats.Command / 100f);
                    tactics = math.saturate((float)stats.Tactics / 100f);
                }

                var captainSkill = math.saturate(command * 0.7f + tactics * 0.3f);
                return math.saturate(cohesion * 0.55f + captainSkill * 0.45f);
            }

            private float ResolveDepartmentCohesion(Entity vesselEntity, DepartmentType department)
            {
                if (DepartmentStatsLookup.HasBuffer(vesselEntity))
                {
                    var buffer = DepartmentStatsLookup[vesselEntity];
                    for (int i = 0; i < buffer.Length; i++)
                    {
                        var stats = buffer[i].Stats;
                        if (stats.Type == department)
                        {
                            return math.saturate((float)stats.Cohesion);
                        }
                    }
                }

                return 0.5f;
            }

            private bool TryResolveCombatManeuver(
                Entity entity,
                in VesselAIState aiState,
                bool hasAttackMove,
                in AttackMoveIntent attackMove,
                in LocalTransform transform,
                in StanceTuningEntry stance,
                ref VesselTurnRateState turnRateState,
                float pilotMastery,
                float navigationCohesion,
                float commandBias,
                out float3 combatDirection,
                out float speedScale)
            {
                combatDirection = default;
                speedScale = 1f;

                if (!WeaponLookup.HasBuffer(entity))
                {
                    return false;
                }

                Entity combatTarget = Entity.Null;
                if (AimDirectiveLookup.HasComponent(entity))
                {
                    var aim = AimDirectiveLookup[entity];
                    if (aim.AimTarget != Entity.Null)
                    {
                        combatTarget = aim.AimTarget;
                    }
                }

                if (combatTarget == Entity.Null && EngagementLookup.HasComponent(entity))
                {
                    var engagement = EngagementLookup[entity];
                    if (engagement.PrimaryTarget != Entity.Null &&
                        engagement.Phase != EngagementPhase.None &&
                        engagement.Phase != EngagementPhase.Destroyed &&
                        engagement.Phase != EngagementPhase.Disabled)
                    {
                        combatTarget = engagement.PrimaryTarget;
                    }
                }

                if (combatTarget == Entity.Null)
                {
                    if (hasAttackMove && attackMove.EngageTarget != Entity.Null)
                    {
                        combatTarget = attackMove.EngageTarget;
                    }
                    else if (aiState.TargetEntity != Entity.Null)
                    {
                        combatTarget = aiState.TargetEntity;
                    }
                }

                if (combatTarget == Entity.Null && PriorityLookup.HasComponent(entity))
                {
                    var priority = PriorityLookup[entity];
                    if (priority.CurrentTarget != Entity.Null)
                    {
                        combatTarget = priority.CurrentTarget;
                    }
                }

                if (combatTarget == Entity.Null || !TargetTransformLookup.HasComponent(combatTarget))
                {
                    return false;
                }

                if (!TryResolveWeaponRangeProfile(entity, out var maxRange, out var optimalRange))
                {
                    return false;
                }

                if (hasAttackMove)
                {
                    if (attackMove.Source == AttackMoveSource.MoveWhileEngaged ||
                        attackMove.Source == AttackMoveSource.Unknown)
                    {
                        return false;
                    }

                    var destToTarget = math.distance(attackMove.Destination, TargetTransformLookup[combatTarget].Position);
                    if (destToTarget > maxRange * 1.5f && attackMove.DestinationRadius <= 0f)
                    {
                        return false;
                    }
                }

                var targetPos = TargetTransformLookup[combatTarget].Position;
                var toTarget = targetPos - transform.Position;
                var distance = math.length(toTarget);
                if (distance <= 0.01f)
                {
                    return false;
                }

                var contactRange = maxRange * 1.05f;
                var inContact = distance <= contactRange;
                if (!inContact && EngagementLookup.HasComponent(entity))
                {
                    var engagement = EngagementLookup[entity];
                    if (engagement.PrimaryTarget == combatTarget && engagement.Phase == EngagementPhase.Engaged)
                    {
                        inContact = true;
                    }
                }

                if (!inContact)
                {
                    return false;
                }

                var preferredRange = optimalRange > 0f ? optimalRange : maxRange * 0.7f;
                var rangeBias = math.lerp(1.15f, 0.85f, math.saturate(stance.AttackMoveBearingWeight));
                var desiredRange = math.max(1f, math.lerp(preferredRange, maxRange, 0.35f) * rangeBias);

                if (CarrierLookup.HasComponent(entity))
                {
                    desiredRange *= 1.1f;
                }

                var commandApproach = math.saturate(commandBias);
                desiredRange *= math.lerp(1.05f, 0.9f, commandApproach);

                var toTargetDir = toTarget / distance;
                var attackRunBias = math.saturate(pilotMastery * 0.6f + commandBias * 0.4f);
                attackRunBias = math.saturate(attackRunBias * math.lerp(0.85f, 1.1f, navigationCohesion));
                if (turnRateState.AttackRunTarget != Entity.Null && turnRateState.AttackRunTarget != combatTarget)
                {
                    turnRateState.AttackRunCommitUntilTick = 0;
                    turnRateState.AttackRunCooldownUntilTick = 0;
                    turnRateState.AttackRunTarget = Entity.Null;
                }
                if (turnRateState.AttackRunCommitUntilTick != 0)
                {
                    if (turnRateState.AttackRunTarget != combatTarget || CurrentTick >= turnRateState.AttackRunCommitUntilTick)
                    {
                        turnRateState.AttackRunCommitUntilTick = 0;
                    }
                }

                if (turnRateState.AttackRunCommitUntilTick != 0 && CurrentTick < turnRateState.AttackRunCommitUntilTick)
                {
                    if (turnRateState.AttackRunTarget == combatTarget && distance <= contactRange * 2.2f)
                    {
                        combatDirection = math.normalizesafe(turnRateState.AttackRunDirection, toTargetDir);
                        speedScale = ResolveAttackRunSpeedScale(attackRunBias, navigationCohesion);
                        return true;
                    }

                    turnRateState.AttackRunCommitUntilTick = 0;
                }

                if (CurrentTick >= turnRateState.AttackRunCooldownUntilTick)
                {
                    var minBias = math.saturate(MovementTuning.AttackRunMinBias);
                    if (attackRunBias >= minBias)
                    {
                        var startRangeScale = math.max(0.1f, MovementTuning.AttackRunStartRangeScale);
                        var minRange = desiredRange * 0.35f;
                        var startRange = desiredRange * startRangeScale;
                        if (distance <= startRange && distance >= minRange)
                        {
                            var dt = math.max(1e-4f, DeltaTime);
                            var commitSeconds = math.max(0.05f, MovementTuning.AttackRunCommitSeconds);
                            var cooldownSeconds = math.max(commitSeconds, MovementTuning.AttackRunCooldownSeconds);
                            var commitTicks = (uint)math.max(1f, math.round(commitSeconds / dt));
                            var cooldownTicks = (uint)math.max(commitTicks, math.round(cooldownSeconds / dt));

                            turnRateState.AttackRunDirection = toTargetDir;
                            turnRateState.AttackRunCommitUntilTick = CurrentTick + commitTicks;
                            turnRateState.AttackRunCooldownUntilTick = CurrentTick + cooldownTicks;
                            turnRateState.AttackRunTarget = combatTarget;

                            combatDirection = toTargetDir;
                            speedScale = ResolveAttackRunSpeedScale(attackRunBias, navigationCohesion);
                            return true;
                        }
                    }
                }
                var rangeError = distance - desiredRange;
                var radialDir = rangeError >= 0f ? toTargetDir : -toTargetDir;

                var orbitDir = math.normalizesafe(math.cross(math.up(), toTargetDir), new float3(1f, 0f, 0f));
                if (math.lengthsq(orbitDir) < 1e-4f)
                {
                    orbitDir = math.normalizesafe(math.cross(toTargetDir, new float3(0f, 0f, 1f)), new float3(1f, 0f, 0f));
                }

                orbitDir *= ResolveOrbitSign(entity, combatTarget);

                var orbitStrength = math.lerp(0.25f, 0.65f, pilotMastery);
                orbitStrength *= math.lerp(0.9f, 1.1f, navigationCohesion);
                orbitStrength *= math.lerp(1.05f, 0.75f, commandApproach);
                if (CarrierLookup.HasComponent(entity))
                {
                    orbitStrength = math.max(orbitStrength, 0.5f);
                }

                var rangeAbs = math.abs(rangeError);
                var rangeBlend = math.saturate(1f - rangeAbs / math.max(desiredRange, 1f));
                var deadbandScale = math.max(0f, MovementTuning.CombatOrbitDeadbandScale);
                var deadband = math.max(0.1f, desiredRange * deadbandScale);
                var radialWeight = math.saturate((rangeAbs - deadband) / math.max(1e-4f, desiredRange));
                var orbitBlend = rangeBlend * orbitStrength;
                orbitBlend *= (1f - radialWeight);
                combatDirection = math.normalizesafe(math.lerp(radialDir, orbitDir, orbitBlend), radialDir);

                var rangeT = math.saturate(rangeError / math.max(desiredRange, 1f));
                var closeT = math.saturate(-rangeError / math.max(desiredRange, 1f));
                speedScale = math.lerp(0.45f, 1f, rangeT);
                speedScale *= math.lerp(1f, 0.6f, closeT);
                speedScale = math.clamp(speedScale, 0.25f, 1.1f);
                return true;
            }

            private float ResolveAttackRunSpeedScale(float bias, float navigationCohesion)
            {
                var minScale = math.max(0.05f, MovementTuning.AttackRunSpeedMinScale);
                var maxScale = math.max(minScale, MovementTuning.AttackRunSpeedMaxScale);
                var scale = math.lerp(minScale, maxScale, math.saturate(bias));
                scale *= math.lerp(0.95f, 1.05f, math.saturate(navigationCohesion));
                return math.clamp(scale, 0.2f, 1.25f);
            }

            private float UpdateCombatBlend(bool combatIntent, float pilotMastery, float navigationCohesion, ref VesselTurnRateState turnRateState)
            {
                var target = combatIntent ? 1f : 0f;
                var minSeconds = math.max(0.05f, MovementTuning.TransitionMinSeconds);
                var maxSeconds = math.max(minSeconds, MovementTuning.TransitionMaxSeconds);
                var responsiveness = math.saturate(pilotMastery * 0.6f + navigationCohesion * 0.4f);
                var transitionSec = math.lerp(maxSeconds, minSeconds, responsiveness);
                var step = transitionSec <= 1e-4f ? 1f : math.saturate(DeltaTime / transitionSec);

                turnRateState.CombatBlend = math.lerp(turnRateState.CombatBlend, target, step);
                if (turnRateState.CombatBlend < 0.001f && target == 0f)
                {
                    turnRateState.CombatBlend = 0f;
                }
                else if (turnRateState.CombatBlend > 0.999f && target == 1f)
                {
                    turnRateState.CombatBlend = 1f;
                }

                return math.saturate(turnRateState.CombatBlend);
            }

            private bool TryResolveWeaponRangeProfile(Entity entity, out float maxRange, out float optimalRange)
            {
                maxRange = 0f;
                optimalRange = 0f;

                if (!WeaponLookup.HasBuffer(entity))
                {
                    return false;
                }

                var weapons = WeaponLookup[entity];
                var hasSubsystems = SubsystemLookup.HasBuffer(entity);
                var hasDisabled = SubsystemDisabledLookup.HasBuffer(entity);
                DynamicBuffer<SubsystemHealth> subsystems = default;
                DynamicBuffer<SubsystemDisabled> disabled = default;
                var weaponsDisabled = false;

                if (hasSubsystems)
                {
                    subsystems = SubsystemLookup[entity];
                    if (hasDisabled)
                    {
                        disabled = SubsystemDisabledLookup[entity];
                        weaponsDisabled = Space4XSubsystemUtility.IsSubsystemDisabled(subsystems, disabled, SubsystemType.Weapons);
                    }
                    else
                    {
                        weaponsDisabled = Space4XSubsystemUtility.IsSubsystemDisabled(subsystems, SubsystemType.Weapons);
                    }
                }

                var weightSum = 0f;
                for (int i = 0; i < weapons.Length; i++)
                {
                    var mount = weapons[i];
                    if (mount.IsEnabled == 0)
                    {
                        continue;
                    }

                    if (weaponsDisabled)
                    {
                        if (hasDisabled)
                        {
                            if (Space4XSubsystemUtility.IsWeaponMountDisabled(entity, i, subsystems, disabled))
                            {
                                continue;
                            }
                        }
                        else if (Space4XSubsystemUtility.ShouldDisableMount(entity, i))
                        {
                            continue;
                        }
                    }

                    if (mount.Weapon.MaxRange <= 0f)
                    {
                        continue;
                    }

                    var weight = ResolveWeaponWeight(mount.Weapon);
                    var optimal = mount.Weapon.OptimalRange > 0f ? mount.Weapon.OptimalRange : mount.Weapon.MaxRange * 0.7f;

                    maxRange = math.max(maxRange, mount.Weapon.MaxRange);
                    optimalRange += optimal * weight;
                    weightSum += weight;
                }

                if (weightSum > 0f)
                {
                    optimalRange /= weightSum;
                }

                return maxRange > 0f;
            }

            private static float ResolveWeaponWeight(in Space4XWeapon weapon)
            {
                var sizeWeight = 1f + 0.35f * (int)weapon.Size;
                var damageWeight = math.max(0f, weapon.BaseDamage) * 0.02f;
                return math.max(0.5f, sizeWeight + damageWeight);
            }

            private static float ResolveOrbitSign(Entity entity, Entity target)
            {
                var seed = math.hash(new uint2((uint)entity.Index, (uint)target.Index));
                return (seed & 1u) == 0 ? -1f : 1f;
            }

            private static float3 StabilizeDirection(
                float3 desiredDirection,
                float3 fallbackDirection,
                ref VesselTurnRateState turnRateState,
                float discipline,
                float intelligence,
                float chaotic,
                float currentSpeed,
                float baseSpeed,
                float deltaTime,
                bool forceStop,
                float stabilityBias,
                float responseMultiplier,
                float jitter)
            {
                if (forceStop)
                {
                    turnRateState.SmoothedDirection = desiredDirection;
                    return desiredDirection;
                }

                if (math.lengthsq(turnRateState.SmoothedDirection) < 1e-4f)
                {
                    turnRateState.SmoothedDirection = math.normalizesafe(desiredDirection, fallbackDirection);
                }

                var stability = math.saturate(discipline * 0.65f + intelligence * 0.35f);
                stability = math.saturate(math.lerp(stability, 1f - chaotic, 0.35f));
                stability = math.saturate(stability * math.max(0.1f, stabilityBias));
                var deadband = math.lerp(0.015f, 0.07f, stability);
                var speedFactor = math.saturate(currentSpeed / math.max(0.1f, baseSpeed));
                var responsiveness = math.lerp(1.35f, 0.55f, stability);
                responsiveness *= math.lerp(1.15f, 0.7f, speedFactor);
                responsiveness *= math.max(0.1f, responseMultiplier);

                var jitterScale = math.saturate(jitter);
                if (jitterScale > 0f)
                {
                    deadband = math.lerp(deadband, deadband * 1.35f, jitterScale);
                    responsiveness = math.lerp(responsiveness, responsiveness * 0.8f, jitterScale);
                }

                var smoothed = turnRateState.SmoothedDirection;
                var dot = math.clamp(math.dot(smoothed, desiredDirection), -1f, 1f);
                var angle = math.acos(dot);
                if (angle < deadband && speedFactor > 0.15f)
                {
                    return smoothed;
                }

                var t = math.saturate(deltaTime * responsiveness);
                smoothed = math.normalizesafe(math.lerp(smoothed, desiredDirection, t), desiredDirection);
                turnRateState.SmoothedDirection = smoothed;
                return smoothed;
            }

            private static float3 ApplyHeadingHold(
                float3 desiredDirection,
                float3 fallbackDirection,
                ref VesselTurnRateState turnRateState,
                uint currentTick,
                uint lastIntentTick,
                uint lastPlanTick,
                float currentSpeed,
                float baseSpeed,
                bool combatManeuver,
                float discipline,
                float intelligence,
                float stabilityBias,
                float responseMultiplier)
            {
                if (math.lengthsq(desiredDirection) < 1e-6f)
                {
                    return desiredDirection;
                }

                var desired = math.normalizesafe(desiredDirection, fallbackDirection);
                if (math.lengthsq(turnRateState.LastDesiredDirection) < 1e-4f)
                {
                    turnRateState.LastDesiredDirection = desired;
                    turnRateState.LastDesiredTick = currentTick;
                    return desired;
                }

                var speedFactor = math.saturate(currentSpeed / math.max(0.1f, baseSpeed));
                var stability = math.saturate(discipline * 0.6f + intelligence * 0.4f);
                stability = math.saturate(stability * math.max(0.1f, stabilityBias));
                var holdTicks = (uint)math.clamp(math.round(math.lerp(4f, 10f, stability)), 3f, 12f);
                if (combatManeuver)
                {
                    holdTicks = (uint)math.clamp(holdTicks - 1u, 2u, 10u);
                }

                var recentIntent = currentTick > lastIntentTick && currentTick - lastIntentTick <= 2u;
                var recentPlan = currentTick > lastPlanTick && currentTick - lastPlanTick <= 2u;
                if (!recentIntent && !recentPlan && speedFactor > 0.2f)
                {
                    var dot = math.clamp(math.dot(turnRateState.LastDesiredDirection, desired), -1f, 1f);
                    var angle = math.acos(dot);
                    var angleThreshold = math.lerp(0.55f, 0.9f, math.saturate(responseMultiplier));
                    if (angle > angleThreshold)
                    {
                        if (turnRateState.HeadingHoldUntilTick == 0 || currentTick >= turnRateState.HeadingHoldUntilTick)
                        {
                            turnRateState.HeadingHoldUntilTick = currentTick + holdTicks;
                        }

                        if (currentTick < turnRateState.HeadingHoldUntilTick)
                        {
                            return turnRateState.LastDesiredDirection;
                        }
                    }
                    else if (turnRateState.HeadingHoldUntilTick != 0 && currentTick >= turnRateState.HeadingHoldUntilTick)
                    {
                        turnRateState.HeadingHoldUntilTick = 0;
                    }
                }

                turnRateState.LastDesiredDirection = desired;
                turnRateState.LastDesiredTick = currentTick;
                return desired;
            }

            private float3 AvoidThreats(float3 desiredDirection, float3 position, float avoidanceRadius, float avoidanceStrength)
            {
                float avoidanceRadiusSq = avoidanceRadius * avoidanceRadius;
                float3 avoidanceVector = float3.zero;

                // Threat avoidance disabled here to keep job free of SystemAPI queries.

                // Combine desired direction with avoidance
                if (math.lengthsq(avoidanceVector) > 0.001f)
                {
                    var combinedDirection = math.normalize(desiredDirection + avoidanceVector);
                    return combinedDirection;
                }

                return desiredDirection;
            }

            private float GetMobilityQuality(Entity vesselEntity)
            {
                if (!QualityLookup.HasComponent(vesselEntity))
                {
                    return 0.5f;
                }

                var quality = QualityLookup[vesselEntity];
                var average = (quality.HullQuality + quality.SystemsQuality + quality.MobilityQuality + quality.IntegrationQuality) * 0.25f;
                return math.saturate(average);
            }

            private float GetModuleSpeedMultiplier(Entity vesselEntity)
            {
                if (!ModuleAggregateLookup.HasComponent(vesselEntity))
                {
                    return 1f;
                }

                var aggregate = ModuleAggregateLookup[vesselEntity];
                return math.max(0.1f, aggregate.SpeedMultiplier);
            }

            private Entity ResolveProfileEntity(Entity vesselEntity)
            {
                if (TryResolveController(vesselEntity, AgencyDomain.Movement, out var controller))
                {
                    return controller != Entity.Null ? controller : vesselEntity;
                }

                if (PilotLookup.HasComponent(vesselEntity))
                {
                    var pilot = PilotLookup[vesselEntity].Pilot;
                    if (pilot != Entity.Null)
                    {
                        return pilot;
                    }
                }

                var navigationOfficer = ResolveSeatOccupant(vesselEntity, RoleNavigationOfficer);
                if (navigationOfficer != Entity.Null)
                {
                    return navigationOfficer;
                }

                var shipmaster = ResolveSeatOccupant(vesselEntity, RoleShipmaster);
                if (shipmaster != Entity.Null)
                {
                    return shipmaster;
                }

                var captain = ResolveSeatOccupant(vesselEntity, RoleCaptain);
                if (captain != Entity.Null)
                {
                    return captain;
                }

                return vesselEntity;
            }

            private bool TryResolveController(Entity vesselEntity, AgencyDomain domain, out Entity controller)
            {
                controller = Entity.Null;
                if (!ResolvedControlLookup.HasBuffer(vesselEntity))
                {
                    return false;
                }

                var resolved = ResolvedControlLookup[vesselEntity];
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

            private Entity ResolveSeatOccupant(Entity vesselEntity, FixedString64Bytes roleId)
            {
                if (!SeatRefLookup.HasBuffer(vesselEntity))
                {
                    return Entity.Null;
                }

                var seats = SeatRefLookup[vesselEntity];
                for (int i = 0; i < seats.Length; i++)
                {
                    var seatEntity = seats[i].SeatEntity;
                    if (seatEntity == Entity.Null || !SeatLookup.HasComponent(seatEntity))
                    {
                        continue;
                    }

                    var seat = SeatLookup[seatEntity];
                    if (!seat.RoleId.Equals(roleId))
                    {
                        continue;
                    }

                    if (SeatOccupantLookup.HasComponent(seatEntity))
                    {
                        return SeatOccupantLookup[seatEntity].OccupantEntity;
                    }

                    return Entity.Null;
                }

                return Entity.Null;
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
                    switch (entry.StanceId)
                    {
                        case StanceId.Loyalist:
                            discipline += 0.2f * weight;
                            break;
                        case StanceId.Fanatic:
                            discipline += 0.25f * weight;
                            break;
                        case StanceId.Opportunist:
                            discipline -= 0.15f * weight;
                            break;
                        case StanceId.Mutinous:
                            discipline -= 0.3f * weight;
                            break;
                    }
                }

                return math.saturate(discipline);
            }

            private float3 ApplyDeviation(float3 direction, Entity vesselEntity, Entity targetEntity, float strength)
            {
                uint targetHash = targetEntity != Entity.Null
                    ? (uint)targetEntity.Index
                    : 0u;
                uint seed = math.hash(new uint2((uint)vesselEntity.Index, targetHash));
                float offset = seed * (1f / uint.MaxValue);
                offset = offset * 2f - 1f;

                var lateral = math.normalize(math.cross(direction, math.up()));
                var adjusted = direction + lateral * offset * strength;
                return math.normalize(adjusted);
            }

            private MoveIntentType ResolveIntentType(Entity vesselEntity, VesselAIState aiState)
            {
                if (aiState.TargetEntity == Entity.Null)
                {
                    return MoveIntentType.None;
                }

                if (MiningStateLookup.HasComponent(vesselEntity))
                {
                    var phase = MiningStateLookup[vesselEntity].Phase;
                    return phase switch
                    {
                        MiningPhase.Docking => MoveIntentType.Dock,
                        MiningPhase.Latching => MoveIntentType.Latch,
                        MiningPhase.Mining => MoveIntentType.Hold,
                        _ => MoveIntentType.MoveTo
                    };
                }

                return MoveIntentType.MoveTo;
            }

            private MovePlanMode ResolvePlanMode(Entity vesselEntity, VesselAIState aiState, float distance, float arrivalDistance, bool isAsteroidTarget)
            {
                if (distance <= arrivalDistance)
                {
                    return MovePlanMode.Arrive;
                }

                if (MiningStateLookup.HasComponent(vesselEntity))
                {
                    var phase = MiningStateLookup[vesselEntity].Phase;
                    return phase switch
                    {
                        MiningPhase.Latching => MovePlanMode.Latch,
                        MiningPhase.Docking => MovePlanMode.Dock,
                        _ => MovePlanMode.Approach
                    };
                }

                if (isAsteroidTarget)
                {
                    return MovePlanMode.Orbit;
                }

                return MovePlanMode.Approach;
            }

            private void UpdateMoveIntent(Entity entity, Entity target, float3 targetPosition, MoveIntentType intentType, ref MovementDebugState debugState, bool hasDebug)
            {
                if (!MoveIntentLookup.HasComponent(entity))
                {
                    return;
                }

                var intent = MoveIntentLookup[entity];
                var targetChanged = intent.TargetEntity != target;
                var intentChanged = intent.IntentType != intentType;
                if (intentChanged && !targetChanged && hasDebug)
                {
                    const uint intentCommitTicks = 6u;
                    if (debugState.LastIntentChangeTick != 0u &&
                        CurrentTick - debugState.LastIntentChangeTick < intentCommitTicks)
                    {
                        intent.TargetPosition = targetPosition;
                        MoveIntentLookup[entity] = intent;
                        return;
                    }
                }

                if (intentChanged || targetChanged)
                {
                    intent.IntentType = intentType;
                    intent.TargetEntity = target;
                    intent.TargetPosition = targetPosition;
                    MoveIntentLookup[entity] = intent;
                    if (hasDebug)
                    {
                        debugState.LastIntentChangeTick = CurrentTick;
                    }
                    PushTraceEvent(entity, MoveTraceEventKind.IntentChanged, target, ref debugState, hasDebug);
                }
                else
                {
                    intent.TargetPosition = targetPosition;
                    MoveIntentLookup[entity] = intent;
                }
            }

            private void UpdateMovePlan(Entity entity, MovePlanMode mode, float3 desiredVelocity, float maxAccel, float eta, ref MovementDebugState debugState, bool hasDebug)
            {
                if (!MovePlanLookup.HasComponent(entity))
                {
                    return;
                }

                var plan = MovePlanLookup[entity];
                if (plan.Mode != mode && hasDebug)
                {
                    const uint planCommitTicks = 6u;
                    if (debugState.LastPlanChangeTick != 0u &&
                        CurrentTick - debugState.LastPlanChangeTick < planCommitTicks)
                    {
                        plan.DesiredVelocity = desiredVelocity;
                        plan.MaxAccel = maxAccel;
                        plan.EstimatedTime = eta;
                        MovePlanLookup[entity] = plan;
                        return;
                    }
                }

                if (plan.Mode != mode)
                {
                    plan.Mode = mode;
                    plan.DesiredVelocity = desiredVelocity;
                    plan.MaxAccel = maxAccel;
                    plan.EstimatedTime = eta;
                    MovePlanLookup[entity] = plan;
                    if (hasDebug)
                    {
                        debugState.LastPlanChangeTick = CurrentTick;
                    }
                    PushTraceEvent(entity, MoveTraceEventKind.PlanChanged, Entity.Null, ref debugState, hasDebug);
                }
                else
                {
                    plan.DesiredVelocity = desiredVelocity;
                    plan.MaxAccel = maxAccel;
                    plan.EstimatedTime = eta;
                    MovePlanLookup[entity] = plan;
                }
            }

            private void UpdateDecisionTrace(Entity entity, DecisionReasonCode reason, Entity target, float score, Entity blocker, ref MovementDebugState debugState, bool hasDebug)
            {
                if (!DecisionTraceLookup.HasComponent(entity))
                {
                    return;
                }

                var trace = DecisionTraceLookup[entity];
                if (trace.ReasonCode != reason || trace.ChosenTarget != target || trace.BlockerEntity != blocker)
                {
                    trace.ReasonCode = reason;
                    trace.ChosenTarget = target;
                    trace.Score = score;
                    trace.BlockerEntity = blocker;
                    trace.SinceTick = CurrentTick;
                    DecisionTraceLookup[entity] = trace;
                    PushTraceEvent(entity, MoveTraceEventKind.DecisionChanged, target, ref debugState, hasDebug);
                }
                else
                {
                    trace.Score = score;
                    DecisionTraceLookup[entity] = trace;
                }
            }

            private void UpdateMovementInvariants(Entity entity, float3 position, float distanceToTarget, float currentSpeed, float baseSpeed, ref MovementDebugState debugState, bool hasDebug)
            {
                if (!hasDebug)
                {
                    return;
                }

                if (debugState.Initialized == 0)
                {
                    debugState.LastPosition = position;
                    debugState.LastDistanceToTarget = distanceToTarget;
                    debugState.LastSpeed = currentSpeed;
                    debugState.LastProgressTick = CurrentTick;
                    debugState.LastSampleTick = CurrentTick;
                    debugState.Initialized = 1;
                    MovementDebugLookup[entity] = debugState;
                    return;
                }

                var delta = math.length(position - debugState.LastPosition);
                var teleportThreshold = math.max(5f, baseSpeed * 4f * DeltaTime);
                if (delta > teleportThreshold)
                {
                    debugState.TeleportCount += 1;
                    debugState.MaxTeleportDistance = math.max(debugState.MaxTeleportDistance, delta);
                }

                var speedDelta = math.abs(currentSpeed - debugState.LastSpeed);
                debugState.MaxSpeedDelta = math.max(debugState.MaxSpeedDelta, speedDelta);

                if (distanceToTarget + 0.05f < debugState.LastDistanceToTarget)
                {
                    debugState.LastProgressTick = CurrentTick;
                }
                else if (CurrentTick - debugState.LastProgressTick > 120)
                {
                    debugState.StuckCount += 1;
                    debugState.LastProgressTick = CurrentTick;
                }

                debugState.LastPosition = position;
                debugState.LastDistanceToTarget = distanceToTarget;
                debugState.LastSpeed = currentSpeed;
                debugState.LastSampleTick = CurrentTick;
                MovementDebugLookup[entity] = debugState;
            }

            private float ResolvePowerAuthority(Entity vesselEntity)
            {
                if (!ShipPowerConsumerLookup.HasBuffer(vesselEntity))
                {
                    return 1f;
                }

                var consumers = ShipPowerConsumerLookup[vesselEntity];
                for (var i = 0; i < consumers.Length; i++)
                {
                    if (consumers[i].Type != ShipPowerConsumerType.Mobility)
                    {
                        continue;
                    }

                    var consumerEntity = consumers[i].Consumer;
                    if (consumerEntity == Entity.Null)
                    {
                        return 1f;
                    }

                    var ratio = 1f;
                    if (PowerConsumerLookup.HasComponent(consumerEntity))
                    {
                        var consumer = PowerConsumerLookup[consumerEntity];
                        var requested = consumer.RequestedDraw > 0f ? consumer.RequestedDraw : consumer.BaselineDraw;
                        if (requested > 0f)
                        {
                            ratio = math.saturate(consumer.AllocatedDraw / requested);
                        }
                    }

                    var effectiveness = 1f;
                    if (PowerEffectivenessLookup.HasComponent(consumerEntity))
                    {
                        effectiveness = math.max(0f, PowerEffectivenessLookup[consumerEntity].Value);
                    }

                    return ratio * effectiveness;
                }

                return 1f;
            }

            private void PushTraceEvent(Entity entity, MoveTraceEventKind kind, Entity target, ref MovementDebugState debugState, bool hasDebug)
            {
                if (!TraceEventLookup.HasBuffer(entity))
                {
                    return;
                }

                var buffer = TraceEventLookup[entity];
                if (buffer.Length >= MovementDebugState.TraceCapacity)
                {
                    buffer.RemoveAt(0);
                }

                buffer.Add(new MoveTraceEvent
                {
                    Kind = kind,
                    Tick = CurrentTick,
                    Target = target
                });

                if (hasDebug)
                {
                    debugState.StateFlipCount += 1;
                    MovementDebugLookup[entity] = debugState;
                }
            }

            private static bool IsFinite(float3 value)
            {
                return math.all(math.isfinite(value));
            }

            private static bool IsFinite(float4 value)
            {
                return math.all(math.isfinite(value));
            }

            private static void ApplyMobilityConstraints(
                ref float3 direction,
                in quaternion rotation,
                in VesselMobilityProfile mobility,
                out float speedMultiplier)
            {
                speedMultiplier = 1f;

                var forward = math.forward(rotation);
                var right = math.normalizesafe(math.cross(math.up(), forward), new float3(1f, 0f, 0f));
                var forwardDot = math.dot(direction, forward);
                var lateralDot = math.dot(direction, right);

                var reverseMultiplier = math.max(0f, mobility.ReverseSpeedMultiplier);
                var strafeMultiplier = math.max(0f, mobility.StrafeSpeedMultiplier);

                switch (mobility.ThrustMode)
                {
                    case VesselThrustMode.ForwardOnly:
                        if (forwardDot <= 0f)
                        {
                            direction = forward;
                            speedMultiplier = 0f;
                            return;
                        }

                        speedMultiplier = math.saturate(forwardDot);
                        direction = math.normalizesafe(forward * forwardDot, forward);
                        return;

                    case VesselThrustMode.Vectored:
                    {
                        var forwardComponent = forwardDot >= 0f
                            ? forwardDot
                            : forwardDot * reverseMultiplier;
                        var lateralComponent = lateralDot * strafeMultiplier;
                        direction = math.normalizesafe(forward * forwardComponent + right * lateralComponent, direction);
                        speedMultiplier = math.saturate(math.abs(forwardComponent) + math.abs(lateralComponent));
                        return;
                    }

                    case VesselThrustMode.Omnidirectional:
                        speedMultiplier = math.lerp(1f, strafeMultiplier, math.abs(lateralDot));
                        return;
                }
            }

            private bool IsNonBlockingHit(Entity hitEntity)
            {
                if (ColliderSpecLookup.HasComponent(hitEntity))
                {
                    var spec = ColliderSpecLookup[hitEntity];
                    if (spec.IsTrigger != 0)
                    {
                        return true;
                    }
                }

                if (SpacePhysicsBodyLookup.HasComponent(hitEntity))
                {
                    var body = SpacePhysicsBodyLookup[hitEntity];
                    if ((body.Flags & SpacePhysicsFlags.IsTrigger) != 0)
                    {
                        return true;
                    }

                    if (body.Layer == Space4XPhysicsLayer.SensorOnly ||
                        body.Layer == Space4XPhysicsLayer.DockingZone)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        private void EnsureMovementDebugSurfaces(ref SystemState state)
        {
            var moveIntentQuery = SystemAPI.QueryBuilder()
                .WithAll<VesselMovement>()
                .WithNone<MoveIntent>()
                .Build();
            if (!moveIntentQuery.IsEmptyIgnoreFilter)
            {
                state.EntityManager.AddComponent<MoveIntent>(moveIntentQuery);
            }

            var movePlanQuery = SystemAPI.QueryBuilder()
                .WithAll<VesselMovement>()
                .WithNone<MovePlan>()
                .Build();
            if (!movePlanQuery.IsEmptyIgnoreFilter)
            {
                state.EntityManager.AddComponent<MovePlan>(movePlanQuery);
            }

            var decisionTraceQuery = SystemAPI.QueryBuilder()
                .WithAll<VesselMovement>()
                .WithNone<DecisionTrace>()
                .Build();
            if (!decisionTraceQuery.IsEmptyIgnoreFilter)
            {
                state.EntityManager.AddComponent<DecisionTrace>(decisionTraceQuery);
            }

            var debugStateQuery = SystemAPI.QueryBuilder()
                .WithAll<VesselMovement>()
                .WithNone<MovementDebugState>()
                .Build();
            if (!debugStateQuery.IsEmptyIgnoreFilter)
            {
                state.EntityManager.AddComponent<MovementDebugState>(debugStateQuery);
            }

            var turnRateQuery = SystemAPI.QueryBuilder()
                .WithAll<VesselMovement>()
                .WithNone<VesselTurnRateState>()
                .Build();
            if (!turnRateQuery.IsEmptyIgnoreFilter)
            {
                state.EntityManager.AddComponent<VesselTurnRateState>(turnRateQuery);
            }

            var throttleQuery = SystemAPI.QueryBuilder()
                .WithAll<VesselMovement>()
                .WithNone<VesselThrottleState>()
                .Build();
            if (!throttleQuery.IsEmptyIgnoreFilter)
            {
                state.EntityManager.AddComponent<VesselThrottleState>(throttleQuery);
            }

            var traceBufferQuery = SystemAPI.QueryBuilder()
                .WithAll<VesselMovement>()
                .WithNone<MoveTraceEvent>()
                .Build();
            if (!traceBufferQuery.IsEmptyIgnoreFilter)
            {
                state.EntityManager.AddComponent<MoveTraceEvent>(traceBufferQuery);
            }
        }
    }
}


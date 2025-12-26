using PureDOTS.Runtime.Authority;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Combat;
using PureDOTS.Systems;
using Space4X.Runtime;
using Space4X.Registry;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityDebug = UnityEngine.Debug;

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
        private BufferLookup<OutlookEntry> _outlookLookup;
        private ComponentLookup<IndividualStats> _statsLookup;
        private ComponentLookup<VesselPilotLink> _pilotLookup;
        private BufferLookup<AuthoritySeatRef> _seatRefLookup;
        private ComponentLookup<AuthoritySeat> _seatLookup;
        private ComponentLookup<AuthoritySeatOccupant> _seatOccupantLookup;
        private ComponentLookup<Carrier> _carrierLookup;
        private ComponentLookup<MiningVessel> _miningVesselLookup;
        private ComponentLookup<MiningState> _miningStateLookup;
        private ComponentLookup<ModuleStatAggregate> _moduleAggregateLookup;
        private ComponentLookup<VesselQuality> _qualityLookup;
        private ComponentLookup<VesselMobilityProfile> _mobilityProfileLookup;
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
            _outlookLookup = state.GetBufferLookup<OutlookEntry>(true);
            _statsLookup = state.GetComponentLookup<IndividualStats>(true);
            _pilotLookup = state.GetComponentLookup<VesselPilotLink>(true);
            _seatRefLookup = state.GetBufferLookup<AuthoritySeatRef>(true);
            _seatLookup = state.GetComponentLookup<AuthoritySeat>(true);
            _seatOccupantLookup = state.GetComponentLookup<AuthoritySeatOccupant>(true);
            _carrierLookup = state.GetComponentLookup<Carrier>(true);
            _miningVesselLookup = state.GetComponentLookup<MiningVessel>(true);
            _miningStateLookup = state.GetComponentLookup<MiningState>(true);
            _moduleAggregateLookup = state.GetComponentLookup<ModuleStatAggregate>(true);
            _qualityLookup = state.GetComponentLookup<VesselQuality>(true);
            _mobilityProfileLookup = state.GetComponentLookup<VesselMobilityProfile>(true);
            _roleNavigationOfficer = new FixedString64Bytes("ship.navigation_officer");
            _roleShipmaster = new FixedString64Bytes("ship.shipmaster");
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

            _threatLookup.Update(ref state);
            _stanceLookup.Update(ref state);
            _capabilityStateLookup.Update(ref state);
            _effectivenessLookup.Update(ref state);
            _alignmentLookup.Update(ref state);
            _outlookLookup.Update(ref state);
            _statsLookup.Update(ref state);
            _pilotLookup.Update(ref state);
            _seatRefLookup.Update(ref state);
            _seatLookup.Update(ref state);
            _seatOccupantLookup.Update(ref state);
            _carrierLookup.Update(ref state);
            _miningVesselLookup.Update(ref state);
            _miningStateLookup.Update(ref state);
            _moduleAggregateLookup.Update(ref state);
            _qualityLookup.Update(ref state);
            _mobilityProfileLookup.Update(ref state);

            var motionConfig = VesselMotionProfileConfig.Default;
            if (SystemAPI.TryGetSingleton<VesselMotionProfileConfig>(out var motionConfigSingleton))
            {
                motionConfig = motionConfigSingleton;
            }

            var job = new UpdateVesselMovementJob
            {
                DeltaTime = deltaTime,
                CurrentTick = currentTick,
                ArrivalDistance = 2f, // Vessels stop 2 units away from target
                BaseRotationSpeed = 2f, // Base rotate speed in radians per second
                MotionConfig = motionConfig,
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
                SeatRefLookup = _seatRefLookup,
                SeatLookup = _seatLookup,
                SeatOccupantLookup = _seatOccupantLookup,
                CarrierLookup = _carrierLookup,
                MiningVesselLookup = _miningVesselLookup,
                MiningStateLookup = _miningStateLookup,
                ModuleAggregateLookup = _moduleAggregateLookup,
                QualityLookup = _qualityLookup,
                MobilityProfileLookup = _mobilityProfileLookup
            };

            state.Dependency = job.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        public partial struct UpdateVesselMovementJob : IJobEntity
        {
            public float DeltaTime;
            public uint CurrentTick;
            public float ArrivalDistance;
            public float BaseRotationSpeed;
            public VesselMotionProfileConfig MotionConfig;
            public FixedString64Bytes RoleNavigationOfficer;
            public FixedString64Bytes RoleShipmaster;
            public FixedString64Bytes RoleCaptain;
            [ReadOnly] public ComponentLookup<ThreatProfile> ThreatLookup;
            [ReadOnly] public ComponentLookup<VesselStanceComponent> StanceLookup;
            [ReadOnly] public ComponentLookup<CapabilityState> CapabilityStateLookup;
            [ReadOnly] public ComponentLookup<CapabilityEffectiveness> EffectivenessLookup;
            [ReadOnly] public ComponentLookup<AlignmentTriplet> AlignmentLookup;
            [ReadOnly] public BufferLookup<OutlookEntry> OutlookLookup;
            [ReadOnly] public ComponentLookup<IndividualStats> StatsLookup;
            [ReadOnly] public ComponentLookup<VesselPilotLink> PilotLookup;
            [ReadOnly] public BufferLookup<AuthoritySeatRef> SeatRefLookup;
            [ReadOnly] public ComponentLookup<AuthoritySeat> SeatLookup;
            [ReadOnly] public ComponentLookup<AuthoritySeatOccupant> SeatOccupantLookup;
            [ReadOnly] public ComponentLookup<Carrier> CarrierLookup;
            [ReadOnly] public ComponentLookup<MiningVessel> MiningVesselLookup;
            [ReadOnly] public ComponentLookup<MiningState> MiningStateLookup;
            [ReadOnly] public ComponentLookup<ModuleStatAggregate> ModuleAggregateLookup;
            [ReadOnly] public ComponentLookup<VesselQuality> QualityLookup;
            [ReadOnly] public ComponentLookup<VesselMobilityProfile> MobilityProfileLookup;

            public void Execute(Entity entity, ref VesselMovement movement, ref LocalTransform transform, in VesselAIState aiState)
            {
                // Check Movement capability - if disabled, stop movement
                if (CapabilityStateLookup.HasComponent(entity))
                {
                    var capabilityState = CapabilityStateLookup[entity];
                    if ((capabilityState.EnabledCapabilities & CapabilityFlags.Movement) == 0)
                    {
                        movement.Velocity = float3.zero;
                        movement.IsMoving = 0;
                        return;
                    }
                }

                // Don't move if mining - stay in place to gather resources
                if (aiState.CurrentState == VesselAIState.State.Mining)
                {
                    movement.Velocity = float3.zero;
                    movement.IsMoving = 0;
                    return;
                }

                // Only check TargetEntity - TargetPosition will be resolved by targeting system
                if (aiState.TargetEntity == Entity.Null)
                {
                    movement.Velocity = float3.zero;
                    movement.IsMoving = 0;
                    return;
                }
                
                // TargetPosition should be resolved by VesselTargetingSystem (runs earlier in Space4XTransportAISystemGroup).
                var targetPosition = aiState.TargetPosition;
                var toTarget = targetPosition - transform.Position;
                var distance = math.length(toTarget);

                var arrivalDistance = movement.ArrivalDistance > 0f ? movement.ArrivalDistance : ArrivalDistance;
                var profileEntity = ResolveProfileEntity(entity);
                var alignment = AlignmentLookup.HasComponent(profileEntity)
                    ? AlignmentLookup[profileEntity]
                    : default;
                var lawfulness = AlignmentMath.Lawfulness(alignment);
                var chaos = AlignmentMath.Chaos(alignment);
                var integrity = AlignmentMath.IntegrityNormalized(alignment);
                var discipline = GetOutlookDiscipline(profileEntity);

                var command = 0.5f;
                var tactics = 0.5f;
                if (StatsLookup.HasComponent(profileEntity))
                {
                    var stats = StatsLookup[profileEntity];
                    command = math.saturate((float)stats.Command / 100f);
                    tactics = math.saturate((float)stats.Tactics / 100f);
                }

                var intelligence = math.saturate((command + tactics) * 0.5f);
                var deliberate = math.saturate(lawfulness * (0.35f + integrity * 0.65f));
                var economic = math.saturate(integrity * (0.4f + lawfulness * 0.6f));
                var chaotic = math.saturate(chaos * (1f - discipline * 0.35f));
                var risk = math.saturate(chaos * 0.6f + (1f - lawfulness) * 0.2f + (1f - discipline) * 0.2f);
                if (distance <= arrivalDistance)
                {
                    movement.Velocity = float3.zero;
                    movement.CurrentSpeed = 0f;
                    movement.IsMoving = 0;
                    // VesselGatheringSystem will transition to Mining state when close enough
                    return;
                }

                var direction = math.normalize(toTarget);
                
                // Get stance parameters (default to Balanced if no stance component)
                var stanceType = VesselStanceMode.Balanced;
                if (StanceLookup.HasComponent(entity))
                {
                    stanceType = StanceLookup[entity].CurrentStance;
                }
                
                var avoidanceRadius = StanceRouting.GetAvoidanceRadius(stanceType);
                var avoidanceStrength = StanceRouting.GetAvoidanceStrength(stanceType);
                var speedMultiplier = StanceRouting.GetSpeedMultiplier(stanceType);
                var rotationMultiplier = StanceRouting.GetRotationMultiplier(stanceType);

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

                if (CarrierLookup.HasComponent(entity))
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

                if (MobilityProfileLookup.HasComponent(entity))
                {
                    var mobilityProfile = MobilityProfileLookup[entity];
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

                if (MiningStateLookup.HasComponent(entity))
                {
                    var phase = MiningStateLookup[entity].Phase;
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
                    rotationMultiplier *= math.lerp(1f, phaseSpeedMultiplier, 0.5f);
                }

                // Apply capability effectiveness to speed (damaged engines reduce speed)
                float effectivenessMultiplier = 1f;
                if (EffectivenessLookup.HasComponent(entity))
                {
                    var effectiveness = EffectivenessLookup[entity];
                    effectivenessMultiplier = math.max(0f, effectiveness.MovementEffectiveness);
                }

                var desiredSpeed = movement.BaseSpeed * speedMultiplier * effectivenessMultiplier;
                var slowdownDistance = movement.SlowdownDistance > 0f ? movement.SlowdownDistance : arrivalDistance * 4f;
                slowdownDistance = math.max(arrivalDistance * 1.5f, slowdownDistance * slowdownMultiplier);
                if (distance < slowdownDistance)
                {
                    desiredSpeed *= math.saturate(distance / slowdownDistance);
                }

                var acceleration = movement.Acceleration > 0f ? movement.Acceleration : math.max(0.1f, movement.BaseSpeed * 2f);
                var deceleration = movement.Deceleration > 0f ? movement.Deceleration : math.max(0.1f, movement.BaseSpeed * 2.5f);
                acceleration = math.max(0.01f, acceleration * accelerationMultiplier);
                deceleration = math.max(0.01f, deceleration * decelerationMultiplier);
                if (movement.CurrentSpeed < desiredSpeed)
                {
                    movement.CurrentSpeed = math.min(desiredSpeed, movement.CurrentSpeed + acceleration * DeltaTime);
                }
                else
                {
                    movement.CurrentSpeed = math.max(desiredSpeed, movement.CurrentSpeed - deceleration * DeltaTime);
                }

                movement.Velocity = direction * movement.CurrentSpeed;
                transform.Position += movement.Velocity * DeltaTime;

                if (math.lengthsq(movement.Velocity) > 0.001f)
                {
                    movement.DesiredRotation = quaternion.LookRotationSafe(direction, math.up());
                    var turnSpeed = movement.TurnSpeed > 0f ? movement.TurnSpeed : BaseRotationSpeed;
                    transform.Rotation = math.slerp(transform.Rotation, movement.DesiredRotation, DeltaTime * turnSpeed * rotationMultiplier);
                }

                movement.IsMoving = 1;
                movement.LastMoveTick = CurrentTick;
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
        }
    }
}

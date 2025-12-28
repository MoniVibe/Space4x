using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Burst;
using PureDOTS.Runtime.Agency;
using PureDOTS.Runtime.Authority;
using PureDOTS.Runtime.Spatial;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Interrupts;
using PureDOTS.Systems;
using Space4X.Runtime;
using UnityEngine;
using Random = Unity.Mathematics.Random;

namespace Space4X.Registry
{
    /// <summary>
    /// Manages carrier patrol behavior, generating waypoints and waiting at them.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(TransformSystemGroup))]
    [BurstCompile]
    public partial struct CarrierPatrolSystem : ISystem
    {
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<CarrierMiningTarget> _miningTargetLookup;
        private ComponentLookup<AlignmentTriplet> _alignmentLookup;
        private BufferLookup<OutlookEntry> _outlookLookup;
        private ComponentLookup<IndividualStats> _statsLookup;
        private ComponentLookup<VesselPilotLink> _pilotLookup;
        private BufferLookup<AuthoritySeatRef> _seatRefLookup;
        private ComponentLookup<AuthoritySeat> _seatLookup;
        private ComponentLookup<AuthoritySeatOccupant> _seatOccupantLookup;
        private BufferLookup<ResolvedControl> _resolvedControlLookup;
        private ComponentLookup<WaypointPath> _waypointPathLookup;
        private BufferLookup<WaypointPathPoint> _waypointPointsLookup;
        private ComponentLookup<EntityIntent> _intentLookup;
        private FixedString64Bytes _roleNavigationOfficer;
        private FixedString64Bytes _roleShipmaster;
        private FixedString64Bytes _roleCaptain;
        private Random _random;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            _transformLookup = state.GetComponentLookup<LocalTransform>(false);
            _miningTargetLookup = state.GetComponentLookup<CarrierMiningTarget>(true);
            _alignmentLookup = state.GetComponentLookup<AlignmentTriplet>(true);
            _outlookLookup = state.GetBufferLookup<OutlookEntry>(true);
            _statsLookup = state.GetComponentLookup<IndividualStats>(true);
            _pilotLookup = state.GetComponentLookup<VesselPilotLink>(true);
            _seatRefLookup = state.GetBufferLookup<AuthoritySeatRef>(true);
            _seatLookup = state.GetComponentLookup<AuthoritySeat>(true);
            _seatOccupantLookup = state.GetComponentLookup<AuthoritySeatOccupant>(true);
            _resolvedControlLookup = state.GetBufferLookup<ResolvedControl>(true);
            _waypointPathLookup = state.GetComponentLookup<WaypointPath>(false);
            _waypointPointsLookup = state.GetBufferLookup<WaypointPathPoint>(true);
            _intentLookup = state.GetComponentLookup<EntityIntent>(true);
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
            _random = new Random(12345u);
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _transformLookup.Update(ref state);
            _miningTargetLookup.Update(ref state);
            _alignmentLookup.Update(ref state);
            _outlookLookup.Update(ref state);
            _statsLookup.Update(ref state);
            _pilotLookup.Update(ref state);
            _seatRefLookup.Update(ref state);
            _seatLookup.Update(ref state);
            _seatOccupantLookup.Update(ref state);
            _resolvedControlLookup.Update(ref state);
            _waypointPathLookup.Update(ref state);
            _waypointPointsLookup.Update(ref state);
            _intentLookup.Update(ref state);

            // Use TimeState.FixedDeltaTime for consistency with PureDOTS patterns
            var deltaTime = SystemAPI.TryGetSingleton<TimeState>(out var timeState) 
                ? timeState.FixedDeltaTime 
                : SystemAPI.Time.DeltaTime;

            var motionConfig = VesselMotionProfileConfig.Default;
            if (SystemAPI.TryGetSingleton<VesselMotionProfileConfig>(out var motionConfigSingleton))
            {
                motionConfig = motionConfigSingleton;
            }

            var carrierQuery = SystemAPI.QueryBuilder()
                .WithAll<Carrier, PatrolBehavior, MovementCommand, LocalTransform>()
                .Build();
            var carrierCount = carrierQuery.CalculateEntityCount();

            if (carrierCount == 0)
            {
                // Only log warning once per session, not every frame
                return;
            }

            foreach (var (carrier, patrol, movement, vesselMovement, transform, entity) in SystemAPI
                         .Query<RefRO<Carrier>, RefRW<PatrolBehavior>, RefRW<MovementCommand>, RefRW<VesselMovement>, RefRW<LocalTransform>>()
                         .WithEntityAccess())
            {
                if (_intentLookup.HasComponent(entity))
                {
                    var intent = _intentLookup[entity];
                    if (intent.IsValid != 0)
                    {
                        continue;
                    }
                }

                var carrierData = carrier.ValueRO;
                var position = transform.ValueRO.Position;
                var movementCmd = movement.ValueRO;
                var patrolBehavior = patrol.ValueRO;
                ComputeMotionProfile(entity, motionConfig, out var speedMultiplier, out var accelerationMultiplier,
                    out var decelerationMultiplier, out var turnMultiplier, out var slowdownMultiplier, out var deviationStrength);

                if (_miningTargetLookup.HasComponent(entity))
                {
                    var miningTarget = _miningTargetLookup[entity];
                    if (miningTarget.TargetEntity != Entity.Null)
                    {
                        var targetPos = miningTarget.TargetPosition;
                        var miningArrivalThreshold = movementCmd.ArrivalThreshold > 0f ? movementCmd.ArrivalThreshold : 1f;
                        var toTarget = targetPos - position;
                        var distanceSq = math.lengthsq(toTarget);

                        if (distanceSq > miningArrivalThreshold * miningArrivalThreshold)
                        {
                            var direction = math.normalize(toTarget);
                            if (deviationStrength > 0.001f && distanceSq > motionConfig.ChaoticDeviationMinDistance * motionConfig.ChaoticDeviationMinDistance)
                            {
                                direction = ApplyDeviation(direction, entity, targetPos, deviationStrength);
                            }

                            ApplyCarrierMotion(ref vesselMovement.ValueRW, ref transform.ValueRW, direction, math.sqrt(distanceSq), carrierData,
                                miningArrivalThreshold, deltaTime, speedMultiplier, accelerationMultiplier, decelerationMultiplier, turnMultiplier, slowdownMultiplier);
                        }

                        movement.ValueRW = new MovementCommand
                        {
                            TargetPosition = targetPos,
                            ArrivalThreshold = miningArrivalThreshold
                        };

                        patrolBehavior.CurrentWaypoint = targetPos;
                        patrolBehavior.WaitTimer = 0f;
                        patrol.ValueRW = patrolBehavior;
                        continue;
                    }
                }

                var arrivalThreshold = movementCmd.ArrivalThreshold > 0f ? movementCmd.ArrivalThreshold : 1f;
                var hasPath = _waypointPathLookup.HasComponent(entity) && _waypointPointsLookup.HasBuffer(entity);
                var path = new WaypointPath();
                DynamicBuffer<WaypointPathPoint> pathPoints = default;
                if (hasPath)
                {
                    pathPoints = _waypointPointsLookup[entity];
                    if (pathPoints.Length == 0)
                    {
                        hasPath = false;
                    }
                    else
                    {
                        path = _waypointPathLookup[entity];
                        if (path.Direction == 0)
                        {
                            path.Direction = 1;
                        }
                        if (path.CurrentIndex >= pathPoints.Length)
                        {
                            path.CurrentIndex = 0;
                        }
                    }
                }

                if (hasPath)
                {
                    var waypointInitialized = math.lengthsq(patrolBehavior.CurrentWaypoint) > 0.01f;
                    if (!waypointInitialized || math.distance(position, patrolBehavior.CurrentWaypoint) < 0.01f)
                    {
                        var currentPathPoint = pathPoints[path.CurrentIndex].Position;
                        patrolBehavior.CurrentWaypoint = currentPathPoint;
                        patrolBehavior.WaitTimer = 0f;
                        movement.ValueRW = new MovementCommand
                        {
                            TargetPosition = currentPathPoint,
                            ArrivalThreshold = arrivalThreshold
                        };
                    }

                    var distanceToWaypoint = math.distance(position, patrolBehavior.CurrentWaypoint);
                    if (distanceToWaypoint <= arrivalThreshold)
                    {
                        patrolBehavior.WaitTimer += deltaTime;
                        vesselMovement.ValueRW.CurrentSpeed = 0f;
                        vesselMovement.ValueRW.Velocity = float3.zero;
                        vesselMovement.ValueRW.IsMoving = 0;

                        if (patrolBehavior.WaitTimer >= patrolBehavior.WaitTime)
                        {
                            AdvanceWaypoint(ref path, pathPoints.Length);
                            var newWaypoint = pathPoints[path.CurrentIndex].Position;
                            patrolBehavior.CurrentWaypoint = newWaypoint;
                            patrolBehavior.WaitTimer = 0f;
                            movement.ValueRW = new MovementCommand
                            {
                                TargetPosition = newWaypoint,
                                ArrivalThreshold = arrivalThreshold
                            };
                        }
                    }
                    else
                    {
                        var toWaypoint = patrolBehavior.CurrentWaypoint - position;
                        var distanceSq = math.lengthsq(toWaypoint);
                        if (distanceSq > 0.0001f)
                        {
                            var direction = math.normalize(toWaypoint);
                            if (deviationStrength > 0.001f && distanceSq > motionConfig.ChaoticDeviationMinDistance * motionConfig.ChaoticDeviationMinDistance)
                            {
                                direction = ApplyDeviation(direction, entity, patrolBehavior.CurrentWaypoint, deviationStrength);
                            }

                            ApplyCarrierMotion(ref vesselMovement.ValueRW, ref transform.ValueRW, direction, math.sqrt(distanceSq), carrierData,
                                arrivalThreshold, deltaTime, speedMultiplier, accelerationMultiplier, decelerationMultiplier, turnMultiplier, slowdownMultiplier);

                            if (math.distance(position, movementCmd.TargetPosition) > arrivalThreshold * 2f)
                            {
                                movement.ValueRW = new MovementCommand
                                {
                                    TargetPosition = patrolBehavior.CurrentWaypoint,
                                    ArrivalThreshold = arrivalThreshold
                                };
                            }
                        }
                    }

                    _waypointPathLookup[entity] = path;
                }
                else
                {
                    var waypointInitialized = math.lengthsq(patrolBehavior.CurrentWaypoint) > 0.01f;
                    if (!waypointInitialized || math.distance(position, patrolBehavior.CurrentWaypoint) < 0.01f)
                    {
                        var angle = _random.NextFloat(0f, math.PI * 2f);
                        var radius = _random.NextFloat(0f, carrierData.PatrolRadius);
                        var offset = new float3(
                            math.cos(angle) * radius,
                            0f,
                            math.sin(angle) * radius
                        );
                        patrolBehavior.CurrentWaypoint = carrierData.PatrolCenter + offset;
                        patrolBehavior.WaitTimer = 0f;
                    }

                    var distanceToWaypoint = math.distance(position, patrolBehavior.CurrentWaypoint);
                    if (distanceToWaypoint <= arrivalThreshold)
                    {
                        patrolBehavior.WaitTimer += deltaTime;
                        vesselMovement.ValueRW.CurrentSpeed = 0f;
                        vesselMovement.ValueRW.Velocity = float3.zero;
                        vesselMovement.ValueRW.IsMoving = 0;

                        if (patrolBehavior.WaitTimer >= patrolBehavior.WaitTime)
                        {
                            var angle = _random.NextFloat(0f, math.PI * 2f);
                            var radius = _random.NextFloat(0f, carrierData.PatrolRadius);
                            var offset = new float3(
                                math.cos(angle) * radius,
                                0f,
                                math.sin(angle) * radius
                            );
                            var newWaypoint = carrierData.PatrolCenter + offset;

                            patrolBehavior.CurrentWaypoint = newWaypoint;
                            patrolBehavior.WaitTimer = 0f;

                            movement.ValueRW = new MovementCommand
                            {
                                TargetPosition = newWaypoint,
                                ArrivalThreshold = arrivalThreshold
                            };
                        }
                    }
                    else
                    {
                        var toWaypoint = patrolBehavior.CurrentWaypoint - position;
                        var distanceSq = math.lengthsq(toWaypoint);
                        if (distanceSq > 0.0001f)
                        {
                            var direction = math.normalize(toWaypoint);
                            if (deviationStrength > 0.001f && distanceSq > motionConfig.ChaoticDeviationMinDistance * motionConfig.ChaoticDeviationMinDistance)
                            {
                                direction = ApplyDeviation(direction, entity, patrolBehavior.CurrentWaypoint, deviationStrength);
                            }

                            ApplyCarrierMotion(ref vesselMovement.ValueRW, ref transform.ValueRW, direction, math.sqrt(distanceSq), carrierData,
                                arrivalThreshold, deltaTime, speedMultiplier, accelerationMultiplier, decelerationMultiplier, turnMultiplier, slowdownMultiplier);

                            if (math.distance(position, movementCmd.TargetPosition) > arrivalThreshold * 2f)
                            {
                                movement.ValueRW = new MovementCommand
                                {
                                    TargetPosition = patrolBehavior.CurrentWaypoint,
                                    ArrivalThreshold = arrivalThreshold
                                };
                            }
                        }
                    }
                }

                patrol.ValueRW = patrolBehavior;
            }
        }

        private static void AdvanceWaypoint(ref WaypointPath path, int count)
        {
            if (count <= 1)
            {
                path.CurrentIndex = 0;
                return;
            }

            switch (path.Mode)
            {
                case WaypointPathMode.Linear:
                    if (path.CurrentIndex + 1 < count)
                    {
                        path.CurrentIndex++;
                    }
                    break;
                case WaypointPathMode.PingPong:
                    sbyte direction = path.Direction == 0 ? (sbyte)1 : path.Direction;
                    var nextIndex = path.CurrentIndex + direction;
                    if (nextIndex < 0 || nextIndex >= count)
                    {
                        direction = (sbyte)(direction > 0 ? -1 : 1);
                        nextIndex = path.CurrentIndex + direction;
                    }
                    path.Direction = direction;
                    path.CurrentIndex = (byte)math.clamp(nextIndex, 0, count - 1);
                    break;
                default:
                    path.CurrentIndex = (byte)((path.CurrentIndex + 1) % count);
                    break;
            }
        }

        private static void ApplyCarrierMotion(
            ref VesselMovement movement,
            ref LocalTransform transform,
            float3 direction,
            float distance,
            in Carrier carrierData,
            float arrivalThreshold,
            float deltaTime,
            float speedMultiplier,
            float accelerationMultiplier,
            float decelerationMultiplier,
            float turnMultiplier,
            float slowdownMultiplier)
        {
            var slowdownDistance = carrierData.SlowdownDistance > 0f
                ? carrierData.SlowdownDistance
                : math.max(arrivalThreshold * 4f, 2f);
            slowdownDistance = math.max(arrivalThreshold * 1.5f, slowdownDistance * slowdownMultiplier);
            var acceleration = carrierData.Acceleration > 0f ? carrierData.Acceleration : math.max(0.1f, carrierData.Speed * 0.5f);
            var deceleration = carrierData.Deceleration > 0f ? carrierData.Deceleration : math.max(0.1f, carrierData.Speed * 0.8f);
            var turnSpeed = carrierData.TurnSpeed > 0f ? carrierData.TurnSpeed : 0.6f;
            acceleration = math.max(0.01f, acceleration * accelerationMultiplier);
            deceleration = math.max(0.01f, deceleration * decelerationMultiplier);
            turnSpeed = math.max(0.01f, turnSpeed * turnMultiplier);

            var desiredSpeed = carrierData.Speed * speedMultiplier;
            if (distance < slowdownDistance)
            {
                desiredSpeed *= math.saturate(distance / slowdownDistance);
            }

            if (movement.CurrentSpeed < desiredSpeed)
            {
                movement.CurrentSpeed = math.min(desiredSpeed, movement.CurrentSpeed + acceleration * deltaTime);
            }
            else
            {
                movement.CurrentSpeed = math.max(desiredSpeed, movement.CurrentSpeed - deceleration * deltaTime);
            }

            movement.Velocity = direction * movement.CurrentSpeed;
            movement.IsMoving = movement.CurrentSpeed > 0.01f ? (byte)1 : (byte)0;

            transform.Position += movement.Velocity * deltaTime;

            if (math.lengthsq(direction) > 0.0001f)
            {
                var desiredRotation = quaternion.LookRotationSafe(direction, math.up());
                transform.Rotation = math.slerp(transform.Rotation, desiredRotation, deltaTime * turnSpeed);
            }
        }

        private void ComputeMotionProfile(
            Entity carrierEntity,
            in VesselMotionProfileConfig config,
            out float speedMultiplier,
            out float accelerationMultiplier,
            out float decelerationMultiplier,
            out float turnMultiplier,
            out float slowdownMultiplier,
            out float deviationStrength)
        {
            var profileEntity = ResolveProfileEntity(carrierEntity);
            var alignment = _alignmentLookup.HasComponent(profileEntity)
                ? _alignmentLookup[profileEntity]
                : default;
            var lawfulness = AlignmentMath.Lawfulness(alignment);
            var chaos = AlignmentMath.Chaos(alignment);
            var integrity = AlignmentMath.IntegrityNormalized(alignment);
            var discipline = GetOutlookDiscipline(profileEntity);

            var command = 0.5f;
            var tactics = 0.5f;
            if (_statsLookup.HasComponent(profileEntity))
            {
                var stats = _statsLookup[profileEntity];
                command = math.saturate((float)stats.Command / 100f);
                tactics = math.saturate((float)stats.Tactics / 100f);
            }

            var intelligence = math.saturate((command + tactics) * 0.5f);
            var deliberate = math.saturate(lawfulness * (0.35f + integrity * 0.65f));
            var economic = math.saturate(integrity * (0.4f + lawfulness * 0.6f));
            var chaotic = math.saturate(chaos * (1f - discipline * 0.35f));

            speedMultiplier = math.lerp(1f, config.DeliberateSpeedMultiplier, deliberate);
            speedMultiplier *= math.lerp(1f, config.ChaoticSpeedMultiplier, chaotic);
            speedMultiplier *= config.CapitalShipSpeedMultiplier;

            accelerationMultiplier = math.lerp(1f, config.EconomyAccelerationMultiplier, economic);
            accelerationMultiplier *= math.lerp(1f, config.ChaoticAccelerationMultiplier, chaotic);
            accelerationMultiplier *= config.CapitalShipAccelerationMultiplier;

            decelerationMultiplier = math.lerp(1f, config.EconomyDecelerationMultiplier, economic);
            decelerationMultiplier *= math.lerp(1f, config.ChaoticDecelerationMultiplier, chaotic);
            decelerationMultiplier *= config.CapitalShipDecelerationMultiplier;

            turnMultiplier = math.lerp(1f, config.DeliberateTurnMultiplier, deliberate);
            turnMultiplier *= math.lerp(1f, config.ChaoticTurnMultiplier, chaotic);
            turnMultiplier *= math.lerp(1f, config.IntelligentTurnMultiplier, intelligence);
            turnMultiplier *= config.CapitalShipTurnMultiplier;

            slowdownMultiplier = math.lerp(1f, config.DeliberateSlowdownMultiplier, deliberate);
            slowdownMultiplier *= math.lerp(1f, config.ChaoticSlowdownMultiplier, chaotic);
            slowdownMultiplier *= math.lerp(1f, config.IntelligentSlowdownMultiplier, intelligence);

            deviationStrength = config.ChaoticDeviationStrength * chaotic;
        }

        private Entity ResolveProfileEntity(Entity carrierEntity)
        {
            if (TryResolveController(carrierEntity, AgencyDomain.Movement, out var controller))
            {
                return controller != Entity.Null ? controller : carrierEntity;
            }

            if (_pilotLookup.HasComponent(carrierEntity))
            {
                var pilot = _pilotLookup[carrierEntity].Pilot;
                if (pilot != Entity.Null)
                {
                    return pilot;
                }
            }

            var navigationOfficer = ResolveSeatOccupant(carrierEntity, _roleNavigationOfficer);
            if (navigationOfficer != Entity.Null)
            {
                return navigationOfficer;
            }

            var shipmaster = ResolveSeatOccupant(carrierEntity, _roleShipmaster);
            if (shipmaster != Entity.Null)
            {
                return shipmaster;
            }

            var captain = ResolveSeatOccupant(carrierEntity, _roleCaptain);
            if (captain != Entity.Null)
            {
                return captain;
            }

            return carrierEntity;
        }

        private bool TryResolveController(Entity carrierEntity, AgencyDomain domain, out Entity controller)
        {
            controller = Entity.Null;
            if (!_resolvedControlLookup.HasBuffer(carrierEntity))
            {
                return false;
            }

            var resolved = _resolvedControlLookup[carrierEntity];
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

        private Entity ResolveSeatOccupant(Entity carrierEntity, FixedString64Bytes roleId)
        {
            if (!_seatRefLookup.HasBuffer(carrierEntity))
            {
                return Entity.Null;
            }

            var seats = _seatRefLookup[carrierEntity];
            for (int i = 0; i < seats.Length; i++)
            {
                var seatEntity = seats[i].SeatEntity;
                if (seatEntity == Entity.Null || !_seatLookup.HasComponent(seatEntity))
                {
                    continue;
                }

                var seat = _seatLookup[seatEntity];
                if (!seat.RoleId.Equals(roleId))
                {
                    continue;
                }

                if (_seatOccupantLookup.HasComponent(seatEntity))
                {
                    return _seatOccupantLookup[seatEntity].OccupantEntity;
                }

                return Entity.Null;
            }

            return Entity.Null;
        }

        private float GetOutlookDiscipline(Entity profileEntity)
        {
            if (!_outlookLookup.HasBuffer(profileEntity))
            {
                return 0.5f;
            }

            var buffer = _outlookLookup[profileEntity];
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

        private static float3 ApplyDeviation(float3 direction, Entity carrierEntity, float3 targetPosition, float strength)
        {
            var targetHash = math.hash(math.asuint(targetPosition));
            var seed = math.hash(new uint2((uint)carrierEntity.Index, targetHash));
            float offset = seed * (1f / uint.MaxValue);
            offset = offset * 2f - 1f;

            var lateral = math.normalize(math.cross(direction, math.up()));
            return math.normalize(direction + lateral * offset * strength);
        }
    }

    /// <summary>
    /// Manages mining vessel behavior: moving to asteroids, mining, returning to carrier, and transferring resources.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(CarrierPatrolSystem))]
    [UpdateBefore(typeof(TransformSystemGroup))]
    [BurstCompile]
    public partial struct MiningVesselSystem : ISystem
    {
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<Carrier> _carrierLookup;
        private ComponentLookup<Asteroid> _asteroidLookup;
        private BufferLookup<ResourceStorage> _resourceStorageLookup;
        private EntityQuery _asteroidQuery;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            _transformLookup = state.GetComponentLookup<LocalTransform>(false);
            _carrierLookup = state.GetComponentLookup<Carrier>(true);
            _asteroidLookup = state.GetComponentLookup<Asteroid>(false);
            _resourceStorageLookup = state.GetBufferLookup<ResourceStorage>(false);

            _asteroidQuery = SystemAPI.QueryBuilder()
                .WithAll<Asteroid, LocalTransform>()
                .Build();
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (SystemAPI.HasSingleton<Space4XLegacyMiningDisabledTag>())
            {
                return;
            }

            _transformLookup.Update(ref state);
            _carrierLookup.Update(ref state);
            _asteroidLookup.Update(ref state);
            _resourceStorageLookup.Update(ref state);

            // Use TimeState.FixedDeltaTime for consistency with PureDOTS patterns
            var deltaTime = SystemAPI.TryGetSingleton<TimeState>(out var timeState) 
                ? timeState.FixedDeltaTime 
                : SystemAPI.Time.DeltaTime;

            // Collect available asteroids
            var asteroidList = new NativeList<(Entity entity, float3 position, Asteroid asteroid)>(Allocator.Temp);
            foreach (var (asteroid, transform, entity) in SystemAPI.Query<RefRO<Asteroid>, RefRO<LocalTransform>>().WithEntityAccess())
            {
                if (asteroid.ValueRO.ResourceAmount > 0f)
                {
                    asteroidList.Add((entity, transform.ValueRO.Position, asteroid.ValueRO));
                }
            }

            var vesselQuery = SystemAPI.QueryBuilder()
                .WithAll<MiningVessel, MiningJob, LocalTransform>()
                .WithNone<MiningOrder>()
                .Build();
            var vesselCount = vesselQuery.CalculateEntityCount();

            // Warnings removed - entities will be created when Space4XMiningScenarioAuthoring is properly configured
            if (vesselCount == 0 || asteroidList.Length == 0)
            {
                return;
            }

            foreach (var (vessel, job, transform, entity) in SystemAPI.Query<RefRW<MiningVessel>, RefRW<MiningJob>, RefRW<LocalTransform>>().WithNone<MiningOrder>().WithEntityAccess())
            {
                var vesselData = vessel.ValueRO;
                var jobData = job.ValueRO;
                var position = transform.ValueRO.Position;

                switch (jobData.State)
                {
                    case MiningJobState.None:
                        // Find nearest asteroid
                        Entity? nearestAsteroid = null;
                        float nearestDistance = float.MaxValue;

                        for (int i = 0; i < asteroidList.Length; i++)
                        {
                            var asteroidEntry = asteroidList[i];
                            var distance = math.distance(position, asteroidEntry.position);
                            if (distance < nearestDistance)
                            {
                                nearestDistance = distance;
                                nearestAsteroid = asteroidEntry.entity;
                            }
                        }

                        if (nearestAsteroid.HasValue)
                        {
                            job.ValueRW = new MiningJob
                            {
                                State = MiningJobState.MovingToAsteroid,
                                TargetAsteroid = nearestAsteroid.Value,
                                MiningProgress = 0f
                            };
                        }
                        break;

                    case MiningJobState.MovingToAsteroid:
                        if (!_asteroidLookup.HasComponent(jobData.TargetAsteroid))
                        {
                            job.ValueRW = new MiningJob { State = MiningJobState.None };
                            break;
                        }

                        var asteroidTransform = _transformLookup[jobData.TargetAsteroid];
                        var asteroidPosition = asteroidTransform.Position;
                        var distanceToAsteroid = math.distance(position, asteroidPosition);

                        if (distanceToAsteroid <= 2f)
                        {
                            job.ValueRW = new MiningJob
                            {
                                State = MiningJobState.Mining,
                                TargetAsteroid = jobData.TargetAsteroid,
                                MiningProgress = 0f
                            };
                        }
                        else
                        {
                            var toAsteroid = asteroidPosition - position;
                            var distanceSq = math.lengthsq(toAsteroid);
                            
                            if (distanceSq > 0.0001f) // Safety check to avoid normalizing zero vector
                            {
                                var direction = math.normalize(toAsteroid);
                                var movementSpeed = vesselData.Speed * deltaTime;
                                var newPosition = position + direction * movementSpeed;
                                transform.ValueRW = LocalTransform.FromPositionRotationScale(newPosition, transform.ValueRO.Rotation, transform.ValueRO.Scale);
                            }
                        }
                        break;

                    case MiningJobState.Mining:
                        if (!_asteroidLookup.HasComponent(jobData.TargetAsteroid))
                        {
                            job.ValueRW = new MiningJob { State = MiningJobState.None };
                            break;
                        }

                        var asteroid = _asteroidLookup[jobData.TargetAsteroid];
                        if (asteroid.ResourceAmount <= 0f || vesselData.CurrentCargo >= vesselData.CargoCapacity)
                        {
                            // Start returning to carrier
                            job.ValueRW = new MiningJob
                            {
                                State = MiningJobState.ReturningToCarrier,
                                TargetAsteroid = jobData.TargetAsteroid,
                                MiningProgress = 0f
                            };
                            break;
                        }

                        // Calculate mining rate
                        var miningRate = vesselData.MiningEfficiency * asteroid.MiningRate * deltaTime;
                        var amountToMine = math.min(miningRate, asteroid.ResourceAmount);
                        amountToMine = math.min(amountToMine, vesselData.CargoCapacity - vesselData.CurrentCargo);

                        // Update asteroid resource amount
                        var asteroidRef = _asteroidLookup.GetRefRW(jobData.TargetAsteroid);
                        asteroidRef.ValueRW.ResourceAmount -= amountToMine;

                        // Update vessel cargo
                        vessel.ValueRW.CurrentCargo += amountToMine;

                        // Update mining progress
                        job.ValueRW = new MiningJob
                        {
                            State = MiningJobState.Mining,
                            TargetAsteroid = jobData.TargetAsteroid,
                            MiningProgress = jobData.MiningProgress + miningRate
                        };

                        if (vessel.ValueRO.CurrentCargo >= vessel.ValueRO.CargoCapacity || asteroidRef.ValueRO.ResourceAmount <= 0f)
                        {
                            job.ValueRW = new MiningJob
                            {
                                State = MiningJobState.ReturningToCarrier,
                                TargetAsteroid = jobData.TargetAsteroid,
                                MiningProgress = 0f
                            };
                        }
                        break;

                    case MiningJobState.ReturningToCarrier:
                        if (!_carrierLookup.HasComponent(vesselData.CarrierEntity))
                        {
                            job.ValueRW = new MiningJob { State = MiningJobState.None };
                            break;
                        }

                        var carrierTransform = _transformLookup[vesselData.CarrierEntity];
                        var carrierPosition = carrierTransform.Position;
                        var distanceToCarrier = math.distance(position, carrierPosition);

                        if (distanceToCarrier <= 3f)
                        {
                            job.ValueRW = new MiningJob
                            {
                                State = MiningJobState.TransferringResources,
                                TargetAsteroid = jobData.TargetAsteroid,
                                MiningProgress = 0f
                            };
                        }
                        else
                        {
                            var toCarrier = carrierPosition - position;
                            var distanceSq = math.lengthsq(toCarrier);
                            
                            if (distanceSq > 0.0001f) // Safety check to avoid normalizing zero vector
                            {
                                var direction = math.normalize(toCarrier);
                                var movementSpeed = vesselData.Speed * deltaTime;
                                var newPosition = position + direction * movementSpeed;
                                transform.ValueRW = LocalTransform.FromPositionRotationScale(newPosition, transform.ValueRO.Rotation, transform.ValueRO.Scale);
                            }
                        }
                        break;

                    case MiningJobState.TransferringResources:
                        if (!_carrierLookup.HasComponent(vesselData.CarrierEntity))
                        {
                            job.ValueRW = new MiningJob { State = MiningJobState.None };
                            break;
                        }

                        if (vessel.ValueRO.CurrentCargo <= 0f)
                        {
                            // Reset and start new mining cycle
                            job.ValueRW = new MiningJob { State = MiningJobState.None };
                            vessel.ValueRW.CurrentCargo = 0f;
                            break;
                        }

                        // Determine resource type from asteroid if available
                        var resourceType = ResourceType.Minerals;
                        if (_asteroidLookup.HasComponent(jobData.TargetAsteroid))
                        {
                            resourceType = _asteroidLookup[jobData.TargetAsteroid].ResourceType;
                        }

                        // Transfer resources to carrier's ResourceStorage buffer
                        if (_resourceStorageLookup.HasBuffer(vesselData.CarrierEntity))
                        {
                            var resourceBuffer = _resourceStorageLookup[vesselData.CarrierEntity];
                            var cargoToTransfer = vessel.ValueRO.CurrentCargo;

                            // Find or create resource storage slot for this type
                            bool foundSlot = false;
                            for (int i = 0; i < resourceBuffer.Length; i++)
                            {
                                if (resourceBuffer[i].Type == resourceType)
                                {
                                    var storage = resourceBuffer[i];
                                    var remaining = storage.AddAmount(cargoToTransfer);
                                    resourceBuffer[i] = storage;

                                    // Update vessel cargo
                                    vessel.ValueRW.CurrentCargo = remaining;
                                    foundSlot = true;
                                    break;
                                }
                            }

                            if (!foundSlot && resourceBuffer.Length < 4)
                            {
                                var newStorage = ResourceStorage.Create(resourceType);
                                var remaining = newStorage.AddAmount(cargoToTransfer);
                                resourceBuffer.Add(newStorage);

                                vessel.ValueRW.CurrentCargo = remaining;
                            }

                            if (vessel.ValueRO.CurrentCargo <= 0f)
                            {
                                job.ValueRW = new MiningJob { State = MiningJobState.None };
                            }
                        }
                        break;
                }
            }

            asteroidList.Dispose();
        }
    }
}

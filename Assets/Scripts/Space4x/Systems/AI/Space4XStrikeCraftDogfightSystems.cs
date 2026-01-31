using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Steering;
using Space4X.Registry;
using Space4X.Runtime;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Systems.AI
{
    [BurstCompile]
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    public partial struct Space4XStrikeCraftGuidanceSystem : ISystem
    {
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<VesselMovement> _movementLookup;
        private ComponentLookup<HullIntegrity> _hullLookup;
        private BufferLookup<AffiliationTag> _affiliationLookup;
        private ComponentLookup<StrikeCraftProfile> _profileLookup;
        private ComponentLookup<StrikeCraftExperience> _experienceLookup;
        private ComponentLookup<PatrolStance> _patrolStanceLookup;
        private ComponentLookup<Space4XEngagement> _engagementLookup;
        private EntityQuery _candidateQuery;
        private EntityQuery _neighborQuery;

        public struct StrikeCraftTargetCandidate
        {
            public Entity Entity;
            public float3 Position;
            public float3 Velocity;
            public Entity AffiliationTarget;
        }

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<StrikeCraftDogfightTag>();

            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _movementLookup = state.GetComponentLookup<VesselMovement>(true);
            _hullLookup = state.GetComponentLookup<HullIntegrity>(true);
            _affiliationLookup = state.GetBufferLookup<AffiliationTag>(true);
            _profileLookup = state.GetComponentLookup<StrikeCraftProfile>(true);
            _experienceLookup = state.GetComponentLookup<StrikeCraftExperience>(true);
            _patrolStanceLookup = state.GetComponentLookup<PatrolStance>(true);
            _engagementLookup = state.GetComponentLookup<Space4XEngagement>(false);
            _candidateQuery = state.GetEntityQuery(
                ComponentType.ReadOnly<LocalTransform>(),
                ComponentType.ReadOnly<HullIntegrity>());
            _neighborQuery = state.GetEntityQuery(
                ComponentType.ReadOnly<LocalTransform>(),
                ComponentType.ReadOnly<StrikeCraftDogfightTag>());
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

            var config = StrikeCraftDogfightConfig.Default;
            if (SystemAPI.TryGetSingleton<StrikeCraftDogfightConfig>(out var configSingleton))
            {
                config = configSingleton;
            }
            var stanceConfig = Space4XStanceTuningConfig.Default;
            if (SystemAPI.TryGetSingleton<Space4XStanceTuningConfig>(out var stanceConfigSingleton))
            {
                stanceConfig = stanceConfigSingleton;
            }

            var targetAcquireRadius = math.max(0f, config.TargetAcquireRadius);
            var targetAcquireRadiusSq = targetAcquireRadius * targetAcquireRadius;

            var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);
            var hasChanges = false;

            using var missingSteering = SystemAPI.QueryBuilder()
                .WithAll<StrikeCraftDogfightTag>()
                .WithNone<StrikeCraftDogfightSteering>()
                .Build()
                .ToEntityArray(Allocator.Temp);
            for (int i = 0; i < missingSteering.Length; i++)
            {
                ecb.AddComponent(missingSteering[i], new StrikeCraftDogfightSteering());
                hasChanges = true;
            }

            using var missingMetrics = SystemAPI.QueryBuilder()
                .WithAll<StrikeCraftDogfightTag>()
                .WithNone<StrikeCraftDogfightMetrics>()
                .Build()
                .ToEntityArray(Allocator.Temp);
            for (int i = 0; i < missingMetrics.Length; i++)
            {
                ecb.AddComponent(missingMetrics[i], new StrikeCraftDogfightMetrics());
                hasChanges = true;
            }

            using var missingSamples = SystemAPI.QueryBuilder()
                .WithAll<StrikeCraftDogfightTag>()
                .WithNone<StrikeCraftDogfightSample>()
                .Build()
                .ToEntityArray(Allocator.Temp);
            for (int i = 0; i < missingSamples.Length; i++)
            {
                ecb.AddBuffer<StrikeCraftDogfightSample>(missingSamples[i]);
                hasChanges = true;
            }

            using var missingTurnRate = SystemAPI.QueryBuilder()
                .WithAll<StrikeCraftDogfightTag>()
                .WithNone<VesselTurnRateState>()
                .Build()
                .ToEntityArray(Allocator.Temp);
            for (int i = 0; i < missingTurnRate.Length; i++)
            {
                ecb.AddComponent(missingTurnRate[i], new VesselTurnRateState());
                hasChanges = true;
            }

            if (hasChanges)
            {
                ecb.Playback(state.EntityManager);
            }

            _transformLookup.Update(ref state);
            _movementLookup.Update(ref state);
            _hullLookup.Update(ref state);
            _affiliationLookup.Update(ref state);
            _profileLookup.Update(ref state);
            _experienceLookup.Update(ref state);
            _patrolStanceLookup.Update(ref state);
            _engagementLookup.Update(ref state);

            var candidateCapacity = math.max(8, _candidateQuery.CalculateEntityCount());
            var candidates = new NativeList<StrikeCraftTargetCandidate>(candidateCapacity, Allocator.TempJob);
            foreach (var (transform, hull, entity) in SystemAPI.Query<RefRO<LocalTransform>, RefRO<HullIntegrity>>()
                         .WithEntityAccess())
            {
                if (hull.ValueRO.Current <= 0f)
                {
                    continue;
                }

                var velocity = float3.zero;
                if (_movementLookup.HasComponent(entity))
                {
                    velocity = _movementLookup[entity].Velocity;
                }

                candidates.Add(new StrikeCraftTargetCandidate
                {
                    Entity = entity,
                    Position = transform.ValueRO.Position,
                    Velocity = velocity,
                    AffiliationTarget = ResolveAffiliationTarget(entity)
                });
            }

            var neighborCapacity = math.max(4, _neighborQuery.CalculateEntityCount());
            var neighbors = new NativeList<float3>(neighborCapacity, Allocator.TempJob);
            foreach (var (transform, entity) in SystemAPI.Query<RefRO<LocalTransform>>()
                         .WithAll<StrikeCraftDogfightTag>()
                         .WithEntityAccess())
            {
                neighbors.Add(transform.ValueRO.Position);
            }

            var job = new UpdateStrikeCraftDogfightGuidanceJob
            {
                CurrentTick = timeState.Tick,
                DeltaTime = timeState.FixedDeltaTime,
                Config = config,
                StanceConfig = stanceConfig,
                TargetAcquireRadiusSq = targetAcquireRadiusSq,
                TargetCandidates = candidates.AsArray(),
                NeighborPositions = neighbors.AsArray(),
                AffiliationLookup = _affiliationLookup,
                TransformLookup = _transformLookup,
                MovementLookup = _movementLookup,
                HullLookup = _hullLookup,
                ProfileLookup = _profileLookup,
                ExperienceLookup = _experienceLookup,
                PatrolStanceLookup = _patrolStanceLookup,
                EngagementLookup = _engagementLookup
            };

            state.Dependency = job.ScheduleParallel(state.Dependency);
            state.Dependency = candidates.Dispose(state.Dependency);
            state.Dependency = neighbors.Dispose(state.Dependency);
        }

        private Entity ResolveAffiliationTarget(Entity entity)
        {
            if (!_affiliationLookup.HasBuffer(entity))
            {
                return Entity.Null;
            }

            var affiliations = _affiliationLookup[entity];
            Entity fallback = Entity.Null;
            for (int i = 0; i < affiliations.Length; i++)
            {
                var tag = affiliations[i];
                if (tag.Target == Entity.Null)
                {
                    continue;
                }

                if (tag.Type == AffiliationType.Faction)
                {
                    return tag.Target;
                }

                if (fallback == Entity.Null && tag.Type == AffiliationType.Fleet)
                {
                    fallback = tag.Target;
                }
                else if (fallback == Entity.Null)
                {
                    fallback = tag.Target;
                }
            }

            if (fallback == Entity.Null || !_affiliationLookup.HasBuffer(fallback))
            {
                return fallback;
            }

            var nested = _affiliationLookup[fallback];
            for (int i = 0; i < nested.Length; i++)
            {
                var tag = nested[i];
                if (tag.Target == Entity.Null)
                {
                    continue;
                }

                if (tag.Type == AffiliationType.Faction)
                {
                    return tag.Target;
                }

                if (tag.Type == AffiliationType.Fleet)
                {
                    return tag.Target;
                }
            }

            return fallback;
        }

        [BurstCompile]
        [WithAll(typeof(StrikeCraftDogfightTag))]
        public partial struct UpdateStrikeCraftDogfightGuidanceJob : IJobEntity
        {
            public uint CurrentTick;
            public float DeltaTime;
            public StrikeCraftDogfightConfig Config;
            public Space4XStanceTuningConfig StanceConfig;
            public float TargetAcquireRadiusSq;
            [ReadOnly] public NativeArray<StrikeCraftTargetCandidate> TargetCandidates;
            [ReadOnly] public NativeArray<float3> NeighborPositions;
            [ReadOnly] public BufferLookup<AffiliationTag> AffiliationLookup;
            [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;
            [ReadOnly] public ComponentLookup<VesselMovement> MovementLookup;
            [ReadOnly] public ComponentLookup<HullIntegrity> HullLookup;
            [ReadOnly] public ComponentLookup<StrikeCraftProfile> ProfileLookup;
            [ReadOnly] public ComponentLookup<StrikeCraftExperience> ExperienceLookup;
            [ReadOnly] public ComponentLookup<PatrolStance> PatrolStanceLookup;
            [NativeDisableParallelForRestriction] public ComponentLookup<Space4XEngagement> EngagementLookup;

            public void Execute(
                Entity entity,
                ref StrikeCraftState state,
                ref StrikeCraftDogfightSteering steering,
                ref StrikeCraftDogfightMetrics metrics,
                in LocalTransform transform,
                in VesselMovement movement,
                DynamicBuffer<WeaponMount> weapons,
                DynamicBuffer<SubsystemHealth> subsystems,
                DynamicBuffer<SubsystemDisabled> disabledSubsystems)
            {
                if (state.DogfightPhase == StrikeCraftDogfightPhase.None)
                {
                    state.DogfightPhase = StrikeCraftDogfightPhase.Approach;
                    state.DogfightPhaseStartTick = CurrentTick;
                    metrics.LastPhaseTick = CurrentTick;
                }

                var craftAffiliation = ResolveAffiliationTarget(entity, AffiliationLookup);
                var position = transform.Position;
                var velocity = movement.Velocity;
                var previousTarget = state.TargetEntity;
                if (!TryResolveTarget(entity, craftAffiliation, position, TargetAcquireRadiusSq, TargetCandidates, ref state,
                        out var targetPosition, out var targetVelocity))
                {
                    steering.Output = default;
                    UpdateEngagement(entity, Entity.Null, 0f, StrikeCraftDogfightPhase.None);
                    state.CurrentState = StrikeCraftState.State.Docked;
                    return;
                }

                if (state.TargetEntity != previousTarget)
                {
                    metrics.EngagementStartTick = CurrentTick;
                    metrics.FirstFireTick = 0;
                    metrics.LastFireTick = 0;
                    metrics.LastKillTick = 0;
                }

                state.TargetPosition = targetPosition;

                var stance = ResolveStance(entity);
                var tuning = StanceConfig.Resolve(stance);

                var experience01 = math.saturate(state.Experience);
                var traits = StrikeCraftTraits.None;
                if (ExperienceLookup.HasComponent(entity))
                {
                    var experience = ExperienceLookup[entity];
                    traits = experience.Traits;
                    experience01 = math.max(experience01, math.saturate(experience.Level / 5f));
                }

                var reaction = math.lerp(0.55f, 0.95f, experience01);
                var speedScale = math.lerp(1.1f, 0.95f, experience01);
                var jinkScale = math.lerp(1.25f, 0.8f, experience01);
                var separationScale = math.lerp(0.6f, 1.1f, experience01);
                var fireConeScale = math.lerp(1.25f, 0.8f, experience01);
                var breakOffScale = math.lerp(1.2f, 0.9f, experience01);

                if ((traits & StrikeCraftTraits.QuickReaction) != 0)
                {
                    reaction = math.min(1f, reaction + 0.1f);
                }
                if ((traits & StrikeCraftTraits.EvasiveManeuvers) != 0)
                {
                    jinkScale *= 1.15f;
                }
                if ((traits & StrikeCraftTraits.FormationDiscipline) != 0)
                {
                    separationScale *= 1.2f;
                }
                if ((traits & StrikeCraftTraits.PrecisionStrike) != 0)
                {
                    fireConeScale *= 0.9f;
                }
                if ((traits & StrikeCraftTraits.AceStatus) != 0)
                {
                    reaction = math.min(1f, reaction + 0.12f);
                    jinkScale *= 0.9f;
                    separationScale *= 1.1f;
                    fireConeScale *= 0.85f;
                }

                jinkScale = math.clamp(jinkScale, 0.5f, 1.6f);
                separationScale = math.clamp(separationScale, 0.5f, 1.5f);

                var fireConeDegrees = math.max(5f, Config.FireConeDegrees * fireConeScale);
                var fireConeCos = math.cos(math.radians(fireConeDegrees));
                var breakOffDistance = math.max(5f, Config.BreakOffDistance * breakOffScale);
                var breakOffTicks = Config.BreakOffTicks > 0
                    ? (uint)math.max(10f, math.round((float)Config.BreakOffTicks * breakOffScale))
                    : 0u;

                var maxLateralAccel = math.max(0.1f, Config.MaxLateralAccel) * math.lerp(0.75f, 1.05f, experience01);
                var jinkStrength = math.max(0f, Config.JinkStrength + tuning.EvasionJinkStrength) * jinkScale;

                var forward = ResolveForward(transform, movement);
                var toTarget = targetPosition - position;
                var distance = math.length(toTarget);
                var directionToTarget = distance > 1e-4f ? toTarget / distance : forward;
                var coneDot = math.dot(forward, directionToTarget);
                var inCone = coneDot >= fireConeCos;
                var maxRange = ResolveMaxWeaponRange(entity, weapons, subsystems, disabledSubsystems);
                var inRange = maxRange > 0f && distance <= maxRange;

                var relativeVelocity = targetVelocity - velocity;
                var closingSpeed = -math.dot(relativeVelocity, directionToTarget);
                var omega = ComputeOmega(toTarget, relativeVelocity);

                if (state.TargetEntity != Entity.Null && HullLookup.HasComponent(state.TargetEntity))
                {
                    var hull = HullLookup[state.TargetEntity];
                    if (hull.Current <= 0f && metrics.LastKillTick == 0)
                    {
                        metrics.LastKillTick = CurrentTick;
                    }
                }

                if (tuning.AbortAttackOnDamageThreshold > 0f && HullLookup.HasComponent(entity))
                {
                    var hull = HullLookup[entity];
                    if (hull.Max > 0f)
                    {
                        var ratio = hull.Current / hull.Max;
                        if (ratio <= tuning.AbortAttackOnDamageThreshold &&
                            state.DogfightPhase != StrikeCraftDogfightPhase.BreakOff)
                        {
                            SetPhase(ref state, ref metrics, StrikeCraftDogfightPhase.BreakOff, CurrentTick);
                        }
                    }
                }

                var context = new SteeringContext
                {
                    Position = position,
                    Velocity = velocity,
                    Forward = forward,
                    MaxSpeed = movement.BaseSpeed * Config.ApproachMaxSpeedMultiplier * speedScale,
                    MaxAccel = maxLateralAccel,
                    DeltaTime = DeltaTime
                };

                SteeringPrimitives.Separation(position, NeighborPositions, Config.SeparationRadius, Config.SeparationStrength * separationScale,
                    out var separation);
                var jink = ComputeJink(entity, forward, jinkStrength, CurrentTick);
                var desiredForward = directionToTarget;
                var desiredAccel = float3.zero;

                switch (state.DogfightPhase)
                {
                    case StrikeCraftDogfightPhase.Approach:
                    {
                        var interceptPoint = targetPosition;
                        if (SteeringPrimitives.LeadInterceptPoint(targetPosition, targetVelocity, position,
                            math.max(1f, context.MaxSpeed), out var computedPoint, out _))
                        {
                            interceptPoint = computedPoint;
                        }

                        desiredForward = math.normalizesafe(interceptPoint - position, directionToTarget);
                        SteeringPrimitives.Seek(ref context, in interceptPoint, out var seekAccel);
                        SteeringPrimitives.PN_Accel(in position, in velocity, in targetPosition, in targetVelocity, Config.NavConstantN,
                            out var pnAccel);
                        desiredAccel = seekAccel + pnAccel + separation + jink;
                        desiredAccel = LimitAccel(desiredAccel, maxLateralAccel);

                        if (inCone && inRange)
                        {
                            SetPhase(ref state, ref metrics, StrikeCraftDogfightPhase.FireWindow, CurrentTick);
                        }

                        state.CurrentState = StrikeCraftState.State.Approaching;
                        UpdateEngagement(entity, state.TargetEntity, distance, StrikeCraftDogfightPhase.Approach);
                        break;
                    }
                    case StrikeCraftDogfightPhase.FireWindow:
                    {
                        desiredForward = directionToTarget;
                        desiredAccel = separation + jink;
                        desiredAccel = LimitAccel(desiredAccel, maxLateralAccel * 0.5f);

                        if (!inCone || !inRange)
                        {
                            SetPhase(ref state, ref metrics, StrikeCraftDogfightPhase.Approach, CurrentTick);
                        }
                        else if (distance <= breakOffDistance)
                        {
                            SetPhase(ref state, ref metrics, StrikeCraftDogfightPhase.BreakOff, CurrentTick);
                        }

                        state.CurrentState = StrikeCraftState.State.Engaging;
                        UpdateEngagement(entity, state.TargetEntity, distance, StrikeCraftDogfightPhase.FireWindow);
                        break;
                    }
                    case StrikeCraftDogfightPhase.BreakOff:
                    {
                        SteeringPrimitives.Evade(ref context, in targetPosition, in targetVelocity, out var evadeAccel);
                        var lateral = math.cross(directionToTarget, math.up());
                        if (math.lengthsq(lateral) < 1e-4f)
                        {
                            lateral = math.cross(directionToTarget, new float3(1f, 0f, 0f));
                        }

                        lateral = math.normalizesafe(lateral) * jinkStrength;
                        desiredAccel = evadeAccel + lateral + separation;
                        desiredAccel = LimitAccel(desiredAccel, maxLateralAccel);

                        if (CurrentTick - state.DogfightPhaseStartTick >= breakOffTicks ||
                            distance > breakOffDistance * 2f)
                        {
                            SetPhase(ref state, ref metrics, StrikeCraftDogfightPhase.Rejoin, CurrentTick);
                        }

                        state.CurrentState = StrikeCraftState.State.Disengaging;
                        UpdateEngagement(entity, state.TargetEntity, distance, StrikeCraftDogfightPhase.BreakOff);
                        break;
                    }
                    case StrikeCraftDogfightPhase.Rejoin:
                    {
                        if (TryResolveLeader(entity, state, out var leader, out var leaderTransform, out var leaderVelocity))
                        {
                            var leaderForward = ResolveForward(leaderTransform, leaderVelocity);
                            var right = math.normalizesafe(math.cross(math.up(), leaderForward), new float3(1f, 0f, 0f));
                            var up = math.normalizesafe(math.cross(leaderForward, right), math.up());
                            var offset = right * Config.RejoinOffset.x + up * Config.RejoinOffset.y + leaderForward * Config.RejoinOffset.z;
                            var leaderPosition = leaderTransform.Position;
                            var rejoinTarget = leaderPosition + offset;
                            desiredForward = math.normalizesafe(rejoinTarget - position, leaderForward);
                            SteeringPrimitives.OffsetPursuit(ref context, in leaderPosition, in leaderVelocity, in offset, out var pursuitAccel);
                            desiredAccel = pursuitAccel + separation;
                            desiredAccel = LimitAccel(desiredAccel, maxLateralAccel);

                            if (math.distance(transform.Position, rejoinTarget) <= Config.RejoinRadius)
                            {
                                SetPhase(ref state, ref metrics, StrikeCraftDogfightPhase.Approach, CurrentTick);
                            }
                        }
                        else
                        {
                            SetPhase(ref state, ref metrics, StrikeCraftDogfightPhase.Approach, CurrentTick);
                        }

                        state.CurrentState = StrikeCraftState.State.Approaching;
                        UpdateEngagement(entity, state.TargetEntity, distance, StrikeCraftDogfightPhase.Rejoin);
                        break;
                    }
                }

                desiredForward = math.normalizesafe(math.lerp(forward, desiredForward, reaction), forward);

                steering.Output = new SteeringOutput
                {
                    DesiredAccel = desiredAccel,
                    DesiredForward = desiredForward,
                    ClosingSpeed = closingSpeed,
                    ConeDot = coneDot,
                    PnOmega = omega
                };
            }

            private static float ResolveMaxWeaponRange(Entity entity, DynamicBuffer<WeaponMount> weapons,
                DynamicBuffer<SubsystemHealth> subsystems, DynamicBuffer<SubsystemDisabled> disabledSubsystems)
            {
                var weaponsDisabled = Space4XSubsystemUtility.IsSubsystemDisabled(subsystems, disabledSubsystems, SubsystemType.Weapons);
                float maxRange = 0f;
                for (int i = 0; i < weapons.Length; i++)
                {
                    var mount = weapons[i];
                    if (mount.IsEnabled == 0)
                    {
                        continue;
                    }
                    if (weaponsDisabled && Space4XSubsystemUtility.IsWeaponMountDisabled(entity, i, subsystems, disabledSubsystems))
                    {
                        continue;
                    }

                    maxRange = math.max(maxRange, mount.Weapon.MaxRange);
                }

                return maxRange;
            }

            private static float3 ResolveForward(in LocalTransform transform, in VesselMovement movement)
            {
                if (math.lengthsq(movement.Velocity) > 0.0001f)
                {
                    return math.normalizesafe(movement.Velocity);
                }

                return math.forward(transform.Rotation);
            }

            private static float3 ResolveForward(in LocalTransform transform, float3 velocity)
            {
                if (math.lengthsq(velocity) > 0.0001f)
                {
                    return math.normalizesafe(velocity);
                }

                return math.forward(transform.Rotation);
            }

            private static bool TryResolveTarget(
                Entity entity,
                Entity craftAffiliation,
                float3 craftPosition,
                float targetAcquireRadiusSq,
                NativeArray<StrikeCraftTargetCandidate> candidates,
                ref StrikeCraftState state,
                out float3 targetPosition,
                out float3 targetVelocity)
            {
                targetPosition = float3.zero;
                targetVelocity = float3.zero;
                var hasRadius = targetAcquireRadiusSq > 0f;

                if (state.TargetEntity != Entity.Null &&
                    TryGetCandidate(state.TargetEntity, craftAffiliation, candidates, out targetPosition, out targetVelocity))
                {
                    if (!hasRadius || math.lengthsq(targetPosition - craftPosition) <= targetAcquireRadiusSq)
                    {
                        return true;
                    }

                    targetPosition = float3.zero;
                    targetVelocity = float3.zero;
                }

                var bestDistanceSq = float.MaxValue;
                Entity bestTarget = Entity.Null;
                float3 bestPosition = float3.zero;
                float3 bestVelocity = float3.zero;

                for (int i = 0; i < candidates.Length; i++)
                {
                    var candidate = candidates[i];
                    if (candidate.Entity == entity)
                    {
                        continue;
                    }

                    if (craftAffiliation != Entity.Null && candidate.AffiliationTarget == craftAffiliation)
                    {
                        continue;
                    }

                    var distSq = math.lengthsq(candidate.Position - craftPosition);
                    if (hasRadius && distSq > targetAcquireRadiusSq)
                    {
                        continue;
                    }

                    if (distSq < bestDistanceSq)
                    {
                        bestDistanceSq = distSq;
                        bestTarget = candidate.Entity;
                        bestPosition = candidate.Position;
                        bestVelocity = candidate.Velocity;
                    }
                }

                if (bestTarget == Entity.Null)
                {
                    state.TargetEntity = Entity.Null;
                    return false;
                }

                state.TargetEntity = bestTarget;
                targetPosition = bestPosition;
                targetVelocity = bestVelocity;
                return true;
            }

            private static bool TryGetCandidate(
                Entity targetEntity,
                Entity craftAffiliation,
                NativeArray<StrikeCraftTargetCandidate> candidates,
                out float3 position,
                out float3 velocity)
            {
                position = float3.zero;
                velocity = float3.zero;

                for (int i = 0; i < candidates.Length; i++)
                {
                    var candidate = candidates[i];
                    if (candidate.Entity != targetEntity)
                    {
                        continue;
                    }

                    if (craftAffiliation != Entity.Null && candidate.AffiliationTarget == craftAffiliation)
                    {
                        return false;
                    }

                    position = candidate.Position;
                    velocity = candidate.Velocity;
                    return true;
                }

                return false;
            }

            private static Entity ResolveAffiliationTarget(Entity entity, BufferLookup<AffiliationTag> affiliationLookup)
            {
                if (!affiliationLookup.HasBuffer(entity))
                {
                    return Entity.Null;
                }

                var affiliations = affiliationLookup[entity];
                Entity fallback = Entity.Null;
                for (int i = 0; i < affiliations.Length; i++)
                {
                    var tag = affiliations[i];
                    if (tag.Target == Entity.Null)
                    {
                        continue;
                    }

                    if (tag.Type == AffiliationType.Faction)
                    {
                        return tag.Target;
                    }

                    if (fallback == Entity.Null && tag.Type == AffiliationType.Fleet)
                    {
                        fallback = tag.Target;
                    }
                    else if (fallback == Entity.Null)
                    {
                        fallback = tag.Target;
                    }
                }

                if (fallback == Entity.Null || !affiliationLookup.HasBuffer(fallback))
                {
                    return fallback;
                }

                var nested = affiliationLookup[fallback];
                for (int i = 0; i < nested.Length; i++)
                {
                    var tag = nested[i];
                    if (tag.Target == Entity.Null)
                    {
                        continue;
                    }

                    if (tag.Type == AffiliationType.Faction)
                    {
                        return tag.Target;
                    }

                    if (tag.Type == AffiliationType.Fleet)
                    {
                        return tag.Target;
                    }
                }

                return fallback;
            }

            private VesselStanceMode ResolveStance(Entity entity)
            {
                if (ProfileLookup.HasComponent(entity))
                {
                    var profile = ProfileLookup[entity];
                    if (profile.Carrier != Entity.Null && PatrolStanceLookup.HasComponent(profile.Carrier))
                    {
                        return PatrolStanceLookup[profile.Carrier].Stance;
                    }
                }

                if (PatrolStanceLookup.HasComponent(entity))
                {
                    return PatrolStanceLookup[entity].Stance;
                }

                return VesselStanceMode.Balanced;
            }

            private static void SetPhase(
                ref StrikeCraftState state,
                ref StrikeCraftDogfightMetrics metrics,
                StrikeCraftDogfightPhase phase,
                uint tick)
            {
                if (state.DogfightPhase == phase)
                {
                    return;
                }

                state.DogfightPhase = phase;
                state.DogfightPhaseStartTick = tick;
                metrics.LastPhaseTick = tick;
            }

            private static float3 ComputeJink(Entity entity, float3 forward, float strength, uint tick)
            {
                if (strength <= 0f)
                {
                    return float3.zero;
                }

                var seed = math.hash(new uint3((uint)entity.Index, tick, 0xC0FFEEu));
                var angle = (seed & 0xFFFF) / 65535f * math.PI * 2f;
                var right = math.normalizesafe(math.cross(forward, math.up()), new float3(1f, 0f, 0f));
                var up = math.normalizesafe(math.cross(right, forward), math.up());
                var lateral = right * math.cos(angle) + up * math.sin(angle);
                return lateral * strength;
            }

            private static float3 LimitAccel(float3 accel, float maxAccel)
            {
                if (maxAccel <= 0f)
                {
                    return float3.zero;
                }

                var magnitudeSq = math.lengthsq(accel);
                if (magnitudeSq <= maxAccel * maxAccel)
                {
                    return accel;
                }

                return math.normalizesafe(accel) * maxAccel;
            }

            private static float ComputeOmega(float3 toTarget, float3 relativeVelocity)
            {
                var distanceSq = math.lengthsq(toTarget);
                if (distanceSq <= 1e-6f)
                {
                    return 0f;
                }

                var losRate = math.cross(toTarget, relativeVelocity) / distanceSq;
                return math.length(losRate);
            }

            private bool TryResolveLeader(
                Entity entity,
                in StrikeCraftState state,
                out Entity leader,
                out LocalTransform leaderTransform,
                out float3 leaderVelocity)
            {
                leader = Entity.Null;
                leaderTransform = default;
                leaderVelocity = float3.zero;

                if (state.DogfightWingLeader != Entity.Null)
                {
                    leader = state.DogfightWingLeader;
                }
                else if (ProfileLookup.HasComponent(entity))
                {
                    leader = ProfileLookup[entity].WingLeader;
                }

                if (leader == Entity.Null || !TransformLookup.HasComponent(leader))
                {
                    return false;
                }

                leaderTransform = TransformLookup[leader];
                if (MovementLookup.HasComponent(leader))
                {
                    leaderVelocity = MovementLookup[leader].Velocity;
                }

                return true;
            }

            private void UpdateEngagement(Entity entity, Entity target, float distance, StrikeCraftDogfightPhase phase)
            {
                if (!EngagementLookup.HasComponent(entity))
                {
                    return;
                }

                var engagement = EngagementLookup.GetRefRW(entity).ValueRW;
                engagement.PrimaryTarget = target;
                engagement.TargetDistance = distance;
                engagement.Phase = phase switch
                {
                    StrikeCraftDogfightPhase.Approach => EngagementPhase.Approaching,
                    StrikeCraftDogfightPhase.FireWindow => EngagementPhase.Engaged,
                    StrikeCraftDogfightPhase.BreakOff => EngagementPhase.Retreating,
                    StrikeCraftDogfightPhase.Rejoin => EngagementPhase.Retreating,
                    _ => EngagementPhase.None
                };
            }
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XStrikeCraftGuidanceSystem))]
    public partial struct Space4XStrikeCraftMotorSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<StrikeCraftDogfightTag>();
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

            var config = StrikeCraftDogfightConfig.Default;
            if (SystemAPI.TryGetSingleton<StrikeCraftDogfightConfig>(out var configSingleton))
            {
                config = configSingleton;
            }

            var job = new ApplyStrikeCraftDogfightMotorJob
            {
                CurrentTick = timeState.Tick,
                DeltaTime = timeState.FixedDeltaTime,
                Config = config
            };

            state.Dependency = job.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        [WithAll(typeof(StrikeCraftDogfightTag))]
        public partial struct ApplyStrikeCraftDogfightMotorJob : IJobEntity
        {
            public uint CurrentTick;
            public float DeltaTime;
            public StrikeCraftDogfightConfig Config;

            public void Execute(
                ref LocalTransform transform,
                ref VesselMovement movement,
                ref VesselTurnRateState turnRateState,
                in StrikeCraftDogfightSteering steering,
                DynamicBuffer<SubsystemHealth> subsystems,
                DynamicBuffer<SubsystemDisabled> disabledSubsystems)
            {
                var wasMoving = movement.IsMoving;
                var engineScale = Space4XSubsystemUtility.ResolveEngineScale(subsystems, disabledSubsystems);
                var accel = steering.Output.DesiredAccel;
                if (!math.all(math.isfinite(accel)))
                {
                    accel = float3.zero;
                }

                accel *= engineScale;
                movement.Velocity += accel * DeltaTime;
                var maxSpeed = math.max(0.1f, movement.BaseSpeed * Config.ApproachMaxSpeedMultiplier * engineScale);
                movement.Velocity = LimitSpeed(movement.Velocity, maxSpeed);

                movement.CurrentSpeed = math.length(movement.Velocity);
                transform.Position += movement.Velocity * DeltaTime;

                var desiredForward = steering.Output.DesiredForward;
                if (math.lengthsq(desiredForward) > 0.0001f)
                {
                    var desiredRotation = quaternion.LookRotationSafe(math.normalizesafe(desiredForward), math.up());
                    var turnSpeed = (movement.TurnSpeed > 0f ? movement.TurnSpeed : Config.MaxTurnRate) * engineScale;
                    var dt = math.max(DeltaTime, 1e-4f);
                    var forward = math.forward(transform.Rotation);
                    var angle = math.acos(math.clamp(math.dot(forward, math.forward(desiredRotation)), -1f, 1f));
                    const float headingDeadbandRadians = 0.026f;
                    if (angle > headingDeadbandRadians)
                    {
                        var maxAngularSpeed = math.PI * 4f;
                        var maxAngularAccel = math.PI * 8f;
                        var desiredAngularSpeed = math.min(maxAngularSpeed, angle * turnSpeed);
                        desiredAngularSpeed = math.min(desiredAngularSpeed, angle / dt);
                        var maxDeltaSpeed = maxAngularAccel * dt;
                        var angularSpeed = math.clamp(desiredAngularSpeed,
                            turnRateState.LastAngularSpeed - maxDeltaSpeed,
                            turnRateState.LastAngularSpeed + maxDeltaSpeed);
                        var stepAngle = angularSpeed * dt;
                        var stepT = stepAngle >= angle ? 1f : math.saturate(stepAngle / angle);
                        transform.Rotation = math.slerp(transform.Rotation, desiredRotation, stepT);
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

                movement.IsMoving = movement.CurrentSpeed > 0.01f ? (byte)1 : (byte)0;
                if ((wasMoving == 0 || movement.MoveStartTick == 0) && movement.IsMoving != 0)
                {
                    movement.MoveStartTick = CurrentTick;
                }
                movement.LastMoveTick = CurrentTick;
            }

            private static float3 LimitSpeed(float3 velocity, float maxSpeed)
            {
                var speedSq = math.lengthsq(velocity);
                var maxSpeedSq = maxSpeed * maxSpeed;
                if (speedSq <= maxSpeedSq)
                {
                    return velocity;
                }

                return math.normalizesafe(velocity) * maxSpeed;
            }
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XStrikeCraftGuidanceSystem))]
    public partial struct Space4XStrikeCraftDogfightTelemetrySystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<StrikeCraftDogfightTag>();
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

            var config = StrikeCraftDogfightConfig.Default;
            if (SystemAPI.TryGetSingleton<StrikeCraftDogfightConfig>(out var configSingleton))
            {
                config = configSingleton;
            }

            if (config.TelemetrySampleTicks == 0)
            {
                return;
            }

            foreach (var (stateRef, metricsRef, steeringRef, transform, buffer, entity) in SystemAPI
                         .Query<RefRO<StrikeCraftState>, RefRW<StrikeCraftDogfightMetrics>, RefRO<StrikeCraftDogfightSteering>,
                             RefRO<LocalTransform>, DynamicBuffer<StrikeCraftDogfightSample>>()
                         .WithAll<StrikeCraftDogfightTag>()
                         .WithEntityAccess())
            {
                var metrics = metricsRef.ValueRO;
                if (metrics.LastTelemetryTick != 0 &&
                    timeState.Tick - metrics.LastTelemetryTick < config.TelemetrySampleTicks)
                {
                    continue;
                }

                var distance = 0f;
                if (stateRef.ValueRO.TargetEntity != Entity.Null)
                {
                    distance = math.distance(transform.ValueRO.Position, stateRef.ValueRO.TargetPosition);
                }

                buffer.Add(new StrikeCraftDogfightSample
                {
                    Tick = timeState.Tick,
                    Phase = stateRef.ValueRO.DogfightPhase,
                    Target = stateRef.ValueRO.TargetEntity,
                    Distance = distance,
                    ClosingSpeed = steeringRef.ValueRO.Output.ClosingSpeed,
                    ConeDot = steeringRef.ValueRO.Output.ConeDot,
                    PnOmega = steeringRef.ValueRO.Output.PnOmega
                });

                metrics.LastTelemetryTick = timeState.Tick;
                metricsRef.ValueRW = metrics;
            }
        }
    }
}

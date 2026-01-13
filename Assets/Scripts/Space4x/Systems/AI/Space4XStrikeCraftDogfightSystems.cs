using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Spatial;
using PureDOTS.Runtime.Steering;
using Space4X.Registry;
using Space4X.Runtime;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs.LowLevel.Unsafe;
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
        private ComponentLookup<StrikeCraftDogfightTag> _dogfightTagLookup;
        private BufferLookup<AffiliationTag> _affiliationLookup;
        private ComponentLookup<StrikeCraftProfile> _profileLookup;
        private ComponentLookup<PatrolStance> _patrolStanceLookup;
        private ComponentLookup<Space4XEngagement> _engagementLookup;
        private EntityQuery _candidateQuery;
        private EntityQuery _neighborQuery;
        private NativeArray<KNearestResult> _targetResultsScratch;
        private NativeArray<KNearestResult> _neighborResultsScratch;
        private NativeArray<KNearestResult> _emptyTargetResults;
        private NativeArray<KNearestResult> _emptyNeighborResults;
        private NativeArray<SpatialGridCellRange> _emptyCellRanges;
        private NativeArray<SpatialGridEntry> _emptyEntries;
        private NativeList<StrikeCraftTargetCandidate> _fallbackCandidates;
        private NativeList<float3> _fallbackNeighbors;

        public struct StrikeCraftTargetCandidate
        {
            public Entity Entity;
            public float3 Position;
            public float3 Velocity;
            public Entity AffiliationTarget;
        }

        private struct StrikeCraftTargetFilter : ISpatialQueryFilter
        {
            [ReadOnly] public ComponentLookup<HullIntegrity> HullLookup;

            public bool Accept(int descriptorIndex, in SpatialQueryDescriptor descriptor, in SpatialGridEntry entry)
            {
                var entity = entry.Entity;
                if (entity == Entity.Null)
                {
                    return false;
                }

                if (!HullLookup.HasComponent(entity))
                {
                    return false;
                }

                return HullLookup[entity].Current > 0f;
            }
        }

        private struct StrikeCraftNeighborFilter : ISpatialQueryFilter
        {
            [ReadOnly] public ComponentLookup<StrikeCraftDogfightTag> DogfightLookup;

            public bool Accept(int descriptorIndex, in SpatialQueryDescriptor descriptor, in SpatialGridEntry entry)
            {
                var entity = entry.Entity;
                if (entity == Entity.Null)
                {
                    return false;
                }

                return DogfightLookup.HasComponent(entity);
            }
        }

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<StrikeCraftDogfightTag>();

            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _movementLookup = state.GetComponentLookup<VesselMovement>(true);
            _hullLookup = state.GetComponentLookup<HullIntegrity>(true);
            _dogfightTagLookup = state.GetComponentLookup<StrikeCraftDogfightTag>(true);
            _affiliationLookup = state.GetBufferLookup<AffiliationTag>(true);
            _profileLookup = state.GetComponentLookup<StrikeCraftProfile>(true);
            _patrolStanceLookup = state.GetComponentLookup<PatrolStance>(true);
            _engagementLookup = state.GetComponentLookup<Space4XEngagement>(false);
            _candidateQuery = state.GetEntityQuery(
                ComponentType.ReadOnly<LocalTransform>(),
                ComponentType.ReadOnly<HullIntegrity>());
            _neighborQuery = state.GetEntityQuery(
                ComponentType.ReadOnly<LocalTransform>(),
                ComponentType.ReadOnly<StrikeCraftDogfightTag>());

            if (!_emptyTargetResults.IsCreated)
            {
                _emptyTargetResults = new NativeArray<KNearestResult>(1, Allocator.Persistent);
            }

            if (!_emptyNeighborResults.IsCreated)
            {
                _emptyNeighborResults = new NativeArray<KNearestResult>(1, Allocator.Persistent);
            }

            if (!_fallbackCandidates.IsCreated)
            {
                _fallbackCandidates = new NativeList<StrikeCraftTargetCandidate>(0, Allocator.Persistent);
            }

            if (!_fallbackNeighbors.IsCreated)
            {
                _fallbackNeighbors = new NativeList<float3>(0, Allocator.Persistent);
            }

            if (!_emptyCellRanges.IsCreated)
            {
                _emptyCellRanges = new NativeArray<SpatialGridCellRange>(1, Allocator.Persistent);
            }

            if (!_emptyEntries.IsCreated)
            {
                _emptyEntries = new NativeArray<SpatialGridEntry>(1, Allocator.Persistent);
            }
        }

        public void OnDestroy(ref SystemState state)
        {
            if (_targetResultsScratch.IsCreated)
            {
                _targetResultsScratch.Dispose();
            }

            if (_neighborResultsScratch.IsCreated)
            {
                _neighborResultsScratch.Dispose();
            }

            if (_emptyTargetResults.IsCreated)
            {
                _emptyTargetResults.Dispose();
            }

            if (_emptyNeighborResults.IsCreated)
            {
                _emptyNeighborResults.Dispose();
            }

            if (_fallbackCandidates.IsCreated)
            {
                _fallbackCandidates.Dispose();
            }

            if (_fallbackNeighbors.IsCreated)
            {
                _fallbackNeighbors.Dispose();
            }

            if (_emptyCellRanges.IsCreated)
            {
                _emptyCellRanges.Dispose();
            }

            if (_emptyEntries.IsCreated)
            {
                _emptyEntries.Dispose();
            }
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
            var maxTargetCandidates = math.max(1, config.MaxTargetCandidates);
            var maxNeighborSamples = math.max(1, config.MaxNeighborSamples);
            var evaluationCadenceTicks = math.max(1u, config.EvaluationCadenceTicks);
            var evaluationStride = 1;
            if (config.MaxEvaluationsPerTick > 0)
            {
                var craftCount = _neighborQuery.CalculateEntityCount();
                evaluationStride = ResolveEvaluationStride(craftCount, config.MaxEvaluationsPerTick);
            }

            _transformLookup.Update(ref state);
            _movementLookup.Update(ref state);
            _hullLookup.Update(ref state);
            _dogfightTagLookup.Update(ref state);
            _affiliationLookup.Update(ref state);
            _profileLookup.Update(ref state);
            _patrolStanceLookup.Update(ref state);
            _engagementLookup.Update(ref state);

            var spatialConfig = default(SpatialGridConfig);
            var hasSpatial = SystemAPI.TryGetSingleton(out spatialConfig) &&
                             SystemAPI.TryGetSingleton(out SpatialGridState _);

            var cellRanges = _emptyCellRanges;
            var gridEntries = _emptyEntries;
            var targetResults = _emptyTargetResults;
            var neighborResults = _emptyNeighborResults;
            if (hasSpatial)
            {
                var gridEntity = SystemAPI.GetSingletonEntity<SpatialGridConfig>();
                cellRanges = SystemAPI.GetBuffer<SpatialGridCellRange>(gridEntity).AsNativeArray();
                gridEntries = SystemAPI.GetBuffer<SpatialGridEntry>(gridEntity).AsNativeArray();

                var scratchCount = JobsUtility.MaxJobThreadCount;
                EnsureScratchCapacity(ref _targetResultsScratch, scratchCount * maxTargetCandidates, ref state);
                EnsureScratchCapacity(ref _neighborResultsScratch, scratchCount * maxNeighborSamples, ref state);
                targetResults = _targetResultsScratch;
                neighborResults = _neighborResultsScratch;
            }

            NativeArray<StrikeCraftTargetCandidate> candidates = default;
            NativeArray<float3> neighbors = default;
            if (!hasSpatial)
            {
                state.Dependency.Complete();

                var candidateCapacity = math.max(8, _candidateQuery.CalculateEntityCount());
                if (_fallbackCandidates.Capacity < candidateCapacity)
                {
                    _fallbackCandidates.Capacity = candidateCapacity;
                }
                _fallbackCandidates.Clear();
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

                    _fallbackCandidates.Add(new StrikeCraftTargetCandidate
                    {
                        Entity = entity,
                        Position = transform.ValueRO.Position,
                        Velocity = velocity,
                        AffiliationTarget = ResolveAffiliationTarget(entity)
                    });
                }

                var neighborCapacity = math.max(4, _neighborQuery.CalculateEntityCount());
                if (_fallbackNeighbors.Capacity < neighborCapacity)
                {
                    _fallbackNeighbors.Capacity = neighborCapacity;
                }
                _fallbackNeighbors.Clear();
                foreach (var (transform, entity) in SystemAPI.Query<RefRO<LocalTransform>>()
                             .WithAll<StrikeCraftDogfightTag>()
                             .WithEntityAccess())
                {
                    _fallbackNeighbors.Add(transform.ValueRO.Position);
                }

                candidates = _fallbackCandidates.AsArray();
                neighbors = _fallbackNeighbors.AsArray();
            }

            var job = new UpdateStrikeCraftDogfightGuidanceJob
            {
                CurrentTick = timeState.Tick,
                DeltaTime = timeState.FixedDeltaTime,
                Config = config,
                StanceConfig = stanceConfig,
                FireConeCos = math.cos(math.radians(config.FireConeDegrees)),
                TargetAcquireRadiusSq = targetAcquireRadiusSq,
                UseSpatialGrid = (byte)(hasSpatial ? 1 : 0),
                SpatialConfig = spatialConfig,
                CellRanges = cellRanges,
                Entries = gridEntries,
                TargetResults = targetResults,
                NeighborResults = neighborResults,
                MaxTargetCandidates = maxTargetCandidates,
                MaxNeighborSamples = maxNeighborSamples,
                EvaluationCadenceTicks = evaluationCadenceTicks,
                EvaluationStride = evaluationStride,
                StaggerEvaluation = config.StaggerEvaluation,
                TargetCandidates = hasSpatial ? _fallbackCandidates.AsArray() : candidates,
                NeighborPositions = hasSpatial ? _fallbackNeighbors.AsArray() : neighbors,
                AffiliationLookup = _affiliationLookup,
                TransformLookup = _transformLookup,
                MovementLookup = _movementLookup,
                HullLookup = _hullLookup,
                DogfightLookup = _dogfightTagLookup,
                ProfileLookup = _profileLookup,
                PatrolStanceLookup = _patrolStanceLookup,
                EngagementLookup = _engagementLookup
            };

            state.Dependency = job.ScheduleParallel(state.Dependency);
        }

        private static void EnsureScratchCapacity(
            ref NativeArray<KNearestResult> scratch,
            int required,
            ref SystemState state)
        {
            required = math.max(1, required);
            if (scratch.IsCreated && scratch.Length >= required)
            {
                return;
            }

            state.Dependency.Complete();
            if (scratch.IsCreated)
            {
                scratch.Dispose();
            }

            scratch = new NativeArray<KNearestResult>(required, Allocator.Persistent);
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

        private static int ResolveEvaluationStride(int totalCount, int maxPerTick)
        {
            if (maxPerTick <= 0 || totalCount <= maxPerTick)
            {
                return 1;
            }

            var stride = (totalCount + maxPerTick - 1) / maxPerTick;
            return math.max(1, stride);
        }

        [BurstCompile]
        [WithAll(typeof(StrikeCraftDogfightTag))]
        public partial struct UpdateStrikeCraftDogfightGuidanceJob : IJobEntity
        {
            public uint CurrentTick;
            public float DeltaTime;
            public StrikeCraftDogfightConfig Config;
            public Space4XStanceTuningConfig StanceConfig;
            public float FireConeCos;
            public float TargetAcquireRadiusSq;
            public byte UseSpatialGrid;
            public SpatialGridConfig SpatialConfig;
            [ReadOnly] public NativeArray<SpatialGridCellRange> CellRanges;
            [ReadOnly] public NativeArray<SpatialGridEntry> Entries;
            [NativeDisableParallelForRestriction] public NativeArray<KNearestResult> TargetResults;
            [NativeDisableParallelForRestriction] public NativeArray<KNearestResult> NeighborResults;
            public int MaxTargetCandidates;
            public int MaxNeighborSamples;
            public uint EvaluationCadenceTicks;
            public int EvaluationStride;
            public byte StaggerEvaluation;
            [ReadOnly] public NativeArray<StrikeCraftTargetCandidate> TargetCandidates;
            [ReadOnly] public NativeArray<float3> NeighborPositions;
            [ReadOnly] public BufferLookup<AffiliationTag> AffiliationLookup;
            [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;
            [ReadOnly] public ComponentLookup<VesselMovement> MovementLookup;
            [ReadOnly] public ComponentLookup<HullIntegrity> HullLookup;
            [ReadOnly] public ComponentLookup<StrikeCraftDogfightTag> DogfightLookup;
            [ReadOnly] public ComponentLookup<StrikeCraftProfile> ProfileLookup;
            [ReadOnly] public ComponentLookup<PatrolStance> PatrolStanceLookup;
            [NativeDisableParallelForRestriction] public ComponentLookup<Space4XEngagement> EngagementLookup;
            [NativeSetThreadIndex] public int ThreadIndex;

            public void Execute(
                [EntityIndexInQuery] int entityIndex,
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
                if (!TryResolveTarget(entityIndex, entity, craftAffiliation, position, TargetAcquireRadiusSq, ref state,
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
                var jinkStrength = math.max(0f, Config.JinkStrength + tuning.EvasionJinkStrength);

                var forward = ResolveForward(transform, movement);
                var toTarget = targetPosition - position;
                var distance = math.length(toTarget);
                var directionToTarget = distance > 1e-4f ? toTarget / distance : forward;
                var coneDot = math.dot(forward, directionToTarget);
                var inCone = coneDot >= FireConeCos;
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
                    MaxSpeed = movement.BaseSpeed * Config.ApproachMaxSpeedMultiplier,
                    MaxAccel = math.max(0.1f, Config.MaxLateralAccel),
                    DeltaTime = DeltaTime
                };

                var separation = ComputeSeparation(entityIndex, entity, position);
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
                        desiredAccel = LimitAccel(desiredAccel, Config.MaxLateralAccel);

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
                        desiredAccel = LimitAccel(desiredAccel, Config.MaxLateralAccel * 0.5f);

                        if (!inCone || !inRange)
                        {
                            SetPhase(ref state, ref metrics, StrikeCraftDogfightPhase.Approach, CurrentTick);
                        }
                        else if (distance <= Config.BreakOffDistance)
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
                        desiredAccel = LimitAccel(desiredAccel, Config.MaxLateralAccel);

                        if (CurrentTick - state.DogfightPhaseStartTick >= Config.BreakOffTicks ||
                            distance > Config.BreakOffDistance * 2f)
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
                            desiredAccel = LimitAccel(desiredAccel, Config.MaxLateralAccel);

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

            private bool TryResolveTarget(
                int entityIndex,
                Entity entity,
                Entity craftAffiliation,
                float3 craftPosition,
                float targetAcquireRadiusSq,
                ref StrikeCraftState state,
                out float3 targetPosition,
                out float3 targetVelocity)
            {
                targetPosition = float3.zero;
                targetVelocity = float3.zero;

                if (TryResolveExistingTarget(entity, craftAffiliation, craftPosition, targetAcquireRadiusSq, ref state,
                        out targetPosition, out targetVelocity))
                {
                    return true;
                }

                if (!ShouldEvaluateTarget(entityIndex))
                {
                    state.TargetEntity = Entity.Null;
                    return false;
                }

                if (UseSpatialGrid != 0 &&
                    TryResolveTargetSpatial(entity, craftAffiliation, craftPosition, targetAcquireRadiusSq,
                        out var spatialTarget, out targetPosition, out targetVelocity))
                {
                    state.TargetEntity = spatialTarget;
                    return true;
                }

                if (TargetCandidates.Length > 0 &&
                    TryResolveTargetFallback(entity, craftAffiliation, craftPosition, targetAcquireRadiusSq, TargetCandidates,
                        out var fallbackTarget, out targetPosition, out targetVelocity))
                {
                    state.TargetEntity = fallbackTarget;
                    return true;
                }

                state.TargetEntity = Entity.Null;
                return false;
            }

            private bool ShouldEvaluateTarget(int entityIndex)
            {
                var cadence = EvaluationCadenceTicks;
                if (cadence > 1)
                {
                    var offset = StaggerEvaluation != 0 ? (uint)entityIndex : 0u;
                    if (((CurrentTick + offset) % cadence) != 0)
                    {
                        return false;
                    }
                }

                if (EvaluationStride > 1)
                {
                    var offset = StaggerEvaluation != 0 ? (int)(CurrentTick % (uint)EvaluationStride) : 0;
                    if (((entityIndex + offset) % EvaluationStride) != 0)
                    {
                        return false;
                    }
                }

                return true;
            }

            private bool TryResolveExistingTarget(
                Entity entity,
                Entity craftAffiliation,
                float3 craftPosition,
                float targetAcquireRadiusSq,
                ref StrikeCraftState state,
                out float3 targetPosition,
                out float3 targetVelocity)
            {
                targetPosition = float3.zero;
                targetVelocity = float3.zero;

                var target = state.TargetEntity;
                if (target == Entity.Null)
                {
                    return false;
                }

                if (!TransformLookup.HasComponent(target) || !HullLookup.HasComponent(target))
                {
                    state.TargetEntity = Entity.Null;
                    return false;
                }

                if (HullLookup[target].Current <= 0f)
                {
                    state.TargetEntity = Entity.Null;
                    return false;
                }

                if (craftAffiliation != Entity.Null)
                {
                    var targetAffiliation = ResolveAffiliationTarget(target, AffiliationLookup);
                    if (targetAffiliation == craftAffiliation)
                    {
                        state.TargetEntity = Entity.Null;
                        return false;
                    }
                }

                var position = TransformLookup[target].Position;
                if (targetAcquireRadiusSq > 0f && math.lengthsq(position - craftPosition) > targetAcquireRadiusSq)
                {
                    return false;
                }

                targetPosition = position;
                targetVelocity = MovementLookup.HasComponent(target) ? MovementLookup[target].Velocity : float3.zero;
                return true;
            }

            private bool TryResolveTargetSpatial(
                Entity entity,
                Entity craftAffiliation,
                float3 craftPosition,
                float targetAcquireRadiusSq,
                out Entity bestTarget,
                out float3 bestPosition,
                out float3 bestVelocity)
            {
                bestTarget = Entity.Null;
                bestPosition = float3.zero;
                bestVelocity = float3.zero;

                if (MaxTargetCandidates <= 0 || !CellRanges.IsCreated || !Entries.IsCreated || !TargetResults.IsCreated)
                {
                    return false;
                }

                var startIndex = ThreadIndex * MaxTargetCandidates;
                if (startIndex >= TargetResults.Length)
                {
                    return false;
                }

                var length = math.min(MaxTargetCandidates, TargetResults.Length - startIndex);
                if (length <= 0)
                {
                    return false;
                }

                var radius = targetAcquireRadiusSq > 0f ? math.sqrt(targetAcquireRadiusSq) : float.MaxValue;
                var descriptor = new SpatialQueryDescriptor
                {
                    Origin = craftPosition,
                    Radius = radius,
                    MaxResults = length,
                    Options = SpatialQueryOptions.RequireDeterministicSorting | SpatialQueryOptions.IgnoreSelf,
                    Tolerance = 1e-4f,
                    ExcludedEntity = entity
                };

                var slice = new NativeSlice<KNearestResult>(TargetResults, startIndex, length);
                var filter = new StrikeCraftTargetFilter
                {
                    HullLookup = HullLookup
                };

                var count = SpatialQueryHelper.CollectKNearest(0, in descriptor, in SpatialConfig, CellRanges, Entries, slice, in filter);
                if (count <= 0)
                {
                    return false;
                }

                var bestDistanceSq = targetAcquireRadiusSq > 0f ? targetAcquireRadiusSq : float.MaxValue;
                for (int i = 0; i < count; i++)
                {
                    var candidate = slice[i];
                    var candidateEntity = candidate.Entity;
                    if (candidateEntity == Entity.Null || candidateEntity == entity)
                    {
                        continue;
                    }

                    if (craftAffiliation != Entity.Null)
                    {
                        var candidateAffiliation = ResolveAffiliationTarget(candidateEntity, AffiliationLookup);
                        if (candidateAffiliation == craftAffiliation)
                        {
                            continue;
                        }
                    }

                    if (!TransformLookup.HasComponent(candidateEntity))
                    {
                        continue;
                    }

                    var distanceSq = candidate.DistanceSq;
                    if (distanceSq > bestDistanceSq)
                    {
                        continue;
                    }

                    bestDistanceSq = distanceSq;
                    bestTarget = candidateEntity;
                    bestPosition = TransformLookup[candidateEntity].Position;
                    bestVelocity = MovementLookup.HasComponent(candidateEntity)
                        ? MovementLookup[candidateEntity].Velocity
                        : float3.zero;
                }

                return bestTarget != Entity.Null;
            }

            private static bool TryResolveTargetFallback(
                Entity entity,
                Entity craftAffiliation,
                float3 craftPosition,
                float targetAcquireRadiusSq,
                NativeArray<StrikeCraftTargetCandidate> candidates,
                out Entity bestTarget,
                out float3 bestPosition,
                out float3 bestVelocity)
            {
                bestTarget = Entity.Null;
                bestPosition = float3.zero;
                bestVelocity = float3.zero;
                var hasRadius = targetAcquireRadiusSq > 0f;
                var bestDistanceSq = hasRadius ? targetAcquireRadiusSq : float.MaxValue;

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

                return bestTarget != Entity.Null;
            }

            private float3 ComputeSeparation(int entityIndex, Entity entity, float3 position)
            {
                if (Config.SeparationRadius <= 0f || Config.SeparationStrength <= 0f)
                {
                    return float3.zero;
                }

                if (UseSpatialGrid == 0)
                {
                    SteeringPrimitives.Separation(position, NeighborPositions, Config.SeparationRadius, Config.SeparationStrength,
                        out var fallback);
                    return fallback;
                }

                if (MaxNeighborSamples <= 0 || !CellRanges.IsCreated || !Entries.IsCreated || !NeighborResults.IsCreated)
                {
                    return float3.zero;
                }

                var startIndex = ThreadIndex * MaxNeighborSamples;
                if (startIndex >= NeighborResults.Length)
                {
                    return float3.zero;
                }

                var length = math.min(MaxNeighborSamples, NeighborResults.Length - startIndex);
                if (length <= 0)
                {
                    return float3.zero;
                }

                var slice = new NativeSlice<KNearestResult>(NeighborResults, startIndex, length);
                var descriptor = new SpatialQueryDescriptor
                {
                    Origin = position,
                    Radius = Config.SeparationRadius,
                    MaxResults = length,
                    Options = SpatialQueryOptions.RequireDeterministicSorting | SpatialQueryOptions.IgnoreSelf,
                    Tolerance = 1e-4f,
                    ExcludedEntity = entity
                };

                var filter = new StrikeCraftNeighborFilter
                {
                    DogfightLookup = DogfightLookup
                };

                var count = SpatialQueryHelper.CollectKNearest(0, in descriptor, in SpatialConfig, CellRanges, Entries, slice, in filter);
                if (count <= 0)
                {
                    return float3.zero;
                }

                var separation = float3.zero;
                var radius = Config.SeparationRadius;
                var radiusSq = radius * radius;
                for (int i = 0; i < count; i++)
                {
                    var neighborEntity = slice[i].Entity;
                    if (neighborEntity == Entity.Null || neighborEntity == entity)
                    {
                        continue;
                    }

                    if (!TransformLookup.HasComponent(neighborEntity))
                    {
                        continue;
                    }

                    var offset = position - TransformLookup[neighborEntity].Position;
                    var distSq = math.lengthsq(offset);
                    if (distSq < 1e-6f || distSq > radiusSq)
                    {
                        continue;
                    }

                    var dist = math.sqrt(distSq);
                    var weight = (radius - dist) / radius;
                    separation += (offset / dist) * weight;
                }

                if (math.lengthsq(separation) > 1e-6f)
                {
                    separation = math.normalizesafe(separation) * Config.SeparationStrength;
                }

                return separation;
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
                in StrikeCraftDogfightSteering steering,
                DynamicBuffer<SubsystemHealth> subsystems,
                DynamicBuffer<SubsystemDisabled> disabledSubsystems)
            {
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
                    var turnSpeed = movement.TurnSpeed > 0f ? movement.TurnSpeed : Config.MaxTurnRate;
                    turnSpeed *= engineScale;
                    transform.Rotation = math.slerp(transform.Rotation, desiredRotation, DeltaTime * turnSpeed);
                }

                movement.IsMoving = movement.CurrentSpeed > 0.01f ? (byte)1 : (byte)0;
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

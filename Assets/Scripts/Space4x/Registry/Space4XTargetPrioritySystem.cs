using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Spatial;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using SpatialSystemGroup = PureDOTS.Systems.SpatialSystemGroup;

namespace Space4X.Registry
{
    using Debug = UnityEngine.Debug;

    
    /// <summary>
    /// Scores potential targets and selects based on alignment-driven profile.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SpatialSystemGroup))]
    [UpdateBefore(typeof(Space4XCaptainOrderSystem))]
    public partial struct Space4XTargetPrioritySystem : ISystem
    {
        private EntityQuery _potentialTargetsQuery;
        private ComponentLookup<VesselStanceComponent> _stanceLookup;
        private ComponentLookup<PatrolStance> _patrolStanceLookup;
        private ComponentLookup<HullIntegrity> _hullLookup;
        private uint _lastTick;

        private struct TargetPrioritySpatialFilter : ISpatialQueryFilter
        {
            [ReadOnly] public ComponentLookup<HullIntegrity> HullLookup;
            public Entity Excluded;

            public bool Accept(int descriptorIndex, in SpatialQueryDescriptor descriptor, in SpatialGridEntry entry)
            {
                var entity = entry.Entity;
                if (entity == Entity.Null || entity == Excluded)
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

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TargetPriority>();
            state.RequireForUpdate<TargetSelectionProfile>();
            state.RequireForUpdate<TimeState>();
            _lastTick = 0;
            _stanceLookup = state.GetComponentLookup<VesselStanceComponent>(true);
            _patrolStanceLookup = state.GetComponentLookup<PatrolStance>(true);
            _hullLookup = state.GetComponentLookup<HullIntegrity>(true);

            // Query for potential hostile targets
            _potentialTargetsQuery = state.GetEntityQuery(
                ComponentType.ReadOnly<LocalTransform>(),
                ComponentType.ReadOnly<HullIntegrity>()
            );
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            var currentTick = timeState.Tick;
            var tickDelta = _lastTick == 0u ? 1u : (currentTick > _lastTick ? currentTick - _lastTick : 1u);
            _lastTick = currentTick;
            var deltaTime = timeState.FixedDeltaTime * tickDelta;

            _stanceLookup.Update(ref state);
            _patrolStanceLookup.Update(ref state);
            _hullLookup.Update(ref state);

            var stanceConfig = Space4XStanceTuningConfig.Default;
            if (SystemAPI.TryGetSingleton<Space4XStanceTuningConfig>(out var stanceConfigSingleton))
            {
                stanceConfig = stanceConfigSingleton;
            }

            var queryConfig = TargetPriorityQueryConfig.Default;
            if (SystemAPI.TryGetSingleton<TargetPriorityQueryConfig>(out var queryConfigSingleton))
            {
                queryConfig = queryConfigSingleton;
            }

            var evaluationCadence = math.max(1u, queryConfig.EvaluationCadenceTicks);
            var maxCandidates = math.max(1, queryConfig.MaxCandidates);
            var maxEvaluationsPerTick = math.max(1, queryConfig.MaxEvaluationsPerTick);
            var staggerEvaluation = queryConfig.StaggerEvaluation != 0;

            SpatialGridConfig spatialConfig = default;
            var hasSpatial = queryConfig.UseSpatialGrid != 0 &&
                             SystemAPI.TryGetSingleton(out spatialConfig) &&
                             SystemAPI.TryGetSingleton(out SpatialGridState _);

            DynamicBuffer<SpatialGridCellRange> cellRanges = default;
            DynamicBuffer<SpatialGridEntry> gridEntries = default;
            if (hasSpatial)
            {
                var gridEntity = SystemAPI.GetSingletonEntity<SpatialGridConfig>();
                cellRanges = SystemAPI.GetBuffer<SpatialGridCellRange>(gridEntity);
                gridEntries = SystemAPI.GetBuffer<SpatialGridEntry>(gridEntity);
            }

            NativeArray<Entity> potentialTargets = default;
            NativeArray<LocalTransform> targetTransforms = default;
            NativeArray<HullIntegrity> targetHulls = default;
            if (!hasSpatial)
            {
                potentialTargets = _potentialTargetsQuery.ToEntityArray(Allocator.Temp);
                targetTransforms = _potentialTargetsQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
                targetHulls = _potentialTargetsQuery.ToComponentDataArray<HullIntegrity>(Allocator.Temp);
            }

            var nearestResults = hasSpatial
                ? new NativeList<KNearestResult>(maxCandidates, Allocator.Temp)
                : default;
            var evaluationsThisTick = 0;

            foreach (var (priority, profile, transform, candidates, entity) in
                SystemAPI.Query<RefRW<TargetPriority>, RefRO<TargetSelectionProfile>, RefRO<LocalTransform>, DynamicBuffer<TargetCandidate>>()
                    .WithEntityAccess())
            {
                if (priority.ValueRO.CurrentTarget != Entity.Null)
                {
                    priority.ValueRW.EngagementDuration += deltaTime;
                }

                // Check if reevaluation is needed (every 10 ticks or forced)
                bool shouldEvaluate = priority.ValueRO.ForceReevaluate == 1 ||
                                      currentTick - priority.ValueRO.LastEvaluationTick >= evaluationCadence;

                if (!shouldEvaluate)
                {
                    continue;
                }

                if (staggerEvaluation && priority.ValueRO.ForceReevaluate == 0)
                {
                    var slot = (uint)(entity.Index % (int)evaluationCadence);
                    if ((currentTick + slot) % evaluationCadence != 0)
                    {
                        continue;
                    }
                }

                if (priority.ValueRO.ForceReevaluate == 0 && evaluationsThisTick >= maxEvaluationsPerTick)
                {
                    continue;
                }

                evaluationsThisTick++;
                candidates.Clear();

                // Score each potential target
                float maxRange = profile.ValueRO.MaxEngagementRange > 0
                    ? profile.ValueRO.MaxEngagementRange
                    : queryConfig.DefaultSearchRadius;

                var stance = VesselStanceMode.Balanced;
                if (_stanceLookup.HasComponent(entity))
                {
                    stance = _stanceLookup[entity].CurrentStance;
                }
                else if (_patrolStanceLookup.HasComponent(entity))
                {
                    stance = _patrolStanceLookup[entity].Stance;
                }

                var tuning = stanceConfig.Resolve(stance);
                if (tuning.AutoEngageRadius > 0f)
                {
                    maxRange = math.min(maxRange, tuning.AutoEngageRadius);
                }

                float3 myPosition = transform.ValueRO.Position;

                if (hasSpatial && maxRange > 0f)
                {
                    var filter = new TargetPrioritySpatialFilter
                    {
                        HullLookup = _hullLookup,
                        Excluded = entity
                    };

                    SpatialQueryHelper.FindKNearestInRadius(
                        ref myPosition,
                        maxRange,
                        maxCandidates,
                        spatialConfig,
                        cellRanges,
                        gridEntries,
                        ref nearestResults,
                        filter);

                    for (int i = 0; i < nearestResults.Length; i++)
                    {
                        var targetEntity = nearestResults[i].Entity;
                        if (!_hullLookup.HasComponent(targetEntity))
                        {
                            continue;
                        }

                        var hull = _hullLookup[targetEntity];
                        float hullRatio = (float)hull.Current / math.max((float)hull.Max, 0.01f);
                        var distance = math.sqrt(nearestResults[i].DistanceSq);

                        var candidate = new TargetCandidate
                        {
                            Entity = targetEntity,
                            Distance = distance,
                            ThreatLevel = (half)0.5f,
                            HullRatio = (half)hullRatio,
                            Value = (half)0.5f,
                            IsThreateningAlly = 0
                        };

                        candidate.Score = TargetPriorityUtility.CalculateScore(
                            profile.ValueRO,
                            candidate,
                            maxRange
                        );

                        if (targetEntity == priority.ValueRO.CurrentTarget)
                        {
                            candidate.Score = TargetPriorityUtility.ApplyEngagementBonus(
                                candidate.Score,
                                priority.ValueRO.EngagementDuration,
                                true
                            );
                        }

                        if ((float)candidate.ThreatLevel < (float)profile.ValueRO.MinThreatThreshold)
                        {
                            continue;
                        }

                        AddCandidate(candidates, candidate, maxCandidates);
                    }
                }
                else
                {
                    for (int i = 0; i < potentialTargets.Length; i++)
                    {
                        Entity targetEntity = potentialTargets[i];

                        // Skip self
                        if (targetEntity == entity)
                        {
                            continue;
                        }

                        float3 targetPosition = targetTransforms[i].Position;
                        float distance = math.distance(myPosition, targetPosition);

                        // Skip out of range
                        if (maxRange > 0f && distance > maxRange)
                        {
                            continue;
                        }

                        var hull = targetHulls[i];
                        float hullRatio = (float)hull.Current / math.max((float)hull.Max, 0.01f);

                        var candidate = new TargetCandidate
                        {
                            Entity = targetEntity,
                            Distance = distance,
                            ThreatLevel = (half)0.5f,
                            HullRatio = (half)hullRatio,
                            Value = (half)0.5f,
                            IsThreateningAlly = 0
                        };

                        candidate.Score = TargetPriorityUtility.CalculateScore(
                            profile.ValueRO,
                            candidate,
                            maxRange
                        );

                        if (targetEntity == priority.ValueRO.CurrentTarget)
                        {
                            candidate.Score = TargetPriorityUtility.ApplyEngagementBonus(
                                candidate.Score,
                                priority.ValueRO.EngagementDuration,
                                true
                            );
                        }

                        if ((float)candidate.ThreatLevel < (float)profile.ValueRO.MinThreatThreshold)
                        {
                            continue;
                        }

                        AddCandidate(candidates, candidate, maxCandidates);
                    }
                }

                // Select best target
                SelectBestTarget(ref priority.ValueRW, candidates, currentTick);
            }

            if (potentialTargets.IsCreated)
            {
                potentialTargets.Dispose();
            }
            if (targetTransforms.IsCreated)
            {
                targetTransforms.Dispose();
            }
            if (targetHulls.IsCreated)
            {
                targetHulls.Dispose();
            }
            if (nearestResults.IsCreated)
            {
                nearestResults.Dispose();
            }
        }

        private static void AddCandidate(
            DynamicBuffer<TargetCandidate> candidates,
            in TargetCandidate candidate,
            int maxCandidates)
        {
            if (maxCandidates <= 0)
            {
                return;
            }

            if (candidates.Length < maxCandidates)
            {
                candidates.Add(candidate);
                return;
            }

            var worstIndex = 0;
            var worstScore = candidates[0].Score;
            for (int i = 1; i < candidates.Length; i++)
            {
                if (candidates[i].Score < worstScore)
                {
                    worstScore = candidates[i].Score;
                    worstIndex = i;
                }
            }

            if (candidate.Score <= worstScore)
            {
                return;
            }

            candidates[worstIndex] = candidate;
        }

        private void SelectBestTarget(
            ref TargetPriority priority,
            DynamicBuffer<TargetCandidate> candidates,
            uint currentTick)
        {
            Entity bestTarget = Entity.Null;
            float bestScore = float.MinValue;
            const float scoreEpsilon = 0.0001f;

            for (int i = 0; i < candidates.Length; i++)
            {
                var candidate = candidates[i];
                if (candidate.Score > bestScore ||
                    (math.abs(candidate.Score - bestScore) <= scoreEpsilon && IsPreferredTarget(candidate.Entity, bestTarget)))
                {
                    bestScore = candidate.Score;
                    bestTarget = candidate.Entity;
                }
            }

            // Check if target changed
            if (bestTarget != priority.CurrentTarget)
            {
                priority.EngagementDuration = 0f;
            }

            priority.CurrentTarget = bestTarget;
            priority.CurrentScore = bestScore;
            priority.LastEvaluationTick = currentTick;
            priority.ForceReevaluate = 0;
        }

        private static bool IsPreferredTarget(Entity candidate, Entity current)
        {
            if (current == Entity.Null)
            {
                return true;
            }

            if (candidate.Index != current.Index)
            {
                return candidate.Index < current.Index;
            }

            return candidate.Version < current.Version;
        }
    }

    /// <summary>
    /// Updates target selection profiles based on alignment changes.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SpatialSystemGroup))]
    [UpdateBefore(typeof(Space4XTargetPrioritySystem))]
    public partial struct Space4XTargetProfileUpdateSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TargetSelectionProfile>();
            state.RequireForUpdate<AlignmentTriplet>();
            state.RequireForUpdate<TimeState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Only update profiles occasionally to save performance
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            var currentTick = timeState.Tick;
            if (currentTick % 60 != 0)
            {
                return;
            }

            foreach (var (profile, alignment, entity) in
                SystemAPI.Query<RefRW<TargetSelectionProfile>, RefRO<AlignmentTriplet>>()
                    .WithEntityAccess())
            {
                // Get captain autonomy if present
                var autonomy = CaptainAutonomy.Tactical;
                if (SystemAPI.HasComponent<CaptainState>(entity))
                {
                    autonomy = SystemAPI.GetComponent<CaptainState>(entity).Autonomy;
                }

                // Only update profile if captain has autonomy to do so
                if (autonomy >= CaptainAutonomy.Tactical)
                {
                    var newProfile = TargetPriorityUtility.ProfileFromAlignment(alignment.ValueRO);

                    // Preserve custom settings but update strategy and weights
                    profile.ValueRW.Strategy = newProfile.Strategy;
                    profile.ValueRW.DistanceWeight = newProfile.DistanceWeight;
                    profile.ValueRW.ThreatWeight = newProfile.ThreatWeight;
                    profile.ValueRW.WeaknessWeight = newProfile.WeaknessWeight;
                    profile.ValueRW.ValueWeight = newProfile.ValueWeight;
                    profile.ValueRW.AllyDefenseWeight = newProfile.AllyDefenseWeight;
                }
            }
        }
    }

    /// <summary>
    /// Tracks damage history for target continuity bonuses.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SpatialSystemGroup))]
    [UpdateAfter(typeof(Space4XTargetPrioritySystem))]
    public partial struct Space4XDamageHistorySystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<DamageHistory>();
            state.RequireForUpdate<TimeState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            var currentTick = timeState.Tick;

            foreach (var damageHistory in SystemAPI.Query<DynamicBuffer<DamageHistory>>())
            {
                // Clean up old damage history (older than 300 ticks)
                for (int i = damageHistory.Length - 1; i >= 0; i--)
                {
                    if (currentTick - damageHistory[i].LastDamageTick > 300)
                    {
                        damageHistory.RemoveAt(i);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Applies hostile species bonuses to target scoring.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SpatialSystemGroup))]
    [UpdateAfter(typeof(Space4XTargetPrioritySystem))]
    public partial struct Space4XHostileSpeciesTargetingSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<HostileSpecies>();
            state.RequireForUpdate<TargetCandidate>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (hostileSpecies, candidates, priority) in
                SystemAPI.Query<DynamicBuffer<HostileSpecies>, DynamicBuffer<TargetCandidate>, RefRW<TargetPriority>>())
            {
                if (hostileSpecies.Length == 0 || candidates.Length == 0)
                {
                    continue;
                }

                // Apply species bonuses to candidates and re-select
                float bestScore = float.MinValue;
                Entity bestTarget = Entity.Null;

                for (int i = 0; i < candidates.Length; i++)
                {
                    var candidate = candidates[i];

                    // Check if target matches any hostile species
                    // Note: Would need SpeciesId component on targets
                    // For now this is a placeholder

                    if (candidate.Score > bestScore)
                    {
                        bestScore = candidate.Score;
                        bestTarget = candidate.Entity;
                    }
                }

                // Update priority if species targeting changed selection
                if (bestTarget != Entity.Null && bestTarget != priority.ValueRO.CurrentTarget)
                {
                    priority.ValueRW.CurrentTarget = bestTarget;
                    priority.ValueRW.CurrentScore = bestScore;
                    priority.ValueRW.EngagementDuration = 0f;
                }
            }
        }
    }

    /// <summary>
    /// Telemetry for target priority system.
    /// </summary>
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    public partial struct Space4XTargetPriorityTelemetrySystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TargetPriority>();
        }

        public void OnUpdate(ref SystemState state)
        {
            int totalEntities = 0;
            int withTargets = 0;
            float avgScore = 0f;
            int defendingAllies = 0;
            int neutralizingThreats = 0;
            int opportunistic = 0;

            foreach (var (priority, profile) in
                SystemAPI.Query<RefRO<TargetPriority>, RefRO<TargetSelectionProfile>>())
            {
                totalEntities++;

                if (priority.ValueRO.CurrentTarget != Entity.Null)
                {
                    withTargets++;
                    avgScore += priority.ValueRO.CurrentScore;
                }

                switch (profile.ValueRO.Strategy)
                {
                    case TargetStrategy.DefendAllies:
                        defendingAllies++;
                        break;
                    case TargetStrategy.NeutralizeThreats:
                        neutralizingThreats++;
                        break;
                    case TargetStrategy.OpportunisticNearest:
                    case TargetStrategy.OpportunisticWeakest:
                        opportunistic++;
                        break;
                }
            }

            if (withTargets > 0)
            {
                avgScore /= withTargets;
            }

            // Would emit to telemetry stream
            // UnityEngine.Debug.Log($"[Target Priority] Entities: {totalEntities}, WithTargets: {withTargets}, AvgScore: {avgScore:F2}, DefendAllies: {defendingAllies}, Neutralize: {neutralizingThreats}, Opportunistic: {opportunistic}");
        }
    }
}

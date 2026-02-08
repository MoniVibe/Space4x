using PureDOTS.Runtime.Components;
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
        private BufferLookup<AffiliationTag> _affiliationLookup;
        private ComponentLookup<Carrier> _carrierLookup;
        private ComponentLookup<Space4XFaction> _factionLookup;
        private BufferLookup<DiplomaticStatusEntry> _diplomaticStatusLookup;
        private BufferLookup<FactionRelationEntry> _factionRelationLookup;
        private uint _lastTick;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TargetPriority>();
            state.RequireForUpdate<TargetSelectionProfile>();
            state.RequireForUpdate<TimeState>();
            _lastTick = 0;
            _stanceLookup = state.GetComponentLookup<VesselStanceComponent>(true);
            _patrolStanceLookup = state.GetComponentLookup<PatrolStance>(true);
            _affiliationLookup = state.GetBufferLookup<AffiliationTag>(true);
            _carrierLookup = state.GetComponentLookup<Carrier>(true);
            _factionLookup = state.GetComponentLookup<Space4XFaction>(true);
            _diplomaticStatusLookup = state.GetBufferLookup<DiplomaticStatusEntry>(true);
            _factionRelationLookup = state.GetBufferLookup<FactionRelationEntry>(true);

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
            _affiliationLookup.Update(ref state);
            _carrierLookup.Update(ref state);
            _factionLookup.Update(ref state);
            _diplomaticStatusLookup.Update(ref state);
            _factionRelationLookup.Update(ref state);

            var stanceConfig = Space4XStanceTuningConfig.Default;
            if (SystemAPI.TryGetSingleton<Space4XStanceTuningConfig>(out var stanceConfigSingleton))
            {
                stanceConfig = stanceConfigSingleton;
            }

            // Get all potential targets; relation filtering happens per-entity during scoring.
            var potentialTargets = _potentialTargetsQuery.ToEntityArray(Allocator.Temp);
            var targetTransforms = _potentialTargetsQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            var targetHulls = _potentialTargetsQuery.ToComponentDataArray<HullIntegrity>(Allocator.Temp);

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
                                      currentTick - priority.ValueRO.LastEvaluationTick >= 10;

                if (!shouldEvaluate)
                {
                    continue;
                }

                candidates.Clear();

                // Score each potential target
                float maxRange = profile.ValueRO.MaxEngagementRange > 0
                    ? profile.ValueRO.MaxEngagementRange
                    : 10000f;

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
                var selfFaction = ResolveFactionEntity(entity);

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

                    var targetFaction = ResolveFactionEntity(targetEntity);
                    bool isFriendly = false;
                    bool isHostile = false;
                    sbyte relationScore = 0;
                    if (selfFaction != Entity.Null && targetFaction != Entity.Null)
                    {
                        if (selfFaction == targetFaction)
                        {
                            isFriendly = true;
                        }
                        else if (TryResolveRelationScore(selfFaction, targetFaction, out relationScore, out var relationStance))
                        {
                            isFriendly = IsFriendlyRelation(relationScore, relationStance);
                            isHostile = IsHostileRelation(relationScore, relationStance);
                        }
                    }

                    if (isFriendly)
                    {
                        continue;
                    }

                    // Create candidate
                    var candidate = new TargetCandidate
                    {
                        Entity = targetEntity,
                        Distance = distance,
                        ThreatLevel = (half)0.5f, // TODO: Get actual threat level
                        HullRatio = (half)hullRatio,
                        Value = (half)0.5f, // TODO: Get actual value
                        IsThreateningAlly = (byte)(isHostile ? 1 : 0)
                    };

                    // Calculate score
                    candidate.Score = TargetPriorityUtility.CalculateScore(
                        profile.ValueRO,
                        candidate,
                        maxRange
                    );

                    if ((profile.ValueRO.EnabledFactors & TargetFactors.FactionRelation) != 0 && (relationScore != 0 || isHostile))
                    {
                        var relationBias = ResolveRelationBias(relationScore);
                        var relationWeight = math.lerp(0.2f, 0.6f, math.saturate((float)profile.ValueRO.ThreatWeight));
                        candidate.Score += relationBias * relationWeight;
                        if (isHostile)
                        {
                            candidate.Score += relationWeight * 0.2f;
                        }
                    }

                    // Apply engagement bonus if this is current target
                    if (targetEntity == priority.ValueRO.CurrentTarget)
                    {
                        candidate.Score = TargetPriorityUtility.ApplyEngagementBonus(
                            candidate.Score,
                            priority.ValueRO.EngagementDuration,
                            true
                        );
                    }

                    // Skip below threshold
                    if ((float)candidate.ThreatLevel < (float)profile.ValueRO.MinThreatThreshold)
                    {
                        continue;
                    }

                    candidates.Add(candidate);
                }

                // Select best target
                SelectBestTarget(ref priority.ValueRW, candidates, currentTick);
            }

            potentialTargets.Dispose();
            targetTransforms.Dispose();
            targetHulls.Dispose();
        }

        private Entity ResolveFactionEntity(Entity entity)
        {
            if (_affiliationLookup.HasBuffer(entity))
            {
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

                if (fallback != Entity.Null && _affiliationLookup.HasBuffer(fallback))
                {
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
                }

                if (fallback != Entity.Null)
                {
                    return fallback;
                }
            }

            if (_carrierLookup.HasComponent(entity))
            {
                var carrier = _carrierLookup[entity];
                if (carrier.AffiliationEntity != Entity.Null)
                {
                    return carrier.AffiliationEntity;
                }
            }

            return Entity.Null;
        }

        private bool TryResolveRelationScore(Entity selfFaction, Entity targetFaction, out sbyte score, out DiplomaticStance stance)
        {
            score = 0;
            stance = DiplomaticStance.Neutral;

            if (selfFaction == Entity.Null || targetFaction == Entity.Null)
            {
                return false;
            }

            if (selfFaction == targetFaction)
            {
                score = 100;
                stance = DiplomaticStance.Allied;
                return true;
            }

            if (!_factionLookup.HasComponent(targetFaction))
            {
                return false;
            }

            ushort targetFactionId = _factionLookup[targetFaction].FactionId;

            if (_diplomaticStatusLookup.HasBuffer(selfFaction))
            {
                var statuses = _diplomaticStatusLookup[selfFaction];
                for (int i = 0; i < statuses.Length; i++)
                {
                    var status = statuses[i].Status;
                    if (status.OtherFactionId == targetFactionId)
                    {
                        score = status.RelationScore;
                        stance = status.Stance;
                        return true;
                    }
                }
            }

            if (_factionRelationLookup.HasBuffer(selfFaction))
            {
                var relations = _factionRelationLookup[selfFaction];
                for (int i = 0; i < relations.Length; i++)
                {
                    var relation = relations[i].Relation;
                    if (relation.OtherFactionId == targetFactionId)
                    {
                        score = relation.Score;
                        stance = DiplomacyMath.DetermineStance(score, DiplomaticStance.Neutral);
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool IsFriendlyRelation(sbyte relationScore, DiplomaticStance stance)
        {
            if (stance == DiplomaticStance.Allied ||
                stance == DiplomaticStance.Friendly ||
                stance == DiplomaticStance.Cordial ||
                stance == DiplomaticStance.Vassal ||
                stance == DiplomaticStance.Overlord)
            {
                return true;
            }

            return relationScore >= 25;
        }

        private static bool IsHostileRelation(sbyte relationScore, DiplomaticStance stance)
        {
            if (stance == DiplomaticStance.War || stance == DiplomaticStance.Hostile)
            {
                return true;
            }

            return relationScore <= -25;
        }

        private static float ResolveRelationBias(sbyte relationScore)
        {
            var relationNorm = math.clamp(relationScore / 100f, -1f, 1f);
            return -relationNorm;
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
        private ComponentLookup<CaptainState> _captainLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TargetSelectionProfile>();
            state.RequireForUpdate<AlignmentTriplet>();
            state.RequireForUpdate<TimeState>();
            _captainLookup = state.GetComponentLookup<CaptainState>(true);
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

            _captainLookup.Update(ref state);

            foreach (var (profile, alignment, entity) in
                SystemAPI.Query<RefRW<TargetSelectionProfile>, RefRO<AlignmentTriplet>>()
                    .WithEntityAccess())
            {
                // Get captain autonomy if present
                var autonomy = CaptainAutonomy.Tactical;
                if (_captainLookup.HasComponent(entity))
                {
                    autonomy = _captainLookup[entity].Autonomy;
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

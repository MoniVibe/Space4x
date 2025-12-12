using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using SpatialSystemGroup = PureDOTS.Systems.SpatialSystemGroup;

namespace Space4X.Registry
{
    /// <summary>
    /// Scores potential targets and selects based on alignment-driven profile.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SpatialSystemGroup))]
    [UpdateBefore(typeof(Space4XCaptainOrderSystem))]
    public partial struct Space4XTargetPrioritySystem : ISystem
    {
        private EntityQuery _potentialTargetsQuery;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TargetPriority>();
            state.RequireForUpdate<TargetSelectionProfile>();

            // Query for potential hostile targets
            _potentialTargetsQuery = state.GetEntityQuery(
                ComponentType.ReadOnly<LocalTransform>(),
                ComponentType.ReadOnly<HullIntegrity>()
            );
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var currentTick = (uint)SystemAPI.Time.ElapsedTime;

            // Get all potential targets (simplified - in reality would filter by faction/hostility)
            var potentialTargets = _potentialTargetsQuery.ToEntityArray(Allocator.Temp);
            var targetTransforms = _potentialTargetsQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            var targetHulls = _potentialTargetsQuery.ToComponentDataArray<HullIntegrity>(Allocator.Temp);

            foreach (var (priority, profile, transform, candidates, entity) in
                SystemAPI.Query<RefRW<TargetPriority>, RefRO<TargetSelectionProfile>, RefRO<LocalTransform>, DynamicBuffer<TargetCandidate>>()
                    .WithEntityAccess())
            {
                // Check if reevaluation is needed (every 10 ticks or forced)
                bool shouldEvaluate = priority.ValueRO.ForceReevaluate == 1 ||
                                      currentTick - priority.ValueRO.LastEvaluationTick >= 10;

                if (!shouldEvaluate)
                {
                    // Update engagement duration
                    if (priority.ValueRO.CurrentTarget != Entity.Null)
                    {
                        priority.ValueRW.EngagementDuration += SystemAPI.Time.DeltaTime;
                    }
                    continue;
                }

                candidates.Clear();

                // Score each potential target
                float maxRange = profile.ValueRO.MaxEngagementRange > 0 ?
                    profile.ValueRO.MaxEngagementRange : 10000f;

                float3 myPosition = transform.ValueRO.Position;

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
                    if (profile.ValueRO.MaxEngagementRange > 0 && distance > profile.ValueRO.MaxEngagementRange)
                    {
                        continue;
                    }

                    var hull = targetHulls[i];
                    float hullRatio = (float)hull.Current / math.max((float)hull.Max, 0.01f);

                    // Create candidate
                    var candidate = new TargetCandidate
                    {
                        Entity = targetEntity,
                        Distance = distance,
                        ThreatLevel = (half)0.5f, // TODO: Get actual threat level
                        HullRatio = (half)hullRatio,
                        Value = (half)0.5f, // TODO: Get actual value
                        IsThreateningAlly = 0 // TODO: Check ally threats
                    };

                    // Calculate score
                    candidate.Score = TargetPriorityUtility.CalculateScore(
                        profile.ValueRO,
                        candidate,
                        maxRange
                    );

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

        private void SelectBestTarget(
            ref TargetPriority priority,
            DynamicBuffer<TargetCandidate> candidates,
            uint currentTick)
        {
            Entity bestTarget = Entity.Null;
            float bestScore = float.MinValue;

            for (int i = 0; i < candidates.Length; i++)
            {
                if (candidates[i].Score > bestScore)
                {
                    bestScore = candidates[i].Score;
                    bestTarget = candidates[i].Entity;
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
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Only update profiles occasionally to save performance
            var currentTick = (uint)SystemAPI.Time.ElapsedTime;
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
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var currentTick = (uint)SystemAPI.Time.ElapsedTime;

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


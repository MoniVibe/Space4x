using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    using Debug = UnityEngine.Debug;

    
    /// <summary>
    /// Updates patriotism modifiers and overall patriotism score.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct Space4XPatriotismSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PatriotismProfile>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (profile, belongings, modifiers) in
                SystemAPI.Query<RefRW<PatriotismProfile>, DynamicBuffer<BelongingEntry>, RefRW<PatriotismModifiers>>())
            {
                // Calculate overall patriotism
                float overall = PatriotismHelpers.CalculateOverallPatriotism(belongings, profile.ValueRO);
                profile.ValueRW.OverallPatriotism = (half)overall;

                // Find primary tier
                profile.ValueRW.PrimaryTier = PatriotismHelpers.GetPrimaryTier(belongings);

                // Update modifiers based on patriotism levels
                byte familyLoyalty = PatriotismHelpers.GetLoyaltyToTier(belongings, BelongingTier.Family);
                byte factionLoyalty = PatriotismHelpers.GetLoyaltyToTier(belongings, BelongingTier.Faction);
                byte speciesLoyalty = PatriotismHelpers.GetLoyaltyToTier(belongings, BelongingTier.Species);
                byte ideologyLoyalty = PatriotismHelpers.GetLoyaltyToTier(belongings, BelongingTier.Ideology);

                // Home territory morale bonus scales with faction/empire loyalty
                modifiers.ValueRW.HomeTerritoryMoraleBonus = (half)(factionLoyalty * 0.002f);

                // Propaganda resistance from loyalty
                modifiers.ValueRW.PropagandaResistance = (half)(overall * 0.5f);

                // Sacrifice willingness from ideology + natural loyalty
                float sacrificeBase = profile.ValueRO.NaturalLoyalty * 0.005f;
                sacrificeBase += ideologyLoyalty * 0.003f;
                modifiers.ValueRW.SacrificeWillingness = (half)sacrificeBase;

                // Family combat bonus
                modifiers.ValueRW.FamilyCombatBonus = (half)(familyLoyalty * 0.002f);

                // Species conflict penalty
                modifiers.ValueRW.SpeciesConflictPenalty = (half)(speciesLoyalty * 0.002f);

                // Ideology diplomacy bonus
                modifiers.ValueRW.IdeologyDiplomacyBonus = (half)(ideologyLoyalty * 0.003f);
            }
        }
    }

    /// <summary>
    /// Detects and creates patriotism conflicts when loyalties clash.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XPatriotismSystem))]
    public partial struct Space4XPatriotismConflictDetectionSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PatriotismTestRequest>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var currentTick = (uint)SystemAPI.Time.ElapsedTime;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (request, profile, belongings, entity) in
                SystemAPI.Query<RefRO<PatriotismTestRequest>, RefRW<PatriotismProfile>, DynamicBuffer<BelongingEntry>>()
                    .WithEntityAccess())
            {
                var testedTier = request.ValueRO.TestedTier;
                byte testedLoyalty = PatriotismHelpers.GetLoyaltyToTier(belongings, testedTier);

                // Check for conflicts with other tiers
                bool hasConflict = false;
                BelongingTier conflictingTier = BelongingTier.None;

                for (int i = 0; i < belongings.Length; i++)
                {
                    var entry = belongings[i];
                    if (entry.Tier == testedTier) continue;

                    // High loyalty to another tier that can conflict
                    if (entry.Loyalty > 60 && PatriotismHelpers.CanTiersConflict(entry.Tier, testedTier))
                    {
                        // Demand severity affects conflict detection
                        float conflictChance = (entry.Loyalty - 60) * 0.02f * (float)request.ValueRO.DemandSeverity;
                        if (conflictChance > 0.3f)
                        {
                            hasConflict = true;
                            conflictingTier = entry.Tier;
                            break;
                        }
                    }
                }

                if (hasConflict)
                {
                    profile.ValueRW.HasConflict = 1;

                    // Create conflict component
                    ecb.AddComponent(entity, new PatriotismConflict
                    {
                        TierA = testedTier,
                        TierB = conflictingTier,
                        DemandingEntityA = request.ValueRO.Demander,
                        DemandingEntityB = Entity.Null,
                        Type = DetermineConflictType(testedTier, conflictingTier),
                        Severity = request.ValueRO.DemandSeverity,
                        StartTick = currentTick,
                        DeadlineTick = currentTick + 1000 // Time to resolve
                    });
                }
                else
                {
                    // No conflict - process compliance
                    bool complies = testedLoyalty >= 40 ||
                        (testedLoyalty >= 20 && profile.ValueRO.NaturalLoyalty > 50);

                    ecb.AddComponent(entity, new PatriotismTestResult
                    {
                        Complied = (byte)(complies ? 1 : 0),
                        LoyaltyChange = (sbyte)(complies ? 2 : -5),
                        TriggeredConflict = 0,
                        ResolvedTick = currentTick
                    });
                }

                ecb.RemoveComponent<PatriotismTestRequest>(entity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private static PatriotismConflictType DetermineConflictType(BelongingTier tierA, BelongingTier tierB)
        {
            if (tierA == BelongingTier.Family || tierB == BelongingTier.Family)
                return PatriotismConflictType.FamilyVsDuty;
            if (tierA == BelongingTier.Guild || tierB == BelongingTier.Guild)
                return PatriotismConflictType.GuildVsFaction;
            if ((tierA == BelongingTier.Colony && tierB == BelongingTier.Empire) ||
                (tierA == BelongingTier.Empire && tierB == BelongingTier.Colony))
                return PatriotismConflictType.ColonyVsEmpire;
            if (tierA == BelongingTier.Ideology || tierB == BelongingTier.Ideology)
                return PatriotismConflictType.FactionVsIdeology;
            if (tierA == BelongingTier.Species || tierB == BelongingTier.Species)
                return PatriotismConflictType.SpeciesVsFaction;
            if (tierA == BelongingTier.Dynasty || tierB == BelongingTier.Dynasty)
                return PatriotismConflictType.DynastyVsFaction;

            return PatriotismConflictType.None;
        }
    }

    /// <summary>
    /// Resolves patriotism conflicts based on profile and circumstances.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XPatriotismConflictDetectionSystem))]
    public partial struct Space4XPatriotismConflictResolutionSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PatriotismConflict>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var currentTick = (uint)SystemAPI.Time.ElapsedTime;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (conflict, profile, belongings, resolutions, entity) in
                SystemAPI.Query<RefRO<PatriotismConflict>, RefRW<PatriotismProfile>, DynamicBuffer<BelongingEntry>, DynamicBuffer<PatriotismResolutionEvent>>()
                    .WithEntityAccess())
            {
                var belongingsBuffer = belongings;
                var resolutionBuffer = resolutions;
                // Check if deadline reached
                if (currentTick < conflict.ValueRO.DeadlineTick)
                {
                    continue;
                }

                // Resolve conflict - entity must choose
                var chosenTier = PatriotismHelpers.PredictConflictChoice(
                    belongings, profile.ValueRO, conflict.ValueRO.TierA, conflict.ValueRO.TierB);

                var betrayedTier = chosenTier == conflict.ValueRO.TierA
                    ? conflict.ValueRO.TierB
                    : conflict.ValueRO.TierA;

                // Record resolution
                resolutionBuffer.Add(new PatriotismResolutionEvent
                {
                    ConflictType = conflict.ValueRO.Type,
                    ChosenTier = chosenTier,
                    BetrayedTier = betrayedTier,
                    ResolvedTick = currentTick,
                    ConsequenceSeverity = conflict.ValueRO.Severity
                });

                // Update loyalties
                for (int i = 0; i < belongingsBuffer.Length; i++)
                {
                    var entry = belongingsBuffer[i];

                    if (entry.Tier == chosenTier)
                    {
                        // Loyalty increases to chosen tier
                        entry.Loyalty = (byte)math.min(100, entry.Loyalty + 10);
                        belongingsBuffer[i] = entry;
                    }
                    else if (entry.Tier == betrayedTier)
                    {
                        // Loyalty decreases to betrayed tier
                        entry.Loyalty = (byte)math.max(0, entry.Loyalty - 20);
                        belongingsBuffer[i] = entry;
                    }
                }

                profile.ValueRW.HasConflict = 0;
                ecb.RemoveComponent<PatriotismConflict>(entity);

                // Add test result
                ecb.AddComponent(entity, new PatriotismTestResult
                {
                    Complied = (byte)(chosenTier == conflict.ValueRO.TierA ? 1 : 0),
                    LoyaltyChange = 10,
                    TriggeredConflict = 1,
                    ResolvedTick = currentTick
                });
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }

    /// <summary>
    /// Decays loyalty over time without reinforcement.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct Space4XPatriotismDecaySystem : ISystem
    {
        private uint _lastDecayTick;
        private const uint DecayInterval = 100; // Decay every N ticks

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BelongingEntry>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var currentTick = (uint)SystemAPI.Time.ElapsedTime;

            if (currentTick - _lastDecayTick < DecayInterval)
            {
                return;
            }
            _lastDecayTick = currentTick;

            foreach (var (belongings, profile) in SystemAPI.Query<DynamicBuffer<BelongingEntry>, RefRO<PatriotismProfile>>())
            {
                var belongingsBuffer = belongings;
                // Higher natural loyalty = slower decay
                float decayRate = 1f - (profile.ValueRO.NaturalLoyalty * 0.005f);

                for (int i = 0; i < belongingsBuffer.Length; i++)
                {
                    var entry = belongingsBuffer[i];

                    // Skip primary identity (decays slower)
                    if (entry.IsPrimaryIdentity != 0)
                    {
                        if (entry.Loyalty > 30)
                        {
                            entry.Loyalty = (byte)math.max(30, entry.Loyalty - 1);
                            belongingsBuffer[i] = entry;
                        }
                        continue;
                    }

                    // Regular decay
                    if (entry.Loyalty > 20)
                    {
                        int decay = (int)(decayRate * 2);
                        entry.Loyalty = (byte)math.max(20, entry.Loyalty - decay);
                        belongingsBuffer[i] = entry;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Links patriotism to existing AffiliationTag loyalty for compatibility.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XPatriotismSystem))]
    public partial struct Space4XPatriotismAffiliationBridgeSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PatriotismProfile>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (belongings, affiliations) in
                SystemAPI.Query<DynamicBuffer<BelongingEntry>, DynamicBuffer<AffiliationTag>>())
            {
                var belongingsBuffer = belongings;
                var affiliationsBuffer = affiliations;
                // Sync faction/fleet loyalty to AffiliationTag
                for (int i = 0; i < affiliationsBuffer.Length; i++)
                {
                    var affiliation = affiliationsBuffer[i];

                    BelongingTier matchingTier = affiliation.Type switch
                    {
                        AffiliationType.Empire => BelongingTier.Empire,
                        AffiliationType.Faction => BelongingTier.Faction,
                        AffiliationType.Fleet => BelongingTier.Faction,
                        AffiliationType.Colony => BelongingTier.Colony,
                        AffiliationType.Guild => BelongingTier.Guild,
                        AffiliationType.Corporation => BelongingTier.Guild,
                        _ => BelongingTier.None
                    };

                    if (matchingTier != BelongingTier.None)
                    {
                        byte belonging = PatriotismHelpers.GetLoyaltyToTier(belongingsBuffer, matchingTier);
                        affiliation.Loyalty = (half)(belonging / 100f);
                        affiliationsBuffer[i] = affiliation;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Telemetry for patriotism system.
    /// </summary>
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    public partial struct Space4XPatriotismTelemetrySystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PatriotismProfile>();
        }

        public void OnUpdate(ref SystemState state)
        {
            int totalEntities = 0;
            float avgPatriotism = 0;
            int conflicts = 0;
            int familyFirst = 0;
            int factionFirst = 0;
            int zealots = 0;

            foreach (var profile in SystemAPI.Query<RefRO<PatriotismProfile>>())
            {
                totalEntities++;
                avgPatriotism += (float)profile.ValueRO.OverallPatriotism;

                if (profile.ValueRO.HasConflict != 0)
                    conflicts++;

                switch (profile.ValueRO.PrimaryTier)
                {
                    case BelongingTier.Family:
                    case BelongingTier.Dynasty:
                        familyFirst++;
                        break;
                    case BelongingTier.Faction:
                    case BelongingTier.Empire:
                        factionFirst++;
                        break;
                    case BelongingTier.Ideology:
                        zealots++;
                        break;
                }
            }

            if (totalEntities > 0)
            {
                avgPatriotism /= totalEntities;
            }

            // Would emit to telemetry stream
            // UnityEngine.Debug.Log($"[Patriotism] Entities: {totalEntities}, AvgPatriotism: {avgPatriotism:P0}, Conflicts: {conflicts}, FamilyFirst: {familyFirst}, FactionFirst: {factionFirst}, Zealots: {zealots}");
        }
    }
}


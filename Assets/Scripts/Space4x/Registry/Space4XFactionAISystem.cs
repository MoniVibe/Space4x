using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    /// <summary>
    /// Updates faction goals based on current state.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct Space4XFactionGoalSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XFaction>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var currentTick = (uint)SystemAPI.Time.ElapsedTime;

            foreach (var (faction, territory, resources, goals, entity) in
                SystemAPI.Query<RefRO<Space4XFaction>, RefRO<Space4XTerritoryControl>, RefRO<FactionResources>, DynamicBuffer<Space4XFactionGoal>>()
                    .WithEntityAccess())
            {
                // Remove completed/expired goals
                for (int i = goals.Length - 1; i >= 0; i--)
                {
                    var goal = goals[i];
                    if ((float)goal.Progress >= 1f ||
                        (goal.DeadlineTick > 0 && currentTick > goal.DeadlineTick))
                    {
                        goals.RemoveAt(i);
                    }
                }

                // Evaluate need for new goals based on faction type
                switch (faction.ValueRO.Type)
                {
                    case FactionType.Empire:
                    case FactionType.Corporation:
                    case FactionType.Player:
                        EvaluateEmpireGoals(faction.ValueRO, territory.ValueRO, resources.ValueRO, goals, currentTick);
                        break;

                    case FactionType.Pirate:
                        // Handled by pirate-specific system
                        break;

                    case FactionType.Fauna:
                        // Handled by fauna-specific system
                        break;

                    case FactionType.Guild:
                        EvaluateGuildGoals(faction.ValueRO, resources.ValueRO, goals, currentTick);
                        break;
                }

                // Sort by priority
                SortGoalsByPriority(goals);
            }
        }

        private void EvaluateEmpireGoals(
            in Space4XFaction faction,
            in Space4XTerritoryControl territory,
            in FactionResources resources,
            DynamicBuffer<Space4XFactionGoal> goals,
            uint currentTick)
        {
            // Defense is always a consideration
            if (!HasGoalType(goals, FactionGoalType.DefendTerritory) && territory.ContestedSectors > 0)
            {
                AddGoal(goals, FactionGoalType.DefendTerritory,
                    FactionAIUtility.CalculateGoalPriority(FactionGoalType.DefendTerritory, faction),
                    currentTick);
            }

            // Expansion if resources allow
            if (!HasGoalType(goals, FactionGoalType.ColonizeSystem) &&
                (float)faction.ExpansionDrive > 0.5f &&
                resources.Credits > 1000f)
            {
                AddGoal(goals, FactionGoalType.ColonizeSystem,
                    FactionAIUtility.CalculateGoalPriority(FactionGoalType.ColonizeSystem, faction),
                    currentTick);
            }

            // Trade route establishment
            if (!HasGoalType(goals, FactionGoalType.EstablishRoute) &&
                (float)faction.TradeFocus > 0.4f &&
                territory.ColonyCount > 1)
            {
                AddGoal(goals, FactionGoalType.EstablishRoute,
                    FactionAIUtility.CalculateGoalPriority(FactionGoalType.EstablishRoute, faction),
                    currentTick);
            }

            // Research priority
            if (!HasGoalType(goals, FactionGoalType.ResearchTech) &&
                (float)faction.ResearchFocus > 0.3f)
            {
                AddGoal(goals, FactionGoalType.ResearchTech,
                    FactionAIUtility.CalculateGoalPriority(FactionGoalType.ResearchTech, faction),
                    currentTick);
            }
        }

        private void EvaluateGuildGoals(
            in Space4XFaction faction,
            in FactionResources resources,
            DynamicBuffer<Space4XFactionGoal> goals,
            uint currentTick)
        {
            // Guilds prioritize trade above all
            if (!HasGoalType(goals, FactionGoalType.SecureTrade))
            {
                AddGoal(goals, FactionGoalType.SecureTrade, 10, currentTick);
            }

            // Establish profitable routes
            if (!HasGoalType(goals, FactionGoalType.EstablishRoute) && resources.Credits > 500f)
            {
                AddGoal(goals, FactionGoalType.EstablishRoute, 20, currentTick);
            }

            // Build infrastructure
            if (!HasGoalType(goals, FactionGoalType.BuildInfrastructure) && resources.Materials > 1000f)
            {
                AddGoal(goals, FactionGoalType.BuildInfrastructure, 30, currentTick);
            }
        }

        private bool HasGoalType(DynamicBuffer<Space4XFactionGoal> goals, FactionGoalType type)
        {
            for (int i = 0; i < goals.Length; i++)
            {
                if (goals[i].Type == type) return true;
            }
            return false;
        }

        private void AddGoal(DynamicBuffer<Space4XFactionGoal> goals, FactionGoalType type, byte priority, uint tick)
        {
            if (goals.Length < goals.Capacity)
            {
                goals.Add(new Space4XFactionGoal
                {
                    Type = type,
                    Priority = priority,
                    Progress = (half)0f,
                    CreatedTick = tick
                });
            }
        }

        private void SortGoalsByPriority(DynamicBuffer<Space4XFactionGoal> goals)
        {
            // Simple bubble sort for small buffer
            for (int i = 0; i < goals.Length - 1; i++)
            {
                for (int j = 0; j < goals.Length - i - 1; j++)
                {
                    if (goals[j].Priority > goals[j + 1].Priority)
                    {
                        var temp = goals[j];
                        goals[j] = goals[j + 1];
                        goals[j + 1] = temp;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Handles pirate-specific AI behavior.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct Space4XPirateAISystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PirateBehavior>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var currentTick = (uint)SystemAPI.Time.ElapsedTime;

            foreach (var (pirate, faction, goals, entity) in
                SystemAPI.Query<RefRW<PirateBehavior>, RefRO<Space4XFaction>, DynamicBuffer<Space4XFactionGoal>>()
                    .WithEntityAccess())
            {
                // Update notoriety decay
                if (pirate.ValueRO.Notoriety > 0)
                {
                    pirate.ValueRW.Notoriety = math.max(0, pirate.ValueRO.Notoriety - 0.001f);
                }

                // Evaluate pirate-specific goals
                bool hasRaidGoal = false;
                bool hasHideoutGoal = false;

                for (int i = 0; i < goals.Length; i++)
                {
                    if (goals[i].Type == FactionGoalType.RaidTarget || goals[i].Type == FactionGoalType.Plunder)
                        hasRaidGoal = true;
                    if (goals[i].Type == FactionGoalType.Hideout)
                        hasHideoutGoal = true;
                }

                // High notoriety = lay low
                if (pirate.ValueRO.Notoriety > 50f && !hasHideoutGoal)
                {
                    if (goals.Length < goals.Capacity)
                    {
                        goals.Add(new Space4XFactionGoal
                        {
                            Type = FactionGoalType.Hideout,
                            Priority = 5,
                            CreatedTick = currentTick
                        });
                    }
                }
                // Low notoriety = raid
                else if (!hasRaidGoal && pirate.ValueRO.Notoriety < 30f)
                {
                    FactionGoalType raidType = (float)faction.ValueRO.Aggression > 0.6f
                        ? FactionGoalType.Plunder
                        : FactionGoalType.RaidTarget;

                    if (goals.Length < goals.Capacity)
                    {
                        goals.Add(new Space4XFactionGoal
                        {
                            Type = raidType,
                            Priority = 15,
                            CreatedTick = currentTick
                        });
                    }
                }

                // Recruitment when low on crew
                bool hasRecruitGoal = false;
                for (int i = 0; i < goals.Length; i++)
                {
                    if (goals[i].Type == FactionGoalType.Recruit) hasRecruitGoal = true;
                }

                if (!hasRecruitGoal && goals.Length < goals.Capacity)
                {
                    goals.Add(new Space4XFactionGoal
                    {
                        Type = FactionGoalType.Recruit,
                        Priority = 40,
                        CreatedTick = currentTick
                    });
                }
            }
        }
    }

    /// <summary>
    /// Handles fauna-specific AI behavior.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct Space4XFaunaAISystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<FaunaBehavior>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var currentTick = (uint)SystemAPI.Time.ElapsedTime;

            foreach (var (fauna, goals, entity) in
                SystemAPI.Query<RefRW<FaunaBehavior>, DynamicBuffer<Space4XFactionGoal>>()
                    .WithEntityAccess())
            {
                // Update hunger
                fauna.ValueRW.Hunger = (half)math.min(1f, (float)fauna.ValueRO.Hunger + 0.0001f);

                // Update breeding readiness if not hungry
                if ((float)fauna.ValueRO.Hunger < 0.3f)
                {
                    fauna.ValueRW.BreedingReadiness = (half)math.min(1f, (float)fauna.ValueRO.BreedingReadiness + 0.00005f);
                }

                // Determine current need (simplified - would use spatial queries for threats)
                float nearbyThreat = 0f; // Would be calculated from nearby hostile entities
                FaunaNeed need = FactionAIUtility.DetermineFaunaNeed(fauna.ValueRO, nearbyThreat);
                fauna.ValueRW.CurrentNeed = need;

                // Clear old fauna goals
                for (int i = goals.Length - 1; i >= 0; i--)
                {
                    var goal = goals[i];
                    if (goal.Type >= FactionGoalType.Feed && goal.Type <= FactionGoalType.DefendTerritory_Fauna)
                    {
                        goals.RemoveAt(i);
                    }
                }

                // Add goal based on need
                FactionGoalType goalType = need switch
                {
                    FaunaNeed.Feed => FactionGoalType.Feed,
                    FaunaNeed.Breed => FactionGoalType.Breed,
                    FaunaNeed.Migrate => FactionGoalType.Migrate,
                    FaunaNeed.Defend => FactionGoalType.DefendTerritory_Fauna,
                    FaunaNeed.Flee => FactionGoalType.Migrate, // Flee uses migrate mechanics
                    _ => FactionGoalType.None
                };

                if (goalType != FactionGoalType.None && goals.Length < goals.Capacity)
                {
                    goals.Add(new Space4XFactionGoal
                    {
                        Type = goalType,
                        Priority = 1, // Instinctual needs are high priority
                        CreatedTick = currentTick
                    });
                }
            }
        }
    }

    /// <summary>
    /// Updates faction relations based on interactions.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct Space4XFactionRelationSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XFaction>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (faction, relations, entity) in
                SystemAPI.Query<RefRO<Space4XFaction>, DynamicBuffer<FactionRelationEntry>>()
                    .WithEntityAccess())
            {
                var relationsBuffer = relations;

                for (int i = 0; i < relationsBuffer.Length; i++)
                {
                    var entry = relationsBuffer[i];
                    var relation = entry.Relation;

                    // Natural relation drift toward neutral
                    if (relation.Score > 0)
                    {
                        relation.Score = (sbyte)math.max(relation.Score - 1, 0);
                    }
                    else if (relation.Score < 0)
                    {
                        relation.Score = (sbyte)math.min(relation.Score + 1, 0);
                    }

                    // Trust decay
                    if ((float)relation.Trust > 0)
                    {
                        relation.Trust = (half)math.max(0, (float)relation.Trust - 0.001f);
                    }

                    // Fear decay
                    if ((float)relation.Fear > 0)
                    {
                        relation.Fear = (half)math.max(0, (float)relation.Fear - 0.0005f);
                    }

                    // Trade improves relations
                    if (relation.TradeVolume > 0)
                    {
                        float tradeBonus = math.min(relation.TradeVolume * 0.001f, 0.1f);
                        relation.Score = (sbyte)math.clamp(relation.Score + (int)tradeBonus, -100, 100);
                        relation.TradeVolume = 0; // Reset for next tick
                    }

                    // Combat worsens relations
                    if (relation.RecentCombats > 0)
                    {
                        relation.Score = (sbyte)math.clamp(relation.Score - (int)relation.RecentCombats * 5, -100, 100);
                        relation.RecentCombats = 0;
                    }

                    entry.Relation = relation;
                    relationsBuffer[i] = entry;
                }
            }
        }
    }

    /// <summary>
    /// Updates faction territory control metrics.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    public partial struct Space4XTerritoryUpdateSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XTerritoryControl>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var factionLookup = SystemAPI.GetComponentLookup<Space4XFaction>(true);

            // Count controlled entities per faction
            var factionFleetStrength = new NativeHashMap<ushort, float>(16, Allocator.Temp);
            var factionColonyCount = new NativeHashMap<ushort, ushort>(16, Allocator.Temp);

            // Count fleet strength
            foreach (var (hull, affiliations) in SystemAPI.Query<RefRO<HullIntegrity>, DynamicBuffer<AffiliationTag>>())
            {
                for (int i = 0; i < affiliations.Length; i++)
                {
                    var affiliation = affiliations[i];
                    if (affiliation.Target == Entity.Null || !factionLookup.HasComponent(affiliation.Target))
                    {
                        continue;
                    }

                    ushort factionId = factionLookup[affiliation.Target].FactionId;
                    float strength = (float)hull.ValueRO.Max; // Hull as proxy for strength

                    if (factionFleetStrength.TryGetValue(factionId, out float currentStrength))
                    {
                        factionFleetStrength[factionId] = currentStrength + strength;
                    }
                    else
                    {
                        factionFleetStrength.Add(factionId, strength);
                    }
                }
            }

            // Count colonies
            foreach (var (_, affiliations) in SystemAPI.Query<RefRO<Space4XColony>, DynamicBuffer<AffiliationTag>>())
            {
                for (int i = 0; i < affiliations.Length; i++)
                {
                    var affiliation = affiliations[i];
                    if (affiliation.Target == Entity.Null || !factionLookup.HasComponent(affiliation.Target))
                    {
                        continue;
                    }

                    ushort factionId = factionLookup[affiliation.Target].FactionId;

                    if (factionColonyCount.TryGetValue(factionId, out ushort currentCount))
                    {
                        factionColonyCount[factionId] = (ushort)(currentCount + 1);
                    }
                    else
                    {
                        factionColonyCount.Add(factionId, 1);
                    }
                }
            }

            // Update territory control components
            foreach (var (faction, territory) in SystemAPI.Query<RefRO<Space4XFaction>, RefRW<Space4XTerritoryControl>>())
            {
                if (factionFleetStrength.TryGetValue(faction.ValueRO.FactionId, out float strength))
                {
                    territory.ValueRW.FleetStrength = strength;
                }

                if (factionColonyCount.TryGetValue(faction.ValueRO.FactionId, out ushort colonies))
                {
                    territory.ValueRW.ColonyCount = colonies;
                }
            }

            factionFleetStrength.Dispose();
            factionColonyCount.Dispose();
        }
    }

    /// <summary>
    /// Telemetry for faction AI.
    /// </summary>
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    public partial struct Space4XFactionTelemetrySystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XFaction>();
        }

        public void OnUpdate(ref SystemState state)
        {
            int empireCount = 0;
            int pirateCount = 0;
            int faunaCount = 0;
            int guildCount = 0;
            int totalGoals = 0;

            foreach (var (faction, goals) in SystemAPI.Query<RefRO<Space4XFaction>, DynamicBuffer<Space4XFactionGoal>>())
            {
                switch (faction.ValueRO.Type)
                {
                    case FactionType.Empire: empireCount++; break;
                    case FactionType.Pirate: pirateCount++; break;
                    case FactionType.Fauna: faunaCount++; break;
                    case FactionType.Guild: guildCount++; break;
                }
                totalGoals += goals.Length;
            }

            // Would emit to telemetry stream
            // UnityEngine.Debug.Log($"[FactionAI] Empires: {empireCount}, Pirates: {pirateCount}, Fauna: {faunaCount}, Guilds: {guildCount}, Goals: {totalGoals}");
        }
    }
}


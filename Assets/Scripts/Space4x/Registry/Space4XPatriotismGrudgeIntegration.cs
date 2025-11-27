using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    /// <summary>
    /// Integrates patriotism into the mutiny system.
    /// High patriotism to faction reduces mutiny risk.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(Space4XMutinySystem))]
    public partial struct Space4XPatriotismMutinyIntegrationSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PatriotismProfile>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (profile, belongings, affiliations) in
                SystemAPI.Query<RefRO<PatriotismProfile>, DynamicBuffer<BelongingEntry>, DynamicBuffer<AffiliationTag>>())
            {
                var affiliationsBuffer = affiliations;
                // Check for patriotism conflicts that might trigger mutiny consideration
                if (profile.ValueRO.HasConflict != 0)
                {
                    // Entity is in loyalty conflict - affects compliance evaluation
                    // The conflict itself may lead to desertion/independence if faction loses
                }

                // High faction/fleet loyalty reduces mutiny risk
                byte factionLoyalty = PatriotismHelpers.GetLoyaltyToTier(belongings, BelongingTier.Faction);

                // Update affiliation loyalty based on patriotism
                for (int i = 0; i < affiliationsBuffer.Length; i++)
                {
                    var affiliation = affiliationsBuffer[i];
                    if (affiliation.Type == AffiliationType.Fleet || affiliation.Type == AffiliationType.Faction)
                    {
                        // Patriotism loyalty synced to affiliation
                        affiliation.Loyalty = (half)(factionLoyalty / 100f);
                        affiliationsBuffer[i] = affiliation;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Integrates grudges into the diplomacy system.
    /// Grudges create negative relation modifiers.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct Space4XGrudgeDiplomacyIntegrationSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GrudgeModifiers>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var currentTick = (uint)SystemAPI.Time.ElapsedTime;

            // For factions with grudges, apply diplomacy penalties to relation modifiers
            foreach (var (grudgeModifiers, factionGrudges, relationModifiers, entity) in
                SystemAPI.Query<
                    RefRO<GrudgeModifiers>,
                    DynamicBuffer<FactionGrudge>,
                    DynamicBuffer<RelationModifier>>()
                    .WithEntityAccess())
            {
                var relationBuffer = relationModifiers;

                // Add/update relation modifiers for faction grudges
                for (int i = 0; i < factionGrudges.Length; i++)
                {
                    var grudge = factionGrudges[i];
                    if (grudge.Intensity == 0) continue;

                    // Calculate relation penalty (scaled to sbyte range)
                    sbyte penalty = (sbyte)(-grudge.Intensity); // -1 to -100

                    // Check if similar modifier already exists (using InsultReceived as proxy for grudge)
                    bool found = false;
                    for (int j = 0; j < relationBuffer.Length; j++)
                    {
                        var mod = relationBuffer[j];
                        if (mod.Type == RelationModifierType.InsultReceived && mod.RemainingTicks == 0)
                        {
                            mod.ScoreChange = penalty;
                            relationBuffer[j] = mod;
                            found = true;
                            break;
                        }
                    }

                    if (!found && relationBuffer.Length < 16)
                    {
                        relationBuffer.Add(new RelationModifier
                        {
                            Type = RelationModifierType.InsultReceived, // Using as proxy for historical grievance
                            ScoreChange = penalty,
                            DecayRate = grudge.Severity == GrievanceSeverity.Eternal ? (half)0f : (half)0.001f,
                            RemainingTicks = 0, // Permanent until decay
                            SourceFactionId = 0,
                            AppliedTick = currentTick
                        });
                    }
                }
            }
        }
    }

    /// <summary>
    /// Integrates grudges into the target priority system.
    /// Grudge targets get priority boost in combat.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(Space4XTargetPrioritySystem))]
    public partial struct Space4XGrudgeTargetingIntegrationSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GrudgeModifiers>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (grudgeModifiers, behavior, factionGrudges, personalGrudges, priority, entity) in
                SystemAPI.Query<
                    RefRO<GrudgeModifiers>,
                    RefRO<GrudgeBehavior>,
                    DynamicBuffer<FactionGrudge>,
                    DynamicBuffer<PersonalGrudge>,
                    RefRW<TargetPriority>>()
                    .WithEntityAccess())
            {
                // Check if current target is a grudge target
                var targetEntity = priority.ValueRO.CurrentTarget;
                if (targetEntity == Entity.Null) continue;

                float grudgeBoost = 0;

                // Check personal grudges
                for (int i = 0; i < personalGrudges.Length; i++)
                {
                    if (personalGrudges[i].Offender == targetEntity)
                    {
                        grudgeBoost = math.max(grudgeBoost,
                            GrudgeHelpers.GetTargetPriorityBoost(personalGrudges[i].Intensity));
                        break;
                    }
                }

                // Apply grudge boost to priority score
                if (grudgeBoost > 0)
                {
                    priority.ValueRW.CurrentScore += grudgeBoost;
                }
            }
        }
    }

    /// <summary>
    /// Integrates patriotism into morale calculations.
    /// High patriotism in home territory boosts morale.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(Space4XMoraleSystem))]
    public partial struct Space4XPatriotismMoraleIntegrationSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PatriotismModifiers>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var currentTick = (uint)SystemAPI.Time.ElapsedTime;

            foreach (var (patriotismModifiers, profile, moraleModifiers, entity) in
                SystemAPI.Query<
                    RefRO<PatriotismModifiers>,
                    RefRO<PatriotismProfile>,
                    DynamicBuffer<MoraleModifier>>()
                    .WithEntityAccess())
            {
                var moraleBuffer = moraleModifiers;
                // Add patriotism-based morale modifier
                float moraleBonus = (float)patriotismModifiers.ValueRO.HomeTerritoryMoraleBonus;

                // Would check if entity is in home territory and apply bonus
                // For now, apply a base patriotism morale effect
                float patriotismEffect = (float)profile.ValueRO.OverallPatriotism * 0.1f;

                // Check if modifier exists
                bool found = false;
                for (int i = 0; i < moraleBuffer.Length; i++)
                {
                    var mod = moraleBuffer[i];
                    if (mod.Source == MoraleModifierSource.Patriotism)
                    {
                        mod.Strength = (half)patriotismEffect;
                        moraleBuffer[i] = mod;
                        found = true;
                        break;
                    }
                }

                if (!found && patriotismEffect > 0.01f && moraleBuffer.Length < 8)
                {
                    moraleBuffer.Add(new MoraleModifier
                    {
                        Source = MoraleModifierSource.Patriotism,
                        Strength = (half)patriotismEffect,
                        RemainingTicks = 0, // Permanent while applicable
                        AppliedTick = currentTick
                    });
                }
            }
        }
    }

    /// <summary>
    /// Integrates grudges into morale (working with grudge targets hurts morale).
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(Space4XMoraleSystem))]
    public partial struct Space4XGrudgeMoraleIntegrationSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GrudgeModifiers>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var currentTick = (uint)SystemAPI.Time.ElapsedTime;

            foreach (var (grudgeModifiers, moraleModifiers, entity) in
                SystemAPI.Query<RefRO<GrudgeModifiers>, DynamicBuffer<MoraleModifier>>()
                    .WithEntityAccess())
            {
                var moraleBuffer = moraleModifiers;
                float moralePenalty = -(float)grudgeModifiers.ValueRO.MoralePenalty;

                if (math.abs(moralePenalty) < 0.01f) continue;

                // Check if modifier exists
                bool found = false;
                for (int i = 0; i < moraleBuffer.Length; i++)
                {
                    var mod = moraleBuffer[i];
                    if (mod.Source == MoraleModifierSource.Grudge)
                    {
                        mod.Strength = (half)moralePenalty;
                        moraleBuffer[i] = mod;
                        found = true;
                        break;
                    }
                }

                if (!found && moraleBuffer.Length < 8)
                {
                    moraleBuffer.Add(new MoraleModifier
                    {
                        Source = MoraleModifierSource.Grudge,
                        Strength = (half)moralePenalty,
                        RemainingTicks = 0,
                        AppliedTick = currentTick
                    });
                }
            }
        }
    }

    /// <summary>
    /// Integrates grudges into combat modifiers.
    /// Fighting grudge targets provides combat bonuses.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct Space4XGrudgeCombatIntegrationSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<InCombatTag>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (engagement, grudgeModifiers, behavior, personalGrudges, entity) in
                SystemAPI.Query<
                    RefRW<Space4XEngagement>,
                    RefRO<GrudgeModifiers>,
                    RefRO<GrudgeBehavior>,
                    DynamicBuffer<PersonalGrudge>>()
                    .WithAll<InCombatTag>()
                    .WithEntityAccess())
            {
                // Check if opponent is a grudge target
                var opponent = engagement.ValueRO.PrimaryTarget;
                if (opponent == Entity.Null) continue;

                float combatBonus = 0;

                for (int i = 0; i < personalGrudges.Length; i++)
                {
                    if (personalGrudges[i].Offender == opponent)
                    {
                        combatBonus = GrudgeHelpers.GetCombatBonus(
                            personalGrudges[i].Intensity,
                            behavior.ValueRO.Vengefulness);
                        break;
                    }
                }

                // Apply combat bonus (stored in engagement for weapon systems to use)
                // This would typically be read by the damage calculation system
            }
        }
    }

    /// <summary>
    /// Integrates patriotism into cooperation calculations.
    /// Same-ideology entities cooperate better.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct Space4XPatriotismCooperationIntegrationSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PatriotismModifiers>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (patriotismModifiers, belongings, departmentStats, entity) in
                SystemAPI.Query<
                    RefRO<PatriotismModifiers>,
                    DynamicBuffer<BelongingEntry>,
                    DynamicBuffer<DepartmentStatsBuffer>>()
                    .WithEntityAccess())
            {
                // Apply cooperation bonuses/penalties to department efficiency
                float coopBonus = (float)patriotismModifiers.ValueRO.IdeologyDiplomacyBonus;
                float dynastyPenalty = -(float)patriotismModifiers.ValueRO.DynastyRivalryPenalty;

                // Would check if working with same-ideology or rival-dynasty crew
                // and apply appropriate modifiers to department cohesion
            }
        }
    }

    /// <summary>
    /// Creates grievances when combat atrocities occur.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct Space4XCombatGrievanceSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<DamageEvent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // Check for damage events that would create grievances
            foreach (var (damageEvents, entity) in
                SystemAPI.Query<DynamicBuffer<DamageEvent>>()
                    .WithEntityAccess())
            {
                for (int i = 0; i < damageEvents.Length; i++)
                {
                    var dmg = damageEvents[i];

                    // Check for atrocity conditions
                    // e.g., attacking unarmed vessels, destroying civilian targets
                    // These would create faction/species level grievances
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }

    /// <summary>
    /// Handles species-level grudge events (genocide, enslavement discovery).
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct Space4XSpeciesGrievanceEventSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XEvent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var currentTick = (uint)SystemAPI.Time.ElapsedTime;

            foreach (var (evt, entity) in
                SystemAPI.Query<RefRO<Space4XEvent>>()
                    .WithEntityAccess())
            {
                // Check for events that create species-level grudges
                if (evt.ValueRO.Category == EventCategory.Crisis ||
                    evt.ValueRO.Category == EventCategory.Political)
                {
                    // Would propagate species-level grudge to all entities of affected species
                }
            }
        }
    }

}


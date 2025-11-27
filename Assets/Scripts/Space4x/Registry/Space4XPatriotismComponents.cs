using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    /// <summary>
    /// Tiers of belonging - entities can feel loyalty at multiple levels.
    /// Higher tiers may conflict with lower tiers (empire vs family duty).
    /// </summary>
    public enum BelongingTier : byte
    {
        /// <summary>
        /// No belonging.
        /// </summary>
        None = 0,

        /// <summary>
        /// Immediate family - parents, siblings, children.
        /// </summary>
        Family = 1,

        /// <summary>
        /// Extended bloodline - ancestors, descendants, clan.
        /// </summary>
        Dynasty = 2,

        /// <summary>
        /// Professional organization - guild, corporation, union.
        /// </summary>
        Guild = 3,

        /// <summary>
        /// Local settlement - colony, station, ship.
        /// </summary>
        Colony = 4,

        /// <summary>
        /// Political group - faction, nation, house.
        /// </summary>
        Faction = 5,

        /// <summary>
        /// Galactic power - empire, federation, alliance.
        /// </summary>
        Empire = 6,

        /// <summary>
        /// Belief system - religion, philosophy, cause.
        /// </summary>
        Ideology = 7,

        /// <summary>
        /// Racial/species pride and solidarity.
        /// </summary>
        Species = 8
    }

    /// <summary>
    /// Entry for a specific tier of belonging.
    /// </summary>
    [InternalBufferCapacity(6)]
    public struct BelongingEntry : IBufferElementData
    {
        /// <summary>
        /// Which tier this entry represents.
        /// </summary>
        public BelongingTier Tier;

        /// <summary>
        /// Target entity (family head, guild, colony, faction, etc.).
        /// </summary>
        public Entity Target;

        /// <summary>
        /// Loyalty intensity to this tier [0, 100].
        /// </summary>
        public byte Loyalty;

        /// <summary>
        /// Priority when loyalties conflict. Higher = takes precedence.
        /// </summary>
        public byte Priority;

        /// <summary>
        /// Whether this is the entity's primary identity.
        /// </summary>
        public byte IsPrimaryIdentity;

        /// <summary>
        /// Tick when this belonging was established.
        /// </summary>
        public uint EstablishedTick;

        /// <summary>
        /// Name identifier for display/telemetry.
        /// </summary>
        public FixedString32Bytes TargetName;
    }

    /// <summary>
    /// Overall patriotism profile for an entity.
    /// </summary>
    public struct PatriotismProfile : IComponentData
    {
        /// <summary>
        /// Innate tendency toward loyalty [0, 100].
        /// High = naturally loyal, low = individualistic.
        /// </summary>
        public byte NaturalLoyalty;

        /// <summary>
        /// How strongly entity prioritizes family over larger groups.
        /// </summary>
        public half FamilyBias;

        /// <summary>
        /// How strongly entity values ideological purity.
        /// </summary>
        public half IdeologicalZeal;

        /// <summary>
        /// Species pride affecting xenophobia/cooperation.
        /// </summary>
        public half SpeciesPride;

        /// <summary>
        /// Tier entity identifies with most strongly.
        /// </summary>
        public BelongingTier PrimaryTier;

        /// <summary>
        /// Whether entity is currently in a loyalty conflict.
        /// </summary>
        public byte HasConflict;

        /// <summary>
        /// Overall patriotism score (aggregate of all loyalties).
        /// </summary>
        public half OverallPatriotism;

        public static PatriotismProfile Default() => new PatriotismProfile
        {
            NaturalLoyalty = 50,
            FamilyBias = (half)0.3f,
            IdeologicalZeal = (half)0.2f,
            SpeciesPride = (half)0.4f,
            PrimaryTier = BelongingTier.Faction,
            HasConflict = 0,
            OverallPatriotism = (half)0.5f
        };

        public static PatriotismProfile FamilyFirst() => new PatriotismProfile
        {
            NaturalLoyalty = 60,
            FamilyBias = (half)0.8f,
            IdeologicalZeal = (half)0.1f,
            SpeciesPride = (half)0.3f,
            PrimaryTier = BelongingTier.Family,
            HasConflict = 0,
            OverallPatriotism = (half)0.6f
        };

        public static PatriotismProfile Zealot() => new PatriotismProfile
        {
            NaturalLoyalty = 80,
            FamilyBias = (half)0.1f,
            IdeologicalZeal = (half)0.9f,
            SpeciesPride = (half)0.2f,
            PrimaryTier = BelongingTier.Ideology,
            HasConflict = 0,
            OverallPatriotism = (half)0.8f
        };

        public static PatriotismProfile Nationalist() => new PatriotismProfile
        {
            NaturalLoyalty = 70,
            FamilyBias = (half)0.2f,
            IdeologicalZeal = (half)0.3f,
            SpeciesPride = (half)0.8f,
            PrimaryTier = BelongingTier.Empire,
            HasConflict = 0,
            OverallPatriotism = (half)0.7f
        };

        public static PatriotismProfile Individualist() => new PatriotismProfile
        {
            NaturalLoyalty = 20,
            FamilyBias = (half)0.4f,
            IdeologicalZeal = (half)0.1f,
            SpeciesPride = (half)0.2f,
            PrimaryTier = BelongingTier.None,
            HasConflict = 0,
            OverallPatriotism = (half)0.2f
        };
    }

    /// <summary>
    /// Tracks when loyalties at different tiers conflict.
    /// </summary>
    public struct PatriotismConflict : IComponentData
    {
        /// <summary>
        /// First conflicting tier.
        /// </summary>
        public BelongingTier TierA;

        /// <summary>
        /// Second conflicting tier.
        /// </summary>
        public BelongingTier TierB;

        /// <summary>
        /// Entity demanding loyalty from TierA.
        /// </summary>
        public Entity DemandingEntityA;

        /// <summary>
        /// Entity demanding loyalty from TierB.
        /// </summary>
        public Entity DemandingEntityB;

        /// <summary>
        /// Conflict type.
        /// </summary>
        public PatriotismConflictType Type;

        /// <summary>
        /// Severity [0, 1]. Higher = more pressure to choose.
        /// </summary>
        public half Severity;

        /// <summary>
        /// Tick when conflict started.
        /// </summary>
        public uint StartTick;

        /// <summary>
        /// Ticks until forced resolution.
        /// </summary>
        public uint DeadlineTick;
    }

    /// <summary>
    /// Types of patriotism conflicts.
    /// </summary>
    public enum PatriotismConflictType : byte
    {
        None = 0,

        /// <summary>
        /// Family wants entity to desert for their sake.
        /// </summary>
        FamilyVsDuty = 1,

        /// <summary>
        /// Guild interests oppose faction orders.
        /// </summary>
        GuildVsFaction = 2,

        /// <summary>
        /// Colony autonomy vs empire control.
        /// </summary>
        ColonyVsEmpire = 3,

        /// <summary>
        /// Faction orders violate ideology.
        /// </summary>
        FactionVsIdeology = 4,

        /// <summary>
        /// Species solidarity vs faction membership.
        /// </summary>
        SpeciesVsFaction = 5,

        /// <summary>
        /// Dynasty ambition vs faction loyalty.
        /// </summary>
        DynastyVsFaction = 6,

        /// <summary>
        /// Ideology demands betrayal of empire.
        /// </summary>
        IdeologyVsEmpire = 7
    }

    /// <summary>
    /// Event when entity resolves a patriotism conflict.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct PatriotismResolutionEvent : IBufferElementData
    {
        /// <summary>
        /// Type of conflict resolved.
        /// </summary>
        public PatriotismConflictType ConflictType;

        /// <summary>
        /// Tier entity chose.
        /// </summary>
        public BelongingTier ChosenTier;

        /// <summary>
        /// Tier entity betrayed.
        /// </summary>
        public BelongingTier BetrayedTier;

        /// <summary>
        /// Tick when resolved.
        /// </summary>
        public uint ResolvedTick;

        /// <summary>
        /// Consequence severity.
        /// </summary>
        public half ConsequenceSeverity;
    }

    /// <summary>
    /// Modifiers from patriotism affecting gameplay.
    /// </summary>
    public struct PatriotismModifiers : IComponentData
    {
        /// <summary>
        /// Morale bonus when in home territory.
        /// </summary>
        public half HomeTerritoryMoraleBonus;

        /// <summary>
        /// Resistance to enemy propaganda/bribes.
        /// </summary>
        public half PropagandaResistance;

        /// <summary>
        /// Willingness to sacrifice for the cause.
        /// </summary>
        public half SacrificeWillingness;

        /// <summary>
        /// Bonus when fighting alongside family.
        /// </summary>
        public half FamilyCombatBonus;

        /// <summary>
        /// Penalty when fighting against own species.
        /// </summary>
        public half SpeciesConflictPenalty;

        /// <summary>
        /// Diplomatic bonus with same ideology.
        /// </summary>
        public half IdeologyDiplomacyBonus;

        /// <summary>
        /// Cooperation penalty with rival dynasties.
        /// </summary>
        public half DynastyRivalryPenalty;

        public static PatriotismModifiers Default() => new PatriotismModifiers
        {
            HomeTerritoryMoraleBonus = (half)0.1f,
            PropagandaResistance = (half)0.3f,
            SacrificeWillingness = (half)0.2f,
            FamilyCombatBonus = (half)0.15f,
            SpeciesConflictPenalty = (half)0.1f,
            IdeologyDiplomacyBonus = (half)0.2f,
            DynastyRivalryPenalty = (half)0.1f
        };
    }

    /// <summary>
    /// Request to test entity's loyalty to a specific tier.
    /// </summary>
    public struct PatriotismTestRequest : IComponentData
    {
        /// <summary>
        /// Tier being tested.
        /// </summary>
        public BelongingTier TestedTier;

        /// <summary>
        /// Entity making the demand.
        /// </summary>
        public Entity Demander;

        /// <summary>
        /// What's being demanded.
        /// </summary>
        public PatriotismDemandType DemandType;

        /// <summary>
        /// Severity of demand [0, 1].
        /// </summary>
        public half DemandSeverity;

        /// <summary>
        /// Tick when test was issued.
        /// </summary>
        public uint IssuedTick;
    }

    /// <summary>
    /// Types of loyalty demands.
    /// </summary>
    public enum PatriotismDemandType : byte
    {
        None = 0,

        /// <summary>
        /// Join military action.
        /// </summary>
        MilitaryService = 1,

        /// <summary>
        /// Betray another tier.
        /// </summary>
        Betrayal = 2,

        /// <summary>
        /// Financial contribution.
        /// </summary>
        Tribute = 3,

        /// <summary>
        /// Information sharing.
        /// </summary>
        Intelligence = 4,

        /// <summary>
        /// Public declaration of loyalty.
        /// </summary>
        Declaration = 5,

        /// <summary>
        /// Sacrifice personal interests.
        /// </summary>
        Sacrifice = 6
    }

    /// <summary>
    /// Result of a patriotism test.
    /// </summary>
    public struct PatriotismTestResult : IComponentData
    {
        /// <summary>
        /// Whether entity complied.
        /// </summary>
        public byte Complied;

        /// <summary>
        /// Loyalty change from this test.
        /// </summary>
        public sbyte LoyaltyChange;

        /// <summary>
        /// Whether this triggered a conflict.
        /// </summary>
        public byte TriggeredConflict;

        /// <summary>
        /// Tick when test was resolved.
        /// </summary>
        public uint ResolvedTick;
    }

    /// <summary>
    /// Static helpers for patriotism calculations.
    /// </summary>
    public static class PatriotismHelpers
    {
        /// <summary>
        /// Gets loyalty to a specific tier.
        /// </summary>
        public static byte GetLoyaltyToTier(
            DynamicBuffer<BelongingEntry> belongings,
            BelongingTier tier)
        {
            for (int i = 0; i < belongings.Length; i++)
            {
                if (belongings[i].Tier == tier)
                {
                    return belongings[i].Loyalty;
                }
            }
            return 0;
        }

        /// <summary>
        /// Gets the primary belonging tier.
        /// </summary>
        public static BelongingTier GetPrimaryTier(DynamicBuffer<BelongingEntry> belongings)
        {
            for (int i = 0; i < belongings.Length; i++)
            {
                if (belongings[i].IsPrimaryIdentity != 0)
                {
                    return belongings[i].Tier;
                }
            }
            return BelongingTier.None;
        }

        /// <summary>
        /// Calculates overall patriotism score.
        /// </summary>
        public static float CalculateOverallPatriotism(
            DynamicBuffer<BelongingEntry> belongings,
            in PatriotismProfile profile)
        {
            if (belongings.Length == 0) return 0;

            float sum = 0;
            float weights = 0;

            for (int i = 0; i < belongings.Length; i++)
            {
                float tierWeight = GetTierWeight(belongings[i].Tier, profile);
                sum += belongings[i].Loyalty * tierWeight;
                weights += tierWeight * 100f;
            }

            return weights > 0 ? sum / weights : 0;
        }

        /// <summary>
        /// Gets weight for a tier based on profile biases.
        /// </summary>
        public static float GetTierWeight(BelongingTier tier, in PatriotismProfile profile)
        {
            return tier switch
            {
                BelongingTier.Family => (float)profile.FamilyBias,
                BelongingTier.Dynasty => (float)profile.FamilyBias * 0.7f,
                BelongingTier.Guild => 0.3f,
                BelongingTier.Colony => 0.4f,
                BelongingTier.Faction => 0.5f,
                BelongingTier.Empire => 0.6f,
                BelongingTier.Ideology => (float)profile.IdeologicalZeal,
                BelongingTier.Species => (float)profile.SpeciesPride,
                _ => 0.1f
            };
        }

        /// <summary>
        /// Checks if two tiers can conflict.
        /// </summary>
        public static bool CanTiersConflict(BelongingTier a, BelongingTier b)
        {
            // Lower tiers can conflict with higher tiers
            if (a == b) return false;
            if (a == BelongingTier.None || b == BelongingTier.None) return false;

            // Certain combinations are more likely to conflict
            return (a == BelongingTier.Family && (int)b > (int)BelongingTier.Dynasty) ||
                   (a == BelongingTier.Ideology && b != BelongingTier.Ideology) ||
                   (a == BelongingTier.Species && (b == BelongingTier.Faction || b == BelongingTier.Empire)) ||
                   (a == BelongingTier.Colony && b == BelongingTier.Empire) ||
                   (a == BelongingTier.Guild && b == BelongingTier.Faction);
        }

        /// <summary>
        /// Predicts which tier entity would choose in conflict.
        /// </summary>
        public static BelongingTier PredictConflictChoice(
            DynamicBuffer<BelongingEntry> belongings,
            in PatriotismProfile profile,
            BelongingTier tierA,
            BelongingTier tierB)
        {
            byte loyaltyA = GetLoyaltyToTier(belongings, tierA);
            byte loyaltyB = GetLoyaltyToTier(belongings, tierB);

            float weightA = GetTierWeight(tierA, profile);
            float weightB = GetTierWeight(tierB, profile);

            float scoreA = loyaltyA * weightA;
            float scoreB = loyaltyB * weightB;

            // Natural loyalty affects threshold
            float threshold = profile.NaturalLoyalty * 0.1f;

            return scoreA + threshold > scoreB ? tierA : tierB;
        }
    }
}


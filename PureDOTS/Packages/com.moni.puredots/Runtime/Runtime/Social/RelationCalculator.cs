using Unity.Mathematics;
using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Runtime.Social
{
    /// <summary>
    /// Static helpers for relationship calculations.
    /// </summary>
    [BurstCompile]
    public static class RelationCalculator
    {
        /// <summary>
        /// Calculates intensity change from an interaction outcome.
        /// </summary>
        public static sbyte CalculateIntensityChange(RelationType type, InteractionOutcome outcome)
        {
            int baseChange = outcome switch
            {
                InteractionOutcome.Neutral => 0,
                InteractionOutcome.Positive => 3,
                InteractionOutcome.VeryPositive => 8,
                InteractionOutcome.Negative => -5,
                InteractionOutcome.VeryNegative => -12,
                InteractionOutcome.Hostile => -20,
                InteractionOutcome.Intimate => 15,
                InteractionOutcome.Professional => 2,
                _ => 0
            };

            // Modify based on relationship type
            float modifier = type switch
            {
                RelationType.Stranger => 1.5f,      // First impressions matter more
                RelationType.Friend => 0.8f,        // Friends are more forgiving
                RelationType.CloseFriend => 0.6f,   // Close friends even more so
                RelationType.Enemy => 1.2f,         // Enemies are harder to please
                RelationType.Spouse => 0.5f,        // Spouses are most forgiving
                RelationType.Mentor => 0.7f,
                _ => 1.0f
            };

            return (sbyte)math.clamp(baseChange * modifier, -50, 50);
        }

        /// <summary>
        /// Determines relationship type from intensity and interaction count.
        /// </summary>
        public static RelationType DetermineRelationType(
            sbyte intensity,
            ushort interactions,
            RelationType currentType)
        {
            // Don't change family relationships based on intensity
            if (IsFamilyRelation(currentType))
                return currentType;

            // Don't change professional relationships easily
            if (IsProfessionalRelation(currentType) && interactions < 20)
                return currentType;

            // Determine based on intensity
            if (intensity >= 80 && interactions >= 50)
                return RelationType.BestFriend;
            if (intensity >= 60 && interactions >= 30)
                return RelationType.CloseFriend;
            if (intensity >= 40 && interactions >= 15)
                return RelationType.Friend;
            if (intensity >= 10 && interactions >= 5)
                return RelationType.Acquaintance;
            if (intensity <= -60 && interactions >= 10)
                return RelationType.Nemesis;
            if (intensity <= -40 && interactions >= 5)
                return RelationType.Enemy;
            if (intensity <= -20)
                return RelationType.Rival;
            if (interactions == 0)
                return RelationType.Stranger;

            return RelationType.Acquaintance;
        }

        /// <summary>
        /// Gets cooperation bonus based on relationship intensity.
        /// </summary>
        public static float GetCooperationBonus(sbyte intensity)
        {
            // -0.5 to +0.5 bonus
            return intensity / 200f;
        }

        /// <summary>
        /// Gets trade price modifier based on relationship.
        /// </summary>
        public static float GetTradePriceModifier(sbyte intensity, byte trust)
        {
            // Better relationships = better prices
            float intensityMod = 1f - (intensity / 200f); // 0.5 to 1.5
            float trustMod = 1f - (trust / 200f);          // 0.5 to 1.0
            
            return math.clamp(intensityMod * trustMod, 0.5f, 1.5f);
        }

        /// <summary>
        /// Gets morale bonus from nearby allies.
        /// </summary>
        public static float GetMoraleBonus(sbyte intensity, RelationType type)
        {
            float baseBonus = intensity / 100f; // -1 to 1
            
            float typeMultiplier = type switch
            {
                RelationType.BestFriend => 2.0f,
                RelationType.CloseFriend => 1.5f,
                RelationType.Friend => 1.2f,
                RelationType.Spouse => 2.5f,
                RelationType.Sibling => 1.8f,
                RelationType.Parent => 1.5f,
                RelationType.Mentor => 1.3f,
                _ => 1.0f
            };

            return baseBonus * typeMultiplier;
        }

        /// <summary>
        /// Calculates initial impression based on various factors.
        /// </summary>
        public static sbyte CalculateInitialImpression(
            byte charisma,
            byte reputation,
            bool sameFacton,
            uint randomSeed)
        {
            // Base impression from charisma
            int impression = (charisma - 50) / 5; // -10 to +10

            // Reputation influence
            impression += (reputation - 50) / 10; // -5 to +5

            // Same faction bonus
            if (sameFacton)
                impression += 10;

            // Random variance
            var random = new Random(randomSeed);
            impression += random.NextInt(-5, 6);

            return (sbyte)math.clamp(impression, -30, 30);
        }

        /// <summary>
        /// Calculates trust change from an interaction.
        /// </summary>
        public static sbyte CalculateTrustChange(InteractionOutcome outcome, byte currentTrust)
        {
            int change = outcome switch
            {
                InteractionOutcome.Positive => 2,
                InteractionOutcome.VeryPositive => 5,
                InteractionOutcome.Negative => -3,
                InteractionOutcome.VeryNegative => -8,
                InteractionOutcome.Hostile => -15,
                InteractionOutcome.Professional => 1,
                _ => 0
            };

            // Trust is harder to gain when already high
            if (change > 0 && currentTrust > 70)
                change /= 2;

            // Trust is easier to lose when already low
            if (change < 0 && currentTrust < 30)
                change = (int)(change * 1.5f);

            return (sbyte)math.clamp(change, -20, 10);
        }

        /// <summary>
        /// Calculates familiarity gain from interaction.
        /// </summary>
        public static byte CalculateFamiliarityGain(
            InteractionOutcome outcome,
            byte currentFamiliarity,
            byte baseFamiliarityGain)
        {
            // Familiarity always increases (you learn about them)
            int gain = baseFamiliarityGain;

            // More significant interactions teach you more
            if (outcome == InteractionOutcome.VeryPositive || 
                outcome == InteractionOutcome.VeryNegative ||
                outcome == InteractionOutcome.Intimate)
            {
                gain *= 2;
            }

            // Diminishing returns at high familiarity
            if (currentFamiliarity > 80)
                gain /= 2;

            return (byte)math.clamp(gain, 1, 10);
        }

        /// <summary>
        /// Calculates relationship decay over time.
        /// </summary>
        public static sbyte CalculateDecay(
            sbyte currentIntensity,
            uint ticksSinceLastInteraction,
            float decayRatePerDay,
            float ticksPerDay,
            sbyte minIntensity)
        {
            if (currentIntensity <= minIntensity)
                return currentIntensity;

            float days = ticksSinceLastInteraction / ticksPerDay;
            float decay = days * decayRatePerDay;

            // Decay towards neutral (0), not below min
            sbyte newIntensity;
            if (currentIntensity > 0)
            {
                newIntensity = (sbyte)math.max(0, currentIntensity - decay);
            }
            else
            {
                newIntensity = (sbyte)math.min(0, currentIntensity + decay);
            }

            return (sbyte)math.max(newIntensity, minIntensity);
        }

        /// <summary>
        /// Checks if a relationship type is a family relation.
        /// </summary>
        public static bool IsFamilyRelation(RelationType type)
        {
            return (byte)type >= 20 && (byte)type <= 29;
        }

        /// <summary>
        /// Checks if a relationship type is professional.
        /// </summary>
        public static bool IsProfessionalRelation(RelationType type)
        {
            return (byte)type >= 30 && (byte)type <= 39;
        }

        /// <summary>
        /// Checks if a relationship type is romantic.
        /// </summary>
        public static bool IsRomanticRelation(RelationType type)
        {
            return (byte)type >= 50 && (byte)type <= 59;
        }

        /// <summary>
        /// Checks if relationship is positive.
        /// </summary>
        public static bool IsPositiveRelation(RelationType type)
        {
            return type == RelationType.Friend ||
                   type == RelationType.CloseFriend ||
                   type == RelationType.BestFriend ||
                   type == RelationType.Ally ||
                   type == RelationType.Spouse ||
                   type == RelationType.Lover;
        }

        /// <summary>
        /// Checks if relationship is negative.
        /// </summary>
        public static bool IsNegativeRelation(RelationType type)
        {
            return type == RelationType.Rival ||
                   type == RelationType.Enemy ||
                   type == RelationType.Nemesis ||
                   type == RelationType.Grudge ||
                   type == RelationType.Hostile ||
                   type == RelationType.AtWar;
        }

        /// <summary>
        /// Finds a relation in the buffer.
        /// </summary>
        public static int FindRelationIndex(
            in DynamicBuffer<EntityRelation> relations,
            Entity otherEntity)
        {
            for (int i = 0; i < relations.Length; i++)
            {
                if (relations[i].OtherEntity == otherEntity)
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// Gets a relation from the buffer.
        /// </summary>
        public static EntityRelation GetRelation(
            in DynamicBuffer<EntityRelation> relations,
            Entity otherEntity)
        {
            int index = FindRelationIndex(relations, otherEntity);
            if (index >= 0)
                return relations[index];
            
            return new EntityRelation
            {
                OtherEntity = otherEntity,
                Type = RelationType.Stranger,
                Intensity = 0,
                Trust = 50,
                Familiarity = 0
            };
        }
    }
}


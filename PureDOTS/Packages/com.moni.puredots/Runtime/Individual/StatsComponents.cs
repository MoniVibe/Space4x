using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Individual
{
    /// <summary>
    /// Core stats for SimIndividuals, unified across Godgame and Space4X.
    /// Values typically range 0-10 for primary stats, can be higher with modifiers.
    /// </summary>
    public struct IndividualStats : IComponentData
    {
        /// <summary>
        /// Physical strength and endurance. Affects combat damage, carrying capacity, health.
        /// </summary>
        public float Physique;

        /// <summary>
        /// Dexterity and precision. Affects accuracy, initiative gain, crafting quality.
        /// </summary>
        public float Finesse;

        /// <summary>
        /// Speed and mobility. Affects movement speed, dodge chance, initiative gain.
        /// </summary>
        public float Agility;

        /// <summary>
        /// Mental acuity and problem-solving. Affects skill learning, magic power, tech research.
        /// </summary>
        public float Intellect;

        /// <summary>
        /// Mental fortitude and determination. Affects morale resistance, magic resistance, initiative gain.
        /// </summary>
        public float Will;

        /// <summary>
        /// Charisma and social skills. Affects leadership, trading, diplomacy, morale influence.
        /// </summary>
        public float Social;

        /// <summary>
        /// Spiritual connection and divine favor. Affects miracle power, worship efficiency, alignment influence.
        /// </summary>
        public float Faith;

        /// <summary>
        /// Create from individual values with optional clamping.
        /// </summary>
        public static IndividualStats FromValues(float physique, float finesse, float agility, float intellect, float will, float social, float faith, bool clamp = false)
        {
            var stats = new IndividualStats
            {
                Physique = physique,
                Finesse = finesse,
                Agility = agility,
                Intellect = intellect,
                Will = will,
                Social = social,
                Faith = faith
            };

            if (clamp)
            {
                stats.Physique = math.max(0f, stats.Physique);
                stats.Finesse = math.max(0f, stats.Finesse);
                stats.Agility = math.max(0f, stats.Agility);
                stats.Intellect = math.max(0f, stats.Intellect);
                stats.Will = math.max(0f, stats.Will);
                stats.Social = math.max(0f, stats.Social);
                stats.Faith = math.max(0f, stats.Faith);
            }

            return stats;
        }

        /// <summary>
        /// Compute average stat value (for aggregate calculations).
        /// </summary>
        public readonly float Average()
        {
            return (Physique + Finesse + Agility + Intellect + Will + Social + Faith) / 7f;
        }
    }

    /// <summary>
    /// Resource pools for HP, Stamina, Mana, Focus.
    /// Each pool has Current and Max values.
    /// </summary>
    public struct ResourcePools : IComponentData
    {
        /// <summary>
        /// Current health points.
        /// </summary>
        public float HP;

        /// <summary>
        /// Maximum health points (derived from Physique + modifiers).
        /// </summary>
        public float MaxHP;

        /// <summary>
        /// Current stamina/energy (for physical actions).
        /// </summary>
        public float Stamina;

        /// <summary>
        /// Maximum stamina (derived from Physique + Agility).
        /// </summary>
        public float MaxStamina;

        /// <summary>
        /// Current mana/magic energy (for spells/miracles).
        /// </summary>
        public float Mana;

        /// <summary>
        /// Maximum mana (derived from Intellect + Faith).
        /// </summary>
        public float MaxMana;

        /// <summary>
        /// Current focus/mental energy (for concentration-based actions).
        /// </summary>
        public float Focus;

        /// <summary>
        /// Maximum focus (derived from Intellect + Will).
        /// </summary>
        public float MaxFocus;

        /// <summary>
        /// Get HP percentage [0..1].
        /// </summary>
        public readonly float HPRatio => MaxHP > 0f ? math.clamp(HP / MaxHP, 0f, 1f) : 0f;

        /// <summary>
        /// Get Stamina percentage [0..1].
        /// </summary>
        public readonly float StaminaRatio => MaxStamina > 0f ? math.clamp(Stamina / MaxStamina, 0f, 1f) : 0f;

        /// <summary>
        /// Get Mana percentage [0..1].
        /// </summary>
        public readonly float ManaRatio => MaxMana > 0f ? math.clamp(Mana / MaxMana, 0f, 1f) : 0f;

        /// <summary>
        /// Get Focus percentage [0..1].
        /// </summary>
        public readonly float FocusRatio => MaxFocus > 0f ? math.clamp(Focus / MaxFocus, 0f, 1f) : 0f;

        /// <summary>
        /// Clamp all current values to their max values.
        /// </summary>
        public void ClampToMax()
        {
            HP = math.min(HP, MaxHP);
            Stamina = math.min(Stamina, MaxStamina);
            Mana = math.min(Mana, MaxMana);
            Focus = math.min(Focus, MaxFocus);
        }

        /// <summary>
        /// Clamp all current values to zero minimum.
        /// </summary>
        public void ClampToZero()
        {
            HP = math.max(HP, 0f);
            Stamina = math.max(Stamina, 0f);
            Mana = math.max(Mana, 0f);
            Focus = math.max(Focus, 0f);
        }
    }
}


using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Individual
{
    /// <summary>
    /// Personality axes that influence behavior and decision-making.
    /// Values are normalized [-1..+1].
    /// </summary>
    public struct PersonalityAxes : IComponentData
    {
        /// <summary>
        /// Boldness: -1 (cowardly) to +1 (brave).
        /// Affects willingness to take risks, enter combat, explore.
        /// </summary>
        public float Boldness;

        /// <summary>
        /// Vengefulness: -1 (forgiving) to +1 (vengeful).
        /// Affects response to wrongs, grudges, retaliation.
        /// </summary>
        public float Vengefulness;

        /// <summary>
        /// Risk tolerance: -1 (risk-averse) to +1 (risk-seeking).
        /// Affects decision-making under uncertainty.
        /// </summary>
        public float RiskTolerance;

        /// <summary>
        /// Selflessness: -1 (selfish) to +1 (selfless).
        /// Affects willingness to help others, share resources, sacrifice.
        /// </summary>
        public float Selflessness;

        /// <summary>
        /// Conviction: -1 (wavering) to +1 (steadfast).
        /// Affects resistance to persuasion, adherence to beliefs.
        /// </summary>
        public float Conviction;

        /// <summary>
        /// Create from individual values with clamping.
        /// </summary>
        public static PersonalityAxes FromValues(float boldness, float vengefulness, float riskTolerance, float selflessness, float conviction)
        {
            return new PersonalityAxes
            {
                Boldness = math.clamp(boldness, -1f, 1f),
                Vengefulness = math.clamp(vengefulness, -1f, 1f),
                RiskTolerance = math.clamp(riskTolerance, -1f, 1f),
                Selflessness = math.clamp(selflessness, -1f, 1f),
                Conviction = math.clamp(conviction, -1f, 1f)
            };
        }
    }

    /// <summary>
    /// Behavior tuning biases computed from alignment, personality, and morale.
    /// These values multiply AI utility scores to influence decision-making.
    /// Values are multipliers [0..2], where 1.0 = neutral.
    /// </summary>
    public struct BehaviorTuning : IComponentData
    {
        /// <summary>
        /// Aggression bias: multiplier for combat/conflict actions.
        /// > 1.0 = more aggressive, < 1.0 = more pacifist.
        /// </summary>
        public float AggressionBias;

        /// <summary>
        /// Social bias: multiplier for social/interaction actions.
        /// > 1.0 = more social, < 1.0 = more solitary.
        /// </summary>
        public float SocialBias;

        /// <summary>
        /// Greed bias: multiplier for resource acquisition actions.
        /// > 1.0 = more greedy, < 1.0 = more generous.
        /// </summary>
        public float GreedBias;

        /// <summary>
        /// Curiosity bias: multiplier for exploration/discovery actions.
        /// > 1.0 = more curious, < 1.0 = more cautious.
        /// </summary>
        public float CuriosityBias;

        /// <summary>
        /// Obedience bias: multiplier for following orders/authority.
        /// > 1.0 = more obedient, < 1.0 = more rebellious.
        /// </summary>
        public float ObedienceBias;

        /// <summary>
        /// Create neutral tuning (all biases = 1.0).
        /// </summary>
        public static BehaviorTuning Neutral()
        {
            return new BehaviorTuning
            {
                AggressionBias = 1f,
                SocialBias = 1f,
                GreedBias = 1f,
                CuriosityBias = 1f,
                ObedienceBias = 1f
            };
        }
    }
}


using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.AI
{
    /// <summary>
    /// Action types for utility-based decision making.
    /// Game-agnostic: covers common action patterns.
    /// </summary>
    public enum ActionType : byte
    {
        None = 0,
        Idle = 1,
        Move = 2,
        Gather = 3,
        Deliver = 4,
        Attack = 5,
        Defend = 6,
        Flee = 7,
        Rest = 8,
        Eat = 9,
        Drink = 10,
        Socialize = 11,
        Work = 12,
        Patrol = 13,
        Guard = 14,
        Explore = 15,
        Follow = 16,
        Custom = 255
    }

    /// <summary>
    /// Scored action candidate for utility-based AI.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct ActionScore : IBufferElementData
    {
        /// <summary>
        /// Type of action.
        /// </summary>
        public ActionType ActionType;

        /// <summary>
        /// Custom action ID (when ActionType == Custom).
        /// </summary>
        public byte CustomActionId;

        /// <summary>
        /// Utility score (higher = more desirable).
        /// </summary>
        public float Score;

        /// <summary>
        /// Target entity for this action (if applicable).
        /// </summary>
        public Entity TargetEntity;

        /// <summary>
        /// Target position for this action (if applicable).
        /// </summary>
        public float3 TargetPosition;

        /// <summary>
        /// Priority modifier (multiplies base score).
        /// </summary>
        public float PriorityModifier;

        /// <summary>
        /// Cooldown remaining before this action can be chosen again.
        /// </summary>
        public float CooldownRemaining;
    }

    /// <summary>
    /// Reference to utility curves blob for scoring.
    /// </summary>
    public struct UtilityCurveRef : IComponentData
    {
        /// <summary>
        /// Blob containing utility curve definitions.
        /// </summary>
        public BlobAssetReference<UtilityCurveBlob> Curves;
    }

    /// <summary>
    /// Blob structure for utility curve data.
    /// </summary>
    public struct UtilityCurveBlob
    {
        /// <summary>
        /// Array of curve definitions.
        /// </summary>
        public BlobArray<UtilityCurveDefinition> CurveDefinitions;
    }

    /// <summary>
    /// Definition of a single utility curve.
    /// </summary>
    public struct UtilityCurveDefinition
    {
        /// <summary>
        /// Curve type (linear, exponential, etc.).
        /// </summary>
        public CurveType Type;

        /// <summary>
        /// Curve slope/steepness.
        /// </summary>
        public float Slope;

        /// <summary>
        /// Curve exponent (for exponential/polynomial).
        /// </summary>
        public float Exponent;

        /// <summary>
        /// Horizontal shift.
        /// </summary>
        public float XShift;

        /// <summary>
        /// Vertical shift.
        /// </summary>
        public float YShift;

        /// <summary>
        /// Minimum output value.
        /// </summary>
        public float MinValue;

        /// <summary>
        /// Maximum output value.
        /// </summary>
        public float MaxValue;
    }

    /// <summary>
    /// Types of utility curves.
    /// </summary>
    public enum CurveType : byte
    {
        /// <summary>Linear: y = slope * x + yShift</summary>
        Linear = 0,
        /// <summary>Quadratic: y = slope * x^2 + yShift</summary>
        Quadratic = 1,
        /// <summary>Exponential: y = slope * e^(exponent * x) + yShift</summary>
        Exponential = 2,
        /// <summary>Logistic: y = maxValue / (1 + e^(-slope * (x - xShift)))</summary>
        Logistic = 3,
        /// <summary>Step: y = x > xShift ? maxValue : minValue</summary>
        Step = 4,
        /// <summary>Inverse: y = slope / (x + xShift) + yShift</summary>
        Inverse = 5
    }

    /// <summary>
    /// Current AI decision state.
    /// </summary>
    public struct UtilityDecisionState : IComponentData
    {
        /// <summary>
        /// Currently selected action.
        /// </summary>
        public ActionType CurrentAction;

        /// <summary>
        /// Custom action ID if using custom action.
        /// </summary>
        public byte CurrentCustomActionId;

        /// <summary>
        /// Score of current action.
        /// </summary>
        public float CurrentScore;

        /// <summary>
        /// Target entity for current action.
        /// </summary>
        public Entity CurrentTarget;

        /// <summary>
        /// Target position for current action.
        /// </summary>
        public float3 CurrentTargetPosition;

        /// <summary>
        /// Tick when action was selected.
        /// </summary>
        public uint ActionSelectedTick;

        /// <summary>
        /// Minimum ticks before reconsidering action.
        /// </summary>
        public uint MinActionDurationTicks;

        /// <summary>
        /// Has the action been interrupted?
        /// </summary>
        public bool Interrupted;
    }

    /// <summary>
    /// Configuration for utility-based decision making.
    /// </summary>
    public struct UtilityConfig : IComponentData
    {
        /// <summary>
        /// How often to reconsider actions (seconds).
        /// </summary>
        public float ReconsiderInterval;

        /// <summary>
        /// Score difference needed to switch actions.
        /// </summary>
        public float SwitchThreshold;

        /// <summary>
        /// Randomization factor (0 = deterministic, 1 = fully random).
        /// </summary>
        public float RandomFactor;

        /// <summary>
        /// Minimum score to consider an action viable.
        /// </summary>
        public float MinViableScore;

        /// <summary>
        /// Creates default config.
        /// </summary>
        public static UtilityConfig Default => new UtilityConfig
        {
            ReconsiderInterval = 1f,
            SwitchThreshold = 0.2f,
            RandomFactor = 0.1f,
            MinViableScore = 0.1f
        };
    }

    /// <summary>
    /// Burst-compatible utility curve evaluation.
    /// </summary>
    public static class UtilityCurveEvaluator
    {
        /// <summary>
        /// Evaluates a utility curve at the given input.
        /// </summary>
        public static float Evaluate(in UtilityCurveDefinition curve, float x)
        {
            float y;
            
            switch (curve.Type)
            {
                case CurveType.Linear:
                    y = curve.Slope * x + curve.YShift;
                    break;
                    
                case CurveType.Quadratic:
                    y = curve.Slope * x * x + curve.YShift;
                    break;
                    
                case CurveType.Exponential:
                    y = curve.Slope * math.exp(curve.Exponent * x) + curve.YShift;
                    break;
                    
                case CurveType.Logistic:
                    y = curve.MaxValue / (1f + math.exp(-curve.Slope * (x - curve.XShift)));
                    break;
                    
                case CurveType.Step:
                    y = x > curve.XShift ? curve.MaxValue : curve.MinValue;
                    break;
                    
                case CurveType.Inverse:
                    var denom = x + curve.XShift;
                    y = math.abs(denom) > 0.0001f 
                        ? curve.Slope / denom + curve.YShift 
                        : curve.MaxValue;
                    break;
                    
                default:
                    y = x;
                    break;
            }
            
            return math.clamp(y, curve.MinValue, curve.MaxValue);
        }
    }
}


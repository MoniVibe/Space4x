using Unity.Entities;
using Unity.Collections;

namespace PureDOTS.Runtime.AI.GOAP
{
    /// <summary>
    /// A goal the AI is trying to achieve.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct AIGoal : IBufferElementData
    {
        public FixedString32Bytes GoalId;     // "satisfy_hunger", "defend_village"
        public float Priority;                 // 0-100, higher = more urgent
        public float Insistence;               // How much priority grows per tick
        public bool IsActive;                  // Currently pursuing
        public Entity TargetEntity;            // Optional goal target
        public uint ActivatedTick;             // When goal became active
        public float SatisfactionThreshold;    // Goal met when satisfaction >= this
        public float CurrentSatisfaction;      // 0-1, how satisfied the goal is
    }

    /// <summary>
    /// An action that can be taken to satisfy goals.
    /// </summary>
    [InternalBufferCapacity(16)]
    public struct AIAction : IBufferElementData
    {
        public FixedString32Bytes ActionId;   // "eat_food", "attack_enemy"
        public FixedString32Bytes GoalId;     // Which goal this satisfies
        public float Cost;                     // Time/resource cost
        public float Utility;                  // How well it satisfies goal
        
        // Preconditions
        public bool RequiresTarget;
        public bool RequiresResource;
        public FixedString32Bytes RequiredState;
        public FixedString32Bytes RequiredResource;
        
        // Effects
        public float GoalSatisfactionDelta;   // How much it satisfies the goal
        public FixedString32Bytes ResultState; // State after action
    }

    /// <summary>
    /// AI planner state.
    /// </summary>
    public struct AIPlanner : IComponentData
    {
        public FixedString32Bytes CurrentGoal;
        public FixedString32Bytes CurrentAction;
        public float PlanConfidence;           // 0-1, replan if low
        public uint PlanCreatedTick;
        public uint ReplanInterval;            // Ticks between replans
        public byte PlanLength;                // Actions in current plan
        public byte PlanProgress;              // Current action index
        public bool NeedsReplan;
    }

    /// <summary>
    /// Planned action sequence.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct PlannedAction : IBufferElementData
    {
        public FixedString32Bytes ActionId;
        public Entity TargetEntity;
        public float ExpectedCost;
        public float ExpectedUtility;
        public byte SequenceIndex;
        public bool IsCompleted;
    }

    /// <summary>
    /// Utility AI option for scoring.
    /// </summary>
    [InternalBufferCapacity(12)]
    public struct UtilityOption : IBufferElementData
    {
        public FixedString32Bytes OptionId;
        public float BaseScore;                // Inherent value
        public float NeedScore;                // From unmet needs
        public float OpportunityScore;         // From environment
        public float DirectiveScore;           // From commander orders
        public float FinalScore;               // Combined
        public Entity TargetEntity;
        public uint EvaluatedTick;
    }

    /// <summary>
    /// Configuration for utility AI.
    /// </summary>
    public struct UtilityConfig : IComponentData
    {
        public float NeedWeight;               // How much needs affect choice
        public float OpportunityWeight;        // How much environment affects
        public float DirectiveWeight;          // How much orders affect
        public float RandomnessWeight;         // Prevent predictability
        public uint EvaluationInterval;        // Ticks between evaluations
        public float MinScoreToAct;            // Don't act if best score below this
    }

    /// <summary>
    /// Directive from a commander/superior.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct AIDirective : IBufferElementData
    {
        public Entity DirectingEntity;         // Village, fleet commander
        public FixedString32Bytes DirectiveType; // "gather_wood", "defend_point"
        public float Priority;
        public uint IssuedTick;
        public uint ExpiryTick;
        public Entity TargetEntity;            // Optional target for directive
        public bool IsCompleted;
        public bool IsCancelled;
    }

    /// <summary>
    /// Subordinate relationship to a commander.
    /// </summary>
    public struct AISubordinate : IComponentData
    {
        public Entity Commander;               // Who directs this entity
        public float Compliance;               // 0-1, how well they follow orders
        public bool HasStandingOrders;
        public uint LastOrderReceivedTick;
    }

    /// <summary>
    /// Commander that can issue directives.
    /// </summary>
    public struct AICommander : IComponentData
    {
        public byte MaxSubordinates;
        public byte CurrentSubordinates;
        public float CommandRadius;            // Range of command
        public float CommandEfficiency;        // How effective orders are
    }

    /// <summary>
    /// Current AI state for decision making.
    /// </summary>
    public struct AIState : IComponentData
    {
        public FixedString32Bytes CurrentState;    // "idle", "working", "combat", "fleeing"
        public FixedString32Bytes PreviousState;
        public uint StateEnteredTick;
        public float StateUtility;                 // How good is current state
        public bool IsInterruptible;               // Can be interrupted by higher priority
    }

    /// <summary>
    /// World state facts for GOAP planning.
    /// </summary>
    [InternalBufferCapacity(16)]
    public struct WorldStateFact : IBufferElementData
    {
        public FixedString32Bytes FactKey;     // "has_food", "near_enemy", "at_home"
        public float FactValue;                 // 0 = false, 1 = true, or numeric
        public uint LastUpdatedTick;
    }

    /// <summary>
    /// Request to evaluate utility options.
    /// </summary>
    public struct EvaluateUtilityRequest : IComponentData
    {
        public bool ForceEvaluation;           // Ignore interval
        public uint RequestTick;
    }

    /// <summary>
    /// Request to create a new plan.
    /// </summary>
    public struct CreatePlanRequest : IComponentData
    {
        public FixedString32Bytes GoalId;
        public bool ForceReplan;
        public uint RequestTick;
    }
}


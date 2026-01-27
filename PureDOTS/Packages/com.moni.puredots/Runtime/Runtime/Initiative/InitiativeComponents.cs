using Unity.Entities;

namespace PureDOTS.Runtime.Initiative
{
    /// <summary>
    /// Entity initiative state for action pacing.
    /// </summary>
    public struct EntityInitiative : IComponentData
    {
        public float BaseInitiative;      // Innate speed (40-120, 100 = average)
        public float CurrentInitiative;   // After modifiers
        public float ActionCooldown;      // Time until next action allowed (seconds)
        public float LastActionTime;      // When entity last acted (game time)
        public byte Urgency;              // 0-100, boosts priority when high
        
        // Modifiers
        public float SpeedModifier;       // Multiplier from buffs/debuffs
        public float FatigueModifier;     // Penalty from fatigue
        public float MoraleModifier;      // Bonus/penalty from morale
        
        // State
        public bool IsReady;              // Can act this frame
        public uint LastReadyTick;        // When became ready
        public uint ActionsThisTurn;      // Actions taken in current turn/round
    }

    /// <summary>
    /// Configuration for initiative system.
    /// </summary>
    public struct InitiativeConfig : IComponentData
    {
        public float BaseActionInterval;   // Default time between actions (1.0s)
        public float UrgencyBoostMax;      // Max speed boost from urgency (0.5 = 50%)
        public float MinActionInterval;    // Fastest possible action rate (0.1s)
        public float MaxActionInterval;    // Slowest action rate (5.0s)
        
        // Initiative scaling
        public float InitiativeScale;      // How much initiative affects speed (0.01 = 1% per point)
        public float BaseInitiative;       // Average initiative (100)
    }

    /// <summary>
    /// Request to perform an action (consumes initiative).
    /// </summary>
    public struct ActionRequest : IComponentData
    {
        public float ActionCost;          // How much cooldown this action adds
        public byte Priority;             // Higher = more urgent
    }

    /// <summary>
    /// Event when entity becomes ready to act.
    /// </summary>
    [InternalBufferCapacity(2)]
    public struct InitiativeReadyEvent : IBufferElementData
    {
        public uint Tick;
        public float Initiative;
    }

    /// <summary>
    /// Turn order entry for sorted combat/action resolution.
    /// </summary>
    public struct TurnOrderEntry : IBufferElementData
    {
        public Entity Entity;
        public float EffectiveInitiative;
        public byte Urgency;
        public uint ReadyTick;
    }
}


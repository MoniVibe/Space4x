using Unity.Entities;

namespace PureDOTS.Runtime.Combat.State
{
    /// <summary>
    /// Combat state enumeration.
    /// Represents the current combat behavior state of an entity.
    /// </summary>
    public enum CombatState : byte
    {
        /// <summary>
        /// Not in combat, no target.
        /// </summary>
        Idle = 0,

        /// <summary>
        /// Moving toward target to engage.
        /// </summary>
        Approaching = 1,

        /// <summary>
        /// In combat range, actively fighting.
        /// </summary>
        Engaged = 2,

        /// <summary>
        /// Performing an attack action.
        /// </summary>
        Attacking = 3,

        /// <summary>
        /// Blocking or parrying incoming attacks.
        /// </summary>
        Defending = 4,

        /// <summary>
        /// Unable to act due to stun effect.
        /// </summary>
        Stunned = 5,

        /// <summary>
        /// Retreating from combat.
        /// </summary>
        Fleeing = 6,

        /// <summary>
        /// Recovering after attack or stun.
        /// </summary>
        Recovering = 7,

        /// <summary>
        /// Entity is dead.
        /// </summary>
        Dead = 8,

        /// <summary>
        /// Casting a spell or ability.
        /// </summary>
        Casting = 9,

        /// <summary>
        /// Channeling a continuous effect.
        /// </summary>
        Channeling = 10,

        /// <summary>
        /// Knocked down, must get up.
        /// </summary>
        KnockedDown = 11,

        /// <summary>
        /// Formation is engaged in combat.
        /// </summary>
        FormationEngaged = 100,

        /// <summary>
        /// Formation has broken (members scattered).
        /// </summary>
        FormationBroken = 101,

        /// <summary>
        /// Formation has routed (fleeing).
        /// </summary>
        FormationRouted = 102,

        /// <summary>
        /// Formation is reforming after breaking.
        /// </summary>
        FormationReforming = 103,

        /// <summary>
        /// Module has been destroyed.
        /// </summary>
        ModuleDestroyed = 100,

        /// <summary>
        /// Module is damaged.
        /// </summary>
        ModuleDamaged = 101,

        /// <summary>
        /// Module is offline.
        /// </summary>
        ModuleOffline = 102,

        /// <summary>
        /// Module is being repaired.
        /// </summary>
        ModuleRepairing = 104
    }

    /// <summary>
    /// Combat state data component.
    /// Tracks current and previous combat states.
    /// </summary>
    public struct CombatStateData : IComponentData
    {
        /// <summary>
        /// Current combat state.
        /// </summary>
        public CombatState Current;

        /// <summary>
        /// Previous combat state (for transitions).
        /// </summary>
        public CombatState Previous;

        /// <summary>
        /// Tick when current state was entered.
        /// </summary>
        public uint StateEnteredTick;

        /// <summary>
        /// Remaining stun duration in seconds.
        /// </summary>
        public float StunDuration;

        /// <summary>
        /// Entity to flee toward (safe location or ally).
        /// </summary>
        public Entity FleeTarget;

        /// <summary>
        /// Recovery time remaining in seconds.
        /// </summary>
        public float RecoveryTime;

        /// <summary>
        /// Whether entity is invulnerable (during certain states).
        /// </summary>
        public bool IsInvulnerable;

        /// <summary>
        /// Whether entity can be interrupted.
        /// </summary>
        public bool CanBeInterrupted;
    }

    /// <summary>
    /// Combat state change event.
    /// Emitted when combat state transitions.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct CombatStateChangeEvent : IBufferElementData
    {
        /// <summary>
        /// Entity whose state changed.
        /// </summary>
        public Entity AffectedEntity;

        /// <summary>
        /// Previous state.
        /// </summary>
        public CombatState FromState;

        /// <summary>
        /// New state.
        /// </summary>
        public CombatState ToState;

        /// <summary>
        /// Tick when change occurred.
        /// </summary>
        public uint Tick;

        /// <summary>
        /// Optional cause entity (e.g., who stunned us).
        /// </summary>
        public Entity CauseEntity;
    }

    /// <summary>
    /// Request to change combat state.
    /// Processed by CombatStateSystem.
    /// </summary>
    public struct CombatStateChangeRequest : IComponentData
    {
        /// <summary>
        /// Requested new state.
        /// </summary>
        public CombatState RequestedState;

        /// <summary>
        /// Duration for timed states (stun, recovery).
        /// </summary>
        public float Duration;

        /// <summary>
        /// Entity causing the state change.
        /// </summary>
        public Entity CauseEntity;

        /// <summary>
        /// Whether to force the change (ignore restrictions).
        /// </summary>
        public bool Force;
    }

    /// <summary>
    /// Combat state configuration.
    /// Defines durations and transition rules.
    /// </summary>
    public struct CombatStateConfig : IComponentData
    {
        /// <summary>
        /// Default recovery time after attacks.
        /// </summary>
        public float DefaultRecoveryTime;

        /// <summary>
        /// Time before fleeing entity can re-engage.
        /// </summary>
        public float FleeReengageDelay;

        /// <summary>
        /// Time to get up from knocked down.
        /// </summary>
        public float KnockdownRecoveryTime;

        /// <summary>
        /// Maximum stun duration (cap).
        /// </summary>
        public float MaxStunDuration;

        /// <summary>
        /// Health threshold for auto-flee (0-1).
        /// </summary>
        public float FleeHealthThreshold;

        /// <summary>
        /// Whether entity can flee automatically.
        /// </summary>
        public bool CanAutoFlee;
    }

    /// <summary>
    /// Static helpers for combat state logic.
    /// </summary>
    public static class CombatStateHelpers
    {
        /// <summary>
        /// Checks if entity can attack in current state.
        /// </summary>
        public static bool CanAttack(CombatState state)
        {
            return state == CombatState.Engaged ||
                   state == CombatState.Approaching;
        }

        /// <summary>
        /// Checks if entity can move in current state.
        /// </summary>
        public static bool CanMove(CombatState state)
        {
            return state != CombatState.Stunned &&
                   state != CombatState.Dead &&
                   state != CombatState.KnockedDown &&
                   state != CombatState.Channeling;
        }

        /// <summary>
        /// Checks if entity can be targeted in current state.
        /// </summary>
        public static bool CanBeTargeted(CombatState state)
        {
            return state != CombatState.Dead;
        }

        /// <summary>
        /// Checks if entity is incapacitated.
        /// </summary>
        public static bool IsIncapacitated(CombatState state)
        {
            return state == CombatState.Stunned ||
                   state == CombatState.Dead ||
                   state == CombatState.KnockedDown;
        }

        /// <summary>
        /// Checks if state transition is valid.
        /// </summary>
        public static bool IsValidTransition(CombatState from, CombatState to)
        {
            // Dead is final
            if (from == CombatState.Dead && to != CombatState.Dead)
            {
                return false;
            }

            // Can't transition out of stun until duration expires
            if (from == CombatState.Stunned && to != CombatState.Recovering && to != CombatState.Dead)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Gets the default duration for a state.
        /// </summary>
        public static float GetDefaultDuration(CombatState state)
        {
            return state switch
            {
                CombatState.Recovering => 0.5f,
                CombatState.KnockedDown => 2f,
                CombatState.Stunned => 1f,
                _ => 0f
            };
        }
    }
}


using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.AI.Constraints
{
    /// <summary>
    /// Type of constraint that can be applied to actions.
    /// </summary>
    public enum ConstraintType : byte
    {
        NonLethal = 0,        // Only non-lethal combat allowed
        NoTrespass = 1,       // Cannot enter forbidden areas
        ObeyLeader = 2,       // Must follow leader orders
        ForbiddenMagic = 3,   // Cannot use forbidden abilities
        NoStealing = 4,       // Cannot take resources without permission
        NoAttacking = 5,      // Cannot attack (pacifist)
        Custom = 255
    }

    /// <summary>
    /// A constraint that limits what actions an entity can take.
    /// Constraints are checked by planners before selecting actions.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct ActionConstraint : IBufferElementData
    {
        /// <summary>
        /// Type of constraint.
        /// </summary>
        public ConstraintType Type;

        /// <summary>
        /// Whether constraint is currently enabled.
        /// </summary>
        public byte Enabled;

        /// <summary>
        /// Optional target action ID (if constraint applies to specific action).
        /// Empty means constraint applies to all actions of this type.
        /// </summary>
        public FixedString32Bytes TargetActionId;

        /// <summary>
        /// Optional target entity (e.g., forbidden area, forbidden target).
        /// </summary>
        public Entity TargetEntity;
    }
}




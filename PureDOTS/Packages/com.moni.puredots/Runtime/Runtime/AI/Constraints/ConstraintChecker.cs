using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.AI.Constraints
{
    /// <summary>
    /// Helper for checking if an action is allowed given constraints.
    /// </summary>
    public static class ConstraintChecker
    {
        /// <summary>
        /// Checks if an action is allowed given the entity's constraints.
        /// </summary>
        public static bool IsActionAllowed(
            in DynamicBuffer<ActionConstraint> constraints,
            FixedString32Bytes actionId,
            Entity targetEntity = default)
        {
            for (int i = 0; i < constraints.Length; i++)
            {
                var constraint = constraints[i];
                if (constraint.Enabled == 0)
                {
                    continue;
                }

                // Check if constraint applies to this action
                if (!constraint.TargetActionId.IsEmpty && !constraint.TargetActionId.Equals(actionId))
                {
                    continue;
                }

                // Check if constraint applies to this target
                if (constraint.TargetEntity != Entity.Null && constraint.TargetEntity != targetEntity)
                {
                    continue;
                }

                // Constraint blocks this action
                return false;
            }

            return true;
        }

        /// <summary>
        /// Checks if a constraint type is active for an entity.
        /// </summary>
        public static bool HasConstraint(
            in DynamicBuffer<ActionConstraint> constraints,
            ConstraintType constraintType)
        {
            for (int i = 0; i < constraints.Length; i++)
            {
                if (constraints[i].Type == constraintType && constraints[i].Enabled != 0)
                {
                    return true;
                }
            }
            return false;
        }
    }
}




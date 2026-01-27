using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.AI.Actions
{
    /// <summary>
    /// Component representing an action intent with primitive + target + parameters.
    /// Replaces/additional to EntityIntent for more structured action representation.
    /// </summary>
    public struct ActionIntent : IComponentData
    {
        /// <summary>
        /// Primitive action to execute.
        /// </summary>
        public ActionPrimitive Primitive;

        /// <summary>
        /// Target entity for this action (if applicable).
        /// </summary>
        public Entity TargetEntity;

        /// <summary>
        /// Target position for this action (if applicable).
        /// </summary>
        public float3 TargetPosition;

        /// <summary>
        /// Optional parameter (e.g., resource type, amount).
        /// </summary>
        public float Parameter;

        /// <summary>
        /// Optional parameter ID (e.g., resource type ID).
        /// </summary>
        public FixedString32Bytes ParameterId;

        /// <summary>
        /// Whether this intent is valid and should be executed.
        /// </summary>
        public byte IsValid;

        /// <summary>
        /// Tick when intent was set.
        /// </summary>
        public uint IntentSetTick;
    }
}




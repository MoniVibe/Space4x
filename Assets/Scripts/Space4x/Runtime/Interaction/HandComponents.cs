using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Runtime.Interaction
{
    /// <summary>
    /// Tag component marking entities that are currently being grabbed by the debug hand.
    /// AI systems should skip entities with this tag.
    /// </summary>
    public struct GrabbedTag : IComponentData 
    {
        /// <summary>
        /// The "hand" / controller entity that grabbed this entity.
        /// </summary>
        public Entity Holder;
    }

    /// <summary>
    /// Request to throw an entity. Written by hand system, consumed by throw systems.
    /// Enables queuing, slingshot mechanics, and determinism for rewind.
    /// </summary>
    public struct ThrowRequest : IBufferElementData
    {
        /// <summary>
        /// Entity to throw.
        /// </summary>
        public Entity Target;

        /// <summary>
        /// Normalized direction vector for the throw.
        /// </summary>
        public float3 Direction;

        /// <summary>
        /// Strength/magnitude of the throw.
        /// </summary>
        public float Strength;

        /// <summary>
        /// Origin point of the throw (for debugging/visualization).
        /// </summary>
        public float3 Origin;

        /// <summary>
        /// Tick when the throw was requested (for rewind compatibility).
        /// </summary>
        public uint Tick;
    }
}


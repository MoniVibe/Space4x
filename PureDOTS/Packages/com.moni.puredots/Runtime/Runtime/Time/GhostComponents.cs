using Unity.Entities;

namespace PureDOTS.Runtime.Components
{
    /// <summary>
    /// Tag component marking an entity as a ghost (preview entity during rewind).
    /// Ghosts are temporary entities that show historical positions/states during rewind preview.
    /// </summary>
    public struct GhostTag : IComponentData { }

    /// <summary>
    /// Reference to the original entity that this ghost represents.
    /// Used to track which real entity the ghost is previewing.
    /// </summary>
    public struct GhostSourceEntity : IComponentData
    {
        /// <summary>The entity this ghost is representing.</summary>
        public Entity SourceEntity;
    }

    /// <summary>
    /// The tick that this ghost is previewing.
    /// Ghosts update their positions based on this tick and ComponentHistory from the source entity.
    /// </summary>
    public struct GhostPreviewTick : IComponentData
    {
        /// <summary>The tick this ghost represents in history.</summary>
        public int Tick;
    }

    /// <summary>
    /// Optional component for ghost visual styling/rendering hints.
    /// Can be used by rendering systems to apply special effects to ghosts.
    /// </summary>
    public struct GhostVisualStyle : IComponentData
    {
        /// <summary>Opacity/alpha for ghost rendering (0-1).</summary>
        public float Opacity;
        /// <summary>Color tint for ghost rendering.</summary>
        public Unity.Mathematics.float4 ColorTint;
    }

    /// <summary>
    /// Tag component marking an entity as a ghost tether line.
    /// </summary>
    public struct GhostTetherTag : IComponentData { }

    /// <summary>
    /// Link data for a ghost tether, tying the tether to its ghost + source entities.
    /// </summary>
    public struct GhostTetherLink : IComponentData
    {
        public Entity GhostEntity;
        public Entity SourceEntity;
    }
}

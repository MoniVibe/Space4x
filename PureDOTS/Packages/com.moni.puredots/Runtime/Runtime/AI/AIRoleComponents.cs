using Unity.Entities;

namespace PureDOTS.Runtime.AI
{
    /// <summary>
    /// Declares the tactical or strategic role an AI-controlled entity fulfills (peacekeeper, army, strike craft, etc).
    /// Values are data-driven and interpreted by each game.
    /// </summary>
    public struct AIRole : IComponentData
    {
        public ushort RoleId;
    }

    /// <summary>
    /// Declares the doctrine or rules-of-engagement assigned to an entity.
    /// </summary>
    public struct AIDoctrine : IComponentData
    {
        public ushort DoctrineId;
    }

    /// <summary>
    /// References the behavior profile blob (utility tree, HTN, etc) currently driving an entity.
    /// </summary>
    public struct AIBehaviorProfile : IComponentData
    {
        /// <summary>
        /// Optional identifier for the profile asset (e.g., enum or catalog row).
        /// </summary>
        public ushort ProfileId;

        /// <summary>
        /// Hash representing the baked profile blob/config.
        /// </summary>
        public uint ProfileHash;

        /// <summary>
        /// Optional entity pointing to the profile blob for direct lookups.
        /// </summary>
        public Entity ProfileEntity;

        /// <summary>
        /// Source identifier describing who assigned the profile (0 = unknown, 1 = scenario, 2 = job, ...).
        /// </summary>
        public byte SourceId;
    }
}

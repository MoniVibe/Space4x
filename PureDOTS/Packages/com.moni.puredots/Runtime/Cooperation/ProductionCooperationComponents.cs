using Unity.Entities;
using Unity.Collections;

namespace PureDOTS.Runtime.Cooperation
{
    /// <summary>
    /// Production role in a team.
    /// </summary>
    public enum ProductionRole : byte
    {
        Leader = 0,
        Builder = 1,
        Craftsman = 2,
        Assistant = 3,
        Hauler = 4
    }

    /// <summary>
    /// Production team status.
    /// </summary>
    public enum ProductionTeamStatus : byte
    {
        Forming = 0,
        Active = 1,
        Completed = 2,
        Disbanded = 3
    }

    /// <summary>
    /// Production team component.
    /// </summary>
    public struct ProductionTeam : IComponentData
    {
        public Entity Leader;
        public byte MemberCount;
        public float Cohesion; // 0-1, affects quality/efficiency
        public ProductionTeamStatus Status;
    }

    /// <summary>
    /// Member entry for a production team.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct ProductionTeamMember : IBufferElementData
    {
        public Entity MemberEntity;
        public ProductionRole Role;
        public float ContributionWeight;
        public float SkillFactor;
    }

    /// <summary>
    /// Crafting phase.
    /// </summary>
    public enum CraftingPhase : byte
    {
        Planning = 0,
        MaterialPrep = 1,
        Assembly = 2,
        Refinement = 3,
        QualityControl = 4,
        Completed = 5
    }

    /// <summary>
    /// Collaborative crafting component.
    /// </summary>
    public struct CollaborativeCrafting : IComponentData
    {
        public Entity TeamEntity;
        public FixedString64Bytes ItemName;
        public CraftingPhase Phase;
        public float Progress; // 0-1
        public float Quality; // 0-1, final quality
        public uint StartedTick;
    }
}


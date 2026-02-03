using Unity.Collections;
using Unity.Entities;

namespace Space4X.Registry
{
    /// <summary>
    /// Links a faction to its parent empire (if any).
    /// </summary>
    public struct EmpireMembership : IComponentData
    {
        public Entity Empire;
        public half Loyalty;
        public byte Autonomy; // 0-100, higher = more autonomous
        public uint JoinedTick;
    }

    /// <summary>
    /// Buffer of member factions for an empire entity.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct EmpireFactionEntry : IBufferElementData
    {
        public Entity Faction;
        public half Loyalty;
        public byte Autonomy;
        public uint JoinedTick;
    }

    /// <summary>
    /// Membership entry for a guild. Allows cross-faction and cross-empire membership.
    /// </summary>
    [InternalBufferCapacity(2)]
    public struct GuildMembershipEntry : IBufferElementData
    {
        public Entity Guild;
        public half Loyalty;
        public byte Status; // 0 = active
        public uint JoinedTick;
    }

    /// <summary>
    /// Member entry stored on a guild entity.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct GuildMemberEntry : IBufferElementData
    {
        public Entity Member;
        public AffiliationType MemberType;
        public half Loyalty;
        public byte Status; // 0 = active
        public uint JoinedTick;
    }

    /// <summary>
    /// Single-guild representation for a business entity.
    /// </summary>
    public struct BusinessGuildLink : IComponentData
    {
        public Entity Guild;
        public half RepresentationStrength;
        public uint LinkedTick;
    }
}

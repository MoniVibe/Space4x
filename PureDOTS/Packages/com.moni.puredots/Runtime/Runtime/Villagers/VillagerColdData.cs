using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Villagers
{
    /// <summary>
    /// Cold data companion component for villagers (AOSoA optimization).
    /// Moved to companion entity to keep hot archetype cache-friendly.
    /// </summary>
    public struct VillagerColdData : IComponentData
    {
        public FixedString64Bytes Name;
        public FixedString128Bytes Biography;
        public int FactionId;
        public uint BirthTick;
    }
    
    /// <summary>
    /// Reference to companion entity containing cold data.
    /// Hot archetype holds only the reference (4 bytes) instead of full cold data.
    /// </summary>
    public struct VillagerColdDataRef : IComponentData
    {
        public Entity CompanionEntity;
    }
    
    /// <summary>
    /// Static skill caps moved to companion entity (cold data).
    /// </summary>
    public struct VillagerSkillCaps : IComponentData
    {
        public byte MaxPhysique;
        public byte MaxFinesse;
        public byte MaxWillpower;
        public byte MaxStrength;
        public byte MaxAgility;
        public byte MaxIntelligence;
        public byte MaxWisdom;
    }
}


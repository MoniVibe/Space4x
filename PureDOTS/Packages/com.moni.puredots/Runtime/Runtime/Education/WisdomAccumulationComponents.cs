using Unity.Entities;

namespace PureDOTS.Runtime.Education
{
    /// <summary>
    /// Tracks accumulated wisdom for villages, families, or dynasties.
    /// Used for cultural knowledge transmission across generations.
    /// </summary>
    public struct WisdomAccumulation : IComponentData
    {
        /// <summary>Cultural wisdom (village average).</summary>
        public float CulturalWisdom;
        
        /// <summary>Familial wisdom (family average).</summary>
        public float FamilialWisdom;
        
        /// <summary>Dynastic wisdom (multi-generation average).</summary>
        public float DynasticWisdom;
        
        /// <summary>Generations tracked.</summary>
        public ushort GenerationsTracked;
        
        /// <summary>Count of high-wisdom members (Wisdom 60+).</summary>
        public ushort HighWisdomMemberCount;
    }
}

























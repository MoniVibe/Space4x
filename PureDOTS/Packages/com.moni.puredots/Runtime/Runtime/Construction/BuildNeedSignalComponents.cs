using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Construction
{
    /// <summary>
    /// Build-relevant signal emitted by individuals based on their needs.
    /// Buffered on group entities (village, guild, colony, etc.).
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct BuildNeedSignal : IBufferElementData
    {
        /// <summary>Category of building needed.</summary>
        public BuildCategory Category;
        
        /// <summary>Strength of need (0-1).</summary>
        public float Strength;
        
        /// <summary>Where the need is felt (for placement guidance).</summary>
        public float3 Position;
        
        /// <summary>Entity that emitted this signal.</summary>
        public Entity SourceEntity;
        
        /// <summary>Tick when signal was emitted.</summary>
        public uint EmittedTick;
    }
}
























using Unity.Entities;

namespace PureDOTS.Runtime.Narrative
{
    /// <summary>
    /// Generic effect request from narrative systems.
    /// </summary>
    [InternalBufferCapacity(16)]
    public struct NarrativeEffectRequest : IBufferElementData
    {
        public int EffectType;
        public int ParamA;
        public int ParamB;
        public Entity SituationEntity;
    }
}


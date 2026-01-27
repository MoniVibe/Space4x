using Unity.Entities;

namespace PureDOTS.Runtime.Narrative
{
    /// <summary>
    /// World fact event for situation condition evaluation.
    /// Written by game-side systems, consumed by SituationUpdateSystem.
    /// </summary>
    [InternalBufferCapacity(16)]
    public struct WorldFactEvent : IBufferElementData
    {
        public int FactType;          // BandArrived, HostagesKilled, RebelsArmed, etc.
        public Entity Subject;
        public Entity Related;
        public int ParamA;
        public int ParamB;
    }
}


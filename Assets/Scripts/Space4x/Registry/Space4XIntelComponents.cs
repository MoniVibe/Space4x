using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    public enum IntelFactType : byte
    {
        Unknown = 0,
        CommandNode = 1,
        PriorityTarget = 2,
        Threat = 3,
        HighValue = 4,
        Objective = 5
    }

    [InternalBufferCapacity(4)]
    public struct IntelTargetFact : IBufferElementData
    {
        public Entity Target;
        public IntelFactType Type;
        public half Confidence;
        public half Weight;
        public uint LastSeenTick;
        public uint ExpireTick;
    }

    public struct Space4XIntelTuning : IComponentData
    {
        public float BaseWeight;
        public float CommandNodeBonus;
        public float PriorityTargetBonus;
        public float ThreatBonus;
        public float HighValueBonus;
        public float ObjectiveBonus;
        public float MaxBonus;
        public float DecayPerTick;
        public float MinConfidence;

        public static Space4XIntelTuning Default => new Space4XIntelTuning
        {
            BaseWeight = 0.25f,
            CommandNodeBonus = 0.8f,
            PriorityTargetBonus = 0.6f,
            ThreatBonus = 0.4f,
            HighValueBonus = 0.35f,
            ObjectiveBonus = 0.5f,
            MaxBonus = 1.6f,
            DecayPerTick = 0.0025f,
            MinConfidence = 0.05f
        };
    }
}

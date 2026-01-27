// [TRI-STUB] Stub components for sense organ system
using Unity.Entities;
using Unity.Collections;

namespace PureDOTS.Runtime.Perception
{
    /// <summary>
    /// Sense organ state - individual sense organ properties.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct SenseOrganState : IBufferElementData
    {
        public SenseOrganType OrganType;
        public PerceptionChannel Channels;
        public float Gain;
        public float Condition;
        public float NoiseFloor;
        public float RangeMultiplier;
    }

    /// <summary>
    /// Sense organ types.
    /// </summary>
    public enum SenseOrganType : byte
    {
        Eye = 0,
        Ear = 1,
        Nose = 2,
        EMSuite = 3,
        GraviticArray = 4,
        ExoticSensor = 5,
        ParanormalOrgan = 6
    }
}


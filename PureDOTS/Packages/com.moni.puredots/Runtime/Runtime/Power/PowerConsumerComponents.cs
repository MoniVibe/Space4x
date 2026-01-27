using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Power
{
    /// <summary>
    /// Definition of a power consumer type (blob asset).
    /// </summary>
    public struct PowerConsumerDefBlob
    {
        public int ConsumerDefId;
        public float BaseDemand;            // MW at full load
        public float MinOperationalFraction;// fraction of BaseDemand needed to be "Online"
        public byte PriorityTier;           // 0 = highest, 255 = lowest
    }

    /// <summary>
    /// Runtime state of a power consumer.
    /// </summary>
    public struct PowerConsumerState : IComponentData
    {
        public int ConsumerDefId;
        public float RequestedDemand;
        public float Supplied;              // assigned this tick
        public byte Online;                 // 0/1
    }

    /// <summary>
    /// Registry blob asset containing all power consumer definitions.
    /// </summary>
    public struct PowerConsumerDefRegistryBlob
    {
        public BlobArray<PowerConsumerDefBlob> ConsumerDefs;
    }

    /// <summary>
    /// Singleton component exposing compiled power consumer definitions.
    /// </summary>
    public struct PowerConsumerDefRegistry : IComponentData
    {
        public BlobAssetReference<PowerConsumerDefRegistryBlob> Value;
    }
}


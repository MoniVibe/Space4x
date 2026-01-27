using Unity.Entities;

namespace PureDOTS.Runtime.Components
{
    /// <summary>
    /// Runtime pooling configuration resolved from authoring data.
    /// </summary>
    public struct PoolingSettingsConfig : IComponentData
    {
        public int NativeListCapacity;
        public int NativeQueueCapacity;
        public int DefaultEntityPrewarmCount;
        public int EntityPoolMaxReserve;
        public int EcbPoolCapacity;
        public int EcbWriterPoolCapacity;
        public bool ResetOnRewind;
    }

    /// <summary>
    /// Singleton baked at runtime that exposes the active pooling settings.
    /// </summary>
    public struct PoolingSettings : IComponentData
    {
        public PoolingSettingsConfig Value;
    }
}


using Unity.Entities;

namespace Space4X.Registry
{
    /// <summary>
    /// Growth policy settings for crew breeding/cloning. Defaults to disabled.
    /// </summary>
    public struct CrewGrowthSettings : IComponentData
    {
        public byte BreedingEnabled;
        public byte CloningEnabled;
        public float BreedingRatePerTick;
        public float CloningRatePerTick;
        public float CloningResourceCost;
        public byte DoctrineAllowsBreeding;
        public byte DoctrineAllowsCloning;
        public uint LastConfiguredTick;
    }

    /// <summary>
    /// Runtime population state for crew-bearing entities.
    /// </summary>
    public struct CrewGrowthState : IComponentData
    {
        public float CurrentCrew;
        public float Capacity;
    }

    /// <summary>
    /// Telemetry counters for growth attempts; populated even when growth is disabled for visibility.
    /// </summary>
    public struct CrewGrowthTelemetry : IComponentData
    {
        public uint LastUpdateTick;
        public uint BreedingAttempts;
        public uint CloningAttempts;
        public uint GrowthSkipped;
    }

    /// <summary>
    /// Command-style log entry for growth decisions so rewind/playback can reconstruct changes later.
    /// </summary>
    public struct CrewGrowthCommandLogEntry : IBufferElementData
    {
        public uint Tick;
        public Entity TargetEntity;
        public float DeltaCrew;
        public byte WasBreeding;
        public byte WasCloning;
        public byte SkippedByPolicy;
    }
}

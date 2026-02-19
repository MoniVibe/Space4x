using Unity.Entities;

namespace Space4X.Registry
{
    /// <summary>
    /// Runtime diagnostics for player flagship fixed-tick input integration.
    /// </summary>
    public struct PlayerFlagshipInputTickDiagnostics : IComponentData
    {
        public uint Tick;
        public uint TickDeltaObserved;
        public uint TickStepsProcessed;
        public uint TickBacklog;
        public uint MaxBacklogObserved;
        public float FixedDeltaTime;
        public float SpeedMultiplier;
    }
}

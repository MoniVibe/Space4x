using Unity.Entities;

namespace PureDOTS.Runtime.Scenarios
{
    /// <summary>
    /// Headless scenario runner tick metadata for systems that need a deterministic tick source.
    /// </summary>
    public struct ScenarioRunnerTick : IComponentData
    {
        public uint Tick;
        public float WorldSeconds;
    }
}

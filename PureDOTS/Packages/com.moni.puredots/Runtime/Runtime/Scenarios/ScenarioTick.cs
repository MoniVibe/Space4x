using Unity.Entities;

namespace PureDOTS.Runtime.Scenarios
{
    /// <summary>
    /// Shared telemetry clock that increments once per simulation step, allowing all systems to agree on a deterministic tick index.
    /// </summary>
    public struct ScenarioTick : IComponentData
    {
        public uint Value;
    }
}

using Unity.Entities;

namespace Space4X.Registry
{
    /// <summary>
    /// Marks an entity as temporarily disabled for simulation (e.g. despawned while docked).
    /// Systems should opt out via WithNone<SimulationDisabledTag>.
    /// </summary>
    public struct SimulationDisabledTag : IComponentData
    {
    }
}

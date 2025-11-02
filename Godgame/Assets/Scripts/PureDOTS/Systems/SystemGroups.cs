using Unity.Entities;
using Unity.Physics.Systems;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Custom system group for time management systems.
    /// Runs first in InitializationSystemGroup.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup), OrderFirst = true)]
    public partial class TimeSystemGroup : ComponentSystemGroup { }

    /// <summary>
    /// System group for rewind/history recording.
    /// Runs after simulation to capture state.
    /// </summary>
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    public partial class HistorySystemGroup : ComponentSystemGroup { }

    /// <summary>
    /// System group for villager AI and behavior.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(FixedStepSimulationSystemGroup))]
    public partial class VillagerSystemGroup : ComponentSystemGroup { }

    /// <summary>
    /// System group for resource management.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(VillagerSystemGroup))]
    public partial class ResourceSystemGroup : ComponentSystemGroup { }

    /// <summary>
    /// System group for combat systems.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(BuildPhysicsWorld))]
    [UpdateBefore(typeof(ExportPhysicsWorld))]
    public partial class CombatSystemGroup : ComponentSystemGroup { }

    /// <summary>
    /// System group for divine hand interaction.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(BuildPhysicsWorld))]
    [UpdateBefore(typeof(ExportPhysicsWorld))]
    public partial class HandSystemGroup : ComponentSystemGroup { }

    /// <summary>
    /// System group for vegetation systems.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(FixedStepSimulationSystemGroup))]
    public partial class VegetationSystemGroup : ComponentSystemGroup { }

    /// <summary>
    /// System group for construction systems.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ResourceSystemGroup))]
    public partial class ConstructionSystemGroup : ComponentSystemGroup { }

    /// <summary>
    /// Late simulation group for cleanup and state recording.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    public partial class LateSimulationSystemGroup : ComponentSystemGroup { }
}

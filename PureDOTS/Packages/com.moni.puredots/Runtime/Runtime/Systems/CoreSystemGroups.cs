using Unity.Entities;

namespace PureDOTS.Systems
{
#if FALSE
    // Legacy duplicate definitions - disabled. Canonical definitions are in PureDOTS.Systems assembly.
    // See: Runtime/Systems/SystemGroups.cs for the authoritative versions.

    /// <summary>
    /// High level gameplay simulation group containing domain-specific subgroups.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.Default)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class GameplaySystemGroup : ComponentSystemGroup { }

    /// <summary>
    /// Environment simulation group; runs after physics and before spatial indexing.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.Default)]
    [UpdateInGroup(typeof(GameplaySystemGroup))]
    public partial class EnvironmentSystemGroup : ComponentSystemGroup { }

    /// <summary>
    /// Shared AI systems that feed data into gameplay domains.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.Default)]
    [UpdateInGroup(typeof(GameplaySystemGroup))]
    public partial class AISystemGroup : ComponentSystemGroup { }

    /// <summary>
    /// System group for villager AI and behavior.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.Default)]
    [UpdateInGroup(typeof(AISystemGroup))]
    public partial class VillagerSystemGroup : ComponentSystemGroup { }

    /// <summary>
    /// System group for divine hand interaction.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.Default)]
    [UpdateInGroup(typeof(GameplaySystemGroup))]
    public partial class HandSystemGroup : ComponentSystemGroup { }

    /// <summary>
    /// Spatial systems run after environment state updates and before gameplay logic.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.Default)]
    [UpdateInGroup(typeof(GameplaySystemGroup))]
    public partial class SpatialSystemGroup : ComponentSystemGroup { }

    /// <summary>
    /// Transport/logistics phase that runs after spatial updates but before general gameplay systems.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.Default)]
    [UpdateInGroup(typeof(GameplaySystemGroup))]
    public partial class TransportPhaseGroup : ComponentSystemGroup { }
#endif
}


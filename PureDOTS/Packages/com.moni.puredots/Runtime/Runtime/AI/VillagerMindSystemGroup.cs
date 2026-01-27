using PureDOTS.Runtime.Core;
using Unity.Entities;

namespace PureDOTS.Runtime.AI
{
    /// <summary>
    /// Logical slice for villager Mind pillar systems (needs, focus, intent).
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class VillagerMindSystemGroup : ComponentSystemGroup
    {
    }
}

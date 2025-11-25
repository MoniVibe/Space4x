using PureDOTS.Systems;
using Unity.Entities;

namespace Space4X.Systems.AI
{
    /// <summary>
    /// System group for AI behavior systems. Processes after compliance/aggregation systems
    /// and before presentation/registry bridge systems.
    /// </summary>
    [UpdateInGroup(typeof(SpatialSystemGroup))]
    [UpdateAfter(typeof(Space4X.Registry.Space4XAffiliationComplianceSystem))]
    public partial class Space4XAISystemGroup : ComponentSystemGroup
    {
    }
}


using PureDOTS.Systems;
using Unity.Entities;

namespace Space4X.Systems.AI
{
    /// <summary>
    /// System group for Space4X-specific vessel AI decision systems.
    /// Runs before PureDOTS ResourceSystemGroup to evaluate vessel goals and assign targets.
    /// </summary>
    /// <remarks>
    /// This group hosts gameplay-specific vessel AI logic that was previously in PureDOTS.
    /// PureDOTS systems now consume the decisions made here via hooks/interfaces.
    /// </remarks>
    [UpdateInGroup(typeof(GameplaySystemGroup))]
    [UpdateAfter(typeof(AISystemGroup))]
    [UpdateBefore(typeof(PureDOTS.Systems.ResourceSystemGroup))]
    public partial class Space4XTransportAISystemGroup : ComponentSystemGroup { }
}


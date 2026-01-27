using Unity.Entities;

namespace PureDOTS.Runtime.Modularity
{
    /// <summary>
    /// Opt-in tag for attaching the Needs module to an entity.
    /// Entities with this tag will be bootstrapped with needs state/components.
    /// </summary>
    public struct NeedsModuleTag : IComponentData
    {
    }

    /// <summary>
    /// Opt-in tag for attaching the Relations module to an entity.
    /// Entities with this tag will be bootstrapped with relationship buffers/state.
    /// </summary>
    public struct RelationsModuleTag : IComponentData
    {
    }

    /// <summary>
    /// Opt-in tag for attaching the Profile module to an entity.
    /// Entities with this tag will be bootstrapped with alignment/personality/morale state.
    /// </summary>
    public struct ProfileModuleTag : IComponentData
    {
    }

    /// <summary>
    /// Opt-in tag for attaching the Agency module to an entity.
    /// Entities with this tag will be bootstrapped with control/agency scaffolding.
    /// </summary>
    public struct AgencyModuleTag : IComponentData
    {
    }

    /// <summary>
    /// Opt-in tag for attaching the Communication module to an entity.
    /// Entities with this tag will be bootstrapped with comm endpoints + buffers.
    /// </summary>
    public struct CommunicationModuleTag : IComponentData
    {
    }

    /// <summary>
    /// Opt-in tag for attaching the group knowledge cache module to an entity.
    /// Entities with this tag will be bootstrapped with bounded group knowledge buffers.
    /// </summary>
    public struct GroupKnowledgeModuleTag : IComponentData
    {
    }
}

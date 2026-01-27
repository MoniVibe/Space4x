using Unity.Entities;

namespace PureDOTS.Runtime.Registry
{
    /// <summary>
    /// Stable content identity used by presentation systems to resolve render bindings.
    /// </summary>
    public struct RegistryIdentity : IComponentData
    {
        public RegistryId Id;
    }
}

using Unity.Entities;
using Unity.Transforms;
using PureDOTS.Runtime.Components;

// Register closed generic history buffer types with the TypeManager.
// Add more [assembly: ...] lines here as you start tracking more components.
[assembly: RegisterGenericComponentType(typeof(ComponentHistory<LocalTransform>))]

namespace PureDOTS.Runtime.Time
{
    /// <summary>
    /// Dummy class so the file has a type; the real work is the assembly attribute above.
    /// This file registers generic component types used by the time history system.
    /// </summary>
    internal static class TimeHistoryRegistrations
    {
    }
}


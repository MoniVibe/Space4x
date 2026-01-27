using PureDOTS.Runtime.Components;
using Unity.Entities;

namespace PureDOTS.Runtime.Miracles
{
    /// <summary>
    /// Tracks previous activation state for edge detection in request creation.
    /// Used by MiracleRequestCreationSystem to detect activation signal transitions.
    /// </summary>
    public struct MiracleRequestCreationState : IComponentData
    {
        /// <summary>Previous IsActivating value (0/1).</summary>
        public byte PreviousIsActivating;

        /// <summary>Previous IsSustained value (0/1).</summary>
        public byte PreviousIsSustained;
    }
}


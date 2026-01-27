using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Components
{
    /// <summary>
    /// [OBSOLETE] Legacy divine hand command component. Use PureDOTS.Runtime.Hand.HandCommand buffer instead.
    /// Migration: Emit commands to DynamicBuffer&lt;PureDOTS.Runtime.Hand.HandCommand&gt; instead.
    /// </summary>
    [System.Obsolete("Use PureDOTS.Runtime.Hand.HandCommand buffer instead. This component is deprecated and will be removed in a future version.")]
    public struct DivineHandCommand : IComponentData
    {
        public DivineHandCommandType Type;
        public Entity TargetEntity;
        public float3 TargetPosition;
        public float3 TargetNormal;
        public float TimeSinceIssued;
    }
}

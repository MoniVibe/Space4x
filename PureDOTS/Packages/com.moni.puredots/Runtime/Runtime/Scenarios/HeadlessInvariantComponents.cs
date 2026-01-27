using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Components
{
    /// <summary>
    /// Tracks last-known movement state for headless invariant checks.
    /// </summary>
    public struct HeadlessInvariantState : IComponentData
    {
        public byte Initialized;
        public float3 LastPosition;
        public quaternion LastRotation;
        public uint LastProgressTick;
        public float LastAngularSpeed;
    }
}

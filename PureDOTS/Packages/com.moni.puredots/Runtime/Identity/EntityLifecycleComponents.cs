using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Identity
{
    public enum EntityLifecycleStatus : byte
    {
        Uninitialized = 0,
        Alive = 1,
        Dormant = 2,
        Disabled = 3,
        Destroyed = 4
    }

    public struct EntityLifecycle : IComponentData
    {
        public EntityLifecycleStatus Status;
        public uint SpawnTick;
        public uint LastChangeTick;
        public FixedString64Bytes Reason;
    }
}




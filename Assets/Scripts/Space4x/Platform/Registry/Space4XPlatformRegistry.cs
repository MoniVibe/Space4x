using PureDOTS.Runtime.Platform;
using PureDOTS.Runtime.Registry;
using Unity.Collections;
using Unity.Entities;

namespace Space4X.Platform.Registry
{
    /// <summary>
    /// Registry bridge component for platforms.
    /// </summary>
    public struct Space4XPlatform : IComponentData
    {
        public FixedString64Bytes PlatformId;
        public PlatformFlags Flags;
        public Space4XPlatformStatus Status;
        public int SectorId;
    }

    /// <summary>
    /// Operational status for platforms.
    /// </summary>
    public enum Space4XPlatformStatus : byte
    {
        Operational = 0,
        Damaged = 1,
        Docked = 2,
        Destroyed = 3
    }
}






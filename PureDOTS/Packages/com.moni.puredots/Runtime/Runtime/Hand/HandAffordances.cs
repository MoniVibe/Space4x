using System;
using Unity.Entities;

namespace PureDOTS.Runtime.Hand
{
    /// <summary>
    /// Affordance flags indicating what actions are available on the hovered target.
    /// Updated by HandAffordanceSystem based on target entity components.
    /// </summary>
    [Flags]
    public enum HandAffordanceFlags : byte
    {
        None = 0,
        CanPickUp = 1 << 0,
        CanSiphon = 1 << 1,
        CanDumpStorehouse = 1 << 2,
        CanDumpConstruction = 1 << 3,
        CanDumpGround = 1 << 4,
        CanCastMiracle = 1 << 5,
    }

    /// <summary>
    /// Affordances available on the current hover target.
    /// </summary>
    public struct HandAffordances : IComponentData
    {
        public HandAffordanceFlags Flags;
        public Entity TargetEntity;
        public ushort ResourceTypeIndex;  // If siphonable/dumpable
    }
}


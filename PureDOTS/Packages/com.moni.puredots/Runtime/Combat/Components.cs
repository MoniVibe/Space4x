using System;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Combat
{
    /// <summary>
    /// Limb/body part type for granular health tracking.
    /// </summary>
    public enum LimbType : byte
    {
        Head,
        Torso,
        LeftArm,
        RightArm,
        LeftLeg,
        RightLeg,
        Tail,
        ExtraLimb1,
        ExtraLimb2
    }

    /// <summary>
    /// Flags indicating limb modifications (augmented, mutated, grafted, severed).
    /// </summary>
    [Flags]
    public enum LimbFlags : byte
    {
        Normal      = 0,
        Augmented   = 1 << 0,
        Mutated     = 1 << 1,
        Grafted     = 1 << 2,
        Severed     = 1 << 3,
    }

    /// <summary>
    /// Health state for a specific limb/body part.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct LimbState : IBufferElementData
    {
        public LimbType Limb;
        public float MaxHP;
        public float CurrentHP;
        public LimbFlags Flags;
    }
}


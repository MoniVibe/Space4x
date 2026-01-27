using System;
using PureDOTS.Runtime.Registry;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Components
{
    // Ability / Special action domain components
    public struct AbilityId : IComponentData
    {
        public FixedString64Bytes Value;
    }

    public struct AbilityState : IComponentData
    {
        public float CooldownRemaining;
        public byte Charges;
        public byte Flags;
        public Entity Owner;
    }

    public static class AbilityStatusFlags
    {
        public const byte Ready = 1 << 0;
        public const byte CoolingDown = 1 << 1;
        public const byte Disabled = 1 << 2;
    }

    public struct AbilityRegistry : IComponentData
    {
        public int TotalAbilities;
        public int ReadyAbilityCount;
        public uint LastUpdateTick;
    }

    public struct AbilityRegistryEntry : IBufferElementData, IComparable<AbilityRegistryEntry>, IRegistryEntry, IRegistryFlaggedEntry
    {
        public Entity AbilityEntity;
        public FixedString64Bytes AbilityId;
        public Entity Owner;
        public float CooldownRemaining;
        public byte Charges;
        public byte Flags;

        public int CompareTo(AbilityRegistryEntry other)
        {
            return AbilityEntity.Index.CompareTo(other.AbilityEntity.Index);
        }

        public Entity RegistryEntity => AbilityEntity;

        public byte RegistryFlags => Flags;
    }
}

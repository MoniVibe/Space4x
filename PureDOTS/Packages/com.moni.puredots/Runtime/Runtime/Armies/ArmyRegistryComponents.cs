using System;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Registry;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Armies
{
    public struct ArmyRegistry : IComponentData
    {
        public int ArmyCount;
        public uint LastUpdateTick;
        public int SupplyRequests;
        public int ResolvedRequests;
    }

    public struct ArmyRegistryEntry : IBufferElementData, IComparable<ArmyRegistryEntry>, IRegistryEntry, IRegistryFlaggedEntry
    {
        public Entity ArmyEntity;
        public ArmyId Id;
        public float3 Position;
        public byte Flags;
        public int CellId;
        public uint SpatialVersion;
        public float SupplyLevel;
        public float Morale;
        public float Fatigue;

        public int CompareTo(ArmyRegistryEntry other)
        {
            return ArmyEntity.Index.CompareTo(other.ArmyEntity.Index);
        }

        public Entity RegistryEntity => ArmyEntity;

        public byte RegistryFlags => Flags;
    }
}

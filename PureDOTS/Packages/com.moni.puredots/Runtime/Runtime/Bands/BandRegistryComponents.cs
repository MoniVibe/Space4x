using System;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Registry;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Bands
{
    public struct BandRegistry : IComponentData
    {
        public int TotalBands;
        public int TotalMembers;
        public float AverageMorale;
        public float AverageCohesion;
        public float AverageDiscipline;
        public uint LastUpdateTick;
        public uint LastSpatialVersion;
        public int SpatialResolvedCount;
        public int SpatialFallbackCount;
        public int SpatialUnmappedCount;
    }

    public struct BandRegistryEntry : IBufferElementData, IComparable<BandRegistryEntry>, IRegistryEntry, IRegistryFlaggedEntry
    {
        public Entity BandEntity;
        public int BandId;
        public float3 Position;
        public int MemberCount;
        public float Morale;
        public float Cohesion;
        public float AverageDiscipline;
        public BandStatusFlags Flags;
        public int CellId;
        public uint SpatialVersion;

        public int CompareTo(BandRegistryEntry other)
        {
            return BandEntity.Index.CompareTo(other.BandEntity.Index);
        }

        public Entity RegistryEntity => BandEntity;

        public byte RegistryFlags => (byte)Flags;
    }
}

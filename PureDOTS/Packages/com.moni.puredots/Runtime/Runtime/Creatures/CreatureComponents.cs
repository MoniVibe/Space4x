using System;
using PureDOTS.Runtime.Registry;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Components
{
    /// <summary>
    /// Identifier assigned to each creature or environmental threat entity.
    /// </summary>
    public struct CreatureId : IComponentData
    {
        public int Value;
    }

    /// <summary>
    /// Describes type metadata and threat metrics for a creature.
    /// </summary>
    public struct CreatureAttributes : IComponentData
    {
        public FixedString64Bytes TypeId;
        public float ThreatLevel;
        public byte Flags;
    }

    /// <summary>
    /// Flags describing current disposition/state of a creature.
    /// </summary>
    public static class CreatureStatusFlags
    {
        public const byte Hostile = 1 << 0;
        public const byte Passive = 1 << 1;
        public const byte Allied = 1 << 2;
    }

    /// <summary>
    /// Registry summary for aggregate creature metrics.
    /// </summary>
    public struct CreatureRegistry : IComponentData
    {
        public int TotalCreatures;
        public float TotalThreatScore;
        public uint LastUpdateTick;
        public uint LastSpatialVersion;
        public int SpatialResolvedCount;
        public int SpatialFallbackCount;
        public int SpatialUnmappedCount;
    }

    /// <summary>
    /// Snapshot entry describing a single creature in the registry buffer.
    /// </summary>
    public struct CreatureRegistryEntry : IBufferElementData, IComparable<CreatureRegistryEntry>, IRegistryEntry, IRegistryFlaggedEntry
    {
        public Entity CreatureEntity;
        public int CreatureId;
        public FixedString64Bytes TypeId;
        public float ThreatLevel;
        public byte Flags;
        public float3 Position;
        public int CellId;
        public uint SpatialVersion;

        public int CompareTo(CreatureRegistryEntry other)
        {
            return CreatureEntity.Index.CompareTo(other.CreatureEntity.Index);
        }

        public Entity RegistryEntity => CreatureEntity;

        public byte RegistryFlags => Flags;
    }
}


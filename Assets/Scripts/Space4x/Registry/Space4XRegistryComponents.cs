using System;
using PureDOTS.Runtime.Registry;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    /// <summary>
    /// Identifies a Space4X colony entity and carries minimal summary data for the registry bridge.
    /// </summary>
    public struct Space4XColony : IComponentData
    {
        public FixedString64Bytes ColonyId;
        public float Population;
        public float StoredResources;
        public Space4XColonyStatus Status;
        public int SectorId;
    }

    /// <summary>
    /// Operational state for a colony. Used to derive registry flags.
    /// </summary>
    public enum Space4XColonyStatus : byte
    {
        Dormant = 0,
        Growing = 1,
        Besieged = 2,
        InCrisis = 3
    }

    /// <summary>
    /// Identifies a fleet entity and exposes minimal summary data for the registry bridge.
    /// </summary>
    public struct Space4XFleet : IComponentData
    {
        public FixedString64Bytes FleetId;
        public int ShipCount;
        public Space4XFleetPosture Posture;
        public int TaskForce;
    }

    /// <summary>
    /// High level posture for a fleet. Used to set registry flags.
    /// </summary>
    public enum Space4XFleetPosture : byte
    {
        Idle = 0,
        Patrol = 1,
        Engaging = 2,
        Retreating = 3,
        Docked = 4
    }

    /// <summary>
    /// Aggregated registry summary for all Space4X colonies.
    /// </summary>
    public struct Space4XColonyRegistry : IComponentData
    {
        public int ColonyCount;
        public float TotalPopulation;
        public float TotalStoredResources;
        public uint LastUpdateTick;
    }

    /// <summary>
    /// Aggregated registry summary for all Space4X fleets.
    /// </summary>
    public struct Space4XFleetRegistry : IComponentData
    {
        public int FleetCount;
        public int TotalShips;
        public uint LastUpdateTick;
        public int ActiveEngagementCount;
    }

    /// <summary>
    /// Lightweight snapshot cached by the registry bridge so presentation systems can publish telemetry.
    /// </summary>
    public struct Space4XRegistrySnapshot : IComponentData
    {
        public int ColonyCount;
        public int FleetCount;
        public int FleetEngagementCount;
        public uint LastRegistryTick;
    }

    /// <summary>
    /// Deterministic registry entry describing a colony snapshot.
    /// </summary>
    public struct Space4XColonyRegistryEntry : IBufferElementData, IComparable<Space4XColonyRegistryEntry>, IRegistryEntry, IRegistryFlaggedEntry
    {
        public Entity ColonyEntity;
        public FixedString64Bytes ColonyId;
        public float Population;
        public float StoredResources;
        public float3 WorldPosition;
        public int SectorId;
        public Space4XColonyStatus Status;
        public byte Flags;

        public int CompareTo(Space4XColonyRegistryEntry other)
        {
            return ColonyEntity.Index.CompareTo(other.ColonyEntity.Index);
        }

        public Entity RegistryEntity => ColonyEntity;

        public byte RegistryFlags => Flags;
    }

    /// <summary>
    /// Deterministic registry entry describing a fleet snapshot.
    /// </summary>
    public struct Space4XFleetRegistryEntry : IBufferElementData, IComparable<Space4XFleetRegistryEntry>, IRegistryEntry, IRegistryFlaggedEntry
    {
        public Entity FleetEntity;
        public FixedString64Bytes FleetId;
        public int ShipCount;
        public Space4XFleetPosture Posture;
        public float3 WorldPosition;
        public byte Flags;

        public int CompareTo(Space4XFleetRegistryEntry other)
        {
            return FleetEntity.Index.CompareTo(other.FleetEntity.Index);
        }

        public Entity RegistryEntity => FleetEntity;

        public byte RegistryFlags => Flags;
    }

    /// <summary>
    /// Helper utilities for translating Space4X state to registry flag semantics.
    /// </summary>
    public static class Space4XRegistryFlags
    {
        public const byte ColonyGrowing = 1 << 0;
        public const byte ColonyCrisis = 1 << 1;
        public const byte ColonyUnderAttack = 1 << 2;

        public const byte FleetActive = 1 << 0;
        public const byte FleetEngaging = 1 << 1;
        public const byte FleetRetreating = 1 << 2;

        public static byte FromColonyStatus(Space4XColonyStatus status)
        {
            byte flags = 0;
            switch (status)
            {
                case Space4XColonyStatus.Growing:
                    flags |= ColonyGrowing;
                    break;
                case Space4XColonyStatus.Besieged:
                    flags |= ColonyUnderAttack;
                    break;
                case Space4XColonyStatus.InCrisis:
                    flags |= ColonyCrisis;
                    break;
            }

            return flags;
        }

        public static byte FromFleetPosture(Space4XFleetPosture posture)
        {
            byte flags = 0;
            switch (posture)
            {
                case Space4XFleetPosture.Patrol:
                case Space4XFleetPosture.Engaging:
                case Space4XFleetPosture.Retreating:
                    flags |= FleetActive;
                    break;
            }

            if (posture == Space4XFleetPosture.Engaging)
            {
                flags |= FleetEngaging;
            }
            else if (posture == Space4XFleetPosture.Retreating)
            {
                flags |= FleetRetreating;
            }

            return flags;
        }
    }

    /// <summary>
    /// Canonical registry archetype identifiers reserved for Space4X.
    /// Values are arbitrary yet stable to keep metadata deterministic.
    /// </summary>
    public static class Space4XRegistryIds
    {
        public const ushort ColonyArchetype = 0x5301;
        public const ushort FleetArchetype = 0x5302;
    }
}


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
    /// Declares a logistics route connecting two colonies or stations.
    /// </summary>
    public struct Space4XLogisticsRoute : IComponentData
    {
        public FixedString64Bytes RouteId;
        public FixedString64Bytes OriginColonyId;
        public FixedString64Bytes DestinationColonyId;
        public float DailyThroughput;
        public float Risk;
        public int Priority;
        public Space4XLogisticsRouteStatus Status;
    }

    /// <summary>
    /// Operational status for logistics routes.
    /// </summary>
    public enum Space4XLogisticsRouteStatus : byte
    {
        Offline = 0,
        Operational = 1,
        Disrupted = 2,
        Overloaded = 3
    }

    /// <summary>
    /// Declares an anomaly or threat that systems should register with shared telemetry.
    /// </summary>
    public struct Space4XAnomaly : IComponentData
    {
        public FixedString64Bytes AnomalyId;
        public FixedString64Bytes Classification;
        public Space4XAnomalySeverity Severity;
        public Space4XAnomalyState State;
        public float Instability;
        public int SectorId;
    }

    /// <summary>
    /// Lifecycle state for anomalies.
    /// </summary>
    public enum Space4XAnomalyState : byte
    {
        Dormant = 0,
        Active = 1,
        Contained = 2,
        Resolved = 3
    }

    /// <summary>
    /// Severity band for anomaly evaluation.
    /// </summary>
    public enum Space4XAnomalySeverity : byte
    {
        None = 0,
        Low = 1,
        Moderate = 2,
        Severe = 3,
        Critical = 4
    }

    /// <summary>
    /// Aggregated registry summary for all Space4X colonies.
    /// </summary>
    public struct Space4XColonyRegistry : IComponentData
    {
        public int ColonyCount;
        public float TotalPopulation;
        public float TotalStoredResources;
        public float TotalSupplyDemand;
        public float TotalSupplyShortage;
        public float AverageSupplyRatio;
        public int BottleneckColonyCount;
        public int CriticalColonyCount;
        public uint LastUpdateTick;
        public uint LastSpatialVersion;
        public int SpatialResolvedCount;
        public int SpatialFallbackCount;
        public int SpatialUnmappedCount;
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
        public uint LastSpatialVersion;
        public int SpatialResolvedCount;
        public int SpatialFallbackCount;
        public int SpatialUnmappedCount;
    }

    /// <summary>
    /// Aggregated registry summary for Space4X logistics routes.
    /// </summary>
    public struct Space4XLogisticsRegistry : IComponentData
    {
        public int RouteCount;
        public int ActiveRouteCount;
        public int HighRiskRouteCount;
        public float TotalDailyThroughput;
        public float AverageRisk;
        public uint LastUpdateTick;
        public uint LastSpatialVersion;
        public int SpatialResolvedCount;
        public int SpatialFallbackCount;
        public int SpatialUnmappedCount;
    }

    /// <summary>
    /// Aggregated registry summary for Space4X anomalies.
    /// </summary>
    public struct Space4XAnomalyRegistry : IComponentData
    {
        public int AnomalyCount;
        public int ActiveAnomalyCount;
        public Space4XAnomalySeverity HighestSeverity;
        public uint LastUpdateTick;
        public uint LastSpatialVersion;
        public int SpatialResolvedCount;
        public int SpatialFallbackCount;
        public int SpatialUnmappedCount;
    }

    /// <summary>
    /// Lightweight snapshot cached by the registry bridge so presentation systems can publish telemetry.
    /// </summary>
    public struct Space4XRegistrySnapshot : IComponentData
    {
        public int ColonyCount;
        public float ColonySupplyDemandTotal;
        public float ColonySupplyShortageTotal;
        public float ColonyAverageSupplyRatio;
        public int ColonyBottleneckCount;
        public int ColonyCriticalCount;
        public int FleetCount;
        public int FleetEngagementCount;
        public int LogisticsRouteCount;
        public int ActiveLogisticsRouteCount;
        public int HighRiskRouteCount;
        public float LogisticsTotalThroughput;
        public float LogisticsAverageRisk;
        public int AnomalyCount;
        public int ActiveAnomalyCount;
        public Space4XAnomalySeverity HighestAnomalySeverity;
        public int MiracleCount;
        public int ActiveMiracleCount;
        public float MiracleTotalEnergyCost;
        public float MiracleTotalCooldownSeconds;
        public float MiracleAverageChargePercent;
        public float MiracleAverageCastLatencySeconds;
        public int MiracleCancellationCount;
        public int ComplianceBreachCount;
        public int ComplianceMutinyCount;
        public int ComplianceDesertionCount;
        public int ComplianceIndependenceCount;
        public float ComplianceAverageSeverity;
        public float ComplianceAverageSuspicion;
        public float ComplianceAverageSpySuspicion;
        public float ComplianceMaxSuspicion;
        public int ComplianceSuspicionAlertCount;
        public uint ComplianceLastUpdateTick;
        public int TechDiffusionActiveCount;
        public int TechDiffusionCompletedCount;
        public uint TechDiffusionLastUpgradeTick;
        public int ModuleRefitCount;
        public int ModuleRefitFieldCount;
        public int ModuleRefitFacilityCount;
        public int ModuleRepairCount;
        public int ModuleRepairFieldCount;
        public float ModuleRepairDurationAvgSeconds;
        public float ModuleRefitDurationAvgSeconds;
        public int ModuleDegradedCount;
        public int ModuleRepairingCount;
        public int ModuleRefittingCount;
        public int ModuleOffenseRatingTotal;
        public int ModuleDefenseRatingTotal;
        public int ModuleUtilityRatingTotal;
        public float ModulePowerBalanceMW;
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
        public float SupplyDemand;
        public float SupplyRatio;
        public float SupplyShortage;
        public int CellId;
        public uint SpatialVersion;

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
        public int CellId;
        public uint SpatialVersion;

        public int CompareTo(Space4XFleetRegistryEntry other)
        {
            return FleetEntity.Index.CompareTo(other.FleetEntity.Index);
        }

        public Entity RegistryEntity => FleetEntity;

        public byte RegistryFlags => Flags;
    }

    /// <summary>
    /// Deterministic registry entry describing a logistics route snapshot.
    /// </summary>
    public struct Space4XLogisticsRegistryEntry : IBufferElementData, IComparable<Space4XLogisticsRegistryEntry>, IRegistryEntry, IRegistryFlaggedEntry
    {
        public Entity RouteEntity;
        public FixedString64Bytes RouteId;
        public FixedString64Bytes OriginColonyId;
        public FixedString64Bytes DestinationColonyId;
        public float DailyThroughput;
        public float Risk;
        public int Priority;
        public Space4XLogisticsRouteStatus Status;
        public float3 WorldPosition;
        public byte Flags;
        public int CellId;
        public uint SpatialVersion;

        public int CompareTo(Space4XLogisticsRegistryEntry other)
        {
            return RouteEntity.Index.CompareTo(other.RouteEntity.Index);
        }

        public Entity RegistryEntity => RouteEntity;

        public byte RegistryFlags => Flags;
    }

    /// <summary>
    /// Deterministic registry entry describing an anomaly snapshot.
    /// </summary>
    public struct Space4XAnomalyRegistryEntry : IBufferElementData, IComparable<Space4XAnomalyRegistryEntry>, IRegistryEntry, IRegistryFlaggedEntry
    {
        public Entity AnomalyEntity;
        public FixedString64Bytes AnomalyId;
        public FixedString64Bytes Classification;
        public Space4XAnomalySeverity Severity;
        public Space4XAnomalyState State;
        public float Instability;
        public int SectorId;
        public float3 WorldPosition;
        public byte Flags;
        public int CellId;
        public uint SpatialVersion;

        public int CompareTo(Space4XAnomalyRegistryEntry other)
        {
            return AnomalyEntity.Index.CompareTo(other.AnomalyEntity.Index);
        }

        public Entity RegistryEntity => AnomalyEntity;

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
        public const byte ColonySupplyStrained = 1 << 3;
        public const byte ColonySupplyCritical = 1 << 4;

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

        public static byte ApplyColonySupply(float supplyRatio)
        {
            byte flags = 0;

            if (supplyRatio < Space4XColonySupply.BottleneckThreshold)
            {
                flags |= ColonySupplyStrained;
            }

            if (supplyRatio < Space4XColonySupply.CriticalThreshold)
            {
                flags |= ColonySupplyCritical;
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
    /// Shared helpers for computing colony supply metrics.
    /// </summary>
    public static class Space4XColonySupply
    {
        public const float DemandPerPopulation = 0.005f;
        public const float BottleneckThreshold = 0.6f;
        public const float CriticalThreshold = 0.3f;

        public static float ComputeDemand(float population)
        {
            return math.max(0f, population * DemandPerPopulation);
        }

        public static float ComputeSupplyRatio(float storedResources, float demand)
        {
            if (demand <= math.FLT_MIN_NORMAL)
            {
                return storedResources > 0f ? 1f : 0f;
            }

            return math.clamp(storedResources / demand, 0f, 4f);
        }

        public static float ComputeShortage(float storedResources, float demand)
        {
            return math.max(0f, demand - storedResources);
        }
    }

    /// <summary>
    /// Helper utilities for translating logistics route data into registry flags.
    /// </summary>
    public static class Space4XLogisticsRegistryFlags
    {
        public const byte RouteActive = 1 << 0;
        public const byte RouteDisrupted = 1 << 1;
        public const byte RouteHighRisk = 1 << 2;

        public const float HighRiskThreshold = 0.6f;

        public static bool IsActive(Space4XLogisticsRouteStatus status)
        {
            return status == Space4XLogisticsRouteStatus.Operational || status == Space4XLogisticsRouteStatus.Overloaded;
        }

        public static byte FromRoute(Space4XLogisticsRouteStatus status, float risk)
        {
            byte flags = 0;

            if (IsActive(status))
            {
                flags |= RouteActive;
            }

            if (status == Space4XLogisticsRouteStatus.Disrupted)
            {
                flags |= RouteDisrupted;
            }

            if (math.clamp(risk, 0f, 1f) >= HighRiskThreshold)
            {
                flags |= RouteHighRisk;
            }

            return flags;
        }
    }

    /// <summary>
    /// Helper utilities for translating anomaly data into registry flags.
    /// </summary>
    public static class Space4XAnomalyRegistryFlags
    {
        public const byte AnomalyActive = 1 << 0;
        public const byte AnomalyCritical = 1 << 1;
        public const byte AnomalyContained = 1 << 2;

        public static bool IsActive(Space4XAnomalyState state)
        {
            return state == Space4XAnomalyState.Active || state == Space4XAnomalyState.Contained;
        }

        public static byte FromAnomaly(Space4XAnomalyState state, Space4XAnomalySeverity severity)
        {
            byte flags = 0;

            if (state == Space4XAnomalyState.Active)
            {
                flags |= AnomalyActive;
            }

            if (state == Space4XAnomalyState.Contained)
            {
                flags |= AnomalyContained;
            }

            if (severity >= Space4XAnomalySeverity.Severe)
            {
                flags |= AnomalyCritical;
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
        public const ushort LogisticsRouteArchetype = 0x5303;
        public const ushort AnomalyArchetype = 0x5304;
    }
}

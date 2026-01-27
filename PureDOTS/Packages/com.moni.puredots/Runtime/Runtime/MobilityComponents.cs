using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Mobility
{
    [System.Flags]
    public enum WaypointFlags : byte
    {
        None = 0,
        PrimaryHub = 1 << 0,
        GatewayLinked = 1 << 1,
        Disabled = 1 << 7
    }

    public struct WaypointNode : IComponentData
    {
        public int WaypointId;
        public float3 Position;
        public byte Flags;
        public float MaintenanceCost;
        public uint LastServiceTick;
    }

    [System.Flags]
    public enum HighwayFlags : byte
    {
        None = 0,
        UnderMaintenance = 1 << 0,
        Blocked = 1 << 1
    }

    public struct HighwaySegment : IComponentData
    {
        public int FromWaypointId;
        public int ToWaypointId;
        public float BaseCost;
        public float BaseTravelTime;
        public byte Flags;
        public uint LastMaintenanceTick;
    }

    [System.Flags]
    public enum GatewayFlags : byte
    {
        None = 0,
        Offline = 1 << 0,
        Restricted = 1 << 1
    }

    public struct GatewayPortal : IComponentData
    {
        public int GatewayId;
        public int FromWaypointId;
        public int ToWaypointId;
        public FixedString64Bytes Label;
        public byte Flags;
        public uint LastSyncTick;
    }

    public struct MobilityNetwork : IComponentData
    {
        public uint Version;
        public uint LastBuildTick;
        public int WaypointCount;
        public int HighwayCount;
        public int GatewayCount;
    }

    public struct MobilityWaypointEntry : IBufferElementData, System.IComparable<MobilityWaypointEntry>, System.IEquatable<MobilityWaypointEntry>
    {
        public int WaypointId;
        public float3 Position;
        public byte Flags;
        public float MaintenanceCost;
        public uint LastServiceTick;
        public int HighwayCount;
        public int GatewayCount;

        public int CompareTo(MobilityWaypointEntry other)
        {
            return WaypointId.CompareTo(other.WaypointId);
        }

        public bool Equals(MobilityWaypointEntry other)
        {
            return WaypointId == other.WaypointId
                   && Flags == other.Flags
                   && MaintenanceCost.Equals(other.MaintenanceCost)
                   && LastServiceTick == other.LastServiceTick
                   && HighwayCount == other.HighwayCount
                   && GatewayCount == other.GatewayCount
                   && Position.Equals(other.Position);
        }
    }

    public struct MobilityHighwayEntry : IBufferElementData, System.IComparable<MobilityHighwayEntry>, System.IEquatable<MobilityHighwayEntry>
    {
        public int FromWaypointId;
        public int ToWaypointId;
        public float Cost;
        public float TravelTime;
        public byte Flags;

        public int CompareTo(MobilityHighwayEntry other)
        {
            var fromCompare = FromWaypointId.CompareTo(other.FromWaypointId);
            if (fromCompare != 0)
            {
                return fromCompare;
            }

            return ToWaypointId.CompareTo(other.ToWaypointId);
        }

        public bool Equals(MobilityHighwayEntry other)
        {
            return FromWaypointId == other.FromWaypointId
                   && ToWaypointId == other.ToWaypointId
                   && Cost.Equals(other.Cost)
                   && TravelTime.Equals(other.TravelTime)
                   && Flags == other.Flags;
        }
    }

    public struct MobilityGatewayEntry : IBufferElementData, System.IComparable<MobilityGatewayEntry>, System.IEquatable<MobilityGatewayEntry>
    {
        public int GatewayId;
        public int FromWaypointId;
        public int ToWaypointId;
        public byte Flags;

        public int CompareTo(MobilityGatewayEntry other)
        {
            var idCompare = GatewayId.CompareTo(other.GatewayId);
            if (idCompare != 0)
            {
                return idCompare;
            }

            return FromWaypointId.CompareTo(other.FromWaypointId);
        }

        public bool Equals(MobilityGatewayEntry other)
        {
            return GatewayId == other.GatewayId
                   && FromWaypointId == other.FromWaypointId
                   && ToWaypointId == other.ToWaypointId
                   && Flags == other.Flags;
        }
    }

    public enum MobilityPathStatus : byte
    {
        Pending = 0,
        Assigned = 1,
        Failed = 2
    }

    [System.Flags]
    public enum MobilityPathRequestFlags : byte
    {
        None = 0,
        AllowInterception = 1 << 0,
        BroadcastRendezvous = 1 << 1
    }

    public struct MobilityPathRequest : IComponentData
    {
        public int FromWaypointId;
        public int ToWaypointId;
        public MobilityPathRequestFlags Flags;
        public float MaxCost;
        public uint RequestedTick;
    }

    public struct MobilityPathResult : IComponentData
    {
        public MobilityPathStatus Status;
        public float EstimatedCost;
        public int HopCount;
        public uint LastUpdateTick;
    }

    public struct MobilityPathWaypoint : IBufferElementData
    {
        public int WaypointId;
    }

    public struct MobilityInterceptionEvent : IBufferElementData
    {
        public int FromWaypointId;
        public int ToWaypointId;
        public uint Tick;
        public byte Type; // 0=rendezvous,1=intercept
    }

    /// <summary>
    /// Tag for entities that should be treated as transport units (ships, wagons, etc.) by the AI.
    /// Replaces explicit checks for specific transport components.
    /// </summary>
    public struct TransportUnitTag : IComponentData { }
}

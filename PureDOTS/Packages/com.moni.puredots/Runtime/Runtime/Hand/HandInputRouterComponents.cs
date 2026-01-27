using System;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Components
{
    public enum HandRouteSource : byte
    {
        None = 0,
        AuthoringBridge = 1,
        ResourceSystem = 2,
        MiracleSystem = 3,
        Gameplay = 4,
        DebugTool = 250
    }

    public enum HandRoutePhase : byte
    {
        Started = 0,
        Performed = 1,
        Canceled = 2
    }

    public static class HandRoutePriority
    {
        public const byte Lowest = 0;
        public const byte HighlightOnly = 10;
        public const byte GroundDrip = 40;
        public const byte DumpToStorehouse = 60;
        public const byte ResourceSiphon = 80;
        public const byte MiracleOverride = 100;
    }

    public struct HandInputRouteRequest : IBufferElementData
    {
        public HandRouteSource Source;
        public HandRoutePhase Phase;
        public byte Priority;
        public DivineHandCommandType CommandType;
        public Entity TargetEntity;
        public float3 TargetPosition;
        public float3 TargetNormal;

        public static HandInputRouteRequest Create(
            HandRouteSource source,
            HandRoutePhase phase,
            byte priority,
            DivineHandCommandType commandType,
            Entity targetEntity,
            in float3 position,
            in float3 normal)
        {
            return new HandInputRouteRequest
            {
                Source = source,
                Phase = phase,
                Priority = priority,
                CommandType = commandType,
                TargetEntity = targetEntity,
                TargetPosition = position,
                TargetNormal = normal
            };
        }
    }

    public struct HandInputRouteResult : IComponentData, IEquatable<HandInputRouteResult>
    {
        public HandRouteSource Source;
        public byte Priority;
        public DivineHandCommandType CommandType;
        public Entity TargetEntity;
        public float3 TargetPosition;
        public float3 TargetNormal;

        public bool Equals(HandInputRouteResult other)
        {
            return Source == other.Source &&
                   Priority == other.Priority &&
                   CommandType == other.CommandType &&
                   TargetEntity == other.TargetEntity &&
                   math.distancesq(TargetPosition, other.TargetPosition) < 1e-6f &&
                   math.distancesq(TargetNormal, other.TargetNormal) < 1e-6f;
        }

        public static HandInputRouteResult None => new HandInputRouteResult
        {
            Source = HandRouteSource.None,
            Priority = HandRoutePriority.Lowest,
            CommandType = DivineHandCommandType.None,
            TargetEntity = Entity.Null,
            TargetPosition = float3.zero,
            TargetNormal = new float3(0f, 1f, 0f)
        };
    }
}

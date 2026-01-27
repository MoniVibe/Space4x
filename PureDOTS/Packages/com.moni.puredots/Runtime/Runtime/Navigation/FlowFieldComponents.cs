using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Navigation
{
    /// <summary>
    /// Configuration for flow field navigation system.
    /// </summary>
    public struct FlowFieldConfig : IComponentData
    {
        public float CellSize;
        public float2 WorldBoundsMin;
        public float2 WorldBoundsMax;
        public int RebuildCadenceTicks;
        public float SteeringWeight;
        public float AvoidanceWeight;
        public float CohesionWeight;
        public float SeparationWeight;
        public uint LastRebuildTick;
        public uint Version;
        public uint TerrainVersion; // Incremented when terrain changes, invalidates flow fields

        public readonly int2 GridSize
        {
            get
            {
                var size = WorldBoundsMax - WorldBoundsMin;
                return new int2(
                    (int)math.ceil(size.x / CellSize),
                    (int)math.ceil(size.y / CellSize)
                );
            }
        }

        public readonly int CellCount
        {
            get
            {
                var size = GridSize;
                return size.x * size.y;
            }
        }
    }

    /// <summary>
    /// Metadata for a flow field layer (one per goal category).
    /// </summary>
    public struct FlowFieldLayer : IBufferElementData
    {
        public ushort LayerId;
        public byte Priority;
        public int RebuildIntervalTicks;
        public uint LastBuildTick;
        public byte IsDirty;
        public FixedString64Bytes Label;

        public readonly bool ShouldRebuild(uint currentTick)
        {
            if (IsDirty != 0)
            {
                return true;
            }

            if (RebuildIntervalTicks <= 0)
            {
                return false;
            }

            return (currentTick - LastBuildTick) >= (uint)RebuildIntervalTicks;
        }
    }

    /// <summary>
    /// Direction and cost data for a single flow field cell.
    /// </summary>
    public struct FlowFieldCellData : IBufferElementData
    {
        public float2 Direction; // Normalized direction vector
        public float Cost; // Accumulated cost from goal
        public byte OccupancyFlags; // Obstacle, hazard, etc.
        public byte LayerId;
    }

    /// <summary>
    /// Request to update flow field goals (e.g., new storehouse built).
    /// </summary>
    public struct FlowFieldRequest : IBufferElementData
    {
        public Entity GoalEntity;
        public ushort LayerId;
        public float3 GoalPosition;
        public byte Priority;
        public uint ValidityTick;
        public byte IsActive;
    }

    /// <summary>
    /// Hazard or cost modifier update for flow field cells.
    /// </summary>
    public struct FlowFieldHazardUpdate : IBufferElementData
    {
        public int CellIndex;
        public float CostDelta;
        public uint ExpirationTick;
        public ushort LayerId;
    }

    /// <summary>
    /// Flow field state for a villager or other moving entity.
    /// </summary>
    public struct FlowFieldState : IComponentData
    {
        public ushort CurrentLayerId;
        public int CurrentCellIndex;
        public float2 CachedDirection;
        public float SpeedScalar;
        public uint LastUpdateTick;
    }

    /// <summary>
    /// Spatial sensor cache for movement/steering decisions.
    /// </summary>
    public struct SpatialSensor : IComponentData
    {
        public uint LastUpdateTick;
        public int UpdateIntervalTicks;
        public float SensorRadius;
        public int NeighborCount;
        public int ThreatCount;
        public int ResourceCount;
    }

    /// <summary>
    /// Cached sensor data buffer (neighbors, threats, resources).
    /// </summary>
    public struct SpatialSensorEntity : IBufferElementData
    {
        public Entity Entity;
        public float3 Position;
        public byte EntityType; // Neighbor, Threat, Resource, etc.
        public float Distance;
    }

    /// <summary>
    /// Steering state for local avoidance and cohesion.
    /// </summary>
    public struct SteeringState : IComponentData
    {
        public float2 SeparationVector;
        public float2 AvoidanceVector;
        public float2 CohesionVector;
        public float2 BlendedHeading;
        public float SpeedModifier;
    }

    /// <summary>
    /// Tag component marking entities that should participate in flow field navigation.
    /// </summary>
    public struct FlowFieldAgentTag : IComponentData
    {
    }

    /// <summary>
    /// Tag component marking entities that block flow field navigation (obstacles).
    /// </summary>
    public struct FlowFieldObstacleTag : IComponentData
    {
    }

    /// <summary>
    /// Tag component marking entities that act as flow field goals (storehouses, rally points, etc.).
    /// </summary>
    public struct FlowFieldGoalTag : IComponentData
    {
        public ushort LayerId;
        public byte Priority;
    }
}


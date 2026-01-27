using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Mobility;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems.Mobility
{
    /// <summary>
    /// Builds a deterministic snapshot of waypoint/highway/gateway data for pathing and telemetry.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    // Removed invalid UpdateAfter: MobilityNetworkBootstrapSystem executes in InitializationSystemGroup, so cross-group ordering isn't supported.
    public partial struct MobilityNetworkSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<MobilityNetwork>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused || (SystemAPI.TryGetSingleton(out RewindState rewindState) && rewindState.Mode != RewindMode.Record))
            {
                return;
            }

            var networkEntity = SystemAPI.GetSingletonEntity<MobilityNetwork>();
            var network = SystemAPI.GetComponentRW<MobilityNetwork>(networkEntity);
            var waypointBuffer = state.EntityManager.GetBuffer<MobilityWaypointEntry>(networkEntity);
            var highwayBuffer = state.EntityManager.GetBuffer<MobilityHighwayEntry>(networkEntity);
            var gatewayBuffer = state.EntityManager.GetBuffer<MobilityGatewayEntry>(networkEntity);

            using var waypointList = new NativeList<MobilityWaypointEntry>(state.WorldUpdateAllocator);
            using var highwayList = new NativeList<MobilityHighwayEntry>(state.WorldUpdateAllocator);
            using var gatewayList = new NativeList<MobilityGatewayEntry>(state.WorldUpdateAllocator);

            foreach (var (node, transform) in SystemAPI.Query<RefRO<WaypointNode>, RefRO<LocalTransform>>())
            {
                waypointList.Add(new MobilityWaypointEntry
                {
                    WaypointId = node.ValueRO.WaypointId,
                    Position = transform.ValueRO.Position,
                    Flags = node.ValueRO.Flags,
                    MaintenanceCost = node.ValueRO.MaintenanceCost,
                    LastServiceTick = node.ValueRO.LastServiceTick
                });
            }

            foreach (var node in SystemAPI.Query<RefRO<WaypointNode>>().WithNone<LocalTransform>())
            {
                waypointList.Add(new MobilityWaypointEntry
                {
                    WaypointId = node.ValueRO.WaypointId,
                    Position = node.ValueRO.Position,
                    Flags = node.ValueRO.Flags,
                    MaintenanceCost = node.ValueRO.MaintenanceCost,
                    LastServiceTick = node.ValueRO.LastServiceTick
                });
            }

            foreach (var segment in SystemAPI.Query<RefRO<HighwaySegment>>())
            {
                highwayList.Add(new MobilityHighwayEntry
                {
                    FromWaypointId = segment.ValueRO.FromWaypointId,
                    ToWaypointId = segment.ValueRO.ToWaypointId,
                    Cost = segment.ValueRO.BaseCost,
                    TravelTime = segment.ValueRO.BaseTravelTime,
                    Flags = segment.ValueRO.Flags
                });
            }

            foreach (var gateway in SystemAPI.Query<RefRO<GatewayPortal>>())
            {
                gatewayList.Add(new MobilityGatewayEntry
                {
                    GatewayId = gateway.ValueRO.GatewayId,
                    FromWaypointId = gateway.ValueRO.FromWaypointId,
                    ToWaypointId = gateway.ValueRO.ToWaypointId,
                    Flags = gateway.ValueRO.Flags
                });
            }

            if (waypointList.Length > 1)
            {
                NativeSortExtension.Sort(waypointList.AsArray());
            }

            if (highwayList.Length > 1)
            {
                NativeSortExtension.Sort(highwayList.AsArray());
            }

            if (gatewayList.Length > 1)
            {
                NativeSortExtension.Sort(gatewayList.AsArray());
            }

            var waypointsChanged = !BuffersEqual(waypointBuffer.AsNativeArray(), waypointList.AsArray());
            var highwaysChanged = !BuffersEqual(highwayBuffer.AsNativeArray(), highwayList.AsArray());
            var gatewaysChanged = !BuffersEqual(gatewayBuffer.AsNativeArray(), gatewayList.AsArray());

            if (waypointsChanged)
            {
                waypointBuffer.Clear();
                waypointBuffer.ResizeUninitialized(waypointList.Length);
                if (waypointList.Length > 0)
                {
                    waypointBuffer.AsNativeArray().CopyFrom(waypointList.AsArray());
                }
            }

            if (highwaysChanged)
            {
                highwayBuffer.Clear();
                highwayBuffer.ResizeUninitialized(highwayList.Length);
                if (highwayList.Length > 0)
                {
                    highwayBuffer.AsNativeArray().CopyFrom(highwayList.AsArray());
                }
            }

            if (gatewaysChanged)
            {
                gatewayBuffer.Clear();
                gatewayBuffer.ResizeUninitialized(gatewayList.Length);
                if (gatewayList.Length > 0)
                {
                    gatewayBuffer.AsNativeArray().CopyFrom(gatewayList.AsArray());
                }
            }

            if (waypointsChanged || highwaysChanged || gatewaysChanged)
            {
                network.ValueRW.Version++;
                network.ValueRW.WaypointCount = waypointList.Length;
                network.ValueRW.HighwayCount = highwayList.Length;
                network.ValueRW.GatewayCount = gatewayList.Length;
            }

            network.ValueRW.LastBuildTick = timeState.Tick;
        }

        private static bool BuffersEqual<T>(NativeArray<T> existing, NativeArray<T> candidate)
            where T : struct, System.IEquatable<T>
        {
            if (existing.Length != candidate.Length)
            {
                return false;
            }

            for (var i = 0; i < existing.Length; i++)
            {
                if (!existing[i].Equals(candidate[i]))
                {
                    return false;
                }
            }

            return true;
        }
    }
}

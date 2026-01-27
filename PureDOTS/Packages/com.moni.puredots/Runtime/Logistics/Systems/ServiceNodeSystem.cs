using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Logistics.Components;
using PureDOTS.Runtime.Telemetry;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Logistics.Systems
{
    /// <summary>
    /// Manages service queues at nodes (docks, loaders, customs).
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ResourceRoutingRerouteSystem))]
    [UpdateBefore(typeof(ResourceLogisticsDeliverySystem))]
    public partial struct ServiceNodeSystem : ISystem
    {
        private const uint TelemetryIntervalTicks = 60;
        private const byte DefaultServiceCapacity = 1;

        private static readonly FixedString64Bytes MetricLoadTotal = "logistics.service.load_active_total";
        private static readonly FixedString64Bytes MetricUnloadTotal = "logistics.service.unload_active_total";
        private static readonly FixedString64Bytes MetricLoadActivePrefix = "logistics.service.load_active.";
        private static readonly FixedString64Bytes MetricUnloadActivePrefix = "logistics.service.unload_active.";
        private static readonly FixedString64Bytes MetricLoadUtilPrefix = "logistics.service.load_util.";
        private static readonly FixedString64Bytes MetricUnloadUtilPrefix = "logistics.service.unload_util.";
        private static readonly FixedString64Bytes MetricCapacityPrefix = "logistics.service.capacity.";

        private ComponentLookup<LogisticsNode> _nodeLookup;
        private ComponentLookup<ServiceReservation> _serviceReservationLookup;

        private struct ServiceNodeTelemetryCounter
        {
            public Entity Node;
            public int NodeId;
            public byte Capacity;
            public int LoadActive;
            public int UnloadActive;
        }

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TickTimeState>();
            state.RequireForUpdate<LogisticsNode>();
            _nodeLookup = state.GetComponentLookup<LogisticsNode>(false);
            _serviceReservationLookup = state.GetComponentLookup<ServiceReservation>(false);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<ScenarioState>(out var scenario) ||
                !scenario.IsInitialized ||
                !scenario.EnableEconomy)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) ||
                rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var tickTime = SystemAPI.GetSingleton<TickTimeState>();
            if (tickTime.Tick % TelemetryIntervalTicks != 0)
            {
                return;
            }

            if (!SystemAPI.TryGetSingletonBuffer<TelemetryMetric>(out var telemetryBuffer))
            {
                return;
            }

            _nodeLookup.Update(ref state);
            _serviceReservationLookup.Update(ref state);

            var nodeIndexLookup = new NativeParallelHashMap<Entity, int>(64, Allocator.Temp);
            var counters = new NativeList<ServiceNodeTelemetryCounter>(Allocator.Temp);

            int totalLoad = 0;
            int totalUnload = 0;

            foreach (var reservation in SystemAPI.Query<RefRO<ServiceReservation>>())
            {
                if (reservation.ValueRO.Status != ReservationStatus.Active)
                {
                    continue;
                }

                var nodeEntity = reservation.ValueRO.ServiceNode;
                if (nodeEntity == Entity.Null || !_nodeLookup.HasComponent(nodeEntity))
                {
                    continue;
                }

                if (!nodeIndexLookup.TryGetValue(nodeEntity, out var counterIndex))
                {
                    counterIndex = counters.Length;
                    nodeIndexLookup.TryAdd(nodeEntity, counterIndex);

                    var node = _nodeLookup[nodeEntity];
                    var capacity = node.Services.SlotCapacity > 0 ? node.Services.SlotCapacity : DefaultServiceCapacity;

                    counters.Add(new ServiceNodeTelemetryCounter
                    {
                        Node = nodeEntity,
                        NodeId = node.NodeId,
                        Capacity = capacity,
                        LoadActive = 0,
                        UnloadActive = 0
                    });
                }

                var counter = counters[counterIndex];
                switch (reservation.ValueRO.ServiceType)
                {
                    case ServiceType.Load:
                        counter.LoadActive++;
                        totalLoad++;
                        break;
                    case ServiceType.Unload:
                        counter.UnloadActive++;
                        totalUnload++;
                        break;
                }

                counters[counterIndex] = counter;
            }

            telemetryBuffer.AddMetric(MetricLoadTotal, totalLoad, TelemetryMetricUnit.Count);
            telemetryBuffer.AddMetric(MetricUnloadTotal, totalUnload, TelemetryMetricUnit.Count);

            for (int i = 0; i < counters.Length; i++)
            {
                var counter = counters[i];
                var capacity = math.max(1, counter.Capacity);
                var loadRatio = math.min(1f, counter.LoadActive / (float)capacity);
                var unloadRatio = math.min(1f, counter.UnloadActive / (float)capacity);

                var nodeId = counter.NodeId;

                // Build FixedString keys - use static readonly for prefix (initialized outside Burst)
                // then append nodeId (int, Burst-safe)
                FixedString64Bytes loadKey = MetricLoadActivePrefix;
                loadKey.Append(nodeId);
                telemetryBuffer.AddMetric(loadKey, counter.LoadActive, TelemetryMetricUnit.Count);

                FixedString64Bytes unloadKey = MetricUnloadActivePrefix;
                unloadKey.Append(nodeId);
                telemetryBuffer.AddMetric(unloadKey, counter.UnloadActive, TelemetryMetricUnit.Count);

                FixedString64Bytes loadUtilKey = MetricLoadUtilPrefix;
                loadUtilKey.Append(nodeId);
                telemetryBuffer.AddMetric(loadUtilKey, loadRatio, TelemetryMetricUnit.Ratio);

                FixedString64Bytes unloadUtilKey = MetricUnloadUtilPrefix;
                unloadUtilKey.Append(nodeId);
                telemetryBuffer.AddMetric(unloadUtilKey, unloadRatio, TelemetryMetricUnit.Ratio);

                FixedString64Bytes capacityKey = MetricCapacityPrefix;
                capacityKey.Append(nodeId);
                telemetryBuffer.AddMetric(capacityKey, capacity, TelemetryMetricUnit.Count);
            }

            nodeIndexLookup.Dispose();
            counters.Dispose();
        }
    }
}


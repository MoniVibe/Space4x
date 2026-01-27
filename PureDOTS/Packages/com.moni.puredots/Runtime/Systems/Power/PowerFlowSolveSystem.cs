using PureDOTS.Runtime;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Power;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Power
{
    /// <summary>
    /// WARM path: Power flow solve per network (few ticks/network changes).
    /// Supply status solving periodically.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PowerSystemGroup))]
    [UpdateAfter(typeof(PowerNetworkBuildSystem))]
    public partial struct PowerFlowSolveSystem : ISystem
    {
        private EntityQuery _sourceQuery;
        private EntityQuery _consumerQuery;
        private ComponentLookup<PowerSourceState> _sourceStateLookup;
        private ComponentLookup<PowerConsumerState> _consumerStateLookup;
        private ComponentLookup<PowerConsumerDefRegistry> _consumerDefRegistryLookup;
        private ComponentLookup<PowerRouteInfo> _routeInfoLookup;
        private ComponentLookup<PowerNode> _nodeLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _sourceQuery = SystemAPI.QueryBuilder()
                .WithAll<PowerSourceState, PowerNode>()
                .Build();

            _consumerQuery = SystemAPI.QueryBuilder()
                .WithAll<PowerConsumerState, PowerNode>()
                .Build();

            _sourceStateLookup = state.GetComponentLookup<PowerSourceState>(true);
            _consumerStateLookup = state.GetComponentLookup<PowerConsumerState>(false);
            _consumerDefRegistryLookup = state.GetComponentLookup<PowerConsumerDefRegistry>(true);
            _routeInfoLookup = state.GetComponentLookup<PowerRouteInfo>(true);
            _nodeLookup = state.GetComponentLookup<PowerNode>(true);

            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<PowerConsumerDefRegistry>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<PowerConsumerDefRegistry>(out var consumerRegistry))
            {
                return;
            }

            _sourceStateLookup.Update(ref state);
            _consumerStateLookup.Update(ref state);
            _consumerDefRegistryLookup.Update(ref state);
            _routeInfoLookup.Update(ref state);
            _nodeLookup.Update(ref state);

            // Precompute network totals
            var networkGeneration = new NativeHashMap<int, float>(16, Allocator.Temp);
            var networkDemand = new NativeHashMap<int, float>(16, Allocator.Temp);

            // Aggregate generation per network
            var sourceEntities = _sourceQuery.ToEntityArray(Allocator.Temp);
            var sourceNodes = _sourceQuery.ToComponentDataArray<PowerNode>(Allocator.Temp);

            for (int i = 0; i < sourceEntities.Length; i++)
            {
                var node = sourceNodes[i];
                if (_sourceStateLookup.HasComponent(sourceEntities[i]))
                {
                    var sourceState = _sourceStateLookup[sourceEntities[i]];
                    if (!networkGeneration.TryGetValue(node.Network.NetworkId, out var netGen))
                    {
                        netGen = 0f;
                    }
                    netGen += sourceState.MaxOutput * (1f - node.LocalLoss);
                    networkGeneration[node.Network.NetworkId] = netGen;
                }
            }

            // Aggregate demand per network
            var consumerEntities = _consumerQuery.ToEntityArray(Allocator.Temp);
            var consumerNodes = _consumerQuery.ToComponentDataArray<PowerNode>(Allocator.Temp);

            for (int i = 0; i < consumerEntities.Length; i++)
            {
                var node = consumerNodes[i];
                if (_consumerStateLookup.HasComponent(consumerEntities[i]))
                {
                    var consumerState = _consumerStateLookup[consumerEntities[i]];
                    if (!networkDemand.TryGetValue(node.Network.NetworkId, out var netDemand))
                    {
                        netDemand = 0f;
                    }
                    netDemand += consumerState.RequestedDemand;
                    networkDemand[node.Network.NetworkId] = netDemand;
                }
            }

            // Distribute power to consumers
            var job = new PowerFlowSolveJob
            {
                ConsumerDefRegistry = consumerRegistry.Value,
                ConsumerStateLookup = _consumerStateLookup,
                RouteInfoLookup = _routeInfoLookup,
                NetworkGeneration = networkGeneration,
                NetworkDemand = networkDemand
            };

            state.Dependency = job.ScheduleParallel(_consumerQuery, state.Dependency);

            sourceEntities.Dispose();
            sourceNodes.Dispose();
            consumerEntities.Dispose();
            consumerNodes.Dispose();
        }
    }

    [BurstCompile]
    [WithAll(typeof(PowerConsumerState), typeof(PowerNode))]
    [WithNone(typeof(PlaybackGuardTag))]
    public partial struct PowerFlowSolveJob : IJobEntity
    {
        [ReadOnly] public BlobAssetReference<PowerConsumerDefRegistryBlob> ConsumerDefRegistry;
        [ReadOnly] public ComponentLookup<PowerRouteInfo> RouteInfoLookup;
        [ReadOnly] public NativeHashMap<int, float> NetworkGeneration;
        [ReadOnly] public NativeHashMap<int, float> NetworkDemand;

        public ComponentLookup<PowerConsumerState> ConsumerStateLookup;

        [BurstCompile]
        private void Execute(Entity entity, ref PowerConsumerState consumerState, in PowerNode node)
        {
            // Find consumer def
            var consumerDef = default(PowerConsumerDefBlob);
            var found = false;
            for (int i = 0; i < ConsumerDefRegistry.Value.ConsumerDefs.Length; i++)
            {
                if (ConsumerDefRegistry.Value.ConsumerDefs[i].ConsumerDefId == consumerState.ConsumerDefId)
                {
                    consumerDef = ConsumerDefRegistry.Value.ConsumerDefs[i];
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                consumerState.Supplied = 0;
                consumerState.Online = 0;
                return;
            }

            // Get network totals
            if (!NetworkGeneration.TryGetValue(node.Network.NetworkId, out var availablePower))
            {
                availablePower = 0f;
            }
            if (!NetworkDemand.TryGetValue(node.Network.NetworkId, out var totalDemand))
            {
                totalDemand = 0f;
            }

            // Simple proportional distribution by priority
            var supplyRatio = totalDemand > 0 ? math.min(1f, availablePower / totalDemand) : 0f;

            // Apply priority: tier 0 gets full supply, then tier 1, etc.
            var prioritySupplyRatio = supplyRatio;
            if (consumerDef.PriorityTier > 0)
            {
                // Lower priority consumers get less (simplified)
                prioritySupplyRatio = math.max(0f, supplyRatio - consumerDef.PriorityTier * 0.1f);
            }

            var supplied = consumerState.RequestedDemand * prioritySupplyRatio;

            // Apply path loss if route info available
            if (RouteInfoLookup.HasComponent(entity))
            {
                var routeInfo = RouteInfoLookup[entity];
                supplied *= (1f - routeInfo.PathLoss);
            }

            consumerState.Supplied = supplied;

            // Set online if supplied >= min operational fraction
            var minRequired = consumerDef.BaseDemand * consumerDef.MinOperationalFraction;
            consumerState.Online = (byte)(supplied >= minRequired ? 1 : 0);
        }
    }
}

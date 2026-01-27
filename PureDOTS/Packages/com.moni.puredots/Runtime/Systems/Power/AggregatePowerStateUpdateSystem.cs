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
    /// Computes aggregate power state per village, colony, ship, or station.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PowerSystemGroup))]
    [UpdateAfter(typeof(PowerFlowSolveSystem))]
    public partial struct AggregatePowerStateUpdateSystem : ISystem
    {
        private EntityQuery _nodeQuery;
        private ComponentLookup<PowerSourceState> _sourceStateLookup;
        private ComponentLookup<PowerConsumerState> _consumerStateLookup;
        private ComponentLookup<PowerNode> _nodeLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _nodeQuery = SystemAPI.QueryBuilder()
                .WithAll<PowerNode>()
                .Build();

            _sourceStateLookup = state.GetComponentLookup<PowerSourceState>(true);
            _consumerStateLookup = state.GetComponentLookup<PowerConsumerState>(true);
            _nodeLookup = state.GetComponentLookup<PowerNode>(true);

            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
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

            _sourceStateLookup.Update(ref state);
            _consumerStateLookup.Update(ref state);
            _nodeLookup.Update(ref state);

            // Group nodes by network
            var networkTotals = new NativeHashMap<int, (float generation, float demand, float supplied)>(16, Allocator.Temp);
            var nodeEntities = _nodeQuery.ToEntityArray(Allocator.Temp);
            var nodeComponents = _nodeQuery.ToComponentDataArray<PowerNode>(Allocator.Temp);

            // Aggregate per network
            for (int i = 0; i < nodeComponents.Length; i++)
            {
                var node = nodeComponents[i];
                var networkId = node.Network.NetworkId;

                if (node.Type == PowerNodeType.Source && _sourceStateLookup.HasComponent(nodeEntities[i]))
                {
                    var sourceState = _sourceStateLookup[nodeEntities[i]];
                    if (!networkTotals.TryGetValue(networkId, out var totals))
                    {
                        totals = (0f, 0f, 0f);
                    }
                    totals.generation += sourceState.MaxOutput * (1f - node.LocalLoss);
                    networkTotals[networkId] = totals;
                }
                else if (node.Type == PowerNodeType.Consumer && _consumerStateLookup.HasComponent(nodeEntities[i]))
                {
                    var consumerState = _consumerStateLookup[nodeEntities[i]];
                    if (!networkTotals.TryGetValue(networkId, out var totals))
                    {
                        totals = (0f, 0f, 0f);
                    }
                    totals.demand += consumerState.RequestedDemand;
                    totals.supplied += consumerState.Supplied;
                    networkTotals[networkId] = totals;
                }
            }

            // Update aggregate state components (attached to village/ship entities)
            // For now, create/update on network entities themselves
            var networkEntities = SystemAPI.QueryBuilder()
                .WithAll<PowerNetwork>()
                .Build()
                .ToEntityArray(Allocator.Temp);

            for (int i = 0; i < networkEntities.Length; i++)
            {
                var networkEntity = networkEntities[i];
                var network = state.EntityManager.GetComponentData<PowerNetwork>(networkEntity);
                
                if (networkTotals.TryGetValue(network.NetworkId, out var totals))
                {
                    var coverage = totals.demand > 0 ? totals.supplied / totals.demand : 1f;
                    var blackoutLevel = math.max(0f, 1f - coverage);

                    var aggregateState = new AggregatePowerState
                    {
                        TotalGeneration = totals.generation,
                        TotalDemand = totals.demand,
                        SuppliedDemand = totals.supplied,
                        Coverage = coverage,
                        BlackoutLevel = blackoutLevel
                    };

                    if (state.EntityManager.HasComponent<AggregatePowerState>(networkEntity))
                    {
                        state.EntityManager.SetComponentData(networkEntity, aggregateState);
                    }
                    else
                    {
                        state.EntityManager.AddComponentData(networkEntity, aggregateState);
                    }
                }
            }

            networkTotals.Dispose();
            nodeEntities.Dispose();
            nodeComponents.Dispose();
            networkEntities.Dispose();
        }
    }
}


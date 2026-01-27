using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Navigation;
using PureDOTS.Runtime.Transport;
using PureDOTS.Runtime.Transport.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems.Navigation
{
    /// <summary>
    /// Builds transport network graph (ferries, hyperways, warp relays, roads) (COLD path).
    /// Creates TransitNode entities and TransitEdge connections.
    /// Event-driven: only rebuilds when dirty edges are marked or transport network changes.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(ColdPathSystemGroup))]
    [UpdateBefore(typeof(NavGraphHierarchySystem))]
    public partial struct TransitGraphBuildSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            // Get hierarchy entity
            if (!SystemAPI.TryGetSingletonEntity<NavGraphHierarchy>(out var hierarchyEntity))
            {
                return;
            }

            var hierarchy = SystemAPI.GetComponent<NavGraphHierarchy>(hierarchyEntity);

            // Check for dirty edges (transit edges)
            bool hasDirtyEdges = false;
            if (state.EntityManager.HasBuffer<DirtyEdge>(hierarchyEntity))
            {
                var dirtyEdges = state.EntityManager.GetBuffer<DirtyEdge>(hierarchyEntity);
                // Check for transit edges (IsRegionEdge == 0)
                for (int i = 0; i < dirtyEdges.Length; i++)
                {
                    if (dirtyEdges[i].IsRegionEdge == 0)
                    {
                        hasDirtyEdges = true;
                        break;
                    }
                }
            }

            // Get or create transit graph entity
            Entity transitGraphEntity;
            if (hierarchy.TransitGraphEntity != Entity.Null)
            {
                transitGraphEntity = hierarchy.TransitGraphEntity;
            }
            else
            {
                // Initial build - create transit graph
                transitGraphEntity = state.EntityManager.CreateEntity();
                state.EntityManager.AddComponent<TransitNode>(transitGraphEntity);
                state.EntityManager.AddBuffer<TransitEdge>(transitGraphEntity);
            }

            // Only rebuild if initial build or if dirty edges are marked
            if (hierarchy.TransitGraphEntity == Entity.Null || hasDirtyEdges)
            {

            var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);
            var transitNodes = new NativeHashMap<int, Entity>(32, Allocator.Temp);
            int nextTransitId = 0;

            // Discover warp relay nodes
            foreach (var (warpNode, transform, entity) in
                SystemAPI.Query<RefRO<WarpRelayNode>, RefRO<LocalTransform>>()
                .WithAll<WarpRelayNodeTag>()
                .WithEntityAccess())
            {
                var node = warpNode.ValueRO;
                if (node.Status == WarpRelayNodeStatus.Destroyed)
                {
                    continue; // Skip destroyed nodes
                }

                int transitId = nextTransitId++;
                transitNodes[transitId] = entity;

                // Create or update TransitNode
                if (!SystemAPI.HasComponent<TransitNode>(entity))
                {
                    ecb.AddComponent(entity, new TransitNode
                    {
                        TransitId = transitId,
                        Position = transform.ValueRO.Position,
                        Type = TransitNodeType.WarpRelay,
                        TransportEntity = entity,
                        BaseCost = 1f,
                        RequiresPayment = (byte)(node.OwnerFactionId != 0 ? 1 : 0)
                    });
                }
            }

            // Discover hyperway network nodes (via HyperwayNetworkRef)
            foreach (var (networkRef, transform, entity) in
                SystemAPI.Query<RefRO<HyperwayNetworkRef>, RefRO<LocalTransform>>()
                .WithEntityAccess())
            {
                // Check if this entity is a warp relay node (already processed)
                if (SystemAPI.HasComponent<WarpRelayNode>(entity))
                {
                    continue;
                }

                int transitId = nextTransitId++;
                transitNodes[transitId] = entity;

                if (!SystemAPI.HasComponent<TransitNode>(entity))
                {
                    ecb.AddComponent(entity, new TransitNode
                    {
                        TransitId = transitId,
                        Position = transform.ValueRO.Position,
                        Type = TransitNodeType.HyperwayNode,
                        TransportEntity = entity,
                        BaseCost = 1f,
                        RequiresPayment = 0
                    });
                }
            }

            // Discover ferry entities (entities with TransportService of type Ferry)
            foreach (var (transportService, transform, entity) in
                SystemAPI.Query<RefRO<TransportService>, RefRO<LocalTransform>>()
                .WithEntityAccess())
            {
                if (transportService.ValueRO.Type == TransportServiceType.Ferry)
                {
                    int transitId = nextTransitId++;
                    transitNodes[transitId] = entity;

                    if (!SystemAPI.HasComponent<TransitNode>(entity))
                    {
                        ecb.AddComponent(entity, new TransitNode
                        {
                            TransitId = transitId,
                            Position = transform.ValueRO.Position,
                            Type = TransitNodeType.FerryLanding,
                            TransportEntity = entity,
                            BaseCost = transportService.ValueRO.BaseCost,
                            RequiresPayment = transportService.ValueRO.RequiresPayment
                        });
                    }
                }
            }

            // Build TransitEdges from discovered nodes
            // For warp relays: create edges between nodes in same system/network
            var transitEdges = state.EntityManager.GetBuffer<TransitEdge>(transitGraphEntity);
            transitEdges.Clear();

            // Create edges between warp relay nodes in same system
            foreach (var (warpNode1, transform1, entity1) in
                SystemAPI.Query<RefRO<WarpRelayNode>, RefRO<LocalTransform>>()
                .WithAll<WarpRelayNodeTag>()
                .WithEntityAccess())
            {
                if (!SystemAPI.HasComponent<TransitNode>(entity1))
                {
                    continue;
                }

                var transitNode1 = SystemAPI.GetComponent<TransitNode>(entity1);
                int systemId1 = warpNode1.ValueRO.SystemId;

                foreach (var (warpNode2, transform2, entity2) in
                    SystemAPI.Query<RefRO<WarpRelayNode>, RefRO<LocalTransform>>()
                    .WithAll<WarpRelayNodeTag>()
                    .WithEntityAccess())
                {
                    if (entity1.Index >= entity2.Index)
                    {
                        continue; // Avoid duplicate edges
                    }

                    if (!SystemAPI.HasComponent<TransitNode>(entity2))
                    {
                        continue;
                    }

                    var transitNode2 = SystemAPI.GetComponent<TransitNode>(entity2);
                    int systemId2 = warpNode2.ValueRO.SystemId;

                    // Create edge if nodes are in same system or connected
                    if (systemId1 == systemId2 || warpNode1.ValueRO.NodeGrade >= 2) // Interstellar grade
                    {
                        float distance = math.length(transform2.ValueRO.Position - transform1.ValueRO.Position);
                        float estimatedTime = distance / 100f; // TODO: Use actual warp speed
                        float estimatedFuel = distance * 0.1f; // TODO: Use actual fuel consumption
                        float estimatedRisk = 0.1f; // TODO: Calculate based on node status, war, etc.

                        transitEdges.Add(new TransitEdge
                        {
                            FromTransitId = transitNode1.TransitId,
                            ToTransitId = transitNode2.TransitId,
                            Cost = estimatedTime + estimatedFuel + estimatedRisk,
                            EstimatedTime = estimatedTime,
                            EstimatedFuel = estimatedFuel,
                            EstimatedRisk = estimatedRisk,
                            TransportEntity = entity1, // Use source node as transport
                            IsBidirectional = 1,
                            RequiresPayment = (byte)(warpNode1.ValueRO.OwnerFactionId != 0 ? 1 : 0)
                        });
                    }
                }
            }

            // Create edges for ferry routes (if ferry has destination info)
            // TODO: When ferry route data exists, create TransitEdges between ferry landings

            // Create edges for hyperway links
            // TODO: When hyperway link data exists, create TransitEdges between hyperway nodes

                transitNodes.Dispose();
                ecb.Playback(state.EntityManager);

                // Clear dirty transit edges after processing
                if (state.EntityManager.HasBuffer<DirtyEdge>(hierarchyEntity))
                {
                    var dirtyEdges = state.EntityManager.GetBuffer<DirtyEdge>(hierarchyEntity);
                    // Remove transit edges (IsRegionEdge == 0)
                    for (int i = dirtyEdges.Length - 1; i >= 0; i--)
                    {
                        if (dirtyEdges[i].IsRegionEdge == 0)
                        {
                            dirtyEdges.RemoveAtSwapBack(i);
                        }
                    }
                }

                // Increment version to signal graph change
                var hierarchyRW = SystemAPI.GetComponentRW<NavGraphHierarchy>(hierarchyEntity);
                hierarchyRW.ValueRW.Version++;
            }
        }
    }
}


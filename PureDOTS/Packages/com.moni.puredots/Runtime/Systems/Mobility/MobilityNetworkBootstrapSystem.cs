using PureDOTS.Runtime.Mobility;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Systems.Mobility
{
    /// <summary>
    /// Seeds the mobility network singleton and buffers so registration systems have a target.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct MobilityNetworkBootstrapSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            EnsureMobilityNetwork(state.EntityManager, state.WorldUpdateAllocator);
            state.Enabled = false;
        }

        public void OnUpdate(ref SystemState state)
        {
        }

        private static void EnsureMobilityNetwork(EntityManager entityManager, Allocator allocator)
        {
            using var queryBuilder = new EntityQueryBuilder(allocator).WithAll<MobilityNetwork>();
            using var query = queryBuilder.Build(entityManager);
            Entity networkEntity;

            if (query.IsEmptyIgnoreFilter)
            {
                networkEntity = entityManager.CreateEntity();
                entityManager.AddComponentData(networkEntity, new MobilityNetwork
                {
                    Version = 0,
                    LastBuildTick = 0,
                    WaypointCount = 0,
                    HighwayCount = 0,
                    GatewayCount = 0
                });
            }
            else
            {
                networkEntity = query.GetSingletonEntity();
            }

            if (!entityManager.HasBuffer<MobilityWaypointEntry>(networkEntity))
            {
                entityManager.AddBuffer<MobilityWaypointEntry>(networkEntity);
            }

            if (!entityManager.HasBuffer<MobilityHighwayEntry>(networkEntity))
            {
                entityManager.AddBuffer<MobilityHighwayEntry>(networkEntity);
            }

            if (!entityManager.HasBuffer<MobilityGatewayEntry>(networkEntity))
            {
                entityManager.AddBuffer<MobilityGatewayEntry>(networkEntity);
            }

            if (!entityManager.HasBuffer<MobilityInterceptionEvent>(networkEntity))
            {
                entityManager.AddBuffer<MobilityInterceptionEvent>(networkEntity);
            }
        }
    }
}

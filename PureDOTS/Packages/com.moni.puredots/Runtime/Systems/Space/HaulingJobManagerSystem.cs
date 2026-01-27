using PureDOTS.Runtime.Space;
using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems.Space
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ResourcePileSystem))]
    public partial struct HaulingJobManagerSystem : ISystem
    {
        private Entity _jobQueueEntity;
        private ComponentLookup<LocalTransform> _storehouseTransformLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            // Create the job queue singleton without using managed ComponentType[] arrays.
            var archetypeEntity = state.EntityManager.CreateEntity();
            state.EntityManager.AddBuffer<HaulingJobQueueEntry>(archetypeEntity);
            _jobQueueEntity = archetypeEntity;
            _storehouseTransformLookup = state.GetComponentLookup<LocalTransform>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var queue = state.EntityManager.GetBuffer<HaulingJobQueueEntry>(_jobQueueEntity);
            queue.Clear();

            _storehouseTransformLookup.Update(ref state);

            // Collect storehouse entities into a NativeList for efficient iteration
            var storehouseList = new NativeList<Entity>(Allocator.TempJob);
            foreach (var (inventory, storehouseEntity) in SystemAPI.Query<RefRO<StorehouseInventory>>().WithEntityAccess())
            {
                storehouseList.Add(storehouseEntity);
            }

            foreach (var (pile, urgency, entity) in SystemAPI.Query<RefRO<ResourcePile>, RefRO<ResourceUrgency>>().WithEntityAccess())
            {
                var destination = FindNearestStorehouse(pile.ValueRO.Position, storehouseList, _storehouseTransformLookup);
                queue.Add(new HaulingJobQueueEntry
                {
                    Priority = HaulingJobPriority.Normal,
                    SourceEntity = entity,
                    DestinationEntity = destination,
                    RequestedAmount = pile.ValueRO.Amount,
                    Urgency = urgency.ValueRO.UrgencyWeight,
                    ResourceValue = urgency.ValueRO.UrgencyWeight
                });
            }

            storehouseList.Dispose();
        }

        private static Entity FindNearestStorehouse(float3 origin, NativeList<Entity> entities, ComponentLookup<LocalTransform> transformLookup)
        {
            if (entities.Length == 0)
            {
                return Entity.Null;
            }

            var best = Entity.Null;
            var bestDist = float.MaxValue;
            for (int i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                if (!transformLookup.HasComponent(entity))
                {
                    continue;
                }
                var transform = transformLookup[entity];
                var dist = math.lengthsq(transform.Position - origin);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = entity;
                }
            }

            return best;
        }

        public BufferLookup<HaulingJobQueueEntry> GetQueueLookup(ref SystemState state)
        {
            return state.GetBufferLookup<HaulingJobQueueEntry>(false);
        }

        public Entity GetQueueEntity() => _jobQueueEntity;

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }
    }
}

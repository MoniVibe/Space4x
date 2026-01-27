using PureDOTS.Runtime.Space;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Systems.Space
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(HaulingJobManagerSystem))]
    public partial struct HaulingJobPrioritySystem : ISystem
    {

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton(out EndSimulationEntityCommandBufferSystem.Singleton ecbSingleton))
            {
                return;
            }

            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            // Get catalog buffer if it exists and copy to NativeArray for IJobEntity
            // We copy to ensure the data is valid during job execution
            NativeArray<ResourceValueEntry> catalogArray = default;
            if (SystemAPI.TryGetSingletonBuffer<ResourceValueEntry>(out var catalogBuffer, true) && catalogBuffer.Length > 0)
            {
                catalogArray = new NativeArray<ResourceValueEntry>(catalogBuffer.Length, Allocator.TempJob);
                for (int i = 0; i < catalogBuffer.Length; i++)
                {
                    catalogArray[i] = catalogBuffer[i];
                }
            }

            // Use IJobEntity for zero-GC processing
            var job = new ProcessPriorityJob
            {
                CatalogArray = catalogArray,
                ECB = ecb.AsParallelWriter()
            };
            var jobHandle = job.ScheduleParallel(state.Dependency);
            
            // Dispose the NativeArray after job completes
            if (catalogArray.IsCreated)
            {
                jobHandle.Complete();
                catalogArray.Dispose();
            }
            
            state.Dependency = jobHandle;
        }

        [BurstCompile]
        private partial struct ProcessPriorityJob : IJobEntity
        {
            [ReadOnly] public NativeArray<ResourceValueEntry> CatalogArray;
            public EntityCommandBuffer.ParallelWriter ECB;

            public void Execute([EntityIndexInQuery] int entityInQueryIndex, Entity entity, in ResourcePileMeta meta)
            {
                var urgency = 1f;
                var value = 1f;
                if (CatalogArray.IsCreated)
                {
                    for (int j = 0; j < CatalogArray.Length; j++)
                    {
                        if (CatalogArray[j].ResourceTypeId.Equals(meta.ResourceTypeId))
                        {
                            value = CatalogArray[j].BaseValue;
                            break;
                        }
                    }
                }

                // SetComponent will add if missing or update if present
                ECB.SetComponent(entityInQueryIndex, entity, new ResourceUrgency
                {
                    ResourceTypeId = meta.ResourceTypeId,
                    UrgencyWeight = urgency * value
                });
            }
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }
    }
}

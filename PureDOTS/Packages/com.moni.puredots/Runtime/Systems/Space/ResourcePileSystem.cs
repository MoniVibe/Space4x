using PureDOTS.Runtime.Space;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Space
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct ResourcePileSystem : ISystem
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

            // Collect piles into NativeList for comparison (still zero-GC, uses TempJob allocator)
            var pileEntities = new NativeList<Entity>(Allocator.TempJob);
            var pileData = new NativeList<ResourcePile>(Allocator.TempJob);
            var pileMeta = new NativeList<ResourcePileMeta>(Allocator.TempJob);

            foreach (var (pile, meta, entity) in SystemAPI.Query<RefRW<ResourcePile>, RefRO<ResourcePileMeta>>().WithEntityAccess())
            {
                pileEntities.Add(entity);
                pileData.Add(pile.ValueRO);
                pileMeta.Add(meta.ValueRO);
            }

            // Merge nearby piles of the same type
            for (int i = 0; i < pileEntities.Length; i++)
            {
                if (pileData[i].Amount <= 0f)
                {
                    continue; // Already marked for destruction
                }

                for (int j = i + 1; j < pileEntities.Length; j++)
                {
                    if (pileData[j].Amount <= 0f)
                    {
                        continue; // Already marked for destruction
                    }

                    if (!pileMeta[i].ResourceTypeId.Equals(pileMeta[j].ResourceTypeId))
                    {
                        continue;
                    }

                    var distSq = math.lengthsq(pileData[i].Position - pileData[j].Position);
                    if (distSq > 1f) // merge when within 1m
                    {
                        continue;
                    }

                    var total = pileData[i].Amount + pileData[j].Amount;
                    var newAmount = math.min(pileMeta[i].MaxCapacity, total);
                    pileData[i] = new ResourcePile { Amount = newAmount, Position = pileData[i].Position };
                    pileData[j] = new ResourcePile { Amount = 0f, Position = pileData[j].Position };
                }
            }

            // Apply changes via ECB
            for (int i = 0; i < pileEntities.Length; i++)
            {
                if (pileData[i].Amount <= 0f)
                {
                    ecb.DestroyEntity(pileEntities[i]);
                }
                else
                {
                    ecb.SetComponent(pileEntities[i], pileData[i]);
                }
            }

            pileEntities.Dispose();
            pileData.Dispose();
            pileMeta.Dispose();
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }
    }
}

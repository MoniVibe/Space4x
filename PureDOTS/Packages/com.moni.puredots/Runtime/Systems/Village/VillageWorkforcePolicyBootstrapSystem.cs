using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Village;
using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Ensures every village has a workforce policy component so other systems can adjust emergency weights.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(VillagerSystemGroup))]
    [UpdateBefore(typeof(VillageWorkforceDemandSystem))]
    public partial struct VillageWorkforcePolicyBootstrapSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PureDOTS.Runtime.Village.VillageId>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);
            foreach (var (_, entity) in SystemAPI.Query<RefRO<PureDOTS.Runtime.Village.VillageId>>().WithNone<VillageWorkforcePolicy>().WithEntityAccess())
            {
                ecb.AddComponent(entity, new VillageWorkforcePolicy
                {
                    ConscriptionActive = 0,
                    ConscriptionUrgency = 0f,
                    DefenseUrgency = 0f
                });
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }
    }
}

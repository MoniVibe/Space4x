using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Systems.Construction
{
    /// <summary>
    /// Ensures construction sites own the command buffers required by downstream systems.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(ConstructionSystemGroup), OrderFirst = true)]
    public partial struct ConstructionSiteBufferEnsureSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ConstructionSiteProgress>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (progress, entity) in SystemAPI.Query<RefRO<ConstructionSiteProgress>>().WithEntityAccess())
            {
                if (!state.EntityManager.HasBuffer<ConstructionDepositCommand>(entity))
                {
                    ecb.AddBuffer<ConstructionDepositCommand>(entity);
                }

                if (!state.EntityManager.HasBuffer<ConstructionProgressCommand>(entity))
                {
                    ecb.AddBuffer<ConstructionProgressCommand>(entity);
                }

                if (!state.EntityManager.HasBuffer<ConstructionIncidentCommand>(entity))
                {
                    ecb.AddBuffer<ConstructionIncidentCommand>(entity);
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();

            state.Enabled = false;
        }
    }
}






using PureDOTS.Runtime.Components;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Queues presentation recycle requests for the next frame.
    /// Structural changes are deferred to EndSimulationEntityCommandBufferSystem to avoid
    /// race conditions with rendering systems that read ECS data after PresentationSystemGroup.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(EndSimulationEntityCommandBufferSystem))]
    [UpdateAfter(typeof(PresentationSpawnSystem))]
    public partial struct PresentationRecycleSystem : ISystem
    {
        private ComponentLookup<PresentationHandle> _handleLookup;

        public void OnCreate(ref SystemState state)
        {
            _handleLookup = state.GetComponentLookup<PresentationHandle>();
            state.RequireForUpdate<PresentationCommandQueue>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        public void OnUpdate(ref SystemState state)
        {
            // Skip during rewind playback (visuals are regenerated or restored from state)
            if (SystemAPI.TryGetSingleton<RewindState>(out var rewindState) && rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var queueEntity = SystemAPI.GetSingletonEntity<PresentationCommandQueue>();
            var recycleBuffer = SystemAPI.GetBuffer<PresentationRecycleRequest>(queueEntity);
            if (recycleBuffer.Length == 0)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton(out EndSimulationEntityCommandBufferSystem.Singleton ecbSingleton))
            {
                return;
            }

            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);
            _handleLookup.Update(ref state);
            uint recycledCount = 0;

            for (int i = 0; i < recycleBuffer.Length; i++)
            {
                var request = recycleBuffer[i];
                if (!_handleLookup.HasComponent(request.Target))
                {
                    continue;
                }

                var handle = _handleLookup[request.Target];
                if (state.EntityManager.Exists(handle.Visual))
                {
                    ecb.DestroyEntity(handle.Visual);
                }

                ecb.RemoveComponent<PresentationHandle>(request.Target);
                recycledCount++;
            }

            recycleBuffer.Clear();
            if (recycledCount > 0 && SystemAPI.TryGetSingletonRW<PresentationPoolStats>(out var stats))
            {
                var value = stats.ValueRO;
                value.RecycledThisFrame += recycledCount;
                value.TotalRecycled += recycledCount;
                value.ActiveVisuals = value.ActiveVisuals >= recycledCount ? value.ActiveVisuals - recycledCount : 0;
                stats.ValueRW = value;
            }
            // ECB playback is handled by EndSimulationEntityCommandBufferSystem
        }
    }
}

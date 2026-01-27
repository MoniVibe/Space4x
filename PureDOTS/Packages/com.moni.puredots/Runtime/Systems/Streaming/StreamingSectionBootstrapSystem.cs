using PureDOTS.Runtime.Streaming;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Systems.Streaming
{
    /// <summary>
    /// Guarantees every section entity carries the runtime/state components required by the loader.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(StreamingCoordinatorBootstrapSystem))]
    public partial struct StreamingSectionBootstrapSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<StreamingSectionDescriptor>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (descriptor, entity) in SystemAPI.Query<RefRO<StreamingSectionDescriptor>>()
                         .WithNone<StreamingSectionState>()
                         .WithEntityAccess())
            {
                ecb.AddComponent(entity, new StreamingSectionState
                {
                    Status = StreamingSectionStatus.Unloaded
                });
            }

            foreach (var (descriptor, entity) in SystemAPI.Query<RefRO<StreamingSectionDescriptor>>()
                         .WithNone<StreamingSectionRuntime>()
                         .WithEntityAccess())
            {
                ecb.AddComponent(entity, new StreamingSectionRuntime
                {
                    SceneEntity = Entity.Null
                });
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}

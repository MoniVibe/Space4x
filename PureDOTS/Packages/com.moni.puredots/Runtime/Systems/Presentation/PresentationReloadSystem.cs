using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Handles requests to rebuild all presentation instances by respawning visuals from handles.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(PresentationSpawnSystem))]
    public partial struct PresentationReloadSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PresentationCommandQueue>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var queueEntity = SystemAPI.GetSingletonEntity<PresentationCommandQueue>();
            if (!state.EntityManager.HasComponent<PresentationReloadCommand>(queueEntity))
            {
                return;
            }

            var spawnBuffer = SystemAPI.GetBuffer<PresentationSpawnRequest>(queueEntity);
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (handleRef, entity) in SystemAPI.Query<RefRW<PresentationHandle>>().WithEntityAccess())
            {
                var handle = handleRef.ValueRO;
                if (handle.Visual != Entity.Null && state.EntityManager.Exists(handle.Visual))
                {
                    ecb.DestroyEntity(handle.Visual);
                }

                var transform = LocalTransform.Identity;
                if (SystemAPI.HasComponent<LocalTransform>(entity))
                {
                    transform = SystemAPI.GetComponent<LocalTransform>(entity);
                }

                handle.Visual = Entity.Null;
                handleRef.ValueRW = handle;

                spawnBuffer.Add(new PresentationSpawnRequest
                {
                    Target = entity,
                    DescriptorHash = handle.DescriptorHash,
                    Position = transform.Position,
                    Rotation = transform.Rotation,
                    ScaleMultiplier = transform.Scale,
                    Tint = float4.zero,
                    VariantSeed = handle.VariantSeed,
                    Flags = PresentationSpawnFlags.AllowPooling
                });
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();

            state.EntityManager.RemoveComponent<PresentationReloadCommand>(queueEntity);
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }
    }
}

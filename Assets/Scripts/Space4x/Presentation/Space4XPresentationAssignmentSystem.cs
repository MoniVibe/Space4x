using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Presentation
{
    /// <summary>
    /// Spawns or recycles presentation visuals for any entity tagged with a Space4XPresentationBinding component.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TransformSystemGroup))]
    public partial struct Space4XPresentationAssignmentSystem : ISystem
    {
        private ComponentLookup<PresentationHandle> _handleLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _handleLookup = state.GetComponentLookup<PresentationHandle>();
            state.RequireForUpdate<Space4XPresentationBinding>();
            state.RequireForUpdate<PresentationCommandQueue>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingletonEntity<PresentationCommandQueue>(out var queueEntity))
            {
                return;
            }

            _handleLookup.Update(ref state);

            var spawnBuffer = state.EntityManager.GetBuffer<PresentationSpawnRequest>(queueEntity);
            var recycleBuffer = state.EntityManager.GetBuffer<PresentationRecycleRequest>(queueEntity);

            var cleanupEcb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (binding, transform, entity) in SystemAPI
                         .Query<RefRO<Space4XPresentationBinding>, RefRO<LocalTransform>>()
                         .WithEntityAccess())
            {
                bool hasHandle = _handleLookup.HasComponent(entity);

                if (!binding.ValueRO.Descriptor.IsValid)
                {
                    if (hasHandle)
                    {
                        recycleBuffer.Add(new PresentationRecycleRequest { Target = entity });
                    }

                    if (SystemAPI.HasComponent<Space4XPresentationDirtyTag>(entity))
                    {
                        cleanupEcb.RemoveComponent<Space4XPresentationDirtyTag>(entity);
                    }

                    continue;
                }

                bool isDirty = SystemAPI.HasComponent<Space4XPresentationDirtyTag>(entity);
                if (isDirty)
                {
                    if (hasHandle)
                    {
                        recycleBuffer.Add(new PresentationRecycleRequest { Target = entity });
                        continue;
                    }

                    cleanupEcb.RemoveComponent<Space4XPresentationDirtyTag>(entity);
                }

                if (hasHandle)
                {
                    var handle = _handleLookup[entity];
                    if (handle.DescriptorHash != binding.ValueRO.Descriptor ||
                        handle.VariantSeed != binding.ValueRO.VariantSeed)
                    {
                        recycleBuffer.Add(new PresentationRecycleRequest { Target = entity });
                    }

                    continue;
                }

                float3 rotatedOffset = math.rotate(transform.ValueRO.Rotation, binding.ValueRO.PositionOffset);
                float3 position = transform.ValueRO.Position + rotatedOffset;
                quaternion rotation = math.mul(transform.ValueRO.Rotation, binding.ValueRO.RotationOffset);
                float scaleMultiplier = math.max(0.01f, binding.ValueRO.ScaleMultiplier <= 0f ? 1f : binding.ValueRO.ScaleMultiplier);

                spawnBuffer.Add(new PresentationSpawnRequest
                {
                    Target = entity,
                    DescriptorHash = binding.ValueRO.Descriptor,
                    Position = position,
                    Rotation = rotation,
                    ScaleMultiplier = scaleMultiplier,
                    Tint = binding.ValueRO.Tint,
                    VariantSeed = binding.ValueRO.VariantSeed,
                    Flags = binding.ValueRO.Flags
                });
            }

            foreach (var (handle, entity) in SystemAPI
                         .Query<RefRO<PresentationHandle>>()
                         .WithNone<Space4XPresentationBinding>()
                         .WithEntityAccess())
            {
                recycleBuffer.Add(new PresentationRecycleRequest { Target = entity });
            }

            cleanupEcb.Playback(state.EntityManager);
            cleanupEcb.Dispose();
        }
    }
}

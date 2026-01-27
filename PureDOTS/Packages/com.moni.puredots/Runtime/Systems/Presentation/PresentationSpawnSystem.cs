using PureDOTS.Runtime.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Queues presentation spawn requests for the next frame.
    /// Structural changes are deferred to EndSimulationEntityCommandBufferSystem to avoid
    /// race conditions with rendering systems that read ECS data after PresentationSystemGroup.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(EndSimulationEntityCommandBufferSystem))]
    public partial struct PresentationSpawnSystem : ISystem
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

            if (!SystemAPI.TryGetSingleton(out PresentationRegistryReference registryRef))
            {
                return;
            }

            if (!registryRef.Registry.IsCreated)
            {
                return;
            }

            var queueEntity = SystemAPI.GetSingletonEntity<PresentationCommandQueue>();
            var spawnBuffer = SystemAPI.GetBuffer<PresentationSpawnRequest>(queueEntity);
            if (spawnBuffer.Length == 0)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton(out EndSimulationEntityCommandBufferSystem.Singleton ecbSingleton))
            {
                return;
            }

            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);
            _handleLookup.Update(ref state);
            uint spawnedCount = 0;

            for (int i = 0; i < spawnBuffer.Length; i++)
            {
                var request = spawnBuffer[i];
                if (!PresentationRegistryUtility.TryGetDescriptor(ref registryRef, request.DescriptorHash, out var descriptor))
                {
                    continue;
                }

                bool prefabHasBaseColor = state.EntityManager.HasComponent<URPMaterialPropertyBaseColor>(descriptor.Prefab);
                bool prefabHasEmission = state.EntityManager.HasComponent<URPMaterialPropertyEmissionColor>(descriptor.Prefab);

                var appliedFlags = descriptor.DefaultFlags | request.Flags;
                var visual = ecb.Instantiate(descriptor.Prefab);

                float3 position = request.Position + math.mul(request.Rotation, descriptor.DefaultOffset);
                quaternion rotation = request.Rotation;

                float scaleMultiplier = appliedFlags.HasFlag(PresentationSpawnFlags.OverrideScale)
                    ? math.max(0.01f, request.ScaleMultiplier)
                    : 1f;

                float scale = math.max(0.01f, descriptor.DefaultScale * scaleMultiplier);

                ecb.SetComponent(visual, LocalTransform.FromPositionRotationScale(position, rotation, scale));

                float4 tintValue = descriptor.DefaultTint;
                bool hasTintOverride = appliedFlags.HasFlag(PresentationSpawnFlags.OverrideTint);
                if (hasTintOverride)
                {
                    tintValue = request.Tint;
                }
                if (prefabHasBaseColor && math.any(tintValue != float4.zero))
                {
                    ecb.SetComponent(visual, new URPMaterialPropertyBaseColor { Value = tintValue });
                }
                if (prefabHasEmission && hasTintOverride)
                {
                    ecb.SetComponent(visual, new URPMaterialPropertyEmissionColor { Value = tintValue });
                }

                if (_handleLookup.HasComponent(request.Target))
                {
                    ecb.SetComponent(request.Target, new PresentationHandle
                    {
                        Visual = visual,
                        DescriptorHash = request.DescriptorHash,
                        VariantSeed = request.VariantSeed
                    });
                }
                else
                {
                    ecb.AddComponent(request.Target, new PresentationHandle
                    {
                        Visual = visual,
                        DescriptorHash = request.DescriptorHash,
                        VariantSeed = request.VariantSeed
                    });
                }

                spawnedCount++;
            }

            spawnBuffer.Clear();
            if (spawnedCount > 0 && SystemAPI.TryGetSingletonRW<PresentationPoolStats>(out var stats))
            {
                var value = stats.ValueRO;
                value.SpawnedThisFrame += spawnedCount;
                value.ActiveVisuals += spawnedCount;
                value.TotalSpawned += spawnedCount;
                stats.ValueRW = value;
            }
            // ECB playback is handled by EndSimulationEntityCommandBufferSystem
        }
    }
}

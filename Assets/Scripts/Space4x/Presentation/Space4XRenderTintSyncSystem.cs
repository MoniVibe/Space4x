using PureDOTS.Runtime.Core;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;

namespace Space4X.Presentation
{
    /// <summary>
    /// Ensures RenderTint drives URP base/emissive colors so entities render with their intended palette.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(Space4XPresentationLifecycleSystem))]
    public partial struct Space4XRenderTintSyncSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!RuntimeMode.IsRenderingEnabled)
            {
                return;
            }

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (tint, entity) in SystemAPI
                         .Query<RefRO<global::PureDOTS.Rendering.RenderTint>>()
                         .WithNone<URPMaterialPropertyBaseColor>()
                         .WithEntityAccess())
            {
                ecb.AddComponent(entity, new URPMaterialPropertyBaseColor
                {
                    Value = tint.ValueRO.Value
                });
            }

            foreach (var (tint, entity) in SystemAPI
                         .Query<RefRO<global::PureDOTS.Rendering.RenderTint>>()
                         .WithNone<URPMaterialPropertyEmissionColor>()
                         .WithEntityAccess())
            {
                ecb.AddComponent(entity, new URPMaterialPropertyEmissionColor
                {
                    Value = new float4(tint.ValueRO.Value.xyz * 0.75f, 1f)
                });
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();

            foreach (var (tint, baseColor, emission) in SystemAPI
                         .Query<RefRO<global::PureDOTS.Rendering.RenderTint>, RefRW<URPMaterialPropertyBaseColor>, RefRW<URPMaterialPropertyEmissionColor>>()
                         .WithChangeFilter<global::PureDOTS.Rendering.RenderTint>())
            {
                var color = tint.ValueRO.Value;
                baseColor.ValueRW.Value = color;
                emission.ValueRW.Value = new float4(color.xyz * 0.75f, 1f);
            }
        }
    }
}

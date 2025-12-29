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
        private static float4 ResolveSafeTint(in float4 color)
        {
            if (color.w <= 0.001f || math.all(color.xyz <= 0.001f))
            {
                return new float4(1f, 1f, 1f, 1f);
            }

            return color;
        }

        private static bool IsNearBlack(in float4 color)
        {
            return color.w <= 0.001f || math.all(color.xyz <= 0.001f);
        }

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
                var resolved = ResolveSafeTint(tint.ValueRO.Value);
                ecb.AddComponent(entity, new URPMaterialPropertyBaseColor
                {
                    Value = resolved
                });
            }

            foreach (var (tint, entity) in SystemAPI
                         .Query<RefRO<global::PureDOTS.Rendering.RenderTint>>()
                         .WithNone<URPMaterialPropertyEmissionColor>()
                         .WithEntityAccess())
            {
                var resolved = ResolveSafeTint(tint.ValueRO.Value);
                ecb.AddComponent(entity, new URPMaterialPropertyEmissionColor
                {
                    Value = new float4(resolved.xyz * 0.75f, 1f)
                });
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();

            foreach (var (tint, baseColor, emission) in SystemAPI
                         .Query<RefRW<global::PureDOTS.Rendering.RenderTint>, RefRW<URPMaterialPropertyBaseColor>, RefRW<URPMaterialPropertyEmissionColor>>())
            {
                var resolved = ResolveSafeTint(tint.ValueRO.Value);
                if (IsNearBlack(tint.ValueRO.Value) || IsNearBlack(baseColor.ValueRO.Value) || IsNearBlack(emission.ValueRO.Value))
                {
                    tint.ValueRW.Value = resolved;
                    baseColor.ValueRW.Value = resolved;
                    emission.ValueRW.Value = new float4(resolved.xyz * 0.75f, 1f);
                }
            }
        }
    }
}

using Space4X.Registry;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Presentation
{
    /// <summary>
    /// Debug system that visualizes LOD levels by changing entity colors.
    /// Toggled via Demo01Authoring.EnableLODDebug.
    /// </summary>
    [UpdateInGroup(typeof(Unity.Entities.PresentationSystemGroup))]
    [UpdateAfter(typeof(Space4XPresentationLODSystem))]
    public partial struct Space4XLODDebugSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<DebugOverlayConfig>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<DebugOverlayConfig>(out var debugConfig))
            {
                return;
            }

            if (!debugConfig.ShowLODVisualization)
            {
                return;
            }

            // Update carrier colors based on LOD
            foreach (var (lodData, materialOverride, entity) in 
                     SystemAPI.Query<RefRO<RenderLODData>, RefRW<MaterialPropertyOverride>>()
                     .WithAll<CarrierPresentationTag>()
                     .WithEntityAccess())
            {
                byte lod = lodData.ValueRO.RecommendedLOD;
                float4 color = GetLODColor(lod);
                materialOverride.ValueRW.BaseColor = color;
            }

            // Update craft colors based on LOD
            foreach (var (lodData, materialOverride, entity) in 
                     SystemAPI.Query<RefRO<RenderLODData>, RefRW<MaterialPropertyOverride>>()
                     .WithAll<CraftPresentationTag>()
                     .WithEntityAccess())
            {
                byte lod = lodData.ValueRO.RecommendedLOD;
                float4 color = GetLODColor(lod);
                materialOverride.ValueRW.BaseColor = color;
            }

            // Update asteroid colors based on LOD
            foreach (var (lodData, materialOverride, entity) in 
                     SystemAPI.Query<RefRO<RenderLODData>, RefRW<MaterialPropertyOverride>>()
                     .WithAll<AsteroidPresentationTag>()
                     .WithEntityAccess())
            {
                byte lod = lodData.ValueRO.RecommendedLOD;
                float4 color = GetLODColor(lod);
                materialOverride.ValueRW.BaseColor = color;
            }
        }

        private static float4 GetLODColor(byte lod)
        {
            return lod switch
            {
                0 => new float4(0f, 1f, 0f, 1f),      // Green - Full detail
                1 => new float4(1f, 1f, 0f, 1f),      // Yellow - Reduced detail
                2 => new float4(1f, 0.5f, 0f, 1f),    // Orange - Impostor
                _ => new float4(1f, 0f, 0f, 1f)       // Red - Hidden/Culled
            };
        }
    }
}


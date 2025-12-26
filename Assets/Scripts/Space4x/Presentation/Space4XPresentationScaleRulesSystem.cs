using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Core;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Presentation
{
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(Space4XPresentationLifecycleSystem))]
    [UpdateBefore(typeof(Space4XPresentationDepthSystem))]
    public partial struct Space4XPresentationScaleRulesSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PresentationContentResolved>();
            state.RequireForUpdate<PresentationScaleMultiplier>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!RuntimeMode.IsRenderingEnabled)
            {
                return;
            }

            var config = SystemAPI.TryGetSingleton<PresentationScaleConfig>(out var overrideConfig)
                ? overrideConfig
                : PresentationScaleConfig.Default;

            foreach (var (resolved, layer, scale) in SystemAPI
                         .Query<RefRO<PresentationContentResolved>, RefRO<PresentationLayer>, RefRW<PresentationScaleMultiplier>>())
            {
                if ((resolved.ValueRO.Flags & PresentationContentFlags.HasBaseScale) == 0)
                {
                    continue;
                }

                var target = math.max(0.001f,
                    resolved.ValueRO.BaseScale * ResolveLayerMultiplier(layer.ValueRO.Value, config));
                if (math.abs(scale.ValueRO.Value - target) > 0.0001f)
                {
                    scale.ValueRW.Value = target;
                }
            }
        }

        private static float ResolveLayerMultiplier(PresentationLayerId layer, in PresentationScaleConfig config)
        {
            return layer switch
            {
                PresentationLayerId.Colony => config.ColonyMultiplier,
                PresentationLayerId.Island => config.IslandMultiplier,
                PresentationLayerId.Continent => config.ContinentMultiplier,
                PresentationLayerId.Planet => config.PlanetMultiplier,
                PresentationLayerId.Orbital => config.OrbitalMultiplier,
                PresentationLayerId.System => config.SystemMultiplier,
                PresentationLayerId.Galactic => config.GalacticMultiplier,
                _ => 1f
            };
        }
    }
}

using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace Space4X.Presentation
{
    /// <summary>
    /// System that handles overlay toggle controls from input.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(Space4XResourceOverlaySystem))]
    public partial struct Space4XOverlayControlSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            // Create debug overlay config singleton if it doesn't exist
            if (!SystemAPI.HasSingleton<DebugOverlayConfig>())
            {
                var entity = state.EntityManager.CreateEntity();
                state.EntityManager.AddComponentData(entity, new DebugOverlayConfig
                {
                    ShowResourceFields = false,
                    ShowFactionZones = false,
                    ShowLogisticsOverlay = false,
                    ShowDebugPaths = false,
                    ShowLODVisualization = false,
                    ShowMetrics = true,
                    ShowInspector = true
                });
            }
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<CommandInput>(out var commandInput))
            {
                return;
            }

            if (!SystemAPI.TryGetSingletonEntity<DebugOverlayConfig>(out var configEntity))
            {
                return;
            }

            var overlayConfig = state.EntityManager.GetComponentData<DebugOverlayConfig>(configEntity);
            bool changed = false;

            // Toggle overlays
            if (commandInput.ToggleOverlaysPressed)
            {
                // Cycle through overlay modes: Resource → Faction → Routes → Off
                if (!overlayConfig.ShowResourceFields && !overlayConfig.ShowFactionZones && !overlayConfig.ShowLogisticsOverlay)
                {
                    overlayConfig.ShowResourceFields = true;
                    changed = true;
                }
                else if (overlayConfig.ShowResourceFields)
                {
                    overlayConfig.ShowResourceFields = false;
                    overlayConfig.ShowFactionZones = true;
                    changed = true;
                }
                else if (overlayConfig.ShowFactionZones)
                {
                    overlayConfig.ShowFactionZones = false;
                    overlayConfig.ShowLogisticsOverlay = true;
                    changed = true;
                }
                else if (overlayConfig.ShowLogisticsOverlay)
                {
                    overlayConfig.ShowLogisticsOverlay = false;
                    changed = true;
                }
            }

            if (changed)
            {
                state.EntityManager.SetComponentData(configEntity, overlayConfig);
            }
        }
    }
}


using PureDOTS.Input;
using PureDOTS.Rendering;
using Unity.Entities;
using UnityDebug = UnityEngine.Debug;

namespace Space4X.Rendering.Systems
{
    /// <summary>
    /// Editor-only harness to flip render themes and verify variant swaps.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct Space4XRenderThemeDebugSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
#if !UNITY_EDITOR
            state.Enabled = false;
#endif
        }

        public void OnUpdate(ref SystemState state)
        {
#if UNITY_EDITOR
            if (!SystemAPI.TryGetSingletonRW<ActiveRenderTheme>(out var theme))
                return;

            if (Hotkeys.F6Down())
            {
                var next = (ushort)(theme.ValueRO.ThemeId == 0 ? 1 : 0);
                theme.ValueRW = new ActiveRenderTheme { ThemeId = next };
                UnityDebug.Log($"[Space4XRenderThemeDebugSystem] Swapped render theme to {next}.");
            }
#endif
        }

        public void OnDestroy(ref SystemState state)
        {
        }
    }
}

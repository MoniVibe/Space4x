using PureDOTS.Rendering;
using Unity.Burst;
using Unity.Entities;

namespace Space4X.Rendering.Systems
{
    /// <summary>
    /// Ensures an ActiveRenderTheme singleton exists so variant resolution has a default theme.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.Default)]
    [UpdateInGroup(typeof(PresentationSystemGroup), OrderFirst = true)]
    public partial struct Space4XRenderThemeBootstrapSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            if (!SystemAPI.HasSingleton<ActiveRenderTheme>())
            {
                var entity = state.EntityManager.CreateEntity();
                state.EntityManager.AddComponentData(entity, new ActiveRenderTheme
                {
                    ThemeId = 0
                });
            }
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }
    }
}

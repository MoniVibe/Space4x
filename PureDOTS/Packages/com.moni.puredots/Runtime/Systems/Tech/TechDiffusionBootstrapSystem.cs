using PureDOTS.Runtime.Tech;
using Unity.Entities;

namespace PureDOTS.Systems.Tech
{
    /// <summary>
    /// Seeds diffusion state for entities that participate in tech diffusion and installs default settings.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct TechDiffusionBootstrapSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TechLevel>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);

            foreach (var (tech, entity) in SystemAPI.Query<RefRO<TechLevel>>().WithNone<TechDiffusionState>().WithEntityAccess())
            {
                ecb.AddComponent(entity, new TechDiffusionState
                {
                    LastSource = Entity.Null,
                    IncomingLevel = tech.ValueRO.Value,
                    Progress = 1f,
                    Distance = 0f,
                    AppliedRate = 0f,
                    LastUpdateTick = 0
                });
            }

            if (!SystemAPI.HasSingleton<TechDiffusionSettings>())
            {
                var settingsEntity = ecb.CreateEntity();
                ecb.AddComponent(settingsEntity, TechDiffusionSettings.CreateDefault());
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}

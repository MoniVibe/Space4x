using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Pooling;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Systems
{
    [UpdateInGroup(typeof(TimeSystemGroup))]
    [UpdateAfter(typeof(TimeSettingsConfigSystem))]
    public partial struct PoolingCoordinatorSystem : ISystem
    {
        private EntityQuery _settingsQuery;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PoolingSettings>();
            state.RequireForUpdate<PoolingSettingsConfig>();

            _settingsQuery = SystemAPI.QueryBuilder()
                .WithAll<PoolingSettings>()
                .Build();

            if (!SystemAPI.HasSingleton<PoolingDiagnostics>())
            {
                var entity = state.EntityManager.CreateEntity();
                state.EntityManager.AddComponent<PoolingDiagnostics>(entity);
            }
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<PoolingSettings>(out var settings))
            {
                return;
            }

            NxPoolingRuntime.Initialise(settings.Value);

            var diagnostics = NxPoolingRuntime.GatherDiagnostics();
            var diagnosticsEntity = SystemAPI.GetSingletonEntity<PoolingDiagnostics>();
            state.EntityManager.SetComponentData(diagnosticsEntity, diagnostics);

            state.Enabled = false;
        }

        public void OnDestroy(ref SystemState state)
        {
            NxPoolingRuntime.Dispose();
        }
    }
}

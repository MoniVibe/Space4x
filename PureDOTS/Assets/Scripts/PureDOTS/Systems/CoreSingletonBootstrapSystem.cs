using PureDOTS.Runtime.Components;
using Unity.Entities;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Ensures the core deterministic singletons exist even without authoring data.
    /// Runs once at startup so downstream systems can safely require these components.
    /// </summary>
    [UpdateInGroup(typeof(TimeSystemGroup), OrderFirst = true)]
    public partial class CoreSingletonBootstrapSystem : SystemBase
    {
        protected override void OnCreate()
        {
            base.OnCreate();
            var entityManager = EntityManager;

            if (!SystemAPI.HasSingleton<TimeState>())
            {
                var entity = entityManager.CreateEntity(typeof(TimeState));
                entityManager.SetComponentData(entity, new TimeState
                {
                    FixedDeltaTime = TimeSettingsDefaults.FixedDeltaTime,
                    CurrentSpeedMultiplier = TimeSettingsDefaults.DefaultSpeedMultiplier,
                    Tick = 0,
                    IsPaused = TimeSettingsDefaults.PauseOnStart
                });
            }

            if (!SystemAPI.HasSingleton<HistorySettings>())
            {
                var entity = entityManager.CreateEntity(typeof(HistorySettings));
                entityManager.SetComponentData(entity, HistorySettingsDefaults.CreateDefault());
            }

            Entity rewindEntity;
            if (!SystemAPI.HasSingleton<RewindState>())
            {
                rewindEntity = entityManager.CreateEntity(typeof(RewindState));
                entityManager.SetComponentData(rewindEntity, new RewindState
                {
                    Mode = RewindMode.Record,
                    StartTick = 0,
                    TargetTick = 0,
                    PlaybackTick = 0,
                    PlaybackTicksPerSecond = HistorySettingsDefaults.DefaultTicksPerSecond,
                    ScrubDirection = 0,
                    ScrubSpeedMultiplier = 1f
                });
            }
            else
            {
                rewindEntity = SystemAPI.GetSingletonEntity<RewindState>();
            }

            if (!entityManager.HasBuffer<TimeControlCommand>(rewindEntity))
            {
                entityManager.AddBuffer<TimeControlCommand>(rewindEntity);
            }

            Enabled = false;
        }

        protected override void OnUpdate()
        {
            // No-op; this system only seeds singleton entities on create.
        }
    }
}

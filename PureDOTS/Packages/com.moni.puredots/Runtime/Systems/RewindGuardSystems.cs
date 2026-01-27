using PureDOTS.Runtime.Components;
using Unity.Entities;
using Unity.Physics.Systems;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Enables or disables <see cref="EnvironmentSystemGroup"/> based on the active rewind mode.
    /// Environment updates are skipped while replaying history to keep the simulation deterministic.
    /// </summary>
    /// <remarks>Order and behaviour documented in Docs/TruthSources/RuntimeLifecycle_TruthSource.md.</remarks>
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    [UpdateBefore(typeof(EnvironmentSystemGroup))]
    public partial struct EnvironmentRewindGuardSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<RewindState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var targetGroup = state.World.GetExistingSystemManaged<EnvironmentSystemGroup>();
            if (targetGroup == null)
            {
                return;
            }

            var rewind = SystemAPI.GetSingleton<RewindState>();
            var shouldRun = rewind.Mode == RewindMode.Record || rewind.Mode == RewindMode.CatchUp;
            if (targetGroup.Enabled != shouldRun)
            {
                targetGroup.Enabled = shouldRun;
            }
        }
    }

    /// <summary>
    /// Guards the spatial rebuild pipeline during rewind modes.
    /// </summary>
    /// <remarks>Order and behaviour documented in Docs/TruthSources/RuntimeLifecycle_TruthSource.md.</remarks>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(EnvironmentRewindGuardSystem))]
    [UpdateBefore(typeof(SpatialSystemGroup))]
    public partial struct SpatialRewindGuardSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<RewindState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var targetGroup = state.World.GetExistingSystemManaged<SpatialSystemGroup>();
            if (targetGroup == null)
            {
                return;
            }

            var rewind = SystemAPI.GetSingleton<RewindState>();
            var shouldRun = rewind.Mode == RewindMode.Record || rewind.Mode == RewindMode.CatchUp;
            if (targetGroup.Enabled != shouldRun)
            {
                targetGroup.Enabled = shouldRun;
            }
        }
    }

    /// <summary>
    /// Disables the high-level gameplay systems while the simulation is replaying history.
    /// </summary>
    /// <remarks>Order and behaviour documented in Docs/TruthSources/RuntimeLifecycle_TruthSource.md.</remarks>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(SpatialRewindGuardSystem))]
    [UpdateBefore(typeof(GameplaySystemGroup))]
    public partial struct GameplayRewindGuardSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<RewindState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var targetGroup = state.World.GetExistingSystemManaged<GameplaySystemGroup>();
            if (targetGroup == null)
            {
                return;
            }

            var rewind = SystemAPI.GetSingleton<RewindState>();
            var shouldRun = rewind.Mode == RewindMode.Record || rewind.Mode == RewindMode.CatchUp;
            if (targetGroup.Enabled != shouldRun)
            {
                targetGroup.Enabled = shouldRun;
            }
        }
    }

    /// <summary>
    /// Guards camera input systems during rewind modes.
    /// Camera input should only run during Record mode to capture new inputs.
    /// </summary>
    /// <remarks>Order and behaviour documented in Docs/TruthSources/RuntimeLifecycle_TruthSource.md.</remarks>
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    [UpdateBefore(typeof(CameraInputSystemGroup))]
    public partial struct CameraInputRewindGuardSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<RewindState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var targetGroup = state.World.GetExistingSystemManaged<CameraInputSystemGroup>();
            if (targetGroup == null)
            {
                return;
            }

            var rewind = SystemAPI.GetSingleton<RewindState>();
            var shouldRun = rewind.Mode == RewindMode.Record || rewind.Mode == RewindMode.CatchUp;
            if (targetGroup.Enabled != shouldRun)
            {
                targetGroup.Enabled = shouldRun;
            }
        }
    }

    /// <summary>
    /// Guards hand interaction systems during rewind modes.
    /// Hand systems should only run during Record and CatchUp modes to allow new inputs or replay stored commands.
    /// </summary>
    /// <remarks>Order and behaviour documented in Docs/TruthSources/RuntimeLifecycle_TruthSource.md.</remarks>
    [UpdateInGroup(typeof(PhysicsSystemGroup))]
    [UpdateBefore(typeof(HandSystemGroup))]
    public partial struct HandRewindGuardSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<RewindState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var targetGroup = state.World.GetExistingSystemManaged<HandSystemGroup>();
            if (targetGroup == null)
            {
                return;
            }

            var rewind = SystemAPI.GetSingleton<RewindState>();
            var shouldRun = rewind.Mode == RewindMode.Record || rewind.Mode == RewindMode.CatchUp;
            if (targetGroup.Enabled != shouldRun)
            {
                targetGroup.Enabled = shouldRun;
            }
        }
    }

    /// <summary>
    /// Presentation runs during normal play and while reviewing playback, but is disabled during catch-up rewinds.
    /// </summary>
    /// <remarks>Order and behaviour documented in Docs/TruthSources/RuntimeLifecycle_TruthSource.md.</remarks>
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    public partial struct PresentationRewindGuardSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<RewindState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var targetGroup = state.World.GetExistingSystemManaged<Unity.Entities.PresentationSystemGroup>();
            if (targetGroup == null)
            {
                return;
            }

            var rewind = SystemAPI.GetSingleton<RewindState>();
            var shouldRun = rewind.Mode == RewindMode.Record || rewind.Mode == RewindMode.Playback;
            if (targetGroup.Enabled != shouldRun)
            {
                targetGroup.Enabled = shouldRun;
            }
        }
    }
}

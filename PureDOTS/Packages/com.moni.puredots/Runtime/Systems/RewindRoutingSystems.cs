using PureDOTS.Runtime.Components;
using Unity.Entities;

namespace PureDOTS.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class RecordSimulationSystemGroup : ComponentSystemGroup
    {
    }

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(RecordSimulationSystemGroup))]
    public partial class CatchUpSimulationSystemGroup : ComponentSystemGroup
    {
    }

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(CatchUpSimulationSystemGroup))]
    public partial class PlaybackSimulationSystemGroup : ComponentSystemGroup
    {
    }

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(RecordSimulationSystemGroup))]
    public partial struct RewindModeRoutingSystem : ISystem
    {
        private RewindMode _lastMode;
        private bool _initialized;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<RewindState>();
            _lastMode = RewindMode.Record;
            _initialized = false;
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton(out RewindState rewindState))
            {
                return;
            }

            if (_initialized && rewindState.Mode == _lastMode)
            {
                return;
            }

            _initialized = true;
            _lastMode = rewindState.Mode;

            var recordGroup = state.World.GetOrCreateSystemManaged<RecordSimulationSystemGroup>();
            var catchUpGroup = state.World.GetOrCreateSystemManaged<CatchUpSimulationSystemGroup>();
            var playbackGroup = state.World.GetOrCreateSystemManaged<PlaybackSimulationSystemGroup>();

            recordGroup.Enabled = rewindState.Mode == RewindMode.Record;
            catchUpGroup.Enabled = rewindState.Mode == RewindMode.CatchUp;
            playbackGroup.Enabled = rewindState.Mode == RewindMode.Playback;
        }
    }
}

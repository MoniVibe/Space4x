using PureDOTS.Runtime.Components;
using Unity.Entities;

namespace PureDOTS.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(InitializationSystemGroup))]
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
    public partial class RewindModeRoutingSystem : SystemBase
    {
        private RecordSimulationSystemGroup _recordGroup;
        private CatchUpSimulationSystemGroup _catchUpGroup;
        private PlaybackSimulationSystemGroup _playbackGroup;

        protected override void OnCreate()
        {
            RequireForUpdate<RewindState>();

            _recordGroup = World.GetExistingSystemManaged<RecordSimulationSystemGroup>();
            _catchUpGroup = World.GetExistingSystemManaged<CatchUpSimulationSystemGroup>();
            _playbackGroup = World.GetExistingSystemManaged<PlaybackSimulationSystemGroup>();
        }

        protected override void OnUpdate()
        {
            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState))
            {
                return;
            }

            switch (rewindState.Mode)
            {
                case RewindMode.Record:
                    EnableGroup(_recordGroup, true);
                    EnableGroup(_catchUpGroup, false);
                    EnableGroup(_playbackGroup, false);
                    break;

                case RewindMode.CatchUp:
                    EnableGroup(_recordGroup, false);
                    EnableGroup(_catchUpGroup, true);
                    EnableGroup(_playbackGroup, false);
                    break;

                case RewindMode.Playback:
                    EnableGroup(_recordGroup, false);
                    EnableGroup(_catchUpGroup, false);
                    EnableGroup(_playbackGroup, true);
                    break;
            }
        }

        private static void EnableGroup(ComponentSystemGroup group, bool enabled)
        {
            if (group != null)
            {
                group.Enabled = enabled;
            }
        }
    }
}

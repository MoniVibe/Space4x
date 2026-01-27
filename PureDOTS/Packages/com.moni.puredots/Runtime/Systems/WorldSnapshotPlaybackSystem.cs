using Unity.Burst;
using Unity.Entities;
using PureDOTS.Runtime.Components;

namespace PureDOTS.Systems
{
    [BurstCompile]
    public partial struct WorldSnapshotPlaybackSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            // Snapshot playback disabled for MVP.
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // MVP: do not perform any snapshot playback.
            // When re-enabling, use state.GetEntityQuery(ComponentType.ReadOnly<WorldSnapshotRestoreRequest>())
            // and process at most one restore request before returning.
            return;
        }
    }
}


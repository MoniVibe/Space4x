using PureDOTS.Runtime.Components;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Tracks rewind guard violations and provides telemetry for systems running during playback/catch-up unexpectedly.
    /// Updates DebugDisplayData with guard violation counts.
    /// Note: Runs in LateSimulationSystemGroup, which executes after SimulationSystemGroup where RewindCoordinatorSystem runs.
    /// </summary>
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    public partial struct RewindTelemetrySystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<RewindState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.HasSingleton<RewindState>() || !SystemAPI.HasSingleton<DebugDisplayData>())
            {
                return;
            }

            var rewindState = SystemAPI.GetSingleton<RewindState>();
            var debugData = SystemAPI.GetSingletonRW<DebugDisplayData>();

            // Check if guarded groups are properly disabled during playback
            int violationCount = 0;
            var violationText = new FixedString512Bytes();

            if (rewindState.Mode == RewindMode.Playback)
            {
                // During playback, these groups should be disabled
                CheckGroupViolation(ref state, typeof(EnvironmentSystemGroup), "Environment", ref violationCount, ref violationText);
                CheckGroupViolation(ref state, typeof(SpatialSystemGroup), "Spatial", ref violationCount, ref violationText);
                CheckGroupViolation(ref state, typeof(GameplaySystemGroup), "Gameplay", ref violationCount, ref violationText);
                CheckGroupViolation(ref state, typeof(CameraInputSystemGroup), "CameraInput", ref violationCount, ref violationText);
                CheckGroupViolation(ref state, typeof(HandSystemGroup), "Hand", ref violationCount, ref violationText);
            }
            else if (rewindState.Mode == RewindMode.CatchUp)
            {
                // During catch-up, Presentation should be disabled
                CheckGroupViolation(ref state, typeof(Unity.Entities.PresentationSystemGroup), "Presentation", ref violationCount, ref violationText);
            }

            debugData.ValueRW.RewindGuardViolationCount = violationCount;
            debugData.ValueRW.RewindGuardViolationText = violationText;
        }

        private void CheckGroupViolation(ref SystemState state, System.Type groupType, FixedString32Bytes groupName, ref int violationCount, ref FixedString512Bytes violationText)
        {
            var group = state.World.GetExistingSystemManaged(groupType);
            if (group != null && group.Enabled)
            {
                violationCount++;
                if (violationText.Length > 0)
                {
                    violationText.Append(", ");
                }
                violationText.Append(groupName);
            }
        }
    }
}


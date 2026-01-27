using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Movement;
using PureDOTS.Systems;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Systems.Movement
{
    /// <summary>
    /// Processes MovementModeRequest buffer and validates energy/heat/cooldown before mode switch.
    /// Sets MovementState.Mode (Cruise, Boost, Drift, Hover).
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(MovementIntegrateSystem))]
    public partial struct MovementModeSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var rewindState = SystemAPI.GetSingleton<RewindState>();
            if (rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var timeState = SystemAPI.GetSingleton<TimeState>();
            var currentTick = timeState.Tick;

            var job = new MovementModeJob
            {
                CurrentTick = currentTick
            };

            state.Dependency = job.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        public partial struct MovementModeJob : IJobEntity
        {
            public uint CurrentTick;

            void Execute(
                Entity entity,
                ref MovementState movementState,
                in MovementModelRef modelRef,
                DynamicBuffer<MovementModeRequest> modeRequests)
            {
                if (modeRequests.Length == 0)
                {
                    return;
                }

                if (!modelRef.Blob.IsCreated)
                {
                    modeRequests.Clear();
                    return;
                }

                ref var spec = ref modelRef.Blob.Value;

                // Process mode requests (most recent first)
                for (int i = modeRequests.Length - 1; i >= 0; i--)
                {
                    var request = modeRequests[i];
                    byte requestedMode = request.Mode;

                    // Validate mode is allowed by capabilities
                    bool isValid = false;
                    switch ((MovementMode)requestedMode)
                    {
                        case MovementMode.Cruise:
                            isValid = true; // Always allowed
                            break;
                        case MovementMode.Boost:
                            isValid = (spec.Caps & MovementCaps.Boost) != 0;
                            break;
                        case MovementMode.Drift:
                            isValid = (spec.Caps & MovementCaps.Drift) != 0;
                            break;
                        case MovementMode.Hover:
                            isValid = spec.Dim == 2 || (spec.Caps & MovementCaps.Vertical) != 0;
                            break;
                        case MovementMode.Brake:
                            isValid = true; // Always allowed
                            break;
                    }

                    if (isValid)
                    {
                        // TODO: Validate energy/heat/stamina costs
                        // For now, accept all valid mode requests

                        movementState.Mode = requestedMode;
                        modeRequests.Clear();
                        return;
                    }
                }

                // No valid requests, clear buffer
                modeRequests.Clear();
            }
        }
    }
}


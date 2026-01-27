using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Miracles;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Systems.Miracles
{
    /// <summary>
    /// Converts MiracleRuntimeStateNew activation signals into MiracleActivationRequest entries.
    /// Uses edge detection to create requests only on activation transitions (0→1).
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(MiracleEffectSystemGroup), OrderFirst = true)]
    [UpdateAfter(typeof(MiracleTargetingSystem))]
    [UpdateBefore(typeof(MiracleActivationSystem))]
    public partial struct MiracleRequestCreationSystem : ISystem
    {
        private ComponentLookup<MiracleRequestCreationState> _trackingLookup;
        private ComponentLookup<MiracleTargetSolution> _targetSolutionLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<MiracleConfigState>();
            _trackingLookup = state.GetComponentLookup<MiracleRequestCreationState>(false);
            _targetSolutionLookup = state.GetComponentLookup<MiracleTargetSolution>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.TempJob);
            _trackingLookup.Update(ref state);
            _targetSolutionLookup.Update(ref state);

            foreach (var (runtimeState, entity) in SystemAPI
                         .Query<RefRO<MiracleRuntimeStateNew>>()
                         .WithEntityAccess())
            {
                var runtime = runtimeState.ValueRO;

                // Skip if no miracle selected
                if (runtime.SelectedId == MiracleId.None)
                {
                    continue;
                }

                // Get or create tracking state
                bool hasTracking = _trackingLookup.HasComponent(entity);
                MiracleRequestCreationState tracking = hasTracking ? _trackingLookup[entity] : default;
                if (!hasTracking)
                {
                    tracking = new MiracleRequestCreationState
                    {
                        PreviousIsActivating = 0,
                        PreviousIsSustained = 0
                    };
                    ecb.AddComponent(entity, tracking);
                }

                // Edge detection: only create request on 0→1 transition
                bool wasActivating = tracking.PreviousIsActivating != 0;
                bool isActivating = runtime.IsActivating != 0;
                bool wasSustained = tracking.PreviousIsSustained != 0;
                bool isSustained = runtime.IsSustained != 0;

                bool shouldCreateRequest = !wasActivating && isActivating;
                bool shouldCreateSustained = !wasSustained && isSustained;

                // Create request if activation signal detected
                if (shouldCreateRequest || shouldCreateSustained)
                {
                    // Read target solution
                    if (_targetSolutionLookup.HasComponent(entity))
                    {
                        var solution = _targetSolutionLookup[entity];
                        // Only create request if target is valid and matches selected miracle
                        if (solution.IsValid != 0 && 
                            solution.SelectedMiracleId != MiracleId.None &&
                            solution.SelectedMiracleId == runtime.SelectedId)
                        {
                            // Ensure request buffer exists
                            if (!SystemAPI.HasBuffer<MiracleActivationRequest>(entity))
                            {
                                ecb.AddBuffer<MiracleActivationRequest>(entity);
                            }

                            // Determine dispense mode
                            byte dispenseMode = isSustained ? (byte)DispenseMode.Sustained : (byte)DispenseMode.Throw;

                            // Create request
                            var request = new MiracleActivationRequest
                            {
                                Id = solution.SelectedMiracleId,
                                TargetPoint = solution.TargetPoint,
                                TargetRadius = solution.Radius,
                                DispenseMode = dispenseMode,
                                PlayerIndex = 0 // TODO: Multiplayer support
                            };

                            // Append request to buffer
                            ecb.AppendToBuffer(entity, request);
                        }
                    }
                }

                // Update tracking state
                tracking.PreviousIsActivating = runtime.IsActivating;
                tracking.PreviousIsSustained = runtime.IsSustained;
                ecb.SetComponent(entity, tracking);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}


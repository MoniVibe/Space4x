using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using PureDOTS.Runtime.Components;
using UnityEngine;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Manages the preview-based rewind control system.
    /// Handles phase transitions, world freezing, and preview tick scrubbing.
    /// Processes TimeControlCommand entities created by TimeAPI.
    /// </summary>
    [UpdateInGroup(typeof(TimeSystemGroup))]
    public partial struct RewindControlSystem : ISystem
    {
        private float _previewAccumulator;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<RewindControlState>();
            state.RequireForUpdate<TickTimeState>(); // Required for HandleBeginPreview
        }

        [BurstDiscard] // Contains Debug.Log calls
        public void OnUpdate(ref SystemState state)
        {
            var controlRW = SystemAPI.GetSingletonRW<RewindControlState>();
            var control = controlRW.ValueRO;
            var em = state.EntityManager;

            // Debug: show we're alive (only log occasionally to reduce spam)
#if UNITY_EDITOR && DEBUG_REWIND
            if (UnityEngine.Time.frameCount % 60 == 0) // Every 60 frames
            {
                Debug.Log($"[RewindControlSystem] OnUpdate Phase={control.Phase}");
            }
#endif

            // Create an ECB on the stack for this frame
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            int commandCount = 0;
            // Process commands
            foreach (var (cmdBuffer, entity) in SystemAPI
                     .Query<DynamicBuffer<TimeControlCommand>>()
                     .WithEntityAccess())
            {
                // The TimeControlCommand buffer is permanent on the RewindState singleton entity (core singleton).
                // Only process/destroy ephemeral command entities created by UI/dev tools.
                bool isCoreCommandEntity = state.EntityManager.HasComponent<RewindState>(entity)
                    || state.EntityManager.HasComponent<TimeState>(entity)
                    || state.EntityManager.HasComponent<TickTimeState>(entity);

                if (isCoreCommandEntity)
                {
                    // IMPORTANT: Never destroy or clear the singleton command buffer here.
                    // Other systems (e.g. RewindCoordinatorSystem / TimeScaleCommandSystem) own processing and clearing.
                    continue;
                }

                for (int i = 0; i < cmdBuffer.Length; i++)
                {
                    var cmd = cmdBuffer[i];
                    commandCount++;

#if UNITY_EDITOR
                    UnityEngine.Debug.Log($"[RewindControlSystem] Got command {cmd.Type} from entity {entity.Index} (FloatParam={cmd.FloatParam})");
#endif

                    switch (cmd.Type)
                    {
                        case TimeControlCommandType.BeginPreviewRewind:
                            HandleBeginPreview(ref state, ref controlRW.ValueRW, cmd.FloatParam, ref ecb);
                            break;

                        case TimeControlCommandType.UpdatePreviewRewindSpeed:
                            controlRW.ValueRW.ScrubSpeed = cmd.FloatParam;
#if UNITY_EDITOR
                            UnityEngine.Debug.Log($"[RewindControlSystem] UpdatePreviewRewindSpeed: {controlRW.ValueRW.ScrubSpeed:F2}x");
#endif
                            break;

                        case TimeControlCommandType.EndScrubPreview:
                            HandleEndScrub(ref controlRW.ValueRW);
                            break;

                        case TimeControlCommandType.CommitRewindFromPreview:
                            HandleCommitRequest(ref controlRW.ValueRW);
                            break;

                        case TimeControlCommandType.CancelRewindPreview:
                            HandleCancel(ref controlRW.ValueRW, ref ecb);
                            break;
                    }
                }

                // Defer entity destruction until after iteration.
                // These ephemeral entities are only used as a transport for one-shot commands.
                ecb.DestroyEntity(entity);
            }

            // Apply all structural changes after iteration
            var phaseAfterCommands = controlRW.ValueRO.Phase;

#if UNITY_EDITOR
            if (commandCount > 0)
            {
                UnityEngine.Debug.Log($"[RewindControlSystem] Processed {commandCount} command(s), Phase now={phaseAfterCommands}");
            }
#endif

            if (phaseAfterCommands == RewindPhase.ScrubbingPreview)
            {
                AdvancePreviewTick(ref state, ref controlRW.ValueRW);
            }
            else
            {
                _previewAccumulator = 0f;
            }

            ecb.Playback(em);
            ecb.Dispose();
        }

        // Keep these NON-static so SystemAPI is allowed inside
        [BurstDiscard]
        private void HandleBeginPreview(ref SystemState state, ref RewindControlState control, float scrubSpeed, ref EntityCommandBuffer ecb)
        {
            // Set phase, ticks, and freeze timescale
            var tickState = SystemAPI.GetSingleton<TickTimeState>();
            control.Phase = RewindPhase.ScrubbingPreview;
            control.PresentTickAtStart = (int)tickState.Tick;
            control.PreviewTick = (int)tickState.Tick;
            control.ScrubSpeed = scrubSpeed;

#if UNITY_EDITOR
            UnityEngine.Debug.Log($"[RewindControlSystem] BeginPreview -> Phase={control.Phase} Present={control.PresentTickAtStart} Preview={control.PreviewTick} Speed={control.ScrubSpeed:F2}x");
#endif

            EnqueueTimeScaleCommand(ref ecb, 0f); // freeze
        }

        [BurstDiscard]
        private void AdvancePreviewTick(ref SystemState state, ref RewindControlState control)
        {
            if (math.abs(control.ScrubSpeed) <= 0f)
            {
                _previewAccumulator = 0f;
                return;
            }

            var tickState = SystemAPI.GetSingleton<TickTimeState>();
            float ticksPerSecond = tickState.FixedDeltaTime > 0f
                ? 1f / tickState.FixedDeltaTime
                : HistorySettingsDefaults.DefaultTicksPerSecond;
            float deltaTime = SystemAPI.Time.DeltaTime;

            _previewAccumulator += deltaTime * ticksPerSecond * math.abs(control.ScrubSpeed);
            int step = (int)math.floor(_previewAccumulator);
            if (step <= 0)
            {
                return;
            }

            _previewAccumulator -= step;

            int maxTick = math.max(0, control.PresentTickAtStart);
            int minTick = 0;
            if (SystemAPI.TryGetSingleton<HistorySettings>(out var settings) && settings.GlobalHorizonTicks > 0)
            {
                int horizon = (int)settings.GlobalHorizonTicks;
                minTick = math.max(0, maxTick - horizon);
            }

            int direction = control.ScrubSpeed >= 0f ? -1 : 1;
            int nextTick = math.clamp(control.PreviewTick + direction * step, minTick, maxTick);
            control.PreviewTick = nextTick;
        }

        [BurstDiscard]
        private void HandleEndScrub(ref RewindControlState control)
        {
            if (control.Phase == RewindPhase.ScrubbingPreview)
            {
                control.Phase = RewindPhase.FrozenPreview;
#if UNITY_EDITOR
                UnityEngine.Debug.Log($"[RewindControlSystem] EndScrub -> FrozenPreview at PreviewTick={control.PreviewTick}");
#endif
            }
        }

        [BurstDiscard]
        private void HandleCommitRequest(ref RewindControlState control)
        {
            if (control.Phase == RewindPhase.ScrubbingPreview ||
                control.Phase == RewindPhase.FrozenPreview)
            {
                control.Phase = RewindPhase.CommitPlayback;
#if UNITY_EDITOR
                UnityEngine.Debug.Log($"[RewindControlSystem] Commit request -> CommitPlayback at PreviewTick={control.PreviewTick}");
#endif
            }
        }

        [BurstDiscard]
        private void HandleCancel(ref RewindControlState control, ref EntityCommandBuffer ecb)
        {
            if (control.Phase == RewindPhase.ScrubbingPreview ||
                control.Phase == RewindPhase.FrozenPreview)
            {
                control.Phase = RewindPhase.Inactive;
                EnqueueTimeScaleCommand(ref ecb, 1f); // unfreeze

#if UNITY_EDITOR
                UnityEngine.Debug.Log($"[RewindControlSystem] Cancel -> Inactive + speed 1.0");
#endif
            }
        }

        [BurstDiscard]
        private void EnqueueTimeScaleCommand(ref EntityCommandBuffer ecb, float targetScale)
        {
            var entity = ecb.CreateEntity();
            var buffer = ecb.AddBuffer<TimeControlCommand>(entity);
            buffer.Add(new TimeControlCommand
            {
                Type = TimeControlCommandType.SetSpeed,  // this MUST be the enum your TimeScaleCommandSystem understands
                FloatParam = targetScale,
                Scope = TimeControlScope.Global,
                Source = TimeControlSource.System,
                PlayerId = 0,
                Priority = 100
            });

#if UNITY_EDITOR
            UnityEngine.Debug.Log($"[RewindControlSystem] Enqueued SetSpeed {targetScale:F2}x");
#endif
        }
    }
}

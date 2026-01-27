using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using PureDOTS.Runtime.Components;
using UnityEngine;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Handles committing rewind from preview phase.
    /// When CommitPlayback phase is active, this system applies the rewind
    /// by restoring entity state from history at PreviewTick.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(TimeSystemGroup))]
    [UpdateAfter(typeof(RewindControlSystem))]
    [UpdateBefore(typeof(TimeTickSystem))]
    public partial struct RewindCommitSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<RewindControlState>();
            state.RequireForUpdate<TickTimeState>(); // Already has guard, keeping for clarity
        }

        [BurstDiscard] // Contains Debug.Log calls
        public void OnUpdate(ref SystemState state)
        {
            var controlStateHandle = SystemAPI.GetSingletonRW<RewindControlState>();
            ref var controlState = ref controlStateHandle.ValueRW;

            // Only process during CommitPlayback phase
            if (controlState.Phase != RewindPhase.CommitPlayback)
            {
                return;
            }

            var tickTimeHandle = SystemAPI.GetSingletonRW<TickTimeState>();
            ref var tickTimeState = ref tickTimeHandle.ValueRW;

            // Apply rewind by restoring entity state from history at PreviewTick
            uint targetTick = (uint)math.max(0, controlState.PreviewTick);
            
            UnityEngine.Debug.Log($"[RewindCommitSystem] Committing rewind to tick {targetTick} (PreviewTick={controlState.PreviewTick}, PresentTickAtStart={controlState.PresentTickAtStart})");

            // Restore LocalTransform from ComponentHistory
            int restoredCount = RestoreLocalTransforms(ref state, targetTick);

            UnityEngine.Debug.Log($"[RewindCommitSystem] Restored {restoredCount} LocalTransform(s) at tick {targetTick}");

            // Update TickTimeState to targetTick
            tickTimeState.Tick = targetTick;
            tickTimeState.TargetTick = targetTick;

            // Restore normal timescale (send command to set speed back to 1x)
            RestoreNormalTimescale(ref state);

            // Transition back to Inactive phase
            controlState.Phase = RewindPhase.Inactive;

            UnityEngine.Debug.Log($"[RewindCommitSystem] Rewind committed: restored {restoredCount} LocalTransform(s), world now at tick {targetTick}, phase reset to Inactive");
        }

        [BurstDiscard] // Contains Debug.Log calls
        private int RestoreLocalTransforms(ref SystemState state, uint targetTick)
        {
            int restoredCount = 0;
            int entitiesWithHistory = 0;
            int entitiesWithoutSamples = 0;

            // Query entities with ComponentHistory<LocalTransform> and RewindableTag
            foreach (var (transform, historyBuffer, entity) in SystemAPI
                .Query<RefRW<LocalTransform>, DynamicBuffer<ComponentHistory<LocalTransform>>>()
                .WithAll<RewindableTag>()
                .WithEntityAccess())
            {
                entitiesWithHistory++;
                
                // Find nearest sample at or before target tick
                if (TryGetNearestSample(historyBuffer, targetTick, out var sample))
                {
                    // Restore transform from history sample
                    var oldPosition = transform.ValueRO.Position;
                    transform.ValueRW = sample.Value;
                    restoredCount++;
                    
                    // Log first few restorations for debugging
                    if (restoredCount <= 5)
                    {
                        UnityEngine.Debug.Log($"[RewindCommitSystem] Entity {entity.Index}: restored from tick {sample.Tick} " +
                            $"(old pos={oldPosition}, new pos={sample.Value.Position})");
                    }
                }
                else
                {
                    entitiesWithoutSamples++;
                }
            }

            if (entitiesWithHistory > 0)
            {
                UnityEngine.Debug.Log($"[RewindCommitSystem] History query: {entitiesWithHistory} entities with history, " +
                    $"{restoredCount} restored, {entitiesWithoutSamples} had no samples at tick {targetTick}");
            }

            return restoredCount;
        }

        [BurstCompile]
        private static bool TryGetNearestSample<T>(DynamicBuffer<ComponentHistory<T>> buffer, uint targetTick,
            out ComponentHistory<T> sample) where T : unmanaged
        {
            sample = default;

            if (buffer.Length == 0)
            {
                return false;
            }

            // Binary search for nearest sample <= targetTick
            int left = 0;
            int right = buffer.Length - 1;
            int bestIndex = -1;

            while (left <= right)
            {
                int mid = left + (right - left) / 2;
                var midSample = buffer[mid];

                if (midSample.Tick <= targetTick)
                {
                    bestIndex = mid;
                    left = mid + 1;
                }
                else
                {
                    right = mid - 1;
                }
            }

            if (bestIndex >= 0)
            {
                sample = buffer[bestIndex];
                return true;
            }

            // If no sample is at or before targetTick, use the earliest available
            if (buffer.Length > 0)
            {
                sample = buffer[0];
                return true;
            }

            return false;
        }

        [BurstDiscard]
        private void RestoreNormalTimescale(ref SystemState state)
        {
            // Find command entity (RewindState or RewindControlState singleton)
            Entity commandEntity = Entity.Null;
            if (SystemAPI.TryGetSingletonEntity<RewindState>(out var rewindEntity))
            {
                commandEntity = rewindEntity;
            }
            else if (SystemAPI.TryGetSingletonEntity<RewindControlState>(out var controlEntity))
            {
                commandEntity = controlEntity;
            }

            if (commandEntity == Entity.Null)
            {
                return;
            }

            // Get or create command buffer
            if (!state.EntityManager.HasBuffer<TimeControlCommand>(commandEntity))
            {
                state.EntityManager.AddBuffer<TimeControlCommand>(commandEntity);
            }

            var commands = state.EntityManager.GetBuffer<TimeControlCommand>(commandEntity);
            
            // Add command to restore normal timescale (1x)
            commands.Add(new TimeControlCommand
            {
                Type = TimeControlCommandType.SetSpeed,
                FloatParam = 1.0f,
                Scope = TimeControlScope.Global,
                Source = TimeControlSource.System,
                PlayerId = 0,
                Priority = 100
            });
        }
    }
}


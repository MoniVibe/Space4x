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
    /// Plays back history samples during Playback or CatchUp mode.
    /// Restores entity state from recorded history buffers.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(HistorySystemGroup))]
    // Removed invalid UpdateBefore: TimeHistoryRecordSystem runs in WarmPathSystemGroup.
    public partial struct TimeHistoryPlaybackSystem : ISystem
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
            // Get feature flags once for the entire method
            if (!SystemAPI.TryGetSingleton<TimeSystemFeatureFlags>(out var flags))
            {
                flags = default;
            }

            // Guard: Do not mutate history/snapshots in multiplayer modes
            if (flags.IsMultiplayerSession)
            {
                // For now, do not mutate history or snapshots in multiplayer modes.
                // When we implement MP, we can selectively allow modes like MP_SnapshotsOnly.
                return;
            }

            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState))
            {
                return;
            }

            // Only playback during Playback or CatchUp mode
            if (rewindState.Mode != RewindMode.Playback && rewindState.Mode != RewindMode.CatchUp)
            {
                return;
            }

            // Check feature flags
            // TODO: In multiplayer server mode, per-player history playback will be implemented
            // For now, continue with single-player behavior
            if (flags.SimulationMode == TimeSimulationMode.MultiplayerServer)
            {
                // TODO: Implement per-player history playback for MP
            }

            // Check if global rewind is enabled
            if (!flags.EnableGlobalRewind && rewindState.Mode == RewindMode.Playback)
            {
                // Log warning but continue in SP mode for now
                // In MP, this would be disabled
            }

            uint targetTick = timeState.Tick;

            // Playback transform history
            int restoredCount = PlaybackTransformHistory(ref state, targetTick);
            
            // Log restored samples count (only log occasionally to avoid spam)
            if (restoredCount > 0 && targetTick % 10 == 0) // Log every 10 ticks
            {
                LogRestoredSamples(restoredCount, targetTick);
            }
        }

        [BurstCompile]
        private int PlaybackTransformHistory(ref SystemState state, uint targetTick)
        {
            int restoredCount = 0;
            
            foreach (var (profile, transform, historyBuffer) in SystemAPI
                .Query<RefRO<HistoryProfile>, RefRW<LocalTransform>, DynamicBuffer<ComponentHistory<LocalTransform>>>()
                .WithAll<RewindableTag>())
            {
                // Check if transform playback is enabled
                if ((profile.ValueRO.RecordFlags & HistoryRecordFlags.Transform) == 0)
                {
                    continue;
                }

                // Find nearest sample at or before target tick
                if (!TryGetNearestSample(historyBuffer, targetTick, out var sample))
                {
                    continue;
                }

                // Apply the sample
                transform.ValueRW = sample.Value;
                restoredCount++;
            }
            
            return restoredCount;
        }
        
        [BurstDiscard]
        private static void LogRestoredSamples(int count, uint targetTick)
        {
            UnityEngine.Debug.Log($"[Rewind] Restored {count} component samples for tick={targetTick}");
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

        /// <summary>
        /// Interpolates between two samples based on target tick.
        /// Useful for smooth playback of transform data.
        /// Burst-friendly: uses ref outResult instead of returning struct.
        /// </summary>
        public static void InterpolateTransform(in ComponentHistory<LocalTransform> before, 
            in ComponentHistory<LocalTransform> after, uint targetTick, ref LocalTransform outResult)
        {
            if (before.Tick == after.Tick || targetTick <= before.Tick)
            {
                outResult = before.Value;
                return;
            }

            if (targetTick >= after.Tick)
            {
                outResult = after.Value;
                return;
            }

            float t = (float)(targetTick - before.Tick) / (after.Tick - before.Tick);
            
            outResult = new LocalTransform
            {
                Position = math.lerp(before.Value.Position, after.Value.Position, t),
                Rotation = math.slerp(before.Value.Rotation, after.Value.Rotation, t),
                Scale = math.lerp(before.Value.Scale, after.Value.Scale, t)
            };
        }

        /// <summary>
        /// Gets interpolated sample from history buffer.
        /// Burst-friendly: buffer passed by ref, result via ref parameter.
        /// </summary>
        public static bool TryGetInterpolatedSample(ref DynamicBuffer<ComponentHistory<LocalTransform>> historyBuffer, 
            uint targetTick, ref LocalTransform result)
        {
            if (historyBuffer.Length == 0)
            {
                return false;
            }

            // Find surrounding samples
            int beforeIndex = -1;
            int afterIndex = -1;

            for (int i = 0; i < historyBuffer.Length; i++)
            {
                if (historyBuffer[i].Tick <= targetTick)
                {
                    beforeIndex = i;
                }
                else if (afterIndex == -1)
                {
                    afterIndex = i;
                    break;
                }
            }

            if (beforeIndex < 0 && afterIndex < 0)
            {
                return false;
            }

            if (beforeIndex < 0)
            {
                result = historyBuffer[afterIndex].Value;
                return true;
            }

            if (afterIndex < 0)
            {
                result = historyBuffer[beforeIndex].Value;
                return true;
            }

            var before = historyBuffer[beforeIndex];
            var after = historyBuffer[afterIndex];
            InterpolateTransform(before, after, targetTick, ref result);
            return true;
        }
    }
}

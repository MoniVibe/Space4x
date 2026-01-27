using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using PureDOTS.Runtime.Components;
using PureDOTS.Systems;

namespace PureDOTS.Systems.Time.Templates
{
    /// <summary>
    /// Example domain: Position + HP rewind.
    /// Demonstrates the rewind lane template pattern for entities with position and health.
    /// Copy this pattern for other domains (combat, villages, fire, ships, etc.).
    /// </summary>

    // Tag component marking entities that participate in PosHp rewinds
    public struct PosHpRewindTag : IComponentData { }

    // Snapshot type - only fields needed to reconstruct gameplay
    public struct PosHpSnapshot
    {
        public float3 Position;
        public float3 Velocity;
        public float HitPoints;
    }

    // History element - stored in DynamicBuffer on entities
    // Note: Tick must be the first field for RewindUtil.TrimHistory() to work correctly
    public struct PosHpHistoryElement : IBufferElementData
    {
        public uint Tick;
        public PosHpSnapshot Snapshot;
    }

    // Simple Health component for example
    public struct Health : IComponentData
    {
        public float Current;
        public float Max;
    }

    /// <summary>
    /// Record system template - captures snapshots based on track config.
    /// Copy this pattern and replace:
    /// - PosHpRewindTag → YourDomainRewindTag
    /// - PosHpSnapshot → YourDomainSnapshot
    /// - PosHpHistoryElement → YourDomainHistoryElement
    /// - Query components (LocalTransform, Health) → Your domain components
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct PosHpHistoryRecordSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<TickTimeState>();
            state.RequireForUpdate<RewindConfigSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState))
                return;
            if (rewindState.Mode == RewindMode.Playback)
                return; // Don't advance history when scrubbing

            var time = SystemAPI.GetSingleton<TickTimeState>();
            var configSingleton = SystemAPI.GetSingleton<RewindConfigSingleton>();
            ref var config = ref configSingleton.Config.Value;

            // Track config - in real usage, this would come from domain config or be parameterized
            var trackId = new RewindTrackId { Value = 1 }; // Example: Combat track
            if (!RewindUtil.TryGetTrackDef(ref config, trackId, out var trackDef))
                return; // Track not configured

            if (!RewindUtil.ShouldRecordTrack(ref config, trackId, time.Tick))
                return;

            uint tick = time.Tick;

            foreach (var (tag, transform, hp, history, scope) in SystemAPI.Query<
                         RefRO<PosHpRewindTag>,
                         RefRO<LocalTransform>,
                         RefRO<Health>,
                         DynamicBuffer<PosHpHistoryElement>,
                         RefRO<RewindScope>>())
            {
                // Filter by track
                if (scope.ValueRO.Track.Value != trackId.Value)
                    continue;

                // Spatial filtering (if configured)
                if (trackDef.Spatial && scope.ValueRO.Zone != Entity.Null)
                {
                    if (!SystemAPI.Exists(scope.ValueRO.Zone))
                        continue;

                    var zoneTransform = SystemAPI.GetComponent<LocalTransform>(scope.ValueRO.Zone);
                    var zone = SystemAPI.GetComponent<RewindZone>(scope.ValueRO.Zone);
                    float3 delta = transform.ValueRO.Position - zoneTransform.Position;
                    float radiusSq = zone.Radius * zone.Radius;
                    if (math.lengthsq(delta) > radiusSq)
                        continue;
                }

                // Capture snapshot
                var snapshot = new PosHpSnapshot
                {
                    Position = transform.ValueRO.Position,
                    Velocity = transform.ValueRO.Forward(), // Or separate velocity component
                    HitPoints = hp.ValueRO.Current
                };

                history.Add(new PosHpHistoryElement
                {
                    Tick = tick,
                    Snapshot = snapshot
                });

                // Trim old snapshots
                RewindUtil.TrimHistory(history, tick, trackDef.WindowTicks);
            }
        }
    }

    /// <summary>
    /// Playback system template - restores state from history during rewind.
    /// Copy this pattern and replace snapshot/component types as in RecordSystem.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(PosHpHistoryRecordSystem))]
    public partial struct PosHpHistoryPlaybackSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<RewindLegacyState>();
            state.RequireForUpdate<RewindConfigSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState))
                return;
            if (rewindState.Mode != RewindMode.Playback)
                return;

            var configSingleton = SystemAPI.GetSingleton<RewindConfigSingleton>();
            ref var config = ref configSingleton.Config.Value;

            var legacy = SystemAPI.GetSingleton<RewindLegacyState>();
            var activeTrack = legacy.ActiveTrack;
            if (!RewindUtil.TryGetTrackDef(ref config, activeTrack, out var trackDef))
                return;

            uint targetTick = legacy.PlaybackTick;

            foreach (var (tag, transform, hp, history, scope) in SystemAPI.Query<
                         RefRO<PosHpRewindTag>,
                         RefRW<LocalTransform>,
                         RefRW<Health>,
                         DynamicBuffer<PosHpHistoryElement>,
                         RefRO<RewindScope>>())
            {
                if (scope.ValueRO.Track.Value != activeTrack.Value)
                    continue;

                // Optional: spatial filtering as in record system
                if (trackDef.Spatial && scope.ValueRO.Zone != Entity.Null)
                {
                    if (!SystemAPI.Exists(scope.ValueRO.Zone))
                        continue;

                    var zoneTransform = SystemAPI.GetComponent<LocalTransform>(scope.ValueRO.Zone);
                    var zone = SystemAPI.GetComponent<RewindZone>(scope.ValueRO.Zone);
                    float3 delta = transform.ValueRO.Position - zoneTransform.Position;
                    float radiusSq = zone.Radius * zone.Radius;
                    if (math.lengthsq(delta) > radiusSq)
                        continue;
                }

                if (history.Length == 0)
                    continue;

                // Find latest snapshot <= targetTick (linear search - optimize with binary search for large buffers)
                int bestIndex = -1;
                uint bestTick = 0;
                for (int i = history.Length - 1; i >= 0; i--)
                {
                    if (history[i].Tick <= targetTick && history[i].Tick > bestTick)
                    {
                        bestIndex = i;
                        bestTick = history[i].Tick;
                    }
                }

                if (bestIndex < 0)
                    continue;

                var snap = history[bestIndex].Snapshot;

                // Restore
                var t = transform.ValueRW;
                t.Position = snap.Position;
                transform.ValueRW = t;

                var h = hp.ValueRW;
                h.Current = snap.HitPoints;
                hp.ValueRW = h;
            }
        }
    }
}

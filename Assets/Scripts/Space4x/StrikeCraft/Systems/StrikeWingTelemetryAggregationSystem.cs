using Space4X.Registry;
using Space4X.Telemetry;
using PureDOTS.Runtime.Telemetry;
using PureDOTS.Runtime.Scenarios;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.StrikeCraft
{
    /// <summary>
    /// Aggregates wing cohesion and escort coverage metrics for headless analysis.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(StrikeCraftTelemetrySystem))]
    public partial struct StrikeWingTelemetryAggregationSystem : ISystem
    {
        private ComponentLookup<LocalTransform> _transformLookup;
        private static readonly FixedString64Bytes SourceId = new FixedString64Bytes("Space4X.StrikeCraft");
        private static readonly FixedString64Bytes EventFormation = new FixedString64Bytes("FormationCohesion");
        private static readonly FixedString64Bytes EventEscort = new FixedString64Bytes("EscortCoverage");

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<StrikeCraftProfile>();
            state.RequireForUpdate<TelemetryStream>();
            state.RequireForUpdate<TelemetryStreamSingleton>();
            state.RequireForUpdate<ScenarioTick>();
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!TryGetTelemetryEventBuffer(ref state, out var eventBuffer))
            {
                return;
            }

            _transformLookup.Update(ref state);
            var tick = SystemAPI.GetSingleton<ScenarioTick>().Value;

            var craftQuery = SystemAPI.QueryBuilder().WithAll<StrikeCraftProfile>().Build();
            var craftCount = craftQuery.CalculateEntityCount();
            craftQuery.Dispose();

            if (craftCount == 0)
            {
                return;
            }

            var wingStats = new NativeHashMap<Entity, WingStats>(craftCount, Allocator.Temp);
            var escortStats = new NativeHashMap<Entity, EscortStats>(craftCount, Allocator.Temp);

            try
            {
                AccumulateWingStats(ref state, ref wingStats, ref escortStats);
                FlushWingEvents(ref wingStats, eventBuffer, tick);
                FlushEscortEvents(ref escortStats, eventBuffer, tick);
            }
            finally
            {
                if (wingStats.IsCreated) wingStats.Dispose();
                if (escortStats.IsCreated) escortStats.Dispose();
            }
        }

        private bool TryGetTelemetryEventBuffer(ref SystemState state, out DynamicBuffer<TelemetryEvent> buffer)
        {
            buffer = default;
            if (!SystemAPI.TryGetSingleton<TelemetryStreamSingleton>(out var telemetryRef))
            {
                return false;
            }

            if (telemetryRef.Entity == Entity.Null || !state.EntityManager.HasBuffer<TelemetryEvent>(telemetryRef.Entity))
            {
                return false;
            }

            buffer = state.EntityManager.GetBuffer<TelemetryEvent>(telemetryRef.Entity);
            return true;
        }

        private void AccumulateWingStats(ref SystemState state, ref NativeHashMap<Entity, WingStats> wingStats, ref NativeHashMap<Entity, EscortStats> escortStats)
        {
            foreach (var (profile, config, transform, entity) in SystemAPI
                         .Query<RefRO<StrikeCraftProfile>, RefRO<AttackRunConfig>, RefRO<LocalTransform>>()
                         .WithEntityAccess())
            {
                var leader = profile.ValueRO.WingLeader;
                if (leader != Entity.Null &&
                    leader != entity &&
                    _transformLookup.HasComponent(leader) &&
                    _transformLookup.HasComponent(entity))
                {
                    var leaderTransform = _transformLookup[leader];
                    var distance = math.distance(transform.ValueRO.Position, leaderTransform.Position);
                    var stats = wingStats.TryGetValue(leader, out var existingWing) ? existingWing : default;
                    stats.MemberCount++;
                    stats.DistanceSum += distance;
                    stats.MaxDistance = math.max(stats.MaxDistance, distance);
                    if (distance > config.ValueRO.FormationSpacing * 1.5f)
                    {
                        stats.Stragglers++;
                    }
                    wingStats[leader] = stats;
                }

                var carrier = profile.ValueRO.Carrier;
                if (carrier != Entity.Null && _transformLookup.HasComponent(carrier) && _transformLookup.HasComponent(entity))
                {
                    var carrierTransform = _transformLookup[carrier];
                    var distanceToCarrier = math.distance(transform.ValueRO.Position, carrierTransform.Position);
                    var stats = escortStats.TryGetValue(carrier, out var existingEscort) ? existingEscort : default;
                    if (profile.ValueRO.Role == StrikeCraftRole.Interceptor && distanceToCarrier <= 600f)
                    {
                        stats.DefendersNearby++;
                    }
                    escortStats[carrier] = stats;
                }

                var target = profile.ValueRO.Target;
                if (target != Entity.Null && _transformLookup.HasComponent(target) && _transformLookup.HasComponent(entity))
                {
                    if (profile.ValueRO.Phase == AttackRunPhase.Approach ||
                        profile.ValueRO.Phase == AttackRunPhase.Execute ||
                        profile.ValueRO.Phase == AttackRunPhase.Disengage)
                    {
                        var stats = escortStats.TryGetValue(target, out var threatStats) ? threatStats : default;
                        stats.AttackersNearby++;
                        escortStats[target] = stats;
                    }
                }
            }
        }

        private static void FlushWingEvents(ref NativeHashMap<Entity, WingStats> wingStats, DynamicBuffer<TelemetryEvent> eventBuffer, uint tick)
        {
            using var wingPairs = wingStats.GetKeyValueArrays(Allocator.Temp);
            for (int i = 0; i < wingPairs.Length; i++)
            {
                var leader = wingPairs.Keys[i];
                var stats = wingPairs.Values[i];
                if (stats.MemberCount == 0)
                {
                    continue;
                }

                var writer = new TelemetryJsonWriter();
                writer.AddEntity("wingLeader", leader);
                writer.AddInt("memberCount", stats.MemberCount);
                writer.AddFloat("meanDistToLeader", stats.DistanceSum / stats.MemberCount);
                writer.AddFloat("maxDist", stats.MaxDistance);
                writer.AddInt("stragglers", stats.Stragglers);
                var payload = writer.Build();
                eventBuffer.AddEvent(EventFormation, tick, SourceId, payload);
            }
        }

        private static void FlushEscortEvents(ref NativeHashMap<Entity, EscortStats> escortStats, DynamicBuffer<TelemetryEvent> eventBuffer, uint tick)
        {
            using var escortPairs = escortStats.GetKeyValueArrays(Allocator.Temp);
            for (int i = 0; i < escortPairs.Length; i++)
            {
                var protectedEntity = escortPairs.Keys[i];
                var stats = escortPairs.Values[i];
                if (stats.DefendersNearby == 0 && stats.AttackersNearby == 0)
                {
                    continue;
                }

                var writer = new TelemetryJsonWriter();
                writer.AddEntity("protected", protectedEntity);
                writer.AddInt("defendersNearby", stats.DefendersNearby);
                writer.AddInt("attackersNearby", stats.AttackersNearby);
                var payload = writer.Build();
                eventBuffer.AddEvent(EventEscort, tick, SourceId, payload);
            }
        }

        private struct WingStats
        {
            public int MemberCount;
            public float DistanceSum;
            public float MaxDistance;
            public int Stragglers;
        }

        private struct EscortStats
        {
            public int DefendersNearby;
            public int AttackersNearby;
        }
    }
}

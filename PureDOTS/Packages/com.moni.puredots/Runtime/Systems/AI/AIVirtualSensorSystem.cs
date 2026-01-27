using PureDOTS.Runtime.AI;
using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.AI
{
    /// <summary>
    /// Populates virtual sensor readings for internal villager needs (Hunger, Energy, Morale).
    /// These readings are inserted at fixed indices (0, 1, 2) so utility curves can reference them.
    /// Prefers VillagerNeedState when present, falls back to legacy VillagerNeeds + VillagerMood.
    /// Runs after AISensorUpdateSystem to inject virtual readings before the scoring stage.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(AISystemGroup))]
    [UpdateAfter(typeof(AISensorUpdateSystem))]
    [UpdateBefore(typeof(AIUtilityScoringSystem))]
    public partial struct AIVirtualSensorSystem : ISystem
    {
        private EntityQuery _needStateQuery;
        private EntityQuery _legacyQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _needStateQuery = SystemAPI.QueryBuilder()
                .WithAll<VillagerNeedState, AISensorReading>()
                .WithNone<PlaybackGuardTag>()
                .Build();

            _legacyQuery = SystemAPI.QueryBuilder()
                .WithAll<VillagerNeeds, VillagerMood, AISensorReading>()
                .WithNone<VillagerNeedState, PlaybackGuardTag>()
                .Build();

            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<MindCadenceSettings>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            var cadenceSettings = SystemAPI.GetSingleton<MindCadenceSettings>();
            if (!CadenceGate.ShouldRun(timeState.Tick, cadenceSettings.SensorCadenceTicks))
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            if (!_needStateQuery.IsEmpty)
            {
                var job = new PopulateNeedStateVirtualSensorsJob();
                state.Dependency = job.ScheduleParallel(_needStateQuery, state.Dependency);
            }

            if (!_legacyQuery.IsEmpty)
            {
                var job = new PopulateLegacyVirtualSensorsJob();
                state.Dependency = job.ScheduleParallel(_legacyQuery, state.Dependency);
            }
        }

        [BurstCompile]
        public partial struct PopulateNeedStateVirtualSensorsJob : IJobEntity
        {
            public void Execute(
                Entity entity,
                in VillagerNeedState needs,
                DynamicBuffer<AISensorReading> readings)
            {
                // Virtual sensor indices:
                // 0 = Hunger (urgency)
                // 1 = Energy (rest urgency)
                // 2 = Morale (max of faith/social urgency)

                var hungerScore = math.saturate(needs.HungerUrgency);
                var energyScore = math.saturate(needs.RestUrgency);
                var moraleScore = math.saturate(math.max(needs.FaithUrgency, needs.SocialUrgency));

                UpsertVirtualReadings(entity, readings, hungerScore, energyScore, moraleScore);
            }
        }

        [BurstCompile]
        public partial struct PopulateLegacyVirtualSensorsJob : IJobEntity
        {
            public void Execute(
                Entity entity,
                in VillagerNeeds needs,
                in VillagerMood mood,
                DynamicBuffer<AISensorReading> readings)
            {
                // Virtual sensor indices:
                // 0 = Hunger (inverted: 1.0 - Food/100, so high score = high need)
                // 1 = Energy (inverted: 1.0 - Energy/100, so high score = high need)
                // 2 = Morale (inverted: 1.0 - Morale/100, so high score = low morale = high need)

                var hungerScore = 1f - math.saturate(needs.HungerFloat / 100f);
                var energyScore = 1f - math.saturate(needs.EnergyFloat / 100f);
                var moraleScore = 1f - math.saturate(mood.Mood / 100f);

                UpsertVirtualReadings(entity, readings, hungerScore, energyScore, moraleScore);
            }
        }

        private static void UpsertVirtualReadings(
            Entity entity,
            DynamicBuffer<AISensorReading> readings,
            float hungerScore,
            float energyScore,
            float moraleScore)
        {
            var hasVirtualPrefix = readings.Length >= 3 &&
                                   IsVirtualReading(readings[0], entity) &&
                                   IsVirtualReading(readings[1], entity) &&
                                   IsVirtualReading(readings[2], entity);

            if (!hasVirtualPrefix)
            {
                var existingCount = readings.Length;
                readings.ResizeUninitialized(existingCount + 3);
                for (int i = existingCount - 1; i >= 0; i--)
                {
                    readings[i + 3] = readings[i];
                }
            }

            readings[0] = CreateVirtualReading(entity, hungerScore);
            readings[1] = CreateVirtualReading(entity, energyScore);
            readings[2] = CreateVirtualReading(entity, moraleScore);
        }

        private static bool IsVirtualReading(in AISensorReading reading, Entity entity)
        {
            return reading.Category == AISensorCategory.None && reading.Target == entity;
        }

        private static AISensorReading CreateVirtualReading(Entity entity, float score)
        {
            return new AISensorReading
            {
                Target = entity,
                DistanceSq = 0f,
                NormalizedScore = score,
                CellId = -1,
                SpatialVersion = 0,
                Category = AISensorCategory.None
            };
        }
    }
}

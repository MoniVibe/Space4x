using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Updates villager availability, discipline, mood, and productivity after needs processing.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(VillagerSystemGroup))]
    [UpdateAfter(typeof(VillagerNeedsSystem))]
    public partial struct VillagerStatusSystem : ISystem
    {
        private EntityQuery _villagerQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _villagerQuery = SystemAPI.QueryBuilder()
                .WithAll<VillagerNeeds, VillagerJob, VillagerAIState, VillagerDisciplineState, VillagerMood, VillagerAvailability>()
                .WithNone<VillagerDeadTag, PlaybackGuardTag>()
                .Build();

            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate(_villagerQuery);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            var rewindState = SystemAPI.GetSingleton<RewindState>();
            if (rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var job = new UpdateVillagerStatusJob
            {
                DeltaTime = timeState.FixedDeltaTime,
                CurrentTick = timeState.Tick
            };

            state.Dependency = job.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        public partial struct UpdateVillagerStatusJob : IJobEntity
        {
            public float DeltaTime;
            public uint CurrentTick;

            public void Execute(
                ref VillagerAvailability availability,
                ref VillagerJob job,
                ref VillagerDisciplineState discipline,
                ref VillagerMood mood,
                in VillagerNeeds needs,
                in VillagerAIState aiState)
            {
                var alive = needs.Health > 0.1f;
                var busy = aiState.CurrentState == VillagerAIState.State.Working || aiState.CurrentGoal == VillagerAIState.Goal.Work;

                var newAvailable = (byte)(alive && !busy ? 1 : 0);
                if (availability.IsAvailable != newAvailable)
                {
                    availability.IsAvailable = newAvailable;
                    availability.LastChangeTick = CurrentTick;
                    availability.BusyTime = 0f;
                }
                else if (busy)
                {
                    availability.BusyTime += DeltaTime;
                }

                availability.IsReserved = (byte)(job.Type != VillagerJob.JobType.None ? 1 : 0);

                if (discipline.Value == VillagerDisciplineType.Unassigned && job.Type != VillagerJob.JobType.None)
                {
                    discipline.Value = MapJobToDiscipline(job.Type);
                }

                var wellbeing = math.clamp((100f - needs.Hunger) * 0.4f + needs.Energy * 0.4f + math.saturate(needs.Health / math.max(1f, needs.MaxHealth)) * 100f * 0.2f, 0f, 100f);
                mood.Wellbeing = wellbeing;
                mood.TargetMood = wellbeing;
                var adjust = math.clamp(DeltaTime * mood.MoodChangeRate, 0f, 1f);
                mood.Mood = math.lerp(mood.Mood, mood.TargetMood, adjust);

                var energyFactor = math.clamp(needs.Energy / 100f, 0f, 1f);
                var moraleFactor = math.clamp(mood.Mood / 100f, 0f, 1f);
                job.Productivity = math.clamp(0.25f + energyFactor * 0.5f + moraleFactor * 0.25f, 0f, 1.5f);
            }

            private static VillagerDisciplineType MapJobToDiscipline(VillagerJob.JobType jobType)
            {
                return jobType switch
                {
                    VillagerJob.JobType.Farmer => VillagerDisciplineType.Farmer,
                    VillagerJob.JobType.Builder => VillagerDisciplineType.Builder,
                    VillagerJob.JobType.Gatherer => VillagerDisciplineType.Forester,
                    VillagerJob.JobType.Hunter => VillagerDisciplineType.Warrior,
                    VillagerJob.JobType.Guard => VillagerDisciplineType.Warrior,
                    _ => VillagerDisciplineType.Unassigned
                };
            }
        }
    }
}

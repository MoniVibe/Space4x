using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Villager;
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
                .WithAll<VillagerNeeds, VillagerJob, VillagerAIState, VillagerDisciplineState, VillagerMood, VillagerAvailability, VillagerFlags>()
                .WithNone<PlaybackGuardTag>()
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

            // Get villager behavior config or use defaults
            var config = SystemAPI.HasSingleton<VillagerBehaviorConfig>()
                ? SystemAPI.GetSingleton<VillagerBehaviorConfig>()
                : VillagerBehaviorConfig.CreateDefaults();

            var job = new UpdateVillagerStatusJob
            {
                DeltaTime = timeState.FixedDeltaTime,
                CurrentTick = timeState.Tick,
                WellbeingHungerWeight = config.WellbeingHungerWeight,
                WellbeingEnergyWeight = config.WellbeingEnergyWeight,
                WellbeingHealthWeight = config.WellbeingHealthWeight,
                ProductivityBase = config.ProductivityBase,
                ProductivityEnergyWeight = config.ProductivityEnergyWeight,
                ProductivityMoraleWeight = config.ProductivityMoraleWeight,
                ProductivityMax = config.ProductivityMax,
                AliveHealthThreshold = config.AliveHealthThreshold
            };

            state.Dependency = job.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        public partial struct UpdateVillagerStatusJob : IJobEntity
        {
            public float DeltaTime;
            public uint CurrentTick;
            public float WellbeingHungerWeight;
            public float WellbeingEnergyWeight;
            public float WellbeingHealthWeight;
            public float ProductivityBase;
            public float ProductivityEnergyWeight;
            public float ProductivityMoraleWeight;
            public float ProductivityMax;
            public float AliveHealthThreshold;

            public void Execute(
                ref VillagerAvailability availability,
                ref VillagerJob job,
                ref VillagerDisciplineState discipline,
                ref VillagerMood mood,
                in VillagerNeeds needs,
                in VillagerAIState aiState,
                in VillagerFlags flags)
            {
                // Skip dead villagers
                if (flags.IsDead)
                {
                    return;
                }
                
                var alive = needs.Health > AliveHealthThreshold;
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

                // Use helper methods to convert ushort to float
                var hunger = needs.HungerFloat;
                var energy = needs.EnergyFloat;
                
                var wellbeing = math.clamp((100f - hunger) * WellbeingHungerWeight + energy * WellbeingEnergyWeight + math.saturate(needs.Health / math.max(1f, needs.MaxHealth)) * 100f * WellbeingHealthWeight, 0f, 100f);
                mood.Wellbeing = wellbeing;
                mood.TargetMood = wellbeing;
                var adjust = math.clamp(DeltaTime * mood.MoodChangeRate, 0f, 1f);
                mood.Mood = math.lerp(mood.Mood, mood.TargetMood, adjust);

                var energyFactor = math.clamp(energy / 100f, 0f, 1f);
                var moraleFactor = math.clamp(mood.Mood / 100f, 0f, 1f);
                job.Productivity = math.clamp(ProductivityBase + energyFactor * ProductivityEnergyWeight + moraleFactor * ProductivityMoraleWeight, 0f, ProductivityMax);
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

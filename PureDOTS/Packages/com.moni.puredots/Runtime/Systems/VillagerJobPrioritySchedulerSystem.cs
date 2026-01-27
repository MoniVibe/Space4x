using PureDOTS.Config;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Villagers;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Schedules job/need priorities per villager archetype.
    /// Uses utility scoring + cooldown to determine which action should be taken next.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(VillagerSystemGroup))]
    [UpdateAfter(typeof(VillagerArchetypeResolutionSystem))]
    public partial struct VillagerJobPrioritySchedulerSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<VillagerArchetypeCatalogComponent>();
            state.RequireForUpdate<JobDefinitionCatalogComponent>();
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            var rewindState = SystemAPI.GetSingleton<RewindState>();
            
            if (timeState.IsPaused || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<VillagerArchetypeCatalogComponent>(out var archetypeCatalog) ||
                !SystemAPI.TryGetSingleton<JobDefinitionCatalogComponent>(out var jobCatalog))
            {
                return;
            }

            var archetypeBlob = archetypeCatalog.Catalog;
            var jobBlob = jobCatalog.Catalog;
            var currentTick = timeState.Tick;
            var deltaTime = timeState.FixedDeltaTime;

            var job = new SchedulePrioritiesJob
            {
                ArchetypeCatalog = archetypeBlob,
                JobCatalog = jobBlob,
                CurrentTick = currentTick,
                DeltaTime = deltaTime
            };

            state.Dependency = job.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        public partial struct SchedulePrioritiesJob : IJobEntity
        {
            [ReadOnly] public BlobAssetReference<VillagerArchetypeCatalogBlob> ArchetypeCatalog;
            [ReadOnly] public BlobAssetReference<JobDefinitionCatalogBlob> JobCatalog;
            public uint CurrentTick;
            public float DeltaTime;

            public void Execute(
                ref VillagerJobPriorityState priorityState,
                in VillagerArchetypeResolved archetype,
                in VillagerNeedsHot needsHot,
                in VillagerJob job)
            {
                if (!ArchetypeCatalog.IsCreated || !JobCatalog.IsCreated)
                {
                    return;
                }

                var archetypeData = archetype.Data;
                
                // Calculate need-based priorities using hot component (already normalized and urgency calculated)
                var hungerUrgency = needsHot.HungerUrgency;
                var energyUrgency = needsHot.EnergyUrgency;
                var moraleUrgency = needsHot.MoraleUrgency;
                
                // Apply archetype decay rates as weights
                var hungerPriority = hungerUrgency * archetypeData.HungerDecayRate * 100f;
                var energyPriority = energyUrgency * archetypeData.EnergyDecayRate * 100f;
                var moralePriority = moraleUrgency * archetypeData.MoraleDecayRate * 100f;
                
                // Calculate job priority based on archetype preferences
                var jobPriority = 0f;
                if (job.Type != VillagerJob.JobType.None &&
                    JobCatalog.Value.TryGetJobIndex((byte)job.Type, out var jobIndex))
                {
                    ref var jobDef = ref JobCatalog.Value.GetJob(jobIndex);
                    var baseJobPriority = jobDef.BasePriority;
                    
                    // Apply archetype job weight
                    var jobWeight = GetJobWeight(archetypeData, job.Type);
                    jobPriority = baseJobPriority * (jobWeight / 100f);
                    
                    // Apply cooldown penalty
                    var timeSinceLastJob = CurrentTick - priorityState.LastJobTick;
                    var cooldownTicks = (uint)(jobDef.CooldownSeconds / DeltaTime);
                    if (timeSinceLastJob < cooldownTicks)
                    {
                        var cooldownProgress = (float)timeSinceLastJob / cooldownTicks;
                        jobPriority *= cooldownProgress; // Reduce priority during cooldown
                    }
                }
                
                // Update priority state
                priorityState.HungerPriority = hungerPriority;
                priorityState.EnergyPriority = energyPriority;
                priorityState.MoralePriority = moralePriority;
                priorityState.JobPriority = jobPriority;
                
                // Determine highest priority action
                var maxPriority = math.max(math.max(hungerPriority, energyPriority), math.max(moralePriority, jobPriority));
                
                if (maxPriority == hungerPriority && hungerPriority > 20f)
                {
                    priorityState.PreferredAction = VillagerPriorityAction.SatisfyHunger;
                }
                else if (maxPriority == energyPriority && energyPriority > 20f)
                {
                    priorityState.PreferredAction = VillagerPriorityAction.Rest;
                }
                else if (maxPriority == moralePriority && moralePriority > 20f)
                {
                    priorityState.PreferredAction = VillagerPriorityAction.ImproveMorale;
                }
                else if (maxPriority == jobPriority && jobPriority > 10f)
                {
                    priorityState.PreferredAction = VillagerPriorityAction.Work;
                }
                else
                {
                    priorityState.PreferredAction = VillagerPriorityAction.Idle;
                }
            }
            
            private static float GetJobWeight(VillagerArchetypeData archetype, VillagerJob.JobType jobType)
            {
                return jobType switch
                {
                    VillagerJob.JobType.Gatherer => archetype.GatherJobWeight,
                    VillagerJob.JobType.Builder => archetype.BuildJobWeight,
                    VillagerJob.JobType.Crafter => archetype.CraftJobWeight,
                    VillagerJob.JobType.Guard => archetype.CombatJobWeight,
                    VillagerJob.JobType.Merchant => archetype.TradeJobWeight,
                    _ => 50f // Default weight
                };
            }
        }
    }
    
    /// <summary>
    /// Component storing calculated priorities for a villager.
    /// </summary>
    public struct VillagerJobPriorityState : IComponentData
    {
        public float HungerPriority;
        public float EnergyPriority;
        public float MoralePriority;
        public float JobPriority;
        public VillagerPriorityAction PreferredAction;
        public uint LastJobTick;
    }
    
    /// <summary>
    /// Preferred action based on priority calculation.
    /// </summary>
    public enum VillagerPriorityAction : byte
    {
        Idle = 0,
        SatisfyHunger = 1,
        Rest = 2,
        ImproveMorale = 3,
        Work = 4
    }
}


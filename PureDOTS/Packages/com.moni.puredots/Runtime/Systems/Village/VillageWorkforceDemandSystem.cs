using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Village;
using PureDOTS.Runtime.Villagers;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Generates workforce demand entries per village based on population, outlook, policy, and simple heuristics.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(VillagerSystemGroup))]
    // Removed invalid UpdateAfter: VillagerJobAssignmentSystem runs in WarmPathSystemGroup.
    public partial struct VillageWorkforceDemandSystem : ISystem
    {
        private ComponentLookup<VillagerAlignment> _alignmentLookup;
        private ComponentLookup<VillageOutlook> _outlookLookup;
        private ComponentLookup<VillageWorkforcePolicy> _policyLookup;
        private ComponentLookup<VillageResidencyState> _residencyLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _alignmentLookup = state.GetComponentLookup<VillagerAlignment>(true);
            _outlookLookup = state.GetComponentLookup<VillageOutlook>(true);
            _policyLookup = state.GetComponentLookup<VillageWorkforcePolicy>(true);
            _residencyLookup = state.GetComponentLookup<VillageResidencyState>(true);
            state.RequireForUpdate<PureDOTS.Runtime.Village.VillageId>();
            state.RequireForUpdate<VillagerJob>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (_, entity) in SystemAPI.Query<RefRO<PureDOTS.Runtime.Village.VillageId>>().WithNone<VillageWorkforceDemandEntry>().WithEntityAccess())
            {
                ecb.AddBuffer<VillageWorkforceDemandEntry>(entity);
            }
            ecb.Playback(state.EntityManager);
            ecb.Dispose();

            var allocator = state.WorldUpdateAllocator;
            var villagerQuery = SystemAPI.QueryBuilder().WithAll<VillagerId, VillagerJob, VillagerFlags>().Build();
            var estimate = math.max(1, villagerQuery.CalculateEntityCount());
            var jobCounts = new NativeParallelHashMap<int, JobCountAccumulator>(estimate, allocator);

            foreach (var (villagerId, job, flags) in SystemAPI
                         .Query<RefRO<VillagerId>, RefRO<VillagerJob>, RefRO<VillagerFlags>>())
            {
                if (flags.ValueRO.IsDead)
                {
                    continue;
                }

                var faction = villagerId.ValueRO.FactionId;
                if (!jobCounts.TryGetValue(faction, out var acc))
                {
                    acc = default;
                }

                acc.Add(job.ValueRO.Type);
                jobCounts[faction] = acc;
            }

            _alignmentLookup.Update(ref state);
            _outlookLookup.Update(ref state);
            _policyLookup.Update(ref state);
            _residencyLookup.Update(ref state);

            foreach (var (villageId, stats, entity) in SystemAPI
                         .Query<RefRO<PureDOTS.Runtime.Village.VillageId>, RefRO<VillageStats>>()
                         .WithEntityAccess())
            {
                var demandBuffer = state.EntityManager.GetBuffer<VillageWorkforceDemandEntry>(entity);
                demandBuffer.Clear();

                var pop = math.max(1, stats.ValueRO.Population);
                jobCounts.TryGetValue(villageId.ValueRO.Value, out var counts);

                var alignment = _alignmentLookup.HasComponent(entity) ? _alignmentLookup[entity] : default;
                var outlook = _outlookLookup.HasComponent(entity) ? _outlookLookup[entity].Flags : VillageOutlookFlags.None;
                var policy = _policyLookup.HasComponent(entity) ? _policyLookup[entity] : default;
                var residency = _residencyLookup.HasComponent(entity) ? _residencyLookup[entity] : default;

                AddDemand(ref demandBuffer, VillagerJob.JobType.Farmer,
                    ComputeEssentialDemand(pop, counts.Farmers, alignment, outlook));
                AddDemand(ref demandBuffer, VillagerJob.JobType.Builder,
                    ComputeBuilderDemand(pop, counts.Builders, alignment, outlook, residency.PendingResidents));
                AddDemand(ref demandBuffer, VillagerJob.JobType.Gatherer,
                    ComputeGathererDemand(pop, counts.Gatherers, alignment));
                AddDemand(ref demandBuffer, VillagerJob.JobType.Hunter,
                    ComputeHunterDemand(pop, counts.Hunters, outlook));
                AddDemand(ref demandBuffer, VillagerJob.JobType.Guard,
                    ComputeGuardDemand(pop, counts.Guards, alignment, outlook, policy));
                AddDemand(ref demandBuffer, VillagerJob.JobType.Priest,
                    ComputePriestDemand(pop, counts.Priests, outlook));
                AddDemand(ref demandBuffer, VillagerJob.JobType.Merchant,
                    ComputeMerchantDemand(pop, counts.Merchants, alignment, outlook));
                AddDemand(ref demandBuffer, VillagerJob.JobType.Crafter,
                    ComputeCrafterDemand(pop, counts.Crafters, outlook));
            }
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }

        private static void AddDemand(ref DynamicBuffer<VillageWorkforceDemandEntry> buffer, VillagerJob.JobType jobType, float shortage)
        {
            if (shortage <= 0f)
            {
                return;
            }

            buffer.Add(new VillageWorkforceDemandEntry
            {
                JobType = jobType,
                Shortage = shortage
            });
        }

        private static float ComputeEssentialDemand(int pop, int current, VillagerAlignment alignment, VillageOutlookFlags outlook)
        {
            float ratio = 0.25f;
            if ((outlook & VillageOutlookFlags.Ascetic) != 0)
            {
                ratio += 0.05f;
            }
            if ((outlook & VillageOutlookFlags.Materialistic) != 0)
            {
                ratio -= 0.05f;
            }
            ratio += math.saturate(alignment.PurityNormalized) * 0.05f;
            var desired = math.max(1f, pop * ratio);
            return math.max(0f, desired - current) / pop;
        }

        private static float ComputeBuilderDemand(int pop, int current, VillagerAlignment alignment, VillageOutlookFlags outlook, int pendingResidents)
        {
            var desired = pop * 0.08f + pendingResidents * 0.5f;
            if ((outlook & VillageOutlookFlags.Expansionist) != 0)
            {
                desired += pop * 0.05f;
            }
            return math.max(0f, desired - current) / pop;
        }

        private static float ComputeGathererDemand(int pop, int current, VillagerAlignment alignment)
        {
            var desired = pop * 0.1f + math.max(0f, -alignment.MaterialismNormalized) * pop * 0.05f;
            return math.max(0f, desired - current) / pop;
        }

        private static float ComputeHunterDemand(int pop, int current, VillageOutlookFlags outlook)
        {
            var desired = pop * 0.05f;
            if ((outlook & VillageOutlookFlags.Warlike) != 0)
            {
                desired += pop * 0.03f;
            }
            return math.max(0f, desired - current) / pop;
        }

        private static float ComputeGuardDemand(int pop, int current, VillagerAlignment alignment, VillageOutlookFlags outlook, VillageWorkforcePolicy policy)
        {
            var desired = pop * 0.08f;
            if ((outlook & VillageOutlookFlags.Warlike) != 0)
            {
                desired += pop * 0.05f;
            }
            desired += policy.DefenseUrgency * pop * 0.1f;
            desired += math.saturate(-alignment.PurityNormalized) * pop * 0.05f;
            return math.max(0f, desired - current) / pop;
        }

        private static float ComputePriestDemand(int pop, int current, VillageOutlookFlags outlook)
        {
            var desired = pop * 0.04f;
            if ((outlook & VillageOutlookFlags.Spiritual) != 0)
            {
                desired += pop * 0.04f;
            }
            return math.max(0f, desired - current) / pop;
        }

        private static float ComputeMerchantDemand(int pop, int current, VillagerAlignment alignment, VillageOutlookFlags outlook)
        {
            var desired = pop * 0.05f + alignment.MaterialismNormalized * pop * 0.05f;
            if ((outlook & VillageOutlookFlags.Materialistic) != 0)
            {
                desired += pop * 0.05f;
            }
            if ((outlook & VillageOutlookFlags.Ascetic) != 0)
            {
                desired -= pop * 0.03f;
            }
            desired = math.max(0f, desired);
            return math.max(0f, desired - current) / pop;
        }

        private static float ComputeCrafterDemand(int pop, int current, VillageOutlookFlags outlook)
        {
            var desired = pop * 0.06f;
            if ((outlook & VillageOutlookFlags.Warlike) != 0)
            {
                desired += pop * 0.04f;
            }
            if ((outlook & VillageOutlookFlags.Materialistic) != 0)
            {
                desired += pop * 0.02f;
            }
            return math.max(0f, desired - current) / pop;
        }

        private struct JobCountAccumulator
        {
            public int Farmers;
            public int Builders;
            public int Gatherers;
            public int Hunters;
            public int Guards;
            public int Priests;
            public int Merchants;
            public int Crafters;

            public void Add(VillagerJob.JobType jobType)
            {
                switch (jobType)
                {
                    case VillagerJob.JobType.Farmer:
                        Farmers++;
                        break;
                    case VillagerJob.JobType.Builder:
                        Builders++;
                        break;
                    case VillagerJob.JobType.Gatherer:
                        Gatherers++;
                        break;
                    case VillagerJob.JobType.Hunter:
                        Hunters++;
                        break;
                    case VillagerJob.JobType.Guard:
                        Guards++;
                        break;
                    case VillagerJob.JobType.Priest:
                        Priests++;
                        break;
                    case VillagerJob.JobType.Merchant:
                        Merchants++;
                        break;
                    case VillagerJob.JobType.Crafter:
                        Crafters++;
                        break;
                }
            }
        }
    }
}

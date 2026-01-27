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
    /// Decides villager workforce intent based on village needs, discipline, and aggregate behavior profile.
    /// Produces intents only; downstream systems assign/transition jobs.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(VillagerSystemGroup))]
    [UpdateAfter(typeof(VillagerNeedsSystem))]
    public partial struct VillageWorkforceDecisionSystem : ISystem
    {
        private ComponentLookup<VillagerAlignment> _alignmentLookup;
        private ComponentLookup<VillageWorkforcePolicy> _policyLookup;
        private BufferLookup<VillageWorkforceDemandEntry> _demandLookup;
        private BufferLookup<VillageJobPreferenceEntry> _preferenceLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _alignmentLookup = state.GetComponentLookup<VillagerAlignment>(true);
            _policyLookup = state.GetComponentLookup<VillageWorkforcePolicy>(true);
            _demandLookup = state.GetBufferLookup<VillageWorkforceDemandEntry>(true);
            _preferenceLookup = state.GetBufferLookup<VillageJobPreferenceEntry>(true);
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();

            state.RequireForUpdate<PureDOTS.Runtime.Village.VillageId>();
            state.RequireForUpdate<AggregateBehaviorProfile>();
            state.RequireForUpdate<VillagerId>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            var tick = timeState.Tick;
            if (timeState.IsPaused)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var profile = SystemAPI.GetSingleton<AggregateBehaviorProfile>();
            if (!profile.Blob.IsCreated)
            {
                return;
            }

            ref var profileBlob = ref profile.Blob.Value;
            if (!SystemAPI.TryGetSingleton(out EndSimulationEntityCommandBufferSystem.Singleton ecbSingleton))
            {
                return;
            }

            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            _alignmentLookup.Update(ref state);
            _policyLookup.Update(ref state);
            _demandLookup.Update(ref state);
            _preferenceLookup.Update(ref state);

            var villageSnapshots = BuildVillageSnapshots(ref state, ref profileBlob);

            foreach (var (villagerId, job, needs, discipline, entity) in SystemAPI
                         .Query<RefRO<VillagerId>, RefRO<VillagerJob>, RefRO<VillagerNeeds>, RefRO<VillagerDisciplineState>>()
                         .WithEntityAccess())
            {
                if (!villageSnapshots.TryGetValue(villagerId.ValueRO.FactionId, out var snapshot) || !snapshot.HasDemand)
                {
                    continue;
                }

                var belonging = SystemAPI.HasComponent<VillagerAggregateBelonging>(entity)
                    ? SystemAPI.GetComponent<VillagerAggregateBelonging>(entity)
                    : default;

                if (!SystemAPI.HasComponent<WorkforceDecisionCooldown>(entity))
                {
                    ecb.AddComponent(entity, new WorkforceDecisionCooldown { NextCheckTick = 0 });
                }

                var cooldown = SystemAPI.GetComponentRW<WorkforceDecisionCooldown>(entity);

                if (tick < cooldown.ValueRO.NextCheckTick)
                {
                    continue;
                }

                var jitter = profileBlob.InitiativeJitterTicks > 0
                    ? (uint)(math.abs(math.hash(new int2(villagerId.ValueRO.Value, (int)tick))) % profileBlob.InitiativeJitterTicks)
                    : 0u;
                cooldown.ValueRW = new WorkforceDecisionCooldown
                {
                    NextCheckTick = tick + profileBlob.InitiativeIntervalTicks + jitter
                };

                var desiredJob = snapshot.PriorityJob;
                if (desiredJob == VillagerJob.JobType.None)
                {
                    continue;
                }

                var collectiveScore = snapshot.Shortage * profileBlob.CollectiveNeedWeight;
                var lawMultiplier = snapshot.LawMultiplier;
                var chaosMultiplier = snapshot.ChaosMultiplier;
                collectiveScore *= lawMultiplier;

                if (profileBlob.AllowConscriptionOverrides && snapshot.Policy.ConscriptionActive != 0 && desiredJob == VillagerJob.JobType.Guard)
                {
                    collectiveScore += profileBlob.ConscriptionWeight * (1f + snapshot.Policy.ConscriptionUrgency);
                }

                collectiveScore += profileBlob.EmergencyOverrideWeight * snapshot.Policy.DefenseUrgency;

                var moraleRatio = needs.ValueRO.MoraleFloat * 0.01f;
                var personalResistance = profileBlob.PersonalAmbitionWeight * chaosMultiplier * moraleRatio;

                if (!DisciplineMatchesJob(discipline.ValueRO.Value, desiredJob))
                {
                    personalResistance += profileBlob.DisciplineResistanceWeight;
                }

                if (snapshot.Shortage < profileBlob.ShortageThreshold)
                {
                    continue;
                }

                var belongingMultiplier = DetermineBelongingMultiplier(belonging, villagerId.ValueRO.FactionId, snapshot.PreferenceWeight);
                var finalScore = (collectiveScore - personalResistance) * belongingMultiplier;
                if (finalScore <= 0f)
                {
                    continue;
                }

                var intent = new VillagerWorkforceIntent
                {
                    DesiredJob = desiredJob,
                    DesireWeight = finalScore
                };

                if (SystemAPI.HasComponent<VillagerWorkforceIntent>(entity))
                {
                    SystemAPI.SetComponent(entity, intent);
                }
                else
                {
                    ecb.AddComponent(entity, intent);
                }
            }
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }

        private NativeParallelHashMap<int, VillageSnapshot> BuildVillageSnapshots(ref SystemState state, ref AggregateBehaviorProfileBlob profileBlob)
        {
            var allocator = state.WorldUpdateAllocator;
            var villageCount = math.max(1, SystemAPI.QueryBuilder().WithAll<PureDOTS.Runtime.Village.VillageId>().Build().CalculateEntityCount());
            var snapshots = new NativeParallelHashMap<int, VillageSnapshot>(villageCount, allocator);

            foreach (var (villageId, entity) in SystemAPI.Query<RefRO<PureDOTS.Runtime.Village.VillageId>>().WithEntityAccess())
            {
                var snapshot = new VillageSnapshot
                {
                    LawMultiplier = 1f,
                    ChaosMultiplier = 1f,
                    Policy = new VillageWorkforcePolicy(),
                    PriorityJob = VillagerJob.JobType.None,
                    Shortage = 0f,
                    HasDemand = false
                };

                if (_alignmentLookup.HasComponent(entity))
                {
                    var alignment = _alignmentLookup[entity];
                    var lawChaos = alignment.OrderNormalized;
                    var materialism = alignment.MaterialismNormalized;
                    snapshot.LawMultiplier = math.saturate(profileBlob.LawfulnessComplianceCurve.Evaluate(lawChaos));
                    snapshot.ChaosMultiplier = math.saturate(profileBlob.ChaosFreedomCurve.Evaluate(materialism));
                }

                if (_policyLookup.HasComponent(entity))
                {
                    snapshot.Policy = _policyLookup[entity];
                }

                if (_demandLookup.HasBuffer(entity))
                {
                    var demandBuffer = _demandLookup[entity];
                    var bestShortage = 0f;
                    var bestJob = VillagerJob.JobType.None;
                    for (int i = 0; i < demandBuffer.Length; i++)
                    {
                        if (demandBuffer[i].Shortage > bestShortage)
                        {
                            bestShortage = demandBuffer[i].Shortage;
                            bestJob = demandBuffer[i].JobType;
                        }
                    }

                    if (bestShortage > 0f)
                    {
                        snapshot.HasDemand = true;
                        snapshot.Shortage = bestShortage;
                        snapshot.PriorityJob = bestJob;
                    }
                }

                if (_preferenceLookup.HasBuffer(entity) && snapshot.PriorityJob != VillagerJob.JobType.None)
                {
                    var prefs = _preferenceLookup[entity];
                    snapshot.PreferenceWeight = 1f;
                    for (int i = 0; i < prefs.Length; i++)
                    {
                        if (prefs[i].JobType == snapshot.PriorityJob)
                        {
                            snapshot.PreferenceWeight = prefs[i].Weight;
                            snapshot.Shortage *= snapshot.PreferenceWeight;
                            break;
                        }
                    }
                }

                snapshots[villageId.ValueRO.Value] = snapshot;
            }

            return snapshots;
        }

        private static bool DisciplineMatchesJob(VillagerDisciplineType discipline, VillagerJob.JobType job)
        {
            return job switch
            {
                VillagerJob.JobType.Farmer => discipline == VillagerDisciplineType.Farmer,
                VillagerJob.JobType.Builder => discipline == VillagerDisciplineType.Builder,
                VillagerJob.JobType.Gatherer => discipline == VillagerDisciplineType.Forester || discipline == VillagerDisciplineType.Miner,
                VillagerJob.JobType.Hunter => discipline == VillagerDisciplineType.Warrior,
                VillagerJob.JobType.Guard => discipline == VillagerDisciplineType.Warrior,
                VillagerJob.JobType.Priest => discipline == VillagerDisciplineType.Worshipper,
                VillagerJob.JobType.Merchant => discipline == VillagerDisciplineType.Breeder,
                VillagerJob.JobType.Crafter => discipline == VillagerDisciplineType.Builder,
                _ => true
            };
        }

        private struct VillageSnapshot
        {
            public bool HasDemand;
            public float Shortage;
            public float LawMultiplier;
            public float ChaosMultiplier;
            public VillagerJob.JobType PriorityJob;
            public VillageWorkforcePolicy Policy;
            public float PreferenceWeight;
        }

        private static float DetermineBelongingMultiplier(in VillagerAggregateBelonging belonging, int villageFaction, float preferenceWeight)
        {
            if (belonging.PrimaryAggregate == Entity.Null)
            {
                return 1f;
            }

            if (belonging.Category == AggregateCategory.Village)
            {
                return 1f + belonging.Loyalty * 0.5f;
            }

            var loyaltyPenalty = math.saturate(belonging.Loyalty);
            var sympathyPenalty = math.saturate(math.max(0f, 0.5f + belonging.Sympathy * 0.5f));
            var penalty = 1f - (loyaltyPenalty * sympathyPenalty);
            return math.max(0.1f, penalty * math.max(0.5f, preferenceWeight));
        }
    }
}

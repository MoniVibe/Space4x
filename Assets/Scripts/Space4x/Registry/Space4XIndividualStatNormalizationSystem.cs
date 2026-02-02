using PureDOTS.Runtime.Components;
using PureDOTS.Systems;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    /// <summary>
    /// Maintains normalized [0..1] stat snapshots for individual entities.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(GameplaySystemGroup))]
    [UpdateBefore(typeof(Space4XPilotProficiencySystem))]
    public partial struct Space4XIndividualStatNormalizationSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);

            foreach (var (_, _, entity) in SystemAPI.Query<RefRO<IndividualStats>, RefRO<PhysiqueFinesseWill>>()
                .WithNone<Space4XNormalizedIndividualStats>()
                .WithEntityAccess())
            {
                ecb.AddComponent(entity, new Space4XNormalizedIndividualStats());
            }

            ecb.Playback(em);

            var wisdomLookup = state.GetComponentLookup<PureDOTS.Runtime.Stats.WisdomStat>(true);
            wisdomLookup.Update(ref state);

            var job = new NormalizeJob
            {
                WisdomLookup = wisdomLookup
            };
            state.Dependency = job.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        public partial struct NormalizeJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<PureDOTS.Runtime.Stats.WisdomStat> WisdomLookup;

            void Execute(
                Entity entity,
                ref Space4XNormalizedIndividualStats normalized,
                in IndividualStats stats,
                in PhysiqueFinesseWill physique)
            {
                normalized.Command = math.saturate((float)stats.Command / 100f);
                normalized.Tactics = math.saturate((float)stats.Tactics / 100f);
                normalized.Logistics = math.saturate((float)stats.Logistics / 100f);
                normalized.Diplomacy = math.saturate((float)stats.Diplomacy / 100f);
                normalized.Engineering = math.saturate((float)stats.Engineering / 100f);
                normalized.Resolve = math.saturate((float)stats.Resolve / 100f);

                normalized.Physique = math.saturate((float)physique.Physique / 100f);
                normalized.Finesse = math.saturate((float)physique.Finesse / 100f);
                normalized.Will = math.saturate((float)physique.Will / 100f);

                var wisdom = normalized.Will;
                if (WisdomLookup.HasComponent(entity))
                {
                    wisdom = math.saturate(WisdomLookup[entity].Wisdom / 100f);
                }

                normalized.Wisdom = wisdom;
            }
        }
    }
}

using PureDOTS.Runtime.Components;
using PureDOTS.Systems;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    /// <summary>
    /// Maintains normalized [0..1] stat snapshots for individual entities.
    /// </summary>
    [UpdateInGroup(typeof(GameplaySystemGroup))]
    [UpdateBefore(typeof(Space4XPilotProficiencySystem))]
    public partial struct Space4XIndividualStatNormalizationSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
        }

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
            foreach (var (normalized, stats, physique, entity) in SystemAPI
                         .Query<RefRW<Space4XNormalizedIndividualStats>, RefRO<IndividualStats>, RefRO<PhysiqueFinesseWill>>()
                         .WithEntityAccess())
            {
                normalized.ValueRW.Command = math.saturate((float)stats.ValueRO.Command / 100f);
                normalized.ValueRW.Tactics = math.saturate((float)stats.ValueRO.Tactics / 100f);
                normalized.ValueRW.Logistics = math.saturate((float)stats.ValueRO.Logistics / 100f);
                normalized.ValueRW.Diplomacy = math.saturate((float)stats.ValueRO.Diplomacy / 100f);
                normalized.ValueRW.Engineering = math.saturate((float)stats.ValueRO.Engineering / 100f);
                normalized.ValueRW.Resolve = math.saturate((float)stats.ValueRO.Resolve / 100f);

                normalized.ValueRW.Physique = math.saturate((float)physique.ValueRO.Physique / 100f);
                normalized.ValueRW.Finesse = math.saturate((float)physique.ValueRO.Finesse / 100f);
                normalized.ValueRW.Will = math.saturate((float)physique.ValueRO.Will / 100f);

                var wisdom = normalized.ValueRW.Will;
                if (em.HasComponent<PureDOTS.Runtime.Stats.WisdomStat>(entity))
                {
                    wisdom = math.saturate(em.GetComponentData<PureDOTS.Runtime.Stats.WisdomStat>(entity).Wisdom / 100f);
                }

                normalized.ValueRW.Wisdom = wisdom;
            }
        }
    }
}

using PureDOTS.Runtime.Bands;
using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Systems
{
    [BurstCompile]
    [UpdateInGroup(typeof(VillagerSystemGroup))]
    [UpdateAfter(typeof(VillagerNeedsSystem))]
    public partial struct BandAggregationSystem : ISystem
    {
        private ComponentLookup<VillagerDisciplineState> _disciplineLookup;
        private ComponentLookup<VillagerMood> _moodLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _disciplineLookup = state.GetComponentLookup<VillagerDisciplineState>(true);
            _moodLookup = state.GetComponentLookup<VillagerMood>(true);
            state.RequireForUpdate<BandId>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var time = SystemAPI.GetSingleton<TimeState>();
            _disciplineLookup.Update(ref state);
            _moodLookup.Update(ref state);

            foreach (var (bandStats, members) in SystemAPI
                         .Query<RefRW<BandStats>, DynamicBuffer<BandMember>>())
            {
                for (int i = members.Length - 1; i >= 0; i--)
                {
                    if (!state.EntityManager.Exists(members[i].Villager))
                    {
                        members.RemoveAt(i);
                    }
                }

                var disciplineSum = 0f;
                var moraleSum = 0f;
                var cohesionSum = 0f;

                for (int i = 0; i < members.Length; i++)
                {
                    var villager = members[i].Villager;
                    if (_disciplineLookup.HasComponent(villager))
                    {
                        disciplineSum += _disciplineLookup[villager].Level;
                    }
                    if (_moodLookup.HasComponent(villager))
                    {
                        var mood = _moodLookup[villager];
                        moraleSum += mood.Mood;
                        cohesionSum += mood.Wellbeing;
                    }
                }

                var memberCount = members.Length;
                var averageDiscipline = memberCount > 0 ? disciplineSum / memberCount : 0f;
                var morale = memberCount > 0 ? moraleSum / memberCount : 0f;
                var cohesion = memberCount > 0 ? cohesionSum / memberCount : 0f;

                var flags = bandStats.ValueRO.Flags;
                if (flags == BandStatusFlags.None && memberCount > 0)
                {
                    flags = BandStatusFlags.Idle;
                }

                bandStats.ValueRW = new BandStats
                {
                    MemberCount = memberCount,
                    AverageDiscipline = averageDiscipline,
                    Morale = morale,
                    Cohesion = cohesion,
                    Fatigue = bandStats.ValueRO.Fatigue,
                    Flags = flags,
                    LastUpdateTick = time.Tick
                };
            }
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }
    }
}

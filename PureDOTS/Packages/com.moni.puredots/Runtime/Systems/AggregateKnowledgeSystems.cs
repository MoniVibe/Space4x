using PureDOTS.Runtime.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems
{
    [UpdateInGroup(typeof(GameplaySystemGroup), OrderFirst = true)]
    public partial struct AggregateKnowledgeBootstrapSystem : ISystem
    {
        private EntityQuery _query;

        public void OnCreate(ref SystemState state)
        {
            _query = SystemAPI.QueryBuilder()
                .WithAll<AggregateEntity>()
                .WithNone<AggregateKnowledge>()
                .Build();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (_query.IsEmpty)
            {
                return;
            }

            var ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (aggregate, entity) in SystemAPI.Query<RefRO<AggregateEntity>>().WithNone<AggregateKnowledge>().WithEntityAccess())
            {
                ecb.AddComponent(entity, new AggregateKnowledge());
            }

            ecb.Playback(state.EntityManager);
        }
    }

    [UpdateInGroup(typeof(GameplaySystemGroup))]
    [UpdateAfter(typeof(AggregateKnowledgeBootstrapSystem))]
    public partial struct AggregateKnowledgeAssimilationSystem : ISystem
    {
        private BufferLookup<AggregateMember> _memberLookup;
        private ComponentLookup<VillagerKnowledge> _villagerKnowledgeLookup;

        public void OnCreate(ref SystemState state)
        {
            _memberLookup = state.GetBufferLookup<AggregateMember>(true);
            _villagerKnowledgeLookup = state.GetComponentLookup<VillagerKnowledge>(true);

            state.RequireForUpdate<AggregateEntity>();
            state.RequireForUpdate<AggregateKnowledge>();
            state.RequireForUpdate<TimeState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            _memberLookup.Update(ref state);
            _villagerKnowledgeLookup.Update(ref state);

            var deltaTime = SystemAPI.GetSingleton<TimeState>().FixedDeltaTime;
            var learnRate = math.max(0.001f, deltaTime) * 0.25f;
            var decayRate = deltaTime * 0.01f;

            foreach (var (aggregateKnowledge, entity) in SystemAPI.Query<RefRW<AggregateKnowledge>>().WithAll<AggregateEntity>().WithEntityAccess())
            {
                if (!_memberLookup.HasBuffer(entity))
                {
                    var knowledge = aggregateKnowledge.ValueRO;
                    knowledge.ApplyDecay(decayRate);
                    aggregateKnowledge.ValueRW = knowledge;
                    continue;
                }

                var members = _memberLookup[entity];
                var knowledgeRef = aggregateKnowledge.ValueRO;
                var totalWeight = 0f;

                for (var i = 0; i < members.Length; i++)
                {
                    var member = members[i];
                    if (member.Member == Entity.Null || !_villagerKnowledgeLookup.HasComponent(member.Member))
                    {
                        continue;
                    }

                    totalWeight += math.max(0.01f, member.Weight <= 0f ? 1f : member.Weight);
                    var villagerKnowledge = _villagerKnowledgeLookup[member.Member];
                    Assimilate(ref knowledgeRef, in villagerKnowledge, learnRate * math.max(0.01f, member.Weight));
                }

                if (members.Length == 0 || totalWeight <= 0f)
                {
                    knowledgeRef.ApplyDecay(decayRate);
                }

                aggregateKnowledge.ValueRW = knowledgeRef;
            }
        }

        private static void Assimilate(ref AggregateKnowledge aggregate, in VillagerKnowledge villager, float rate)
        {
            if (villager.Lessons.Length == 0 || rate <= 0f)
            {
                return;
            }

            for (var i = 0; i < villager.Lessons.Length; i++)
            {
                var lesson = villager.Lessons[i];
                if (lesson.LessonId.Length == 0)
                {
                    continue;
                }

                var aggregateProgress = aggregate.GetProgress(lesson.LessonId);
                var delta = (lesson.Progress - aggregateProgress) * rate;
                aggregate.AddProgress(lesson.LessonId, delta, out _);
            }
        }
    }
}

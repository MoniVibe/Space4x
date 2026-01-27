using PureDOTS.Runtime.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems
{
    [UpdateInGroup(typeof(VillagerSystemGroup), OrderFirst = true)]
    public partial struct VillagerAggregateLessonTrackerBootstrapSystem : ISystem
    {
        private EntityQuery _missingTrackerQuery;

        public void OnCreate(ref SystemState state)
        {
            _missingTrackerQuery = SystemAPI.QueryBuilder()
                .WithAll<VillagerKnowledge>()
                .WithNone<VillagerAggregateLessonTracker>()
                .Build();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (_missingTrackerQuery.IsEmpty)
            {
                return;
            }

            var ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var entity in _missingTrackerQuery.ToEntityArray(Allocator.Temp))
            {
                ecb.AddBuffer<VillagerAggregateLessonTracker>(entity);
            }

            ecb.Playback(state.EntityManager);
        }
    }

    [UpdateInGroup(typeof(VillagerSystemGroup))]
    [UpdateAfter(typeof(VillagerFamilyLessonShareSystem))]
    public partial struct AggregateLessonPropagationSystem : ISystem
    {
        private ComponentLookup<AggregateKnowledge> _aggregateKnowledgeLookup;
        private BufferLookup<VillagerAggregateMembership> _villagerMembershipLookup;
        private EntityQuery _villagerLessonQuery;

        public void OnCreate(ref SystemState state)
        {
            _aggregateKnowledgeLookup = state.GetComponentLookup<AggregateKnowledge>(true);
            _villagerMembershipLookup = state.GetBufferLookup<VillagerAggregateMembership>(true);
            _villagerLessonQuery = SystemAPI.QueryBuilder()
                .WithAll<VillagerLessonShare, VillagerAggregateLessonTracker>()
                .Build();

            state.RequireForUpdate<AggregateKnowledge>();
            state.RequireForUpdate<VillagerLessonShare>();
        }

        public void OnUpdate(ref SystemState state)
        {
            _aggregateKnowledgeLookup.Update(ref state);
            _villagerMembershipLookup.Update(ref state);

            var timeState = SystemAPI.TryGetSingleton(out TimeState ts) ? ts : new TimeState { FixedDeltaTime = 1f / 60f, DeltaTime = 1f / 60f };
            var baseRate = math.max(0.01f, timeState.FixedDeltaTime) * 0.35f;

            using var villagerEntities = _villagerLessonQuery.ToEntityArray(Allocator.Temp);
            var entityManager = state.EntityManager;

            foreach (var entity in villagerEntities)
            {
                var lessonShares = entityManager.GetBuffer<VillagerLessonShare>(entity);
                var trackers = entityManager.GetBuffer<VillagerAggregateLessonTracker>(entity);

                if (!_villagerMembershipLookup.HasBuffer(entity))
                {
                    continue;
                }

                var memberships = _villagerMembershipLookup[entity];
                for (var m = 0; m < memberships.Length; m++)
                {
                    var membership = memberships[m];
                    if (membership.Aggregate == Entity.Null || !_aggregateKnowledgeLookup.HasComponent(membership.Aggregate))
                    {
                        continue;
                    }

                    var loyalty = math.saturate(math.max(0.05f, membership.Loyalty));
                    var aggregateKnowledge = _aggregateKnowledgeLookup[membership.Aggregate];
                    var reinforcement = loyalty * baseRate;

                    for (var i = 0; i < aggregateKnowledge.Lessons.Length; i++)
                    {
                        var lesson = aggregateKnowledge.Lessons[i];
                        if (lesson.LessonId.Length == 0 || lesson.Progress <= 0f)
                        {
                            continue;
                        }

                        var shareAmount = lesson.Progress * reinforcement;
                        var shares = lessonShares;
                        var trackersBuffer = trackers;
                        VillagerLessonShareUtility.AccumulateShare(ref shares, in lesson.LessonId, shareAmount, VillagerLessonShareSource.Aggregate);
                        TrackAggregateLesson(ref trackersBuffer, membership.Aggregate, lesson.LessonId, loyalty * 0.5f);
                    }
                }
            }
        }

        private static void TrackAggregateLesson(
            ref DynamicBuffer<VillagerAggregateLessonTracker> trackers,
            Entity aggregate,
            in FixedString64Bytes lessonId,
            float reinforcement)
        {
            if (lessonId.Length == 0 || aggregate == Entity.Null)
            {
                return;
            }

            for (var i = 0; i < trackers.Length; i++)
            {
                var tracker = trackers[i];
                if (!tracker.LessonId.Equals(lessonId) || tracker.Aggregate != aggregate)
                {
                    continue;
                }

                tracker.Support = math.saturate(tracker.Support + reinforcement);
                trackers[i] = tracker;
                return;
            }

            trackers.Add(new VillagerAggregateLessonTracker
            {
                LessonId = lessonId,
                Aggregate = aggregate,
                Support = math.saturate(reinforcement)
            });
        }
    }

    [UpdateInGroup(typeof(VillagerSystemGroup))]
    [UpdateAfter(typeof(AggregateLessonPropagationSystem))]
    public partial struct VillagerAggregateLessonDecaySystem : ISystem
    {
        private BufferLookup<VillagerAggregateMembership> _membershipLookup;

        public void OnCreate(ref SystemState state)
        {
            _membershipLookup = state.GetBufferLookup<VillagerAggregateMembership>(true);
            state.RequireForUpdate<VillagerAggregateLessonTracker>();
            state.RequireForUpdate<TimeState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            _membershipLookup.Update(ref state);
            var timeState = SystemAPI.GetSingleton<TimeState>();
            var decayStep = math.max(0.005f, timeState.FixedDeltaTime * 0.2f);
            var entityManager = state.EntityManager;

            foreach (var (knowledge, entity) in SystemAPI
                         .Query<RefRW<VillagerKnowledge>>()
                         .WithAll<VillagerAggregateLessonTracker>()
                         .WithEntityAccess())
            {
                var trackers = entityManager.GetBuffer<VillagerAggregateLessonTracker>(entity);
                if (trackers.Length == 0)
                {
                    continue;
                }

                var hasMemberships = _membershipLookup.HasBuffer(entity);
                var memberships = hasMemberships ? _membershipLookup[entity] : default;

                for (var i = trackers.Length - 1; i >= 0; i--)
                {
                    var tracker = trackers[i];
                    var loyalty = hasMemberships ? GetLoyalty(in memberships, tracker.Aggregate) : 0f;
                    if (loyalty > 0f)
                    {
                        tracker.Support = math.saturate(tracker.Support + loyalty * timeState.FixedDeltaTime);
                        trackers[i] = tracker;
                        continue;
                    }

                    tracker.Support -= decayStep;
                    knowledge.ValueRW.AddProgress(tracker.LessonId, -decayStep, out var newProgress);
                    if (tracker.Support <= 0f || newProgress <= 0f)
                    {
                        trackers.RemoveAt(i);
                    }
                    else
                    {
                        trackers[i] = tracker;
                    }
                }
            }
        }

        private static float GetLoyalty(in DynamicBuffer<VillagerAggregateMembership> memberships, Entity aggregate)
        {
            for (var i = 0; i < memberships.Length; i++)
            {
                if (memberships[i].Aggregate == aggregate)
                {
                    return math.saturate(memberships[i].Loyalty);
                }
            }

            return 0f;
        }
    }
}

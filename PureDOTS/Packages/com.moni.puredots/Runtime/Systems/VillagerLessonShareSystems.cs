using PureDOTS.Runtime.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems
{
    internal static class VillagerLessonShareConstants
    {
        public const float FamilyShareFraction = 0.35f;
        public const float MentorShareFraction = 0.2f;
        public const float SquadShareFraction = 0.15f;
        public const float MentorShareIntervalSeconds = 20f;
        public const float SquadShareIntervalSeconds = 12f;
        public const float MentorShareRadius = 15f;
        public const float SquadShareRadius = 18f;
        public const int MaxLessonsPerPulse = 4;
    }

    [UpdateInGroup(typeof(VillagerSystemGroup), OrderFirst = true)]
    public partial struct VillagerFamilyLessonShareSystem : ISystem
    {
        private ComponentLookup<VillagerKnowledge> _knowledgeLookup;

        public void OnCreate(ref SystemState state)
        {
            _knowledgeLookup = state.GetComponentLookup<VillagerKnowledge>(true);
            state.RequireForUpdate<VillagerLessonShareState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            _knowledgeLookup.Update(ref state);
            var timeState = SystemAPI.GetSingleton<TimeState>();

            foreach (var (relationships, shareBuffer, shareState, entity) in SystemAPI
                         .Query<DynamicBuffer<VillagerRelationship>, DynamicBuffer<VillagerLessonShare>, RefRW<VillagerLessonShareState>>()
                         .WithEntityAccess())
            {
                if (shareState.ValueRO.HasFamilyApplied || relationships.Length == 0)
                {
                    continue;
                }

                var applied = VillagerLessonShareUtility.ApplyFamilyShares(entity, relationships, shareBuffer, _knowledgeLookup);
                if (!applied)
                {
                    continue;
                }

                var stateValue = shareState.ValueRO;
                stateValue.Flags |= VillagerLessonShareState.FlagFamilyApplied;
                stateValue.LastFamilyShareTick = timeState.Tick;
                shareState.ValueRW = stateValue;
            }
        }
    }

    [UpdateInGroup(typeof(VillagerJobFixedStepGroup))]
    // Removed invalid UpdateBefore: VillagerJobExecutionSystem runs in HotPathSystemGroup; cross-group ordering must be handled via group composition.
    public partial struct VillagerMentorLessonShareSystem : ISystem
    {
        private ComponentLookup<VillagerKnowledge> _knowledgeLookup;
        private ComponentLookup<LocalTransform> _transformLookup;

        public void OnCreate(ref SystemState state)
        {
            _knowledgeLookup = state.GetComponentLookup<VillagerKnowledge>(true);
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);

            state.RequireForUpdate<VillagerLessonShareState>();
            state.RequireForUpdate<TimeState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            _knowledgeLookup.Update(ref state);
            _transformLookup.Update(ref state);

            var timeState = SystemAPI.GetSingleton<TimeState>();
            var intervalTicks = VillagerLessonShareUtility.GetIntervalTicks(timeState.FixedDeltaTime, VillagerLessonShareConstants.MentorShareIntervalSeconds);
            var radiusSq = VillagerLessonShareConstants.MentorShareRadius * VillagerLessonShareConstants.MentorShareRadius;

            foreach (var (relationships, shareBufferRO, shareState, transform, entity) in SystemAPI
                         .Query<DynamicBuffer<VillagerRelationship>, DynamicBuffer<VillagerLessonShare>, RefRW<VillagerLessonShareState>, RefRO<LocalTransform>>()
                         .WithEntityAccess())
            {
                var shareBuffer = shareBufferRO;
                if (relationships.Length == 0)
                {
                    continue;
                }

                if (timeState.Tick - shareState.ValueRO.LastMentorShareTick < intervalTicks)
                {
                    continue;
                }

                var emitted = VillagerLessonShareUtility.ApplyRelationalShares(
                    relationships,
                    ref shareBuffer,
                    sourceType: VillagerLessonShareSource.Mentor,
                    targetTransform: transform.ValueRO,
                    radiusSq,
                    VillagerLessonShareConstants.MentorShareFraction,
                    _knowledgeLookup,
                    _transformLookup,
                    VillagerRelationshipTypes.Mentor);

                if (!emitted)
                {
                    continue;
                }

                var stateValue = shareState.ValueRO;
                stateValue.LastMentorShareTick = timeState.Tick;
                shareState.ValueRW = stateValue;
            }
        }
    }

    [UpdateInGroup(typeof(VillagerJobFixedStepGroup))]
    // Removed invalid UpdateBefore: VillagerJobExecutionSystem runs in HotPathSystemGroup; cross-group ordering must be handled via group composition.
    public partial struct VillagerSquadLessonShareSystem : ISystem
    {
        private ComponentLookup<VillagerKnowledge> _knowledgeLookup;
        private ComponentLookup<LocalTransform> _transformLookup;

        public void OnCreate(ref SystemState state)
        {
            _knowledgeLookup = state.GetComponentLookup<VillagerKnowledge>(true);
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);

            state.RequireForUpdate<VillagerLessonShareState>();
            state.RequireForUpdate<TimeState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            _knowledgeLookup.Update(ref state);
            _transformLookup.Update(ref state);

            var timeState = SystemAPI.GetSingleton<TimeState>();
            var intervalTicks = VillagerLessonShareUtility.GetIntervalTicks(timeState.FixedDeltaTime, VillagerLessonShareConstants.SquadShareIntervalSeconds);
            var radiusSq = VillagerLessonShareConstants.SquadShareRadius * VillagerLessonShareConstants.SquadShareRadius;

            foreach (var (relationships, shareBufferRO, shareState, transform, entity) in SystemAPI
                         .Query<DynamicBuffer<VillagerRelationship>, DynamicBuffer<VillagerLessonShare>, RefRW<VillagerLessonShareState>, RefRO<LocalTransform>>()
                         .WithEntityAccess())
            {
                var shareBuffer = shareBufferRO;
                if (relationships.Length == 0)
                {
                    continue;
                }

                if (timeState.Tick - shareState.ValueRO.LastSquadShareTick < intervalTicks)
                {
                    continue;
                }

                var emitted = VillagerLessonShareUtility.ApplyRelationalShares(
                    relationships,
                    ref shareBuffer,
                    sourceType: VillagerLessonShareSource.Squad,
                    targetTransform: transform.ValueRO,
                    radiusSq,
                    VillagerLessonShareConstants.SquadShareFraction,
                    _knowledgeLookup,
                    _transformLookup,
                    VillagerRelationshipTypes.Squad);

                if (!emitted)
                {
                    continue;
                }

                var stateValue = shareState.ValueRO;
                stateValue.LastSquadShareTick = timeState.Tick;
                shareState.ValueRW = stateValue;
            }
        }
    }

    internal static class VillagerLessonShareUtility
    {
        public static bool ApplyFamilyShares(
            Entity child,
            DynamicBuffer<VillagerRelationship> relationships,
            DynamicBuffer<VillagerLessonShare> shareBuffer,
            ComponentLookup<VillagerKnowledge> knowledgeLookup)
        {
            var applied = false;
            var parentsConsidered = 0;

            for (var i = 0; i < relationships.Length && parentsConsidered < 2; i++)
            {
                var relation = relationships[i];
                if (relation.RelationType != VillagerRelationshipTypes.Family)
                {
                    continue;
                }

                var parent = relation.OtherVillager;
                if (parent == Entity.Null || !knowledgeLookup.HasComponent(parent))
                {
                    continue;
                }

                var parentKnowledge = knowledgeLookup[parent];
                var affinity = RelationshipValueToAffinity(relation.RelationshipValue);
                if (affinity <= 0f)
                {
                    continue;
                }

                applied |= EmitLessonShares(
                    ref shareBuffer,
                    in parentKnowledge.Lessons,
                    affinity,
                    VillagerLessonShareConstants.FamilyShareFraction,
                    VillagerLessonShareSource.Family);

                parentsConsidered++;
            }

            return applied;
        }

        public static bool ApplyRelationalShares(
            DynamicBuffer<VillagerRelationship> relationships,
            ref DynamicBuffer<VillagerLessonShare> shareBuffer,
            VillagerLessonShareSource sourceType,
            in LocalTransform targetTransform,
            float radiusSq,
            float baseFraction,
            ComponentLookup<VillagerKnowledge> knowledgeLookup,
            ComponentLookup<LocalTransform> transformLookup,
            byte relationType)
        {
            var emitted = false;

            for (var i = 0; i < relationships.Length; i++)
            {
                var relation = relationships[i];
                if (relation.RelationType != relationType)
                {
                    continue;
                }

                var other = relation.OtherVillager;
                if (other == Entity.Null || !knowledgeLookup.HasComponent(other) || !transformLookup.HasComponent(other))
                {
                    continue;
                }

                var distanceSq = math.distancesq(targetTransform.Position, transformLookup[other].Position);
                if (distanceSq > radiusSq)
                {
                    continue;
                }

                var affinity = RelationshipValueToAffinity(relation.RelationshipValue);
                if (affinity <= 0f)
                {
                    continue;
                }

                var otherKnowledge = knowledgeLookup[other];
                emitted |= EmitLessonShares(
                    ref shareBuffer,
                    in otherKnowledge.Lessons,
                    affinity,
                    baseFraction,
                    sourceType);
            }

            return emitted;
        }

        public static bool EmitLessonShares(
            ref DynamicBuffer<VillagerLessonShare> shareBuffer,
            in FixedList32Bytes<VillagerLessonProgress> lessons,
            float affinity,
            float baseFraction,
            VillagerLessonShareSource source)
        {
            if (lessons.Length == 0 || affinity <= 0f)
            {
                return false;
            }

            var emitted = false;
            var maxLessons = math.min(VillagerLessonShareConstants.MaxLessonsPerPulse, lessons.Length);
            var emittedCount = 0;

            for (var i = 0; i < lessons.Length && emittedCount < maxLessons; i++)
            {
                var lesson = lessons[i];
                if (lesson.Progress <= 0f || lesson.LessonId.Length == 0)
                {
                    continue;
                }

                var contribution = math.saturate(lesson.Progress * baseFraction * affinity);
                if (contribution <= 0f)
                {
                    continue;
                }

                AccumulateShare(ref shareBuffer, in lesson.LessonId, contribution, source);
                emitted = true;
                emittedCount++;
            }

            return emitted;
        }

        public static void AccumulateShare(
            ref DynamicBuffer<VillagerLessonShare> buffer,
            in FixedString64Bytes lessonId,
            float value,
            VillagerLessonShareSource source)
        {
            if (lessonId.Length == 0 || value <= 0f)
            {
                return;
            }

            for (var i = 0; i < buffer.Length; i++)
            {
                var entry = buffer[i];
                if (!entry.LessonId.Equals(lessonId) || entry.Source != source)
                {
                    continue;
                }

                entry.Progress = math.saturate(entry.Progress + value);
                buffer[i] = entry;
                return;
            }

            buffer.Add(new VillagerLessonShare
            {
                LessonId = lessonId,
                Progress = math.saturate(value),
                Source = source
            });
        }

        public static uint GetIntervalTicks(float fixedDeltaTime, float intervalSeconds)
        {
            if (intervalSeconds <= 0f || fixedDeltaTime <= 0f)
            {
                return 1u;
            }

            var ticks = (uint)math.round(intervalSeconds / fixedDeltaTime);
            return math.max(1u, ticks);
        }

        private static float RelationshipValueToAffinity(float relationshipValue)
        {
            return math.saturate((relationshipValue + 100f) * 0.005f);
        }
    }
}

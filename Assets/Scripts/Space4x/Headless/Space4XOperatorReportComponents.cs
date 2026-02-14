using Unity.Collections;
using Unity.Entities;

namespace Space4X.Headless
{
    public struct Space4XOperatorReportTag : IComponentData
    {
    }

    public struct Space4XHeadlessAnswersFlushRequest : IComponentData
    {
        public uint RequestedTick;
    }

    [InternalBufferCapacity(8)]
    public struct Space4XOperatorBlackCat : IBufferElementData
    {
        public FixedString64Bytes Id;
        public Entity Primary;
        public Entity Secondary;
        public uint StartTick;
        public uint EndTick;
        public float MetricA;
        public float MetricB;
        public float MetricC;
        public float MetricD;
        public byte Classification;
    }

    [InternalBufferCapacity(16)]
    public struct Space4XOperatorMetric : IBufferElementData
    {
        public FixedString64Bytes Key;
        public float Value;
    }

    public static class Space4XOperatorReportUtility
    {
        public static bool TryGetBlackCatBuffer(ref SystemState state, out DynamicBuffer<Space4XOperatorBlackCat> buffer)
        {
            buffer = default;
            if (!state.EntityManager.WorldUnmanaged.IsCreated)
            {
                return false;
            }

            using var query = state.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<Space4XOperatorReportTag>());
            var entity = query.IsEmptyIgnoreFilter
                ? state.EntityManager.CreateEntity(typeof(Space4XOperatorReportTag))
                : query.GetSingletonEntity();

            if (!state.EntityManager.HasBuffer<Space4XOperatorBlackCat>(entity))
            {
                state.EntityManager.AddBuffer<Space4XOperatorBlackCat>(entity);
            }

            buffer = state.EntityManager.GetBuffer<Space4XOperatorBlackCat>(entity);
            return true;
        }

        public static bool TryGetMetricBuffer(ref SystemState state, out DynamicBuffer<Space4XOperatorMetric> buffer)
        {
            buffer = default;
            if (!state.EntityManager.WorldUnmanaged.IsCreated)
            {
                return false;
            }

            using var query = state.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<Space4XOperatorReportTag>());
            var entity = query.IsEmptyIgnoreFilter
                ? state.EntityManager.CreateEntity(typeof(Space4XOperatorReportTag))
                : query.GetSingletonEntity();

            if (!state.EntityManager.HasBuffer<Space4XOperatorMetric>(entity))
            {
                state.EntityManager.AddBuffer<Space4XOperatorMetric>(entity);
            }

            buffer = state.EntityManager.GetBuffer<Space4XOperatorMetric>(entity);
            return true;
        }

        public static void RequestHeadlessAnswersFlush(ref SystemState state, uint tick)
        {
            if (!state.EntityManager.WorldUnmanaged.IsCreated)
            {
                return;
            }

            using var query = state.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<Space4XOperatorReportTag>());
            var entity = query.IsEmptyIgnoreFilter
                ? state.EntityManager.CreateEntity(typeof(Space4XOperatorReportTag))
                : query.GetSingletonEntity();

            if (!state.EntityManager.HasComponent<Space4XHeadlessAnswersFlushRequest>(entity))
            {
                state.EntityManager.AddComponentData(entity, new Space4XHeadlessAnswersFlushRequest
                {
                    RequestedTick = tick
                });
                return;
            }

            var request = state.EntityManager.GetComponentData<Space4XHeadlessAnswersFlushRequest>(entity);
            request.RequestedTick = tick;
            state.EntityManager.SetComponentData(entity, request);
        }

        public static bool TryConsumeHeadlessAnswersFlushRequest(ref SystemState state, out uint requestedTick)
        {
            requestedTick = 0u;
            if (!state.EntityManager.WorldUnmanaged.IsCreated)
            {
                return false;
            }

            using var query = state.EntityManager.CreateEntityQuery(
                ComponentType.ReadOnly<Space4XOperatorReportTag>(),
                ComponentType.ReadOnly<Space4XHeadlessAnswersFlushRequest>());
            if (query.IsEmptyIgnoreFilter)
            {
                return false;
            }

            var entity = query.GetSingletonEntity();
            var request = state.EntityManager.GetComponentData<Space4XHeadlessAnswersFlushRequest>(entity);
            requestedTick = request.RequestedTick;
            state.EntityManager.RemoveComponent<Space4XHeadlessAnswersFlushRequest>(entity);
            return true;
        }
    }
}

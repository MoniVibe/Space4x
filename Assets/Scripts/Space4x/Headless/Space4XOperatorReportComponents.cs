using Unity.Collections;
using Unity.Entities;

namespace Space4X.Headless
{
    public struct Space4XOperatorReportTag : IComponentData
    {
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
    }
}

using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace Space4X.Registry
{
    /// <summary>
    /// Ensures carriers with a hull id receive their default segment assembly.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateBefore(typeof(Space4XCarrierModuleRefitSystem))]
    public partial struct Space4XCarrierHullSegmentBootstrapSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<HullCatalogSingleton>();
            state.RequireForUpdate<HullSegmentCatalogSingleton>();
            state.RequireForUpdate<CarrierHullId>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var changed = false;

            foreach (var (hullId, entity) in SystemAPI.Query<RefRO<CarrierHullId>>().WithNone<CarrierHullSegment>().WithEntityAccess())
            {
                if (!ModuleCatalogUtility.TryGetHullSpec(ref state, hullId.ValueRO.HullId, out var catalogRef, out var hullIndex))
                {
                    continue;
                }

                ref var hullSpec = ref catalogRef.Value.Hulls[hullIndex];
                ref var defaultSegments = ref hullSpec.DefaultSegmentIds;
                if (defaultSegments.Length == 0)
                {
                    continue;
                }

                var segmentBuffer = ecb.AddBuffer<CarrierHullSegment>(entity);
                for (int i = 0; i < defaultSegments.Length; i++)
                {
                    segmentBuffer.Add(new CarrierHullSegment
                    {
                        SegmentIndex = (byte)(i > 255 ? 255 : i),
                        SegmentId = defaultSegments[i]
                    });
                }

                changed = true;
            }

            if (changed)
            {
                ecb.Playback(state.EntityManager);
            }

            ecb.Dispose();
        }
    }
}

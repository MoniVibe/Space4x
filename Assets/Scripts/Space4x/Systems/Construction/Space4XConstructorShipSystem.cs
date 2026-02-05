using PureDOTS.Runtime.Components;
using Space4X.Registry;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Systems.Construction
{
    [BurstCompile]
    [UpdateInGroup(typeof(PureDOTS.Systems.ConstructionSystemGroup))]
    [UpdateAfter(typeof(PureDOTS.Systems.Construction.ConstructionSiteBufferEnsureSystem))]
    public partial struct Space4XConstructorShipSystem : ISystem
    {
        private EntityQuery _siteQuery;
        private ComponentLookup<LocalTransform> _transformLookup;
        private BufferLookup<ConstructionProgressCommand> _progressLookup;
        private ComponentLookup<ConstructionSiteId> _siteIdLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ConstructorShipTag>();
            state.RequireForUpdate<ConstructionSiteProgress>();
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();

            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _progressLookup = state.GetBufferLookup<ConstructionProgressCommand>(false);
            _siteIdLookup = state.GetComponentLookup<ConstructionSiteId>(true);

            _siteQuery = SystemAPI.QueryBuilder()
                .WithAll<ConstructionSiteProgress, LocalTransform>()
                .Build();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var time = SystemAPI.GetSingleton<TimeState>();
            if (time.IsPaused)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewind) ||
                rewind.Mode != RewindMode.Record)
            {
                return;
            }

            var deltaTime = SystemAPI.TryGetSingleton<GameplayFixedStep>(out var fixedStep)
                ? fixedStep.FixedDeltaTime
                : (float)SystemAPI.Time.DeltaTime;

            _transformLookup.Update(ref state);
            _progressLookup.Update(ref state);
            _siteIdLookup.Update(ref state);

            using var siteEntities = _siteQuery.ToEntityArray(Allocator.Temp);
            using var siteTransforms = _siteQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);

            if (siteEntities.Length == 0)
            {
                return;
            }

            foreach (var (rig, shipTransform, entity) in SystemAPI
                .Query<RefRO<ConstructionRig>, RefRO<LocalTransform>>()
                .WithAll<ConstructorShipTag>()
                .WithEntityAccess())
            {
                var range = math.max(0f, rig.ValueRO.RangeMeters);
                if (range <= 0f)
                {
                    continue;
                }

                var rangeSq = range * range;
                var bestIndex = -1;
                var bestDistSq = float.MaxValue;

                for (int i = 0; i < siteEntities.Length; i++)
                {
                    var distSq = math.distancesq(shipTransform.ValueRO.Position, siteTransforms[i].Position);
                    if (distSq <= rangeSq && distSq < bestDistSq)
                    {
                        bestDistSq = distSq;
                        bestIndex = i;
                    }
                }

                if (bestIndex < 0)
                {
                    continue;
                }

                var siteEntity = siteEntities[bestIndex];
                if (!_progressLookup.HasBuffer(siteEntity))
                {
                    continue;
                }

                var delta = math.max(0f, rig.ValueRO.BuildRatePerSecond) * math.max(0f, deltaTime);
                if (delta <= 0f)
                {
                    continue;
                }

                var siteId = _siteIdLookup.HasComponent(siteEntity) ? _siteIdLookup[siteEntity].Value : 0;
                var buffer = _progressLookup[siteEntity];
                buffer.Add(new ConstructionProgressCommand
                {
                    SiteId = siteId,
                    Delta = delta
                });
            }
        }
    }
}

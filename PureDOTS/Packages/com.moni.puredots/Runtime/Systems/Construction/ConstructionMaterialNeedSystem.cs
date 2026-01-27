using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Construction;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Construction
{
    /// <summary>
    /// Recomputes outstanding material needs per construction site (hauling bulletin).
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(ConstructionSystemGroup))]
    [UpdateAfter(typeof(ConstructionProgressSystem))]
    public partial struct ConstructionMaterialNeedSystem : ISystem
    {
        private BufferLookup<ConstructionCostElement> _costLookup;
        private BufferLookup<ConstructionDeliveredElement> _deliveredLookup;
        private BufferLookup<ConstructionMaterialNeed> _needLookup;
        private ComponentLookup<ConstructionSitePhaseSettings> _phaseSettingsLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<ResourceTypeIndex>();

            _costLookup = state.GetBufferLookup<ConstructionCostElement>(true);
            _deliveredLookup = state.GetBufferLookup<ConstructionDeliveredElement>(true);
            _needLookup = state.GetBufferLookup<ConstructionMaterialNeed>(false);
            _phaseSettingsLookup = state.GetComponentLookup<ConstructionSitePhaseSettings>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused
                || !SystemAPI.TryGetSingleton<RewindState>(out var rewindState)
                || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var resourceIndex = SystemAPI.GetSingleton<ResourceTypeIndex>();
            if (!resourceIndex.Catalog.IsCreated)
            {
                return;
            }

            _costLookup.Update(ref state);
            _deliveredLookup.Update(ref state);
            _needLookup.Update(ref state);
            _phaseSettingsLookup.Update(ref state);

            ref var catalog = ref resourceIndex.Catalog.Value;

            foreach (var (progress, entity) in SystemAPI.Query<RefRO<ConstructionSiteProgress>>().WithEntityAccess())
            {
                if (!_costLookup.HasBuffer(entity))
                {
                    continue;
                }

                if (!_needLookup.HasBuffer(entity))
                {
                    state.EntityManager.AddBuffer<ConstructionMaterialNeed>(entity);
                }

                var needs = _needLookup[entity];
                needs.Clear();

                var costs = _costLookup[entity];
                var delivered = _deliveredLookup.HasBuffer(entity)
                    ? _deliveredLookup[entity]
                    : default;

                byte basePriority = 128;
                if (_phaseSettingsLookup.HasComponent(entity))
                {
                    var settings = _phaseSettingsLookup[entity];
                    if (settings.LogisticsPriority > 0)
                    {
                        basePriority = settings.LogisticsPriority;
                    }
                }

                for (int i = 0; i < costs.Length; i++)
                {
                    var cost = costs[i];
                    var resourceIndexId = catalog.LookupIndex(cost.ResourceTypeId);
                    if (resourceIndexId < 0 || resourceIndexId > ushort.MaxValue)
                    {
                        continue;
                    }

                    var deliveredUnits = GetDeliveredUnits(delivered, cost.ResourceTypeId);
                    var outstanding = math.max(0f, cost.UnitsRequired - deliveredUnits);
                    if (outstanding <= 0.01f)
                    {
                        continue;
                    }

                    needs.Add(new ConstructionMaterialNeed
                    {
                        ResourceTypeIndex = (ushort)resourceIndexId,
                        OutstandingUnits = outstanding,
                        Priority = basePriority
                    });
                }
            }
        }

        private static float GetDeliveredUnits(DynamicBuffer<ConstructionDeliveredElement> delivered, in FixedString64Bytes resourceId)
        {
            if (!delivered.IsCreated)
            {
                return 0f;
            }

            for (int i = 0; i < delivered.Length; i++)
            {
                if (delivered[i].ResourceTypeId.Equals(resourceId))
                {
                    return delivered[i].UnitsDelivered;
                }
            }

            return 0f;
        }
    }
}



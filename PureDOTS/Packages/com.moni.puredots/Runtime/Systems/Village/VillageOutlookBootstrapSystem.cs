using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Village;
using PureDOTS.Runtime.Villagers;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Adds default outlook/policy components to villages based on alignment until bespoke data is authored.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(VillageJobPreferenceSystem))]
    public partial struct VillageOutlookBootstrapSystem : ISystem
    {
        private EntityQuery _villageQuery;
        private ComponentLookup<VillagerAlignment> _alignmentLookup;
        private ComponentLookup<VillageOutlook> _outlookLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _villageQuery = state.GetEntityQuery(ComponentType.ReadOnly<PureDOTS.Runtime.Village.VillageId>());
            state.RequireForUpdate(_villageQuery);
            _alignmentLookup = state.GetComponentLookup<VillagerAlignment>(true);
            _outlookLookup = state.GetComponentLookup<VillageOutlook>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;

            var noOutlookQuery = SystemAPI.QueryBuilder()
                .WithAll<PureDOTS.Runtime.Village.VillageId>()
                .WithNone<VillageOutlook>()
                .Build();
            using (var entities = noOutlookQuery.ToEntityArray(Allocator.Temp))
            {
                foreach (var e in entities)
                {
                    em.AddComponentData(e, new VillageOutlook { Flags = VillageOutlookFlags.None });
                }
            }

            var noPolicyQuery = SystemAPI.QueryBuilder()
                .WithAll<PureDOTS.Runtime.Village.VillageId>()
                .WithNone<VillageWorkforcePolicy>()
                .Build();
            using (var entities = noPolicyQuery.ToEntityArray(Allocator.Temp))
            {
                foreach (var e in entities)
                {
                    em.AddComponentData(e, new VillageWorkforcePolicy
                    {
                        ConscriptionUrgency = 0f,
                        DefenseUrgency = 0f,
                        ConscriptionActive = 0
                    });
                }
            }

            var noAlignmentQuery = SystemAPI.QueryBuilder()
                .WithAll<PureDOTS.Runtime.Village.VillageId>()
                .WithNone<VillagerAlignment>()
                .Build();
            using (var entities = noAlignmentQuery.ToEntityArray(Allocator.Temp))
            {
                foreach (var e in entities)
                {
                    em.AddComponentData(e, new VillagerAlignment());
                }
            }

            _alignmentLookup.Update(ref state);
            _outlookLookup.Update(ref state);

            foreach (var (villageId, entity) in SystemAPI.Query<RefRO<PureDOTS.Runtime.Village.VillageId>>().WithEntityAccess())
            {
                if (!_outlookLookup.HasComponent(entity))
                {
                    continue;
                }

                var flags = VillageOutlookFlags.None;
                if (_alignmentLookup.HasComponent(entity))
                {
                    var alignment = _alignmentLookup[entity];
                    if (alignment.MaterialismNormalized > 0.25f)
                    {
                        flags |= VillageOutlookFlags.Materialistic;
                    }
                    if (alignment.MaterialismNormalized < -0.25f)
                    {
                        flags |= VillageOutlookFlags.Ascetic;
                    }
                    if (alignment.PurityNormalized < -0.25f && math.abs(alignment.OrderNormalized) > 0.2f)
                    {
                        flags |= VillageOutlookFlags.Warlike;
                    }
                    if (alignment.OrderNormalized > 0.3f)
                    {
                        flags |= VillageOutlookFlags.Expansionist;
                    }
                }

                var outlook = _outlookLookup[entity];
                outlook.Flags = flags;
                _outlookLookup[entity] = outlook;
            }
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }
    }
}

using PureDOTS.Runtime.Alignment;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Ships;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Ships
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    // Removed invalid UpdateAfter: CoreSingletonBootstrapSystem executes in TimeSystemGroup; ordering is governed at the group level.
    public partial struct CrewAggregationSystem : ISystem
    {
        private EntityQuery _missingComplianceQuery;
        private EntityQuery _missingSamplesQuery;
        private EntityQuery _missingAlertsQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CrewAggregate>();
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            _missingComplianceQuery = SystemAPI.QueryBuilder()
                .WithAll<CrewAggregate>()
                .WithNone<CrewCompliance>()
                .Build();

            _missingSamplesQuery = SystemAPI.QueryBuilder()
                .WithAll<CrewAggregate>()
                .WithNone<CrewAlignmentSample>()
                .Build();

            _missingAlertsQuery = SystemAPI.QueryBuilder()
                .WithAll<CrewAggregate>()
                .WithNone<ComplianceAlert>()
                .Build();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (SystemAPI.GetSingleton<RewindState>().Mode != RewindMode.Record)
            {
                return;
            }

            if (!_missingComplianceQuery.IsEmptyIgnoreFilter)
            {
                state.EntityManager.AddComponent(_missingComplianceQuery, ComponentType.ReadWrite<CrewCompliance>());
            }

            if (!_missingSamplesQuery.IsEmptyIgnoreFilter)
            {
                state.EntityManager.AddComponent(_missingSamplesQuery, ComponentType.ReadWrite<CrewAlignmentSample>());
            }

            if (!_missingAlertsQuery.IsEmptyIgnoreFilter)
            {
                state.EntityManager.AddComponent(_missingAlertsQuery, ComponentType.ReadWrite<ComplianceAlert>());
            }

            var thresholds = SystemAPI.TryGetSingleton<ComplianceThresholds>(out var singletonThresholds)
                ? singletonThresholds
                : ComplianceThresholds.CreateDefault();

            foreach (var (_, complianceRef, samples, alerts, entity) in SystemAPI
                         .Query<RefRO<CrewAggregate>, RefRW<CrewCompliance>, DynamicBuffer<CrewAlignmentSample>, DynamicBuffer<ComplianceAlert>>()
                         .WithEntityAccess())
            {
                ref var compliance = ref complianceRef.ValueRW;
                alerts.Clear();

                float loyaltySum = 0f;
                float suspicionSum = 0f;
                float fanaticismSum = 0f;
                var count = 0;
                var missingData = (byte)(samples.Length == 0 ? 1 : 0);
                DoctrineId doctrine = compliance.Doctrine;
                AffiliationId affiliation = compliance.Affiliation;

                if (SystemAPI.HasComponent<DoctrineRef>(entity))
                {
                    doctrine = state.EntityManager.GetComponentData<DoctrineRef>(entity).Id;
                }

                for (int i = 0; i < samples.Length; i++)
                {
                    var sample = samples[i];
                    if (sample.Affiliation.Value.Length == 0)
                    {
                        missingData = 1;
                        continue;
                    }

                    loyaltySum += math.clamp(sample.Loyalty, 0f, 1f);
                    suspicionSum += math.max(0f, sample.Suspicion);
                    fanaticismSum += math.max(0f, sample.Fanaticism);
                    count++;
                    affiliation = sample.Affiliation;
                    if (doctrine.Value.Length == 0 && sample.Doctrine.Value.Length > 0)
                    {
                        doctrine = sample.Doctrine;
                    }
                }

                var avgLoyalty = count > 0 ? loyaltySum / count : 0f;
                var avgSuspicion = count > 0 ? suspicionSum / count : 0f;
                var avgFanaticism = count > 0 ? fanaticismSum / count : 0f;
                var delta = avgSuspicion - compliance.AverageSuspicion;

                var status = ComplianceStatus.Nominal;
                if (missingData != 0 || count == 0 || doctrine.Value.Length == 0)
                {
                    status = ComplianceStatus.Warning;
                }

                if (avgLoyalty <= thresholds.LoyaltyBreach || delta >= thresholds.SuspicionDeltaBreach)
                {
                    status = ComplianceStatus.Breach;
                }
                else if (avgLoyalty <= thresholds.LoyaltyWarning || delta >= thresholds.SuspicionDeltaWarning)
                {
                    status = (ComplianceStatus)math.max((int)status, (int)ComplianceStatus.Warning);
                }

                compliance.Affiliation = affiliation;
                compliance.Doctrine = doctrine;
                compliance.AverageLoyalty = avgLoyalty;
                compliance.AverageSuspicion = avgSuspicion;
                compliance.AverageFanaticism = avgFanaticism;
                compliance.SuspicionDelta = delta;
                compliance.Status = status;
                compliance.MissingData = missingData;
                compliance.LastUpdateTick = timeState.Tick;

                if (status != ComplianceStatus.Nominal)
                {
                    alerts.Add(new ComplianceAlert
                    {
                        Affiliation = affiliation,
                        Doctrine = doctrine,
                        Status = status,
                        SuspicionDelta = delta,
                        Loyalty = avgLoyalty,
                        Suspicion = avgSuspicion,
                        MissingData = missingData
                    });
                }
            }
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }
    }
}

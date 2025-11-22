using PureDOTS.Runtime.Components;
using PureDOTS.Systems;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine.Assertions;

namespace Space4X.Registry
{
    /// <summary>
    /// Evaluates entity affiliations against doctrine expectations to surface mutiny/desertion/independence signals.
    /// </summary>
    [UpdateInGroup(typeof(SpatialSystemGroup))]
    [UpdateBefore(typeof(Space4XRegistryBridgeSystem))]
    public partial struct Space4XAffiliationComplianceSystem : ISystem
    {
        private EntityQuery _evaluationQuery;
        private ComponentLookup<DoctrineProfile> _doctrineLookup;
        private BufferLookup<DoctrineAxisExpectation> _axisExpectationLookup;
        private BufferLookup<DoctrineOutlookExpectation> _outlookExpectationLookup;
        private ComponentLookup<ContractBinding> _contractLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();

            _doctrineLookup = state.GetComponentLookup<DoctrineProfile>(true);
            _axisExpectationLookup = state.GetBufferLookup<DoctrineAxisExpectation>(true);
            _outlookExpectationLookup = state.GetBufferLookup<DoctrineOutlookExpectation>(true);
            _contractLookup = state.GetComponentLookup<ContractBinding>(true);

            _evaluationQuery = SystemAPI.QueryBuilder()
                .WithAll<AlignmentTriplet>()
                .WithAllRW<AffiliationTag>()
                .Build();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (_evaluationQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            _doctrineLookup.Update(ref state);
            _axisExpectationLookup.Update(ref state);
            _outlookExpectationLookup.Update(ref state);
            _contractLookup.Update(ref state);

            var timeState = SystemAPI.GetSingleton<TimeState>();

            foreach (var (alignment, affiliations, entity) in SystemAPI.Query<RefRO<AlignmentTriplet>, DynamicBuffer<AffiliationTag>>().WithEntityAccess())
            {
                var alignmentValues = alignment.ValueRO.AsFloat3();
                var chaosScore = AlignmentMath.Chaos(alignment.ValueRO);
                var lawfulnessScore = AlignmentMath.Lawfulness(alignment.ValueRO);

                var topOutlooks = TopThreeOutlooks.Empty;
                if (SystemAPI.HasBuffer<OutlookEntry>(entity))
                {
                    topOutlooks.Populate(SystemAPI.GetBuffer<OutlookEntry>(entity));
                }
                else if (SystemAPI.HasBuffer<TopOutlook>(entity))
                {
                    topOutlooks.Populate(SystemAPI.GetBuffer<TopOutlook>(entity));
                }

                var hasAxisBuffer = SystemAPI.HasBuffer<EthicAxisValue>(entity);
                var axisBuffer = hasAxisBuffer ? SystemAPI.GetBuffer<EthicAxisValue>(entity) : default;

                bool hasBreachBuffer = SystemAPI.HasBuffer<ComplianceBreach>(entity);
                DynamicBuffer<ComplianceBreach> breachBuffer = default;
                if (hasBreachBuffer)
                {
                    breachBuffer = SystemAPI.GetBuffer<ComplianceBreach>(entity);
                    breachBuffer.Clear();
                }

                var isSpy = SystemAPI.HasComponent<SpyRole>(entity);

                if (!isSpy)
                {
                    DecaySuspicion(ref state, entity, 0.05f);
                }

                bool contractActive = _contractLookup.HasComponent(entity) && _contractLookup[entity].ExpirationTick > timeState.Tick;

                for (int i = 0; i < affiliations.Length; i++)
                {
                    var affiliation = affiliations[i];
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Assert.IsTrue(affiliation.Target == Entity.Null || _doctrineLookup.HasComponent(affiliation.Target),
                        $"Affiliation target {affiliation.Target.Index} missing DoctrineProfile for entity {entity.Index}");
#endif
                    if (!_doctrineLookup.HasComponent(affiliation.Target))
                    {
                        continue;
                    }

                    var doctrine = _doctrineLookup[affiliation.Target];
                    var alignmentDeviation = doctrine.AlignmentWindow.ComputeDeviation(alignmentValues);
                    var axisDeviation = EvaluateAxisDeviation(affiliation.Target, hasAxisBuffer, axisBuffer, (float)doctrine.AxisTolerance);
                    var outlookDeviation = EvaluateOutlookDeviation(affiliation.Target, ref topOutlooks, (float)doctrine.OutlookTolerance);

                    var totalDeviation = math.csum(alignmentDeviation) + axisDeviation + outlookDeviation;
                    if (totalDeviation <= 0.0001f)
                    {
                        continue;
                    }

                    var loyalty = math.saturate((float)affiliation.Loyalty);
                    var severity = totalDeviation * math.lerp(1f, 0.35f, loyalty);

                    if (contractActive && lawfulnessScore >= (float)doctrine.LawfulContractFloor && !isSpy)
                    {
                        continue;
                    }

                    if (isSpy)
                    {
                        var suspicionGain = math.max(0.01f, (float)doctrine.SuspicionGain);
                        ApplySuspicion(ref state, entity, severity * suspicionGain);
                        continue;
                    }

                    var warAxisValue = hasAxisBuffer ? GetAxisValue(EthicAxisId.War, axisBuffer) : 0f;
                    var breachType = DetermineBreachType(chaosScore, lawfulnessScore, warAxisValue, (float)doctrine.ChaosMutinyThreshold, (float)doctrine.LawfulContractFloor);

                    if (!hasBreachBuffer)
                    {
                        breachBuffer = state.EntityManager.AddBuffer<ComplianceBreach>(entity);
                        hasBreachBuffer = true;
                    }

                    breachBuffer.Add(new ComplianceBreach
                    {
                        Affiliation = affiliation.Target,
                        Type = breachType,
                        Severity = (half)math.saturate(severity)
                    });
                }
            }
        }

        private float EvaluateAxisDeviation(Entity doctrineEntity, bool hasAxisBuffer, DynamicBuffer<EthicAxisValue> axisBuffer, float tolerance)
        {
            if (!_axisExpectationLookup.HasBuffer(doctrineEntity))
            {
                return 0f;
            }

            var expectations = _axisExpectationLookup[doctrineEntity];
            if (expectations.Length == 0)
            {
                return 0f;
            }

            float deviation = 0f;
            for (int i = 0; i < expectations.Length; i++)
            {
                var expectation = expectations[i];
                var value = hasAxisBuffer ? GetAxisValue(expectation.Axis, axisBuffer) : 0f;

                float axisDeviation = 0f;
                var min = (float)expectation.Min;
                var max = (float)expectation.Max;

                if (value < min)
                {
                    axisDeviation = min - value;
                }
                else if (value > max)
                {
                    axisDeviation = value - max;
                }

                axisDeviation = math.max(0f, axisDeviation - tolerance);
                deviation += axisDeviation;
            }

            return deviation;
        }

        private float EvaluateOutlookDeviation(Entity doctrineEntity, ref TopThreeOutlooks topOutlooks, float tolerance)
        {
            if (!_outlookExpectationLookup.HasBuffer(doctrineEntity))
            {
                return 0f;
            }

            var expectations = _outlookExpectationLookup[doctrineEntity];
            if (expectations.Length == 0)
            {
                return 0f;
            }

            float deviation = 0f;
            for (int i = 0; i < expectations.Length; i++)
            {
                var expectation = expectations[i];
                if (!topOutlooks.TryGet(expectation.OutlookId, out var weight))
                {
                    var missing = (float)expectation.MinimumWeight;
                    if (missing > tolerance)
                    {
                        deviation += missing - tolerance;
                    }
                    continue;
                }

                var required = (float)expectation.MinimumWeight;
                var shortfall = required - weight;
                if (shortfall > tolerance)
                {
                    deviation += shortfall - tolerance;
                }
            }

            return deviation;
        }

        private static ComplianceBreachType DetermineBreachType(float chaos, float lawfulness, float warAxis, float chaosThreshold, float lawfulFloor)
        {
            if (chaos > chaosThreshold)
            {
                return warAxis > 0.25f ? ComplianceBreachType.Desertion : ComplianceBreachType.Independence;
            }

            if (lawfulness >= lawfulFloor)
            {
                return ComplianceBreachType.Mutiny;
            }

            return warAxis <= 0f ? ComplianceBreachType.Independence : ComplianceBreachType.Desertion;
        }

        private static float GetAxisValue(EthicAxisId axisId, DynamicBuffer<EthicAxisValue> axisBuffer)
        {
            for (int i = 0; i < axisBuffer.Length; i++)
            {
                if (axisBuffer[i].Axis == axisId)
                {
                    return (float)axisBuffer[i].Value;
                }
            }

            return 0f;
        }

        private void ApplySuspicion(ref SystemState state, Entity entity, float delta)
        {
            var suspicion = EnsureSuspicion(ref state, entity);
            var value = math.saturate((float)suspicion.ValueRO.Value + delta);
            suspicion.ValueRW.Value = (half)value;
        }

        private void DecaySuspicion(ref SystemState state, Entity entity, float decay)
        {
            if (!SystemAPI.HasComponent<SuspicionScore>(entity))
            {
                return;
            }

            var suspicion = SystemAPI.GetComponentRW<SuspicionScore>(entity);
            var value = math.max(0f, (float)suspicion.ValueRO.Value - decay);
            suspicion.ValueRW.Value = (half)value;
        }

        private RefRW<SuspicionScore> EnsureSuspicion(ref SystemState state, Entity entity)
        {
            if (!SystemAPI.HasComponent<SuspicionScore>(entity))
            {
                state.EntityManager.AddComponentData(entity, new SuspicionScore { Value = (half)0f });
            }

            return SystemAPI.GetComponentRW<SuspicionScore>(entity);
        }

        private struct OutlookSample
        {
            public OutlookId Id;
            public float Weight;
            public float Magnitude;
        }

        private struct TopThreeOutlooks
        {
            public OutlookSample First;
            public OutlookSample Second;
            public OutlookSample Third;
            public int Count;

            public static TopThreeOutlooks Empty => default;

            public void Populate(DynamicBuffer<OutlookEntry> buffer)
            {
                for (int i = 0; i < buffer.Length; i++)
                {
                    Consider(buffer[i].OutlookId, (float)buffer[i].Weight);
                }
            }

            public void Populate(DynamicBuffer<TopOutlook> buffer)
            {
                for (int i = 0; i < buffer.Length; i++)
                {
                    Consider(buffer[i].OutlookId, (float)buffer[i].Weight);
                }
            }

            public void Consider(OutlookId outlookId, float weight)
            {
                var sample = new OutlookSample
                {
                    Id = outlookId,
                    Weight = weight,
                    Magnitude = math.abs(weight)
                };

                if (TryReplaceExisting(ref sample))
                {
                    return;
                }

                Insert(ref sample);
            }

            public bool TryGet(OutlookId outlookId, out float weight)
            {
                if (Count > 0 && First.Id == outlookId)
                {
                    weight = First.Weight;
                    return true;
                }

                if (Count > 1 && Second.Id == outlookId)
                {
                    weight = Second.Weight;
                    return true;
                }

                if (Count > 2 && Third.Id == outlookId)
                {
                    weight = Third.Weight;
                    return true;
                }

                weight = 0f;
                return false;
            }

            private bool TryReplaceExisting(ref OutlookSample sample)
            {
                if (Count > 0 && First.Id == sample.Id)
                {
                    if (sample.Magnitude > First.Magnitude)
                    {
                        First = sample;
                    }
                    return true;
                }

                if (Count > 1 && Second.Id == sample.Id)
                {
                    if (sample.Magnitude > Second.Magnitude)
                    {
                        Second = sample;
                    }
                    return true;
                }

                if (Count > 2 && Third.Id == sample.Id)
                {
                    if (sample.Magnitude > Third.Magnitude)
                    {
                        Third = sample;
                    }
                    return true;
                }

                return false;
            }

            private void Insert(ref OutlookSample sample)
            {
                if (Count == 0)
                {
                    First = sample;
                    Count = 1;
                    return;
                }

                if (sample.Magnitude > First.Magnitude)
                {
                    Third = Second;
                    Second = First;
                    First = sample;
                    Count = math.min(Count + 1, 3);
                    return;
                }

                if (Count < 2)
                {
                    Second = sample;
                    Count = math.min(Count + 1, 3);
                    return;
                }

                if (sample.Magnitude > Second.Magnitude)
                {
                    Third = Second;
                    Second = sample;
                    Count = math.min(Count + 1, 3);
                    return;
                }

                if (Count < 3)
                {
                    Third = sample;
                    Count = 3;
                    return;
                }

                if (sample.Magnitude > Third.Magnitude)
                {
                    Third = sample;
                }
            }
        }
    }
}

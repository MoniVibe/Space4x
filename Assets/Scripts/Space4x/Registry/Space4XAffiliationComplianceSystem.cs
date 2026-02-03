using PureDOTS.Runtime.Components;
using PureDOTS.Systems;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

using SpatialSystemGroup = PureDOTS.Systems.SpatialSystemGroup;

namespace Space4X.Registry
{
    using Debug = UnityEngine.Debug;

    
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
        private BufferLookup<DoctrineStanceExpectation> _stanceExpectationLookup;
        private ComponentLookup<ContractBinding> _contractLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();

            _doctrineLookup = state.GetComponentLookup<DoctrineProfile>(true);
            _axisExpectationLookup = state.GetBufferLookup<DoctrineAxisExpectation>(true);
            _stanceExpectationLookup = state.GetBufferLookup<DoctrineStanceExpectation>(true);
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
            _stanceExpectationLookup.Update(ref state);
            _contractLookup.Update(ref state);

            var timeState = SystemAPI.GetSingleton<TimeState>();

            foreach (var (alignment, affiliations, entity) in SystemAPI.Query<RefRO<AlignmentTriplet>, DynamicBuffer<AffiliationTag>>().WithEntityAccess())
            {
                var alignmentValues = alignment.ValueRO.AsFloat3();
                var chaosScore = AlignmentMath.Chaos(alignment.ValueRO);
                var lawfulnessScore = AlignmentMath.Lawfulness(alignment.ValueRO);

                var topStances = TopThreeStances.Empty;
                if (SystemAPI.HasBuffer<StanceEntry>(entity))
                {
                    topStances.Populate(SystemAPI.GetBuffer<StanceEntry>(entity));
                }
                else if (SystemAPI.HasBuffer<TopStance>(entity))
                {
                    topStances.Populate(SystemAPI.GetBuffer<TopStance>(entity));
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

                bool hasTicketBuffer = SystemAPI.HasBuffer<ComplianceTicket>(entity);
                DynamicBuffer<ComplianceTicket> ticketBuffer = default;
                if (hasTicketBuffer)
                {
                    ticketBuffer = SystemAPI.GetBuffer<ComplianceTicket>(entity);
                    ticketBuffer.Clear();
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
                    if (!_doctrineLookup.HasComponent(affiliation.Target))
                    {
#if UNITY_EDITOR && SPACE4X_DEBUG_AFFILIATION
                        UnityEngine.Debug.LogWarning(
                            $"[AffiliationCompliance] Target {affiliation.Target.Index} missing DoctrineProfile for entity {entity.Index}");
#endif
                        // Skip this entry, don't crash the sim
                        continue;
                    }

                    var doctrine = _doctrineLookup[affiliation.Target];
                    var alignmentDeviation = doctrine.AlignmentWindow.ComputeDeviation(alignmentValues);
                    var axisDeviation = EvaluateAxisDeviation(affiliation.Target, hasAxisBuffer, axisBuffer, (float)doctrine.AxisTolerance);
                    var stanceDeviation = EvaluateStanceDeviation(affiliation.Target, ref topStances, (float)doctrine.StanceTolerance);

                    var totalDeviation = math.csum(alignmentDeviation) + axisDeviation + stanceDeviation;
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

                    if (!hasTicketBuffer)
                    {
                        ticketBuffer = state.EntityManager.AddBuffer<ComplianceTicket>(entity);
                        hasTicketBuffer = true;
                    }

                    ticketBuffer.Add(new ComplianceTicket
                    {
                        Affiliation = affiliation.Target,
                        Type = breachType,
                        Severity = (half)math.saturate(severity),
                        Tick = timeState.Tick
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

        private float EvaluateStanceDeviation(Entity doctrineEntity, ref TopThreeStances topStances, float tolerance)
        {
            if (!_stanceExpectationLookup.HasBuffer(doctrineEntity))
            {
                return 0f;
            }

            var expectations = _stanceExpectationLookup[doctrineEntity];
            if (expectations.Length == 0)
            {
                return 0f;
            }

            float deviation = 0f;
            for (int i = 0; i < expectations.Length; i++)
            {
                var expectation = expectations[i];
                if (!topStances.TryGet(expectation.StanceId, out var weight))
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

        private struct StanceSample
        {
            public StanceId Id;
            public float Weight;
            public float Magnitude;
        }

        private struct TopThreeStances
        {
            public StanceSample First;
            public StanceSample Second;
            public StanceSample Third;
            public int Count;

            public static TopThreeStances Empty => default;

            public void Populate(DynamicBuffer<StanceEntry> buffer)
            {
                for (int i = 0; i < buffer.Length; i++)
                {
                    Consider(buffer[i].StanceId, (float)buffer[i].Weight);
                }
            }

            public void Populate(DynamicBuffer<TopStance> buffer)
            {
                for (int i = 0; i < buffer.Length; i++)
                {
                    Consider(buffer[i].StanceId, (float)buffer[i].Weight);
                }
            }

            public void Consider(StanceId StanceId, float weight)
            {
                var sample = new StanceSample
                {
                    Id = StanceId,
                    Weight = weight,
                    Magnitude = math.abs(weight)
                };

                if (TryReplaceExisting(ref sample))
                {
                    return;
                }

                Insert(ref sample);
            }

            public bool TryGet(StanceId StanceId, out float weight)
            {
                if (Count > 0 && First.Id == StanceId)
                {
                    weight = First.Weight;
                    return true;
                }

                if (Count > 1 && Second.Id == StanceId)
                {
                    weight = Second.Weight;
                    return true;
                }

                if (Count > 2 && Third.Id == StanceId)
                {
                    weight = Third.Weight;
                    return true;
                }

                weight = 0f;
                return false;
            }

            private bool TryReplaceExisting(ref StanceSample sample)
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

            private void Insert(ref StanceSample sample)
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



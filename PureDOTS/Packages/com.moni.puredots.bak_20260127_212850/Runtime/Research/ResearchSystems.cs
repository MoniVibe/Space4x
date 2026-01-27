using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Research
{
    public partial struct ResearchTelemetryBootstrapSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            var query = state.GetEntityQuery(ComponentType.ReadOnly<ResearchTelemetry>());
            if (query.IsEmptyIgnoreFilter)
            {
                var entity = state.EntityManager.CreateEntity(typeof(ResearchTelemetry));
                state.EntityManager.SetComponentData(entity, default(ResearchTelemetry));
            }

            state.Enabled = false;
        }

        public void OnUpdate(ref SystemState state)
        {
        }
    }

    public partial struct ResearchModuleBufferBootstrapSystem : ISystem
    {
        private EntityQuery _missingRequestQuery;
        private EntityQuery _missingResultQuery;

        public void OnCreate(ref SystemState state)
        {
            _missingRequestQuery = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<CarrierResearchModuleTag>() },
                None = new[] { ComponentType.ReadOnly<ResearchTransferRequest>() }
            });

            _missingResultQuery = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<CarrierResearchModuleTag>(), ComponentType.ReadOnly<ResearchTransferRequest>() },
                None = new[] { ComponentType.ReadOnly<ResearchTransferResult>() }
            });
        }

        public void OnUpdate(ref SystemState state)
        {
            var anyMissing = !_missingRequestQuery.IsEmptyIgnoreFilter || !_missingResultQuery.IsEmptyIgnoreFilter;
            if (!anyMissing)
                return;

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            var missingRequests = _missingRequestQuery.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < missingRequests.Length; i++)
            {
                ecb.AddBuffer<ResearchTransferRequest>(missingRequests[i]);
            }
            missingRequests.Dispose();

            var missingResults = _missingResultQuery.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < missingResults.Length; i++)
            {
                ecb.AddBuffer<ResearchTransferResult>(missingResults[i]);
            }
            missingResults.Dispose();

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }

    [BurstCompile]
    [UpdateAfter(typeof(ResearchModuleBufferBootstrapSystem))]
    public partial struct ResearchAutoAssignmentSystem : ISystem
    {
        private EntityQuery _anomalyQuery;
        private uint _nextAnomalyIndex;

        public void OnCreate(ref SystemState state)
        {
            _anomalyQuery = state.GetEntityQuery(ComponentType.ReadOnly<AnomalyConfig>(), ComponentType.ReadOnly<AnomalyState>());
        }

        public void OnUpdate(ref SystemState state)
        {
            if (_anomalyQuery.IsEmptyIgnoreFilter)
                return;

            var anomalies = _anomalyQuery.ToEntityArray(Allocator.Temp);
            if (anomalies.Length == 0)
                return;

            foreach (var assignment in SystemAPI.Query<RefRW<CarrierResearchAssignment>>().WithAll<CarrierResearchModuleTag>())
            {
                if (assignment.ValueRO.TargetAnomaly != Entity.Null)
                    continue;

                var index = (int)math.min(anomalies.Length - 1, _nextAnomalyIndex % (uint)anomalies.Length);
                assignment.ValueRW.TargetAnomaly = anomalies[index];
                assignment.ValueRW.NextHarvestInTicks = math.max(assignment.ValueRO.CooldownTicks, 0.5f);
                _nextAnomalyIndex++;
            }

            anomalies.Dispose();
        }
    }

    [BurstCompile]
    public partial struct ResearchBandwidthRefillSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ResearchBandwidthState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var deltaTime = SystemAPI.Time.DeltaTime;
            foreach (var bandwidth in SystemAPI.Query<RefRW<ResearchBandwidthState>>())
            {
                var value = bandwidth.ValueRO;
                if (value.RefillPerTick <= 0f)
                    continue;

                value.CurrentBandwidth = math.min(value.Capacity, value.CurrentBandwidth + value.RefillPerTick * deltaTime);
                bandwidth.ValueRW = value;
            }
        }
    }

    [BurstCompile]
    [UpdateAfter(typeof(ResearchBandwidthRefillSystem))]
    public partial struct CarrierAnomalyHarvestSystem : ISystem
    {
        private ComponentLookup<AnomalyConfig> _anomalyConfigLookup;
        private ComponentLookup<AnomalyState> _anomalyStateLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CarrierResearchModuleTag>();
            _anomalyConfigLookup = state.GetComponentLookup<AnomalyConfig>(true);
            _anomalyStateLookup = state.GetComponentLookup<AnomalyState>(false);
        }

        public void OnUpdate(ref SystemState state)
        {
            var deltaTime = SystemAPI.Time.DeltaTime;
            _anomalyConfigLookup.Update(ref state);
            _anomalyStateLookup.Update(ref state);

            var hasTelemetry = SystemAPI.TryGetSingletonRW<ResearchTelemetry>(out var telemetry);

            foreach (var (assignment, bandwidth, requests) in SystemAPI.Query<RefRW<CarrierResearchAssignment>, RefRW<ResearchBandwidthState>, DynamicBuffer<ResearchTransferRequest>>().WithAll<CarrierResearchModuleTag>())
            {
                if (assignment.ValueRO.TargetAnomaly == Entity.Null)
                    continue;

                if (!_anomalyConfigLookup.HasComponent(assignment.ValueRO.TargetAnomaly) ||
                    !_anomalyStateLookup.HasComponent(assignment.ValueRO.TargetAnomaly))
                {
                    assignment.ValueRW.TargetAnomaly = Entity.Null;
                    continue;
                }

                if (assignment.ValueRO.NextHarvestInTicks > 0f)
                {
                    assignment.ValueRW.NextHarvestInTicks = math.max(0f, assignment.ValueRO.NextHarvestInTicks - deltaTime);
                    continue;
                }

                var anomalyEntity = assignment.ValueRO.TargetAnomaly;
                var config = _anomalyConfigLookup[anomalyEntity];
                var anomalyState = _anomalyStateLookup.GetRefRW(anomalyEntity);

                if (anomalyState.ValueRO.RemainingCharge <= 0f)
                {
                    if (config.IsPermanent == 0)
                    {
                        assignment.ValueRW.TargetAnomaly = Entity.Null;
                    }
                    else
                    {
                        assignment.ValueRW.NextHarvestInTicks = math.max(assignment.ValueRO.CooldownTicks, 0.25f);
                    }
                    continue;
                }

                if (bandwidth.ValueRO.CurrentBandwidth < config.BandwidthCost)
                {
                    assignment.ValueRW.NextHarvestInTicks = math.max(assignment.ValueRO.CooldownTicks, 0.25f);
                    continue;
                }

                var extracted = math.min(config.BaseYieldPerTick, anomalyState.ValueRO.RemainingCharge);
                anomalyState.ValueRW.RemainingCharge = math.max(0f, anomalyState.ValueRO.RemainingCharge - extracted);
                anomalyState.ValueRW.ActiveHarvesters = (byte)math.min(255, anomalyState.ValueRO.ActiveHarvesters + 1);

                var bandwidthValue = bandwidth.ValueRO;
                bandwidthValue.CurrentBandwidth = math.max(0f, bandwidthValue.CurrentBandwidth - config.BandwidthCost);
                bandwidth.ValueRW = bandwidthValue;

                var request = new ResearchTransferRequest
                {
                    SourceAnomaly = anomalyEntity,
                    RequestedPoints = extracted,
                    LossPerUnit = math.clamp(bandwidthValue.LossFraction, 0f, 1f)
                };
                requests.Add(request);

                assignment.ValueRW.NextHarvestInTicks = math.max(assignment.ValueRO.CooldownTicks, 0.1f);

                if (hasTelemetry)
                {
                    telemetry.ValueRW.CompletedHarvests++;
                    telemetry.ValueRW.TotalBandwidthUsed += config.BandwidthCost;
                }
            }
        }
    }

    [BurstCompile]
    [UpdateAfter(typeof(CarrierAnomalyHarvestSystem))]
    public partial struct ResearchTransferProcessingSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ResearchTransferRequest>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var hasTelemetry = SystemAPI.TryGetSingletonRW<ResearchTelemetry>(out var telemetry);

            foreach (var (harvest, requests, results) in SystemAPI.Query<RefRW<ResearchHarvestState>, DynamicBuffer<ResearchTransferRequest>, DynamicBuffer<ResearchTransferResult>>())
            {
                results.Clear();

                for (int i = 0; i < requests.Length; i++)
                {
                    var req = requests[i];
                    var loss = math.clamp(req.LossPerUnit, 0f, 1f) * req.RequestedPoints;
                    var delivered = math.max(0f, req.RequestedPoints - loss);

                    harvest.ValueRW.PendingRawPoints += delivered;

                    results.Add(new ResearchTransferResult
                    {
                        SourceAnomaly = req.SourceAnomaly,
                        DeliveredPoints = delivered,
                        LostPoints = loss
                    });

                    if (hasTelemetry)
                    {
                        telemetry.ValueRW.TotalLoss += loss;
                    }
                }

                requests.Clear();
            }
        }
    }

    [BurstCompile]
    public partial struct AnomalyRechargeSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<AnomalyState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var deltaTime = SystemAPI.Time.DeltaTime;
            foreach (var (config, anomalyState) in SystemAPI.Query<RefRO<AnomalyConfig>, RefRW<AnomalyState>>())
            {
                anomalyState.ValueRW.ActiveHarvesters = 0;
                if (config.ValueRO.IsPermanent == 0)
                    continue;

                var nextCharge = anomalyState.ValueRO.RemainingCharge + config.ValueRO.RechargePerTick * deltaTime;
                anomalyState.ValueRW.RemainingCharge = math.min(config.ValueRO.ChargeCapacity, nextCharge);
            }
        }
    }

    [BurstCompile]
    [UpdateAfter(typeof(ResearchTransferProcessingSystem))]
    public partial struct ResearchRefinementSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ResearchHarvestState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            foreach (var harvest in SystemAPI.Query<RefRW<ResearchHarvestState>>())
            {
                if (harvest.ValueRO.PendingRawPoints <= 0f)
                    continue;

                harvest.ValueRW.RefinedPoints += harvest.ValueRO.PendingRawPoints;
                harvest.ValueRW.PendingRawPoints = 0f;
            }
        }
    }

}

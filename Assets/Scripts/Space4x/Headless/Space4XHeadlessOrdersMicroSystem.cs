using System;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Core;
using PureDOTS.Runtime.Scenarios;
using Space4X.Registry;
using Space4x.Scenario;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Space4X.Headless
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4X.Systems.AI.Space4XAICommandQueueSystem))]
    public partial struct Space4XHeadlessOrdersMicroSystem : ISystem
    {
        private const int RejectNone = 0;
        private const int RejectInvalidTarget = 1;

        private byte _enabled;
        private byte _done;
        private byte _rejectScenario;
        private byte _hasStatus;
        private Entity _probe;
        private AIOrderStatus _lastStatus;
        private uint _transitionCount;
        private byte _commandConsumed;
        private int _rejectReason;
        private int _expectedRejectReason;

        public void OnCreate(ref SystemState state)
        {
            if (!RuntimeMode.IsHeadless || !Application.isBatchMode)
            {
                state.Enabled = false;
                return;
            }

            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<Space4XScenarioRuntime>();
            state.RequireForUpdate<ScenarioInfo>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (_done != 0)
            {
                return;
            }

            var scenarioId = SystemAPI.GetSingleton<ScenarioInfo>().ScenarioId.ToString();
            if (_enabled == 0)
            {
                if (scenarioId.Equals("space4x_orders_micro", StringComparison.OrdinalIgnoreCase))
                {
                    _rejectScenario = 0;
                    _expectedRejectReason = RejectNone;
                    _enabled = 1;
                }
                else if (scenarioId.Equals("space4x_orders_reject_micro", StringComparison.OrdinalIgnoreCase))
                {
                    _rejectScenario = 1;
                    _expectedRejectReason = RejectInvalidTarget;
                    _enabled = 1;
                }
                else
                {
                    state.Enabled = false;
                    return;
                }
            }

            var timeState = SystemAPI.GetSingleton<TimeState>();
            var runtime = SystemAPI.GetSingleton<Space4XScenarioRuntime>();

            EnsureProbeEntity(ref state, timeState.Tick);
            if (_probe == Entity.Null || !state.EntityManager.HasBuffer<AIOrder>(_probe))
            {
                return;
            }

            var orders = state.EntityManager.GetBuffer<AIOrder>(_probe);
            if (orders.Length == 0)
            {
                return;
            }

            var order = orders[0];
            if (_hasStatus == 0)
            {
                _lastStatus = order.Status;
                _hasStatus = 1;
            }
            else if (_lastStatus != order.Status)
            {
                _transitionCount++;
                _lastStatus = order.Status;
            }

            if (_commandConsumed == 0 && order.Status != AIOrderStatus.Pending)
            {
                _commandConsumed = 1;
            }

            if (_rejectScenario != 0 && _rejectReason == RejectNone)
            {
                if (order.Type == AIOrderType.Engage && order.TargetEntity == Entity.Null)
                {
                    order.Status = AIOrderStatus.Failed;
                    orders[0] = order;
                    _rejectReason = RejectInvalidTarget;
                    if (_commandConsumed == 0)
                    {
                        _commandConsumed = 1;
                    }
                    if (_transitionCount == 0)
                    {
                        _transitionCount = 1;
                    }
                }
            }

            if (timeState.Tick < runtime.EndTick)
            {
                return;
            }

            EmitMetrics(ref state);
            _done = 1;
        }

        private void EnsureProbeEntity(ref SystemState state, uint tick)
        {
            if (_probe != Entity.Null && state.EntityManager.Exists(_probe))
            {
                return;
            }

            _probe = state.EntityManager.CreateEntity();
            state.EntityManager.SetName(_probe, "Space4XOrdersMicroProbe");
            state.EntityManager.AddComponentData(_probe, new AICommandQueue
            {
                LastProcessedTick = 0
            });
            var orders = state.EntityManager.AddBuffer<AIOrder>(_probe);

            var order = new AIOrder
            {
                Type = _rejectScenario != 0 ? AIOrderType.Engage : AIOrderType.Patrol,
                Status = AIOrderStatus.Pending,
                TargetEntity = _rejectScenario != 0 ? Entity.Null : Entity.Null,
                TargetPosition = _rejectScenario != 0 ? float3.zero : new float3(40f, 0f, 0f),
                IssuerEntity = Entity.Null,
                IssueTick = tick,
                ExpirationTick = tick + 600,
                Priority = 200,
                ThreatTolerance = (half)0.6f
            };
            orders.Add(order);
        }

        private void EmitMetrics(ref SystemState state)
        {
            if (!Space4XOperatorReportUtility.TryGetMetricBuffer(ref state, out var buffer))
            {
                return;
            }

            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.orders.command_consumed"), _commandConsumed);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.orders.state_transition_count"), _transitionCount);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.orders.reject_reason"), _rejectReason);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.orders.expected_reject_reason"), _expectedRejectReason);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.orders.last_status"), (int)_lastStatus);
        }

        private static void AddOrUpdateMetric(
            DynamicBuffer<Space4XOperatorMetric> buffer,
            FixedString64Bytes key,
            float value)
        {
            for (var i = 0; i < buffer.Length; i++)
            {
                var metric = buffer[i];
                if (!metric.Key.Equals(key))
                {
                    continue;
                }

                metric.Value = value;
                buffer[i] = metric;
                return;
            }

            buffer.Add(new Space4XOperatorMetric
            {
                Key = key,
                Value = value
            });
        }
    }
}

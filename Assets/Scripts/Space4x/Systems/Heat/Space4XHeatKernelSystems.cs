using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Perception;
using PureDOTS.Runtime.Power;
using PureDOTS.Systems;
using PureDOTS.Systems.Perception;
using Space4X.Perception;
using Space4X.Physics;
using Space4X.Runtime;
using Space4x.Fleetcrawl;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using PowerHeatState = PureDOTS.Runtime.Power.HeatState;

namespace Space4X.Systems.Heat
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct Space4XHeatKernelBootstrapSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            if (!SystemAPI.TryGetSingletonEntity<Space4XHeatKernelConfig>(out _))
            {
                var configEntity = em.CreateEntity();
                em.AddComponentData(configEntity, Space4XHeatKernelConfig.Default);
            }

            var config = SystemAPI.GetSingleton<Space4XHeatKernelConfig>();
            var query = SystemAPI.QueryBuilder()
                .WithAll<SensorSignature>()
                .WithNone<Prefab>()
                .Build();
            using var entities = query.ToEntityArray(Allocator.Temp);

            for (var i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                if (!em.HasComponent<Space4XHeatKernelState>(entity))
                {
                    TryAddComponent(em, entity, new Space4XHeatKernelState
                    {
                        CurrentHeat = 0f,
                        HeatCapacity = math.max(1f, config.DefaultHeatCapacity),
                        DissipationPerSecond = math.max(0f, config.DefaultDissipationPerSecond)
                    });
                }

                if (!em.HasComponent<Space4XHeatKernelOutput>(entity))
                {
                    TryAddComponent(em, entity, new Space4XHeatKernelOutput
                    {
                        Thermal01 = 0f,
                        CurrentHeat = 0f,
                        HeatCapacity = math.max(1f, config.DefaultHeatCapacity),
                        GeneratedPerSecond = 0f,
                        DissipationPerSecond = math.max(0f, config.DefaultDissipationPerSecond),
                        PowerLoad01 = 0f,
                        ConsumerHeat01 = 0f,
                        Speed = 0f,
                        SourceKind = Space4XHeatKernelSourceKind.Generic,
                        IsSaturated = 0
                    });
                }

                // Do not auto-add action event buffers on live entities.
                // High-density archetypes can already be at chunk-size limits.
            }
        }

        private static void TryAddComponent<T>(EntityManager entityManager, Entity entity, in T data)
            where T : unmanaged, IComponentData
        {
            try
            {
                entityManager.AddComponentData(entity, data);
            }
            catch (System.InvalidOperationException)
            {
                // Some entities are already at chunk-size limits; skip optional bootstrap data.
            }
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(PerceptionSystemGroup))]
    [UpdateBefore(typeof(Space4XSignatureTelemetryUpdateSystem))]
    public partial struct Space4XHeatKernelUpdateSystem : ISystem
    {
        private ComponentLookup<PowerLedger> _ledgerLookup;
        private BufferLookup<ShipPowerConsumer> _shipConsumerLookup;
        private ComponentLookup<PowerConsumer> _powerConsumerLookup;
        private ComponentLookup<PowerHeatState> _consumerHeatLookup;
        private ComponentLookup<SpaceVelocity> _spaceVelocityLookup;
        private ComponentLookup<Space4XFrameTransform> _frameTransformLookup;
        private ComponentLookup<FleetcrawlHeatOutputState> _fleetcrawlHeatLookup;
        private BufferLookup<Space4XHeatKernelActionEvent> _actionLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<Space4XHeatKernelConfig>();

            _ledgerLookup = state.GetComponentLookup<PowerLedger>(true);
            _shipConsumerLookup = state.GetBufferLookup<ShipPowerConsumer>(true);
            _powerConsumerLookup = state.GetComponentLookup<PowerConsumer>(true);
            _consumerHeatLookup = state.GetComponentLookup<PowerHeatState>(true);
            _spaceVelocityLookup = state.GetComponentLookup<SpaceVelocity>(true);
            _frameTransformLookup = state.GetComponentLookup<Space4XFrameTransform>(true);
            _fleetcrawlHeatLookup = state.GetComponentLookup<FleetcrawlHeatOutputState>(true);
            _actionLookup = state.GetBufferLookup<Space4XHeatKernelActionEvent>(false);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var time = SystemAPI.GetSingleton<TimeState>();
            if (time.IsPaused)
            {
                return;
            }

            var config = SystemAPI.GetSingleton<Space4XHeatKernelConfig>();
            var deltaTime = math.max(0.0001f, time.FixedDeltaTime > 0f ? time.FixedDeltaTime : time.DeltaTime);

            _ledgerLookup.Update(ref state);
            _shipConsumerLookup.Update(ref state);
            _powerConsumerLookup.Update(ref state);
            _consumerHeatLookup.Update(ref state);
            _spaceVelocityLookup.Update(ref state);
            _frameTransformLookup.Update(ref state);
            _fleetcrawlHeatLookup.Update(ref state);
            _actionLookup.Update(ref state);

            foreach (var (heatStateRef, heatOutputRef, entity) in
                     SystemAPI.Query<RefRW<Space4XHeatKernelState>, RefRW<Space4XHeatKernelOutput>>()
                         .WithAll<SensorSignature>()
                         .WithNone<Prefab>()
                         .WithEntityAccess())
            {
                var heatState = heatStateRef.ValueRO;
                if (heatState.HeatCapacity <= 0f)
                {
                    heatState.HeatCapacity = math.max(1f, config.DefaultHeatCapacity);
                }

                if (heatState.DissipationPerSecond <= 0f)
                {
                    heatState.DissipationPerSecond = math.max(0f, config.DefaultDissipationPerSecond);
                }

                var powerLoad01 = ResolvePowerLoad01(entity, out var consumerHeat01);
                var speed01 = ResolveSpeed01(entity, out var speed);
                var actionHeatPerSecond = ConsumeActionHeatPerSecond(entity);

                var generatedPerSecond =
                    math.max(0f, config.BaselineHeatPerSecond) +
                    math.max(0f, config.PowerLoadHeatPerSecond) * powerLoad01 +
                    math.max(0f, config.ConsumerHeatPerSecond) * consumerHeat01 +
                    math.max(0f, config.SpeedHeatPerSecond) * speed01 +
                    math.max(0f, actionHeatPerSecond);

                var heatCapacity = math.max(1f, heatState.HeatCapacity);
                var dissipationPerSecond = math.max(0f, heatState.DissipationPerSecond);
                var maxHeat = heatCapacity * math.max(1f, config.MaxHeatRatio);
                heatState.CurrentHeat = math.clamp(
                    heatState.CurrentHeat + (generatedPerSecond - dissipationPerSecond) * deltaTime,
                    0f,
                    maxHeat);

                var thermalFromState = math.saturate(heatState.CurrentHeat / heatCapacity);
                var thermalFloor = math.saturate(
                    powerLoad01 * math.max(0f, config.ThermalFloorFromPower) +
                    speed01 * math.max(0f, config.ThermalFloorFromSpeed));
                var resolvedThermal01 = math.max(thermalFromState, thermalFloor);
                var sourceKind = Space4XHeatKernelSourceKind.Generic;

                if (_fleetcrawlHeatLookup.HasComponent(entity))
                {
                    var fleetcrawlThermal01 = math.saturate(FleetcrawlHeatResolver.ResolveHeatSignature01(_fleetcrawlHeatLookup[entity]));
                    resolvedThermal01 = math.max(
                        fleetcrawlThermal01,
                        resolvedThermal01 * math.clamp(config.GenericBlendWhenFleetcrawl, 0f, 1f));
                    sourceKind = Space4XHeatKernelSourceKind.Fleetcrawl;
                }

                var previousThermal = math.saturate(heatOutputRef.ValueRO.Thermal01);
                var responseLerp = math.clamp(config.ResponseLerp, 0.02f, 1f);
                var thermal01 = math.lerp(previousThermal, resolvedThermal01, responseLerp);
                var isSaturated = heatState.CurrentHeat >= heatCapacity ? (byte)1 : (byte)0;

                heatStateRef.ValueRW = heatState;
                heatOutputRef.ValueRW = new Space4XHeatKernelOutput
                {
                    Thermal01 = thermal01,
                    CurrentHeat = heatState.CurrentHeat,
                    HeatCapacity = heatCapacity,
                    GeneratedPerSecond = generatedPerSecond,
                    DissipationPerSecond = dissipationPerSecond,
                    PowerLoad01 = powerLoad01,
                    ConsumerHeat01 = consumerHeat01,
                    Speed = speed,
                    SourceKind = sourceKind,
                    IsSaturated = isSaturated
                };
            }
        }

        private float ResolvePowerLoad01(Entity entity, out float consumerHeat01)
        {
            consumerHeat01 = 0f;
            var heatSampleSum = 0f;
            var heatSamples = 0;

            var allocatedSum = 0f;
            var requestedSum = 0f;

            if (_shipConsumerLookup.HasBuffer(entity))
            {
                var consumers = _shipConsumerLookup[entity];
                for (var i = 0; i < consumers.Length; i++)
                {
                    var consumerEntity = consumers[i].Consumer;
                    if (consumerEntity == Entity.Null)
                    {
                        continue;
                    }

                    if (_powerConsumerLookup.HasComponent(consumerEntity))
                    {
                        var consumer = _powerConsumerLookup[consumerEntity];
                        var requested = consumer.RequestedDraw > 0f ? consumer.RequestedDraw : consumer.BaselineDraw;
                        requested = math.max(0.001f, requested);
                        var allocated = math.max(0f, consumer.AllocatedDraw);
                        requestedSum += requested;
                        allocatedSum += allocated;
                    }

                    if (_consumerHeatLookup.HasComponent(consumerEntity))
                    {
                        var heat = _consumerHeatLookup[consumerEntity];
                        if (heat.MaxHeatCapacity > 1e-5f)
                        {
                            heatSampleSum += math.saturate(heat.CurrentHeat / heat.MaxHeatCapacity);
                            heatSamples++;
                        }
                    }
                }
            }

            var loadFromConsumers = requestedSum > 0.001f
                ? math.saturate(allocatedSum / requestedSum)
                : 0f;
            if (heatSamples > 0)
            {
                consumerHeat01 = math.saturate(heatSampleSum / heatSamples);
            }

            var loadFromLedger = 0f;
            if (_ledgerLookup.HasComponent(entity))
            {
                var ledger = _ledgerLookup[entity];
                var available = math.max(1f, math.max(ledger.DistributionOutputMW, ledger.GenerationMW) + math.max(0f, ledger.BatteryDischargeMW));
                loadFromLedger = math.saturate(math.max(0f, ledger.TotalAllocatedMW) / available);
            }

            var powerLoad01 = math.saturate(math.max(loadFromConsumers, loadFromLedger));
            if (consumerHeat01 <= 1e-5f && powerLoad01 > 0f)
            {
                consumerHeat01 = math.saturate(powerLoad01 * 0.35f);
            }

            return powerLoad01;
        }

        private float ResolveSpeed01(Entity entity, out float speed)
        {
            speed = 0f;

            if (_spaceVelocityLookup.HasComponent(entity))
            {
                speed = math.length(_spaceVelocityLookup[entity].Linear);
            }
            else if (_frameTransformLookup.HasComponent(entity))
            {
                speed = math.length((float3)_frameTransformLookup[entity].VelocityWorld);
            }

            return math.saturate(speed / (speed + 12f));
        }

        private float ConsumeActionHeatPerSecond(Entity entity)
        {
            if (!_actionLookup.HasBuffer(entity))
            {
                return 0f;
            }

            var actions = _actionLookup[entity];
            var total = 0f;
            for (var i = 0; i < actions.Length; i++)
            {
                var action = actions[i];
                var scale = action.Scale <= 0f ? 1f : action.Scale;
                total += math.max(0f, action.HeatPerSecond * scale);
            }

            if (actions.Length > 0)
            {
                actions.Clear();
            }

            return total;
        }
    }
}

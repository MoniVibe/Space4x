using System;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Perception;
using PureDOTS.Runtime.Power;
using PureDOTS.Systems;
using PureDOTS.Systems.Perception;
using Space4X.Physics;
using Space4X.Runtime;
using Space4x.Fleetcrawl;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Perception
{
    public struct Space4XSignatureTelemetryConfig : IComponentData
    {
        public uint UpdateCadenceTicks;
        public float MinSignature;
        public float MaxSignature;
        public float PowerToEmWeight;
        public float SensorToEmWeight;
        public float WeaponToEmWeight;
        public float HeatToThermalWeight;
        public float MassToGraviticWeight;
        public float SpeedToGraviticWeight;
        public float OverclockToPsiWeight;
        public float ExoticBaselineFloor;
        public float ParanormalBaselineFloor;

        public static Space4XSignatureTelemetryConfig Default => new Space4XSignatureTelemetryConfig
        {
            UpdateCadenceTicks = 4u,
            MinSignature = 0.01f,
            MaxSignature = 4f,
            PowerToEmWeight = 0.85f,
            SensorToEmWeight = 0.9f,
            WeaponToEmWeight = 0.45f,
            HeatToThermalWeight = 0.9f,
            MassToGraviticWeight = 0.8f,
            SpeedToGraviticWeight = 0.65f,
            OverclockToPsiWeight = 0.35f,
            ExoticBaselineFloor = 0.08f,
            ParanormalBaselineFloor = 0f
        };
    }

    public struct Space4XSignatureTelemetry : IComponentData
    {
        public byte Initialized;
        public uint LastUpdatedTick;
        public float BaseVisualSignature;
        public float BaseAuditorySignature;
        public float BaseOlfactorySignature;
        public float BaseEMSignature;
        public float BaseGraviticSignature;
        public float BaseExoticSignature;
        public float BaseParanormalSignature;
        public float PowerLoad01;
        public float EmPassive01;
        public float EmActive01;
        public float Thermal01;
        public float Mass01;
        public float Psi01;
        public float Speed;
        public float MassTons;
    }

    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(Space4XPerceptionBootstrapSystem))]
    public partial struct Space4XSignatureTelemetryBootstrapSystem : ISystem
    {
        private EntityQuery _missingTelemetryQuery;
        private NativeParallelHashSet<Entity> _overflowEntities;
        private bool _overflowWarned;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            _missingTelemetryQuery = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<SensorSignature>() },
                None = new[]
                {
                    ComponentType.ReadOnly<Space4XSignatureTelemetry>(),
                    ComponentType.ReadOnly<Prefab>()
                }
            });
            _overflowEntities = new NativeParallelHashSet<Entity>(128, Allocator.Persistent);
            _overflowWarned = false;
        }

        public void OnDestroy(ref SystemState state)
        {
            if (_overflowEntities.IsCreated)
            {
                _overflowEntities.Dispose();
            }
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingletonEntity<Space4XSignatureTelemetryConfig>(out _))
            {
                var configEntity = state.EntityManager.CreateEntity();
                state.EntityManager.AddComponentData(configEntity, Space4XSignatureTelemetryConfig.Default);
            }

            var em = state.EntityManager;
            if (_missingTelemetryQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            using var entities = _missingTelemetryQuery.ToEntityArray(Allocator.Temp);
            using var signatures = _missingTelemetryQuery.ToComponentDataArray<SensorSignature>(Allocator.Temp);
            var count = math.min(entities.Length, signatures.Length);
            for (var i = 0; i < count; i++)
            {
                var entity = entities[i];
                if (_overflowEntities.Contains(entity))
                {
                    continue;
                }

                try
                {
                    em.AddComponentData(entity, CreateTelemetry(signatures[i]));
                }
                catch (InvalidOperationException ex) when (IsArchetypeCapacityException(ex))
                {
                    _overflowEntities.Add(entity);
                    if (!_overflowWarned)
                    {
                        _overflowWarned = true;
                        UnityEngine.Debug.LogWarning("[Space4XSignatureTelemetryBootstrapSystem] Skipping telemetry add on oversize archetype entities.");
                    }
                }
            }
        }

        private static bool IsArchetypeCapacityException(InvalidOperationException ex)
        {
            if (ex == null || string.IsNullOrWhiteSpace(ex.Message))
            {
                return false;
            }

            var message = ex.Message;
            return message.IndexOf("Entity archetype component data is too large", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   message.IndexOf("Maximum chunk size", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        internal static Space4XSignatureTelemetry CreateTelemetry(in SensorSignature signature)
        {
            return new Space4XSignatureTelemetry
            {
                Initialized = 1,
                LastUpdatedTick = 0u,
                BaseVisualSignature = math.max(0f, signature.VisualSignature),
                BaseAuditorySignature = math.max(0f, signature.AuditorySignature),
                BaseOlfactorySignature = math.max(0f, signature.OlfactorySignature),
                BaseEMSignature = math.max(0.01f, signature.EMSignature),
                BaseGraviticSignature = math.max(0.01f, signature.GraviticSignature),
                BaseExoticSignature = math.max(0f, signature.ExoticSignature),
                BaseParanormalSignature = math.max(0f, signature.ParanormalSignature),
                PowerLoad01 = 0f,
                EmPassive01 = 0f,
                EmActive01 = 0f,
                Thermal01 = 0f,
                Mass01 = 0f,
                Psi01 = 0f,
                Speed = 0f,
                MassTons = 0f
            };
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(PerceptionSystemGroup))]
    [UpdateBefore(typeof(PerceptionUpdateSystem))]
    public partial struct Space4XSignatureTelemetryUpdateSystem : ISystem
    {
        private ComponentLookup<PowerLedger> _ledgerLookup;
        private BufferLookup<ShipPowerConsumer> _shipConsumerLookup;
        private ComponentLookup<PowerConsumer> _powerConsumerLookup;
        private ComponentLookup<Space4XHeatKernelOutput> _heatKernelOutputLookup;
        private ComponentLookup<FleetcrawlHeatOutputState> _heatOutputLookup;
        private ComponentLookup<VesselPhysicalProperties> _physicalLookup;
        private ComponentLookup<SpaceVelocity> _spaceVelocityLookup;
        private ComponentLookup<Space4XFrameTransform> _frameTransformLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<Space4XSignatureTelemetryConfig>();

            _ledgerLookup = state.GetComponentLookup<PowerLedger>(true);
            _shipConsumerLookup = state.GetBufferLookup<ShipPowerConsumer>(true);
            _powerConsumerLookup = state.GetComponentLookup<PowerConsumer>(true);
            _heatKernelOutputLookup = state.GetComponentLookup<Space4XHeatKernelOutput>(true);
            _heatOutputLookup = state.GetComponentLookup<FleetcrawlHeatOutputState>(true);
            _physicalLookup = state.GetComponentLookup<VesselPhysicalProperties>(true);
            _spaceVelocityLookup = state.GetComponentLookup<SpaceVelocity>(true);
            _frameTransformLookup = state.GetComponentLookup<Space4XFrameTransform>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var time = SystemAPI.GetSingleton<TimeState>();
            if (time.IsPaused)
            {
                return;
            }

            var config = SystemAPI.GetSingleton<Space4XSignatureTelemetryConfig>();
            var cadenceTicks = math.max(1u, config.UpdateCadenceTicks);

            _ledgerLookup.Update(ref state);
            _shipConsumerLookup.Update(ref state);
            _powerConsumerLookup.Update(ref state);
            _heatKernelOutputLookup.Update(ref state);
            _heatOutputLookup.Update(ref state);
            _physicalLookup.Update(ref state);
            _spaceVelocityLookup.Update(ref state);
            _frameTransformLookup.Update(ref state);

            var routingBinding = ShipPowerRoutingBinding.Default;
            var routingRuntime = ShipPowerRoutingRuntime.Default;
            var hasBinding = SystemAPI.TryGetSingleton(out routingBinding);
            var hasRuntime = SystemAPI.TryGetSingleton(out routingRuntime);
            var hasPlayerRouting = hasBinding && hasRuntime && routingBinding.Ship != Entity.Null;
            var playerShip = hasPlayerRouting ? routingBinding.Ship : Entity.Null;

            foreach (var (signatureRef, telemetryRef, entity) in
                     SystemAPI.Query<RefRW<SensorSignature>, RefRW<Space4XSignatureTelemetry>>()
                         .WithNone<Prefab>()
                         .WithEntityAccess())
            {
                var signature = signatureRef.ValueRO;
                var telemetry = telemetryRef.ValueRO;
                if (telemetry.Initialized == 0)
                {
                    telemetry = Space4XSignatureTelemetryBootstrapSystem.CreateTelemetry(signature);
                }

                if (telemetry.LastUpdatedTick != 0u && (time.Tick - telemetry.LastUpdatedTick) < cadenceTicks)
                {
                    continue;
                }

                var powerLoad01 = ResolvePowerLoad01(entity, out var sensorLoad01, out var weaponLoad01, out var engineLoad01, out var reactorLoad01);
                var thermal01 = ResolveThermal01(entity, powerLoad01);
                var mass01 = ResolveMass01(entity, out var massTons);
                var speed01 = ResolveSpeed01(entity, out var speed);
                var overclock01 = 0f;

                if (hasPlayerRouting && entity == playerShip)
                {
                    var routingOverclock01 = math.saturate(math.max(0f, routingRuntime.OverclockPercent) / 500f);
                    overclock01 = math.max(overclock01, routingOverclock01);
                    sensorLoad01 = math.max(sensorLoad01, math.saturate(routingRuntime.SensorsFactor / 1.8f));
                    weaponLoad01 = math.max(weaponLoad01, math.saturate(routingRuntime.WeaponsFactor / 1.8f));
                    engineLoad01 = math.max(engineLoad01, math.saturate(routingRuntime.EnginesFactor / 1.8f));
                    reactorLoad01 = math.max(reactorLoad01, math.saturate(routingRuntime.ReactorFactor / 1.8f));
                }

                var emPassive01 = math.saturate(powerLoad01 * config.PowerToEmWeight + thermal01 * 0.2f);
                var emActive01 = math.saturate(sensorLoad01 * config.SensorToEmWeight + weaponLoad01 * config.WeaponToEmWeight + reactorLoad01 * 0.15f);
                var gravitic01 = math.saturate(mass01 * config.MassToGraviticWeight + speed01 * config.SpeedToGraviticWeight + engineLoad01 * 0.2f);
                var psi01 = math.saturate(math.max(telemetry.BaseParanormalSignature, overclock01 * config.OverclockToPsiWeight));

                var emScale = math.clamp(0.35f + emPassive01 * 1.1f + emActive01 * 1.2f, 0.1f, 4f);
                var graviticScale = math.clamp(0.3f + gravitic01 * 2f, 0.1f, 4f);
                var thermalScale = math.clamp(0.2f + thermal01 * 2.4f, 0.05f, 4f);
                var psiScale = math.clamp(0.25f + psi01 * 2f, 0f, 4f);

                var emBase = math.max(config.MinSignature, telemetry.BaseEMSignature);
                var graviticBase = math.max(config.MinSignature, telemetry.BaseGraviticSignature);
                var exoticBase = math.max(config.ExoticBaselineFloor, telemetry.BaseExoticSignature);
                var paranormalBase = math.max(config.ParanormalBaselineFloor, telemetry.BaseParanormalSignature);

                signature.EMSignature = ClampSignature(emBase * emScale, config);
                signature.GraviticSignature = ClampSignature(graviticBase * graviticScale, config);
                signature.ExoticSignature = ClampSignature(exoticBase * thermalScale, config);
                signature.ParanormalSignature = paranormalBase > 0f
                    ? ClampSignature(paranormalBase * psiScale, config)
                    : 0f;

                signatureRef.ValueRW = signature;

                telemetry.LastUpdatedTick = time.Tick;
                telemetry.PowerLoad01 = powerLoad01;
                telemetry.EmPassive01 = emPassive01;
                telemetry.EmActive01 = emActive01;
                telemetry.Thermal01 = thermal01;
                telemetry.Mass01 = mass01;
                telemetry.Psi01 = psi01;
                telemetry.Speed = speed;
                telemetry.MassTons = massTons;
                telemetryRef.ValueRW = telemetry;
            }
        }

        private float ResolvePowerLoad01(
            Entity entity,
            out float sensorLoad01,
            out float weaponLoad01,
            out float engineLoad01,
            out float reactorLoad01)
        {
            sensorLoad01 = 0f;
            weaponLoad01 = 0f;
            engineLoad01 = 0f;
            reactorLoad01 = 0f;

            var loadFromConsumers = 0f;
            var sampledConsumers = 0;

            if (_shipConsumerLookup.HasBuffer(entity))
            {
                var consumers = _shipConsumerLookup[entity];
                var allocatedSum = 0f;
                var requestedSum = 0f;

                for (var i = 0; i < consumers.Length; i++)
                {
                    var consumerEntity = consumers[i].Consumer;
                    if (consumerEntity == Entity.Null || !_powerConsumerLookup.HasComponent(consumerEntity))
                    {
                        continue;
                    }

                    var consumer = _powerConsumerLookup[consumerEntity];
                    var requested = consumer.RequestedDraw > 0f ? consumer.RequestedDraw : consumer.BaselineDraw;
                    requested = math.max(0.001f, requested);
                    var allocated = math.max(0f, consumer.AllocatedDraw);
                    var ratio = math.saturate(allocated / requested);

                    allocatedSum += allocated;
                    requestedSum += requested;
                    sampledConsumers++;

                    switch (consumers[i].Type)
                    {
                        case ShipPowerConsumerType.Mobility:
                            engineLoad01 = math.max(engineLoad01, ratio);
                            break;
                        case ShipPowerConsumerType.Weapons:
                            weaponLoad01 = math.max(weaponLoad01, ratio);
                            break;
                        case ShipPowerConsumerType.Sensors:
                            sensorLoad01 = math.max(sensorLoad01, ratio);
                            break;
                        case ShipPowerConsumerType.Stealth:
                        case ShipPowerConsumerType.LifeSupport:
                            reactorLoad01 = math.max(reactorLoad01, ratio);
                            break;
                    }
                }

                if (requestedSum > 0.001f)
                {
                    loadFromConsumers = math.saturate(allocatedSum / requestedSum);
                    reactorLoad01 = math.max(reactorLoad01, loadFromConsumers);
                }
            }

            var loadFromLedger = 0f;
            if (_ledgerLookup.HasComponent(entity))
            {
                var ledger = _ledgerLookup[entity];
                var available = math.max(1f, math.max(ledger.DistributionOutputMW, ledger.GenerationMW) + math.max(0f, ledger.BatteryDischargeMW));
                loadFromLedger = math.saturate(math.max(0f, ledger.TotalAllocatedMW) / available);
                reactorLoad01 = math.max(reactorLoad01, loadFromLedger);
            }

            var powerLoad01 = math.max(loadFromConsumers, loadFromLedger);
            if (sampledConsumers == 0)
            {
                var fallback = powerLoad01 > 0f ? powerLoad01 : 0.15f;
                sensorLoad01 = fallback;
                weaponLoad01 = fallback * 0.8f;
                engineLoad01 = fallback * 0.7f;
                reactorLoad01 = math.max(reactorLoad01, fallback);
            }

            return math.saturate(powerLoad01);
        }

        private float ResolveThermal01(Entity entity, float powerLoad01)
        {
            if (_heatKernelOutputLookup.HasComponent(entity))
            {
                return math.saturate(_heatKernelOutputLookup[entity].Thermal01);
            }

            if (_heatOutputLookup.HasComponent(entity))
            {
                return math.saturate(FleetcrawlHeatResolver.ResolveHeatSignature01(_heatOutputLookup[entity]));
            }

            return math.saturate(powerLoad01 * 0.45f);
        }

        private float ResolveMass01(Entity entity, out float massTons)
        {
            massTons = _physicalLookup.HasComponent(entity)
                ? math.max(0.1f, _physicalLookup[entity].BaseMass)
                : 5f;

            var normalized = math.log2(1f + massTons) / 8f;
            return math.saturate(normalized);
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

            var normalized = speed / (speed + 12f);
            return math.saturate(normalized);
        }

        private static float ClampSignature(float value, in Space4XSignatureTelemetryConfig config)
        {
            return math.clamp(math.max(0f, value), config.MinSignature, config.MaxSignature);
        }
    }
}

using PureDOTS.Runtime.Combat;
using PureDOTS.Runtime.Perception;
using PureDOTS.Runtime.Power;
using PureDOTS.Systems;
using Space4X.Registry;
using Space4X.Runtime;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Systems.Power
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(PureDOTS.Systems.Combat.CapabilityDisableSystem))]
    [UpdateAfter(typeof(PureDOTS.Systems.Power.PowerBudgetSystem))]
    [UpdateBefore(typeof(Space4XPlayerShipPowerRoutingEffectSystem))]
    [UpdateBefore(typeof(Space4XWeaponSystem))]
    [UpdateBefore(typeof(Space4XShieldRegenSystem))]
    public partial struct Space4XShipPowerDeficitCapabilitySystem : ISystem
    {
        private BufferLookup<ShipPowerConsumer> _shipPowerConsumerLookup;
        private ComponentLookup<PowerConsumer> _powerConsumerLookup;
        private ComponentLookup<PowerEffectiveness> _powerEffectivenessLookup;
        private ComponentLookup<CapabilityEffectiveness> _capabilityEffectivenessLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ShipPowerConsumer>();
            state.RequireForUpdate<CapabilityEffectiveness>();
            _shipPowerConsumerLookup = state.GetBufferLookup<ShipPowerConsumer>(true);
            _powerConsumerLookup = state.GetComponentLookup<PowerConsumer>(true);
            _powerEffectivenessLookup = state.GetComponentLookup<PowerEffectiveness>(true);
            _capabilityEffectivenessLookup = state.GetComponentLookup<CapabilityEffectiveness>(false);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _shipPowerConsumerLookup.Update(ref state);
            _powerConsumerLookup.Update(ref state);
            _powerEffectivenessLookup.Update(ref state);
            _capabilityEffectivenessLookup.Update(ref state);

            foreach (var (consumers, capability) in
                     SystemAPI.Query<DynamicBuffer<ShipPowerConsumer>, RefRW<CapabilityEffectiveness>>())
            {
                var movementScale = ResolveDomainScale(consumers, ShipPowerConsumerType.Mobility);
                var weaponsScale = ResolveDomainScale(consumers, ShipPowerConsumerType.Weapons);
                var shieldsScale = ResolveDomainScale(consumers, ShipPowerConsumerType.Shields);
                var sensorsScale = ResolveDomainScale(consumers, ShipPowerConsumerType.Sensors);
                var reactorScale = ResolveDomainScale(consumers, ShipPowerConsumerType.Stealth);
                var lifeSupportScale = ResolveDomainScale(consumers, ShipPowerConsumerType.LifeSupport);
                var communicationScale = math.clamp(math.lerp(sensorsScale, reactorScale, 0.35f), 0f, 1.8f);

                var value = capability.ValueRO;
                value.MovementEffectiveness = math.clamp(math.clamp(value.MovementEffectiveness, 0f, 1f) * movementScale, 0f, 1.8f);
                value.FiringEffectiveness = math.clamp(math.clamp(value.FiringEffectiveness, 0f, 1f) * weaponsScale, 0f, 1.8f);
                value.ShieldEffectiveness = math.clamp(math.clamp(value.ShieldEffectiveness, 0f, 1f) * shieldsScale, 0f, 1.8f);
                value.SensorEffectiveness = math.clamp(math.clamp(value.SensorEffectiveness, 0f, 1f) * sensorsScale, 0f, 1.8f);
                value.CommunicationEffectiveness = math.clamp(math.clamp(value.CommunicationEffectiveness, 0f, 1f) * communicationScale, 0f, 1.8f);
                value.LifeSupportEffectiveness = math.clamp(math.clamp(value.LifeSupportEffectiveness, 0f, 1f) * lifeSupportScale, 0f, 1.8f);
                capability.ValueRW = value;
            }
        }

        private float ResolveDomainScale(DynamicBuffer<ShipPowerConsumer> consumers, ShipPowerConsumerType type)
        {
            var factorSum = 0f;
            var factorCount = 0;
            for (var i = 0; i < consumers.Length; i++)
            {
                var entry = consumers[i];
                if (entry.Type != type || entry.Consumer == Entity.Null)
                {
                    continue;
                }

                var consumerEntity = entry.Consumer;
                var allocationRatio = 1f;
                if (_powerConsumerLookup.HasComponent(consumerEntity))
                {
                    var consumer = _powerConsumerLookup[consumerEntity];
                    var requested = consumer.RequestedDraw > 0f ? consumer.RequestedDraw : consumer.BaselineDraw;
                    if (requested > 0.001f)
                    {
                        allocationRatio = math.max(0f, consumer.AllocatedDraw / requested);
                    }
                    else
                    {
                        allocationRatio = consumer.Online != 0 ? 1f : 0f;
                    }
                }

                var effectiveness = 1f;
                if (_powerEffectivenessLookup.HasComponent(consumerEntity))
                {
                    effectiveness = math.max(0f, _powerEffectivenessLookup[consumerEntity].Value);
                }

                factorSum += math.clamp(allocationRatio * effectiveness, 0f, 2.25f);
                factorCount++;
            }

            if (factorCount == 0)
            {
                return 1f;
            }

            return math.clamp(factorSum / factorCount, 0f, 2.25f);
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct Space4XPlayerShipPowerRoutingBootstrapSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerFlagshipTag>();
            state.RequireForUpdate<ShipPowerConsumer>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var routingEntity = EnsureRoutingStateEntity(ref state);
            var entityManager = state.EntityManager;

            var flagship = Entity.Null;
            foreach (var (_, entity) in SystemAPI.Query<DynamicBuffer<ShipPowerConsumer>>().WithAll<PlayerFlagshipTag>().WithEntityAccess())
            {
                flagship = entity;
                break;
            }

            var binding = entityManager.GetComponentData<ShipPowerRoutingBinding>(routingEntity);
            if (binding.Ship == flagship)
            {
                return;
            }

            binding.Ship = flagship;
            entityManager.SetComponentData(routingEntity, binding);

            var baseline = entityManager.GetComponentData<ShipPowerRoutingSensorBaseline>(routingEntity);
            baseline.Ship = Entity.Null;
            baseline.Initialized = 0;
            entityManager.SetComponentData(routingEntity, baseline);
        }

        private Entity EnsureRoutingStateEntity(ref SystemState state)
        {
            if (SystemAPI.TryGetSingletonEntity<ShipPowerRoutingBinding>(out var singletonEntity))
            {
                return singletonEntity;
            }

            var created = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponentData(created, ShipPowerRoutingBinding.Default);
            state.EntityManager.AddComponentData(created, ShipPowerRoutingProfile.Default);
            state.EntityManager.AddComponentData(created, ShipPowerRoutingRuntime.Default);
            state.EntityManager.AddComponentData(created, ShipPowerRoutingSensorBaseline.Default);
            return created;
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(PowerSystemGroup))]
    [UpdateAfter(typeof(Space4XShipPowerFocusPresetSystem))]
    [UpdateBefore(typeof(Space4XShipPowerAllocationSyncSystem))]
    public partial struct Space4XPlayerShipPowerRoutingAllocationSystem : ISystem
    {
        private BufferLookup<ShipPowerConsumer> _shipPowerConsumerLookup;
        private ComponentLookup<PowerAllocationTarget> _allocationTargetLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ShipPowerRoutingBinding>();
            state.RequireForUpdate<ShipPowerRoutingProfile>();
            _shipPowerConsumerLookup = state.GetBufferLookup<ShipPowerConsumer>(true);
            _allocationTargetLookup = state.GetComponentLookup<PowerAllocationTarget>(false);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _shipPowerConsumerLookup.Update(ref state);
            _allocationTargetLookup.Update(ref state);

            if (!SystemAPI.TryGetSingleton<ShipPowerRoutingBinding>(out var binding) ||
                !SystemAPI.TryGetSingleton<ShipPowerRoutingProfile>(out var routing))
            {
                return;
            }

            if (binding.Ship == Entity.Null || !_shipPowerConsumerLookup.HasBuffer(binding.Ship))
            {
                return;
            }

            var consumers = _shipPowerConsumerLookup[binding.Ship];
            var profile = ClampProfile(routing);
            for (var i = 0; i < consumers.Length; i++)
            {
                var consumerEntity = consumers[i].Consumer;
                if (consumerEntity == Entity.Null || !_allocationTargetLookup.HasComponent(consumerEntity))
                {
                    continue;
                }

                _allocationTargetLookup[consumerEntity] = new PowerAllocationTarget
                {
                    Value = ResolveTargetPercent(consumers[i].Type, in profile)
                };
            }
        }

        private static ShipPowerRoutingProfile ClampProfile(in ShipPowerRoutingProfile profile)
        {
            return new ShipPowerRoutingProfile
            {
                EnginesPercent = math.clamp(profile.EnginesPercent, 10f, 250f),
                WeaponsPercent = math.clamp(profile.WeaponsPercent, 10f, 250f),
                ShieldsPercent = math.clamp(profile.ShieldsPercent, 10f, 250f),
                ReactorPercent = math.clamp(profile.ReactorPercent, 10f, 250f),
                SensorsPercent = math.clamp(profile.SensorsPercent, 10f, 250f)
            };
        }

        private static float ResolveTargetPercent(ShipPowerConsumerType type, in ShipPowerRoutingProfile profile)
        {
            return type switch
            {
                ShipPowerConsumerType.Mobility => profile.EnginesPercent,
                ShipPowerConsumerType.Weapons => profile.WeaponsPercent,
                ShipPowerConsumerType.Shields => profile.ShieldsPercent,
                ShipPowerConsumerType.Sensors => profile.SensorsPercent,
                ShipPowerConsumerType.Stealth => profile.ReactorPercent,
                ShipPowerConsumerType.LifeSupport => 100f,
                _ => 100f
            };
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(PureDOTS.Systems.Combat.CapabilityDisableSystem))]
    [UpdateAfter(typeof(PureDOTS.Systems.Power.PowerBudgetSystem))]
    [UpdateBefore(typeof(Space4XWeaponSystem))]
    [UpdateBefore(typeof(Space4XShieldRegenSystem))]
    public partial struct Space4XPlayerShipPowerRoutingEffectSystem : ISystem
    {
        private ComponentLookup<ShipPowerRoutingRuntime> _runtimeLookup;
        private ComponentLookup<ShipPowerRoutingSensorBaseline> _baselineLookup;
        private BufferLookup<ShipPowerConsumer> _shipPowerConsumerLookup;
        private ComponentLookup<PowerConsumer> _powerConsumerLookup;
        private ComponentLookup<PowerEffectiveness> _powerEffectivenessLookup;
        private ComponentLookup<SenseCapability> _senseLookup;
        private ComponentLookup<SensorSignature> _signatureLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ShipPowerRoutingBinding>();
            state.RequireForUpdate<ShipPowerRoutingProfile>();
            state.RequireForUpdate<ShipPowerRoutingRuntime>();

            _runtimeLookup = state.GetComponentLookup<ShipPowerRoutingRuntime>(false);
            _baselineLookup = state.GetComponentLookup<ShipPowerRoutingSensorBaseline>(false);
            _shipPowerConsumerLookup = state.GetBufferLookup<ShipPowerConsumer>(true);
            _powerConsumerLookup = state.GetComponentLookup<PowerConsumer>(true);
            _powerEffectivenessLookup = state.GetComponentLookup<PowerEffectiveness>(true);
            _senseLookup = state.GetComponentLookup<SenseCapability>(false);
            _signatureLookup = state.GetComponentLookup<SensorSignature>(false);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _runtimeLookup.Update(ref state);
            _baselineLookup.Update(ref state);
            _shipPowerConsumerLookup.Update(ref state);
            _powerConsumerLookup.Update(ref state);
            _powerEffectivenessLookup.Update(ref state);
            _senseLookup.Update(ref state);
            _signatureLookup.Update(ref state);

            if (!SystemAPI.TryGetSingletonEntity<ShipPowerRoutingProfile>(out var routingEntity))
            {
                return;
            }

            var profile = ClampProfile(SystemAPI.GetComponent<ShipPowerRoutingProfile>(routingEntity));
            var binding = SystemAPI.GetComponent<ShipPowerRoutingBinding>(routingEntity);
            var flagship = binding.Ship;
            var hasFlagshipConsumers = flagship != Entity.Null && _shipPowerConsumerLookup.HasBuffer(flagship);

            var fallbackEnginesFactor = math.clamp(PowerCoreMath.CalculateModuleEffectiveness(profile.EnginesPercent), 0f, 2.25f);
            var fallbackWeaponsFactor = math.clamp(PowerCoreMath.CalculateModuleEffectiveness(profile.WeaponsPercent), 0f, 2.25f);
            var fallbackShieldsFactor = math.clamp(PowerCoreMath.CalculateModuleEffectiveness(profile.ShieldsPercent), 0f, 2.25f);
            var fallbackReactorFactor = math.clamp(PowerCoreMath.CalculateModuleEffectiveness(profile.ReactorPercent), 0f, 2.25f);
            var fallbackSensorsFactor = math.clamp(PowerCoreMath.CalculateModuleEffectiveness(profile.SensorsPercent), 0f, 2.25f);

            var enginesFactor = fallbackEnginesFactor;
            var weaponsFactor = fallbackWeaponsFactor;
            var shieldsFactor = fallbackShieldsFactor;
            var reactorFactor = fallbackReactorFactor;
            var sensorsFactor = fallbackSensorsFactor;

            if (hasFlagshipConsumers)
            {
                var consumers = _shipPowerConsumerLookup[flagship];
                enginesFactor = ResolveDomainFactor(consumers, ShipPowerConsumerType.Mobility, profile.EnginesPercent);
                weaponsFactor = ResolveDomainFactor(consumers, ShipPowerConsumerType.Weapons, profile.WeaponsPercent);
                shieldsFactor = ResolveDomainFactor(consumers, ShipPowerConsumerType.Shields, profile.ShieldsPercent);
                reactorFactor = ResolveDomainFactor(consumers, ShipPowerConsumerType.Stealth, profile.ReactorPercent);
                sensorsFactor = ResolveDomainFactor(consumers, ShipPowerConsumerType.Sensors, profile.SensorsPercent);
            }

            var reactorSignatureTarget = math.clamp(0.35f + (profile.ReactorPercent / 100f) * 0.65f, 0.2f, 2.25f);
            var reactorSignatureFactor = math.clamp(reactorSignatureTarget * math.max(0.2f, reactorFactor), 0.2f, 2.5f);

            var overcharge = math.max(0f, profile.WeaponsPercent - 100f);
            var brute = math.max(0f, profile.WeaponsPercent - 160f);
            var starvePenalty = math.max(0f, 1f - weaponsFactor);
            var jamRiskPerTick = math.saturate(overcharge * 0.00006f + brute * 0.00009f + starvePenalty * 0.002f);

            _runtimeLookup[routingEntity] = new ShipPowerRoutingRuntime
            {
                EnginesFactor = enginesFactor,
                WeaponsFactor = weaponsFactor,
                ShieldsFactor = shieldsFactor,
                ReactorFactor = reactorFactor,
                SensorsFactor = sensorsFactor,
                ReactorSignatureFactor = reactorSignatureFactor,
                JamRiskPerTick = jamRiskPerTick,
                AllocationTotalPercent = profile.EnginesPercent + profile.WeaponsPercent + profile.ShieldsPercent + profile.ReactorPercent + profile.SensorsPercent,
                OverclockPercent = math.max(0f, profile.EnginesPercent - 100f) +
                                   math.max(0f, profile.WeaponsPercent - 100f) +
                                   math.max(0f, profile.ShieldsPercent - 100f) +
                                   math.max(0f, profile.ReactorPercent - 100f) +
                                   math.max(0f, profile.SensorsPercent - 100f),
                UnderclockPercent = math.max(0f, 100f - profile.EnginesPercent) +
                                    math.max(0f, 100f - profile.WeaponsPercent) +
                                    math.max(0f, 100f - profile.ShieldsPercent) +
                                    math.max(0f, 100f - profile.ReactorPercent) +
                                    math.max(0f, 100f - profile.SensorsPercent)
            };

            if (!hasFlagshipConsumers)
            {
                return;
            }

            ApplySensorAndSignatureEffects(routingEntity, flagship, sensorsFactor, reactorSignatureFactor);
        }

        private static ShipPowerRoutingProfile ClampProfile(in ShipPowerRoutingProfile profile)
        {
            return new ShipPowerRoutingProfile
            {
                EnginesPercent = math.clamp(profile.EnginesPercent, 10f, 250f),
                WeaponsPercent = math.clamp(profile.WeaponsPercent, 10f, 250f),
                ShieldsPercent = math.clamp(profile.ShieldsPercent, 10f, 250f),
                ReactorPercent = math.clamp(profile.ReactorPercent, 10f, 250f),
                SensorsPercent = math.clamp(profile.SensorsPercent, 10f, 250f)
            };
        }

        private float ResolveDomainFactor(DynamicBuffer<ShipPowerConsumer> consumers, ShipPowerConsumerType type, float fallbackTargetPercent)
        {
            var fallbackEffect = PowerCoreMath.CalculateModuleEffectiveness(math.clamp(fallbackTargetPercent, 0f, 250f));
            for (var i = 0; i < consumers.Length; i++)
            {
                var entry = consumers[i];
                if (entry.Type != type || entry.Consumer == Entity.Null)
                {
                    continue;
                }

                var consumerEntity = entry.Consumer;
                var allocationRatio = 1f;
                if (_powerConsumerLookup.HasComponent(consumerEntity))
                {
                    var consumer = _powerConsumerLookup[consumerEntity];
                    var requested = consumer.RequestedDraw > 0f ? consumer.RequestedDraw : consumer.BaselineDraw;
                    if (requested > 0.001f)
                    {
                        allocationRatio = math.max(0f, consumer.AllocatedDraw / requested);
                    }
                    else
                    {
                        allocationRatio = consumer.Online != 0 ? 1f : 0f;
                    }
                }

                var effectiveness = fallbackEffect;
                if (_powerEffectivenessLookup.HasComponent(consumerEntity))
                {
                    effectiveness = math.max(0f, _powerEffectivenessLookup[consumerEntity].Value);
                }

                return math.clamp(allocationRatio * effectiveness, 0f, 2.25f);
            }

            return math.clamp(fallbackEffect, 0f, 2.25f);
        }

        private void ApplySensorAndSignatureEffects(Entity routingEntity, Entity flagship, float sensorsFactor, float reactorSignatureFactor)
        {
            var hasSense = _senseLookup.HasComponent(flagship);
            var hasSignature = _signatureLookup.HasComponent(flagship);
            if (!hasSense && !hasSignature)
            {
                return;
            }

            var baseline = _baselineLookup[routingEntity];
            if (baseline.Ship != flagship)
            {
                baseline = ShipPowerRoutingSensorBaseline.Default;
                baseline.Ship = flagship;
            }

            if (baseline.Initialized == 0 || baseline.Ship != flagship)
            {
                baseline.Ship = flagship;
                baseline.Initialized = 1;
                baseline.Range = hasSense ? math.max(0.01f, _senseLookup[flagship].Range) : 1f;
                baseline.Acuity = hasSense ? math.max(0.01f, _senseLookup[flagship].Acuity) : 1f;
                baseline.EmSignature = hasSignature ? math.max(0.01f, _signatureLookup[flagship].EMSignature) : 1f;
                baseline.GraviticSignature = hasSignature ? math.max(0.01f, _signatureLookup[flagship].GraviticSignature) : 1f;
            }

            if (hasSense)
            {
                var sense = _senseLookup[flagship];
                var senseScale = math.clamp(sensorsFactor, 0.2f, 1.8f);
                var acuityScale = math.lerp(0.55f, 1.85f, math.saturate((senseScale - 0.2f) / 1.6f));

                sense.Range = math.max(1f, baseline.Range * senseScale);
                sense.Acuity = math.clamp(baseline.Acuity * acuityScale, 0.05f, 1.95f);
                _senseLookup[flagship] = sense;
            }

            if (hasSignature)
            {
                var signature = _signatureLookup[flagship];
                var emScale = math.clamp(reactorSignatureFactor, 0.2f, 2.5f);
                var gravScale = math.lerp(0.65f, 1.55f, math.saturate((emScale - 0.2f) / 2.3f));

                signature.EMSignature = math.max(0.01f, baseline.EmSignature * emScale);
                signature.GraviticSignature = math.max(0.01f, baseline.GraviticSignature * gravScale);
                _signatureLookup[flagship] = signature;
            }

            _baselineLookup[routingEntity] = baseline;
        }
    }
}

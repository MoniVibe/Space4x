using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Modules;
using PureDOTS.Runtime.Perception;
using PureDOTS.Systems;
using Space4X.Registry;
using Space4X.Runtime;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using RuntimeModuleSpec = PureDOTS.Runtime.Modules.ModuleSpec;

namespace Space4X.Systems.Modules
{
    [BurstCompile]
    [UpdateInGroup(typeof(AISystemGroup))]
    public partial struct Space4XCaptainPolicySystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ManeuverMode>();
            state.RequireForUpdate<OfficerProfile>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (profile, mode) in SystemAPI.Query<RefRO<OfficerProfile>, RefRW<ManeuverMode>>())
            {
                var horizon = math.max(0f, profile.ValueRO.ExpectedManeuverHorizonSeconds);
                var risk = math.saturate(profile.ValueRO.RiskTolerance);

                var hotThreshold = math.lerp(3f, 1f, risk);
                var warmThreshold = math.lerp(8f, 3f, risk);

                var desiredMode = horizon <= hotThreshold
                    ? ShipManeuverMode.Maneuver
                    : horizon <= warmThreshold
                        ? ShipManeuverMode.Transit
                        : ShipManeuverMode.Anchor;

                mode.ValueRW.Mode = desiredMode;
            }
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(AISystemGroup), OrderFirst = true)]
    [UpdateBefore(typeof(Space4XEngineProfileDerivationSystem))]
    public partial struct Space4XModuleCatalogStampSystem : ISystem
    {
        private ComponentLookup<ModuleTypeId> _moduleTypeLookup;
        private ComponentLookup<ModuleQuality> _qualityLookup;
        private ComponentLookup<ModuleTier> _tierLookup;
        private ComponentLookup<ModuleRarityComponent> _rarityLookup;
        private ComponentLookup<ModuleManufacturer> _manufacturerLookup;
        private ComponentLookup<ModuleFunctionData> _functionLookup;
        private ComponentLookup<RuntimeModuleSpec> _runtimeSpecLookup;
        private ComponentLookup<ModuleRuntimeState> _runtimeStateLookup;
        private ComponentLookup<ModulePowerRequest> _powerRequestLookup;
        private ComponentLookup<ModulePowerAllocation> _powerAllocationLookup;
        private ComponentLookup<ModuleLimbProfile> _limbProfileLookup;
        private BufferLookup<ModuleLimbState> _limbStateLookup;
        private ComponentLookup<EngineProfile> _engineProfileLookup;
        private ComponentLookup<BridgeModuleProfile> _bridgeProfileLookup;
        private ComponentLookup<CockpitModuleProfile> _cockpitProfileLookup;
        private ComponentLookup<AmmoModuleProfile> _ammoProfileLookup;
        private ComponentLookup<ShieldModuleProfile> _shieldProfileLookup;
        private ComponentLookup<ArmorModuleProfile> _armorProfileLookup;
        private ComponentLookup<SensorModuleProfile> _sensorProfileLookup;
        private ComponentLookup<WeaponModuleProfile> _weaponProfileLookup;
        private BufferLookup<ModuleCommand> _commandLookup;

        private BlobAssetReference<ModuleSpecBlob> _thrustSpec;
        private BlobAssetReference<ModuleSpecBlob> _turnSpec;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ModuleCatalogSingleton>();
            state.RequireForUpdate<ModuleTypeId>();

            _moduleTypeLookup = state.GetComponentLookup<ModuleTypeId>(true);
            _qualityLookup = state.GetComponentLookup<ModuleQuality>(true);
            _tierLookup = state.GetComponentLookup<ModuleTier>(true);
            _rarityLookup = state.GetComponentLookup<ModuleRarityComponent>(true);
            _manufacturerLookup = state.GetComponentLookup<ModuleManufacturer>(true);
            _functionLookup = state.GetComponentLookup<ModuleFunctionData>(true);
            _runtimeSpecLookup = state.GetComponentLookup<RuntimeModuleSpec>(true);
            _runtimeStateLookup = state.GetComponentLookup<ModuleRuntimeState>(true);
            _powerRequestLookup = state.GetComponentLookup<ModulePowerRequest>(true);
            _powerAllocationLookup = state.GetComponentLookup<ModulePowerAllocation>(true);
            _limbProfileLookup = state.GetComponentLookup<ModuleLimbProfile>(true);
            _limbStateLookup = state.GetBufferLookup<ModuleLimbState>(true);
            _engineProfileLookup = state.GetComponentLookup<EngineProfile>(true);
            _bridgeProfileLookup = state.GetComponentLookup<BridgeModuleProfile>(true);
            _cockpitProfileLookup = state.GetComponentLookup<CockpitModuleProfile>(true);
            _ammoProfileLookup = state.GetComponentLookup<AmmoModuleProfile>(true);
            _shieldProfileLookup = state.GetComponentLookup<ShieldModuleProfile>(true);
            _armorProfileLookup = state.GetComponentLookup<ArmorModuleProfile>(true);
            _sensorProfileLookup = state.GetComponentLookup<SensorModuleProfile>(true);
            _weaponProfileLookup = state.GetComponentLookup<WeaponModuleProfile>(true);
            _commandLookup = state.GetBufferLookup<ModuleCommand>(true);
        }

        public void OnDestroy(ref SystemState state)
        {
            if (_thrustSpec.IsCreated)
            {
                _thrustSpec.Dispose();
                _thrustSpec = default;
            }

            if (_turnSpec.IsCreated)
            {
                _turnSpec.Dispose();
                _turnSpec = default;
            }
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<ModuleCatalogSingleton>(out var catalog) || !catalog.Catalog.IsCreated)
            {
                return;
            }

            var hasEngineCatalog = SystemAPI.TryGetSingleton<EngineModuleCatalogSingleton>(out var engineCatalog) &&
                                   engineCatalog.Catalog.IsCreated;
            var hasShieldCatalog = SystemAPI.TryGetSingleton<ShieldModuleCatalogSingleton>(out var shieldCatalog) &&
                                   shieldCatalog.Catalog.IsCreated;
            var hasSensorCatalog = SystemAPI.TryGetSingleton<SensorModuleCatalogSingleton>(out var sensorCatalog) &&
                                   sensorCatalog.Catalog.IsCreated;
            var hasArmorCatalog = SystemAPI.TryGetSingleton<ArmorModuleCatalogSingleton>(out var armorCatalog) &&
                                  armorCatalog.Catalog.IsCreated;
            var hasWeaponCatalog = SystemAPI.TryGetSingleton<WeaponModuleCatalogSingleton>(out var weaponCatalog) &&
                                   weaponCatalog.Catalog.IsCreated;
            var hasBridgeCatalog = SystemAPI.TryGetSingleton<BridgeModuleCatalogSingleton>(out var bridgeCatalog) &&
                                   bridgeCatalog.Catalog.IsCreated;
            var hasCockpitCatalog = SystemAPI.TryGetSingleton<CockpitModuleCatalogSingleton>(out var cockpitCatalog) &&
                                    cockpitCatalog.Catalog.IsCreated;
            var hasAmmoCatalog = SystemAPI.TryGetSingleton<AmmoModuleCatalogSingleton>(out var ammoCatalog) &&
                                 ammoCatalog.Catalog.IsCreated;
            var hasLimbCatalog = SystemAPI.TryGetSingleton<ModuleLimbCatalogSingleton>(out var limbCatalog) &&
                                 limbCatalog.Catalog.IsCreated;

            _moduleTypeLookup.Update(ref state);
            _qualityLookup.Update(ref state);
            _tierLookup.Update(ref state);
            _rarityLookup.Update(ref state);
            _manufacturerLookup.Update(ref state);
            _functionLookup.Update(ref state);
            _runtimeSpecLookup.Update(ref state);
            _runtimeStateLookup.Update(ref state);
            _powerRequestLookup.Update(ref state);
            _powerAllocationLookup.Update(ref state);
            _limbProfileLookup.Update(ref state);
            _limbStateLookup.Update(ref state);
            _engineProfileLookup.Update(ref state);
            _bridgeProfileLookup.Update(ref state);
            _cockpitProfileLookup.Update(ref state);
            _ammoProfileLookup.Update(ref state);
            _shieldProfileLookup.Update(ref state);
            _armorProfileLookup.Update(ref state);
            _sensorProfileLookup.Update(ref state);
            _weaponProfileLookup.Update(ref state);
            _commandLookup.Update(ref state);

            ref var modules = ref catalog.Catalog.Value.Modules;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (moduleType, entity) in SystemAPI.Query<RefRO<ModuleTypeId>>().WithEntityAccess())
            {
                if (!TryGetModuleSpec(ref modules, moduleType.ValueRO.Value, out var spec))
                {
                    continue;
                }

                if (!_qualityLookup.HasComponent(entity))
                {
                    ecb.AddComponent(entity, new ModuleQuality { Value = math.saturate(spec.Quality) });
                }

                if (!_tierLookup.HasComponent(entity))
                {
                    ecb.AddComponent(entity, new ModuleTier { Value = spec.Tier });
                }

                if (!_rarityLookup.HasComponent(entity))
                {
                    ecb.AddComponent(entity, new ModuleRarityComponent { Value = spec.Rarity });
                }

                if (!_manufacturerLookup.HasComponent(entity) && !spec.ManufacturerId.IsEmpty)
                {
                    ecb.AddComponent(entity, new ModuleManufacturer { ManufacturerId = spec.ManufacturerId });
                }

                if (!_functionLookup.HasComponent(entity) && spec.Function != ModuleFunction.None)
                {
                    ecb.AddComponent(entity, new ModuleFunctionData
                    {
                        Function = spec.Function,
                        Capacity = spec.FunctionCapacity,
                        Description = spec.FunctionDescription
                    });
                }

                var hasLimbProfile = _limbProfileLookup.HasComponent(entity);
                ModuleLimbProfile limbProfile = default;
                if (!hasLimbProfile)
                {
                    var qualityInput = ResolveQualityInput(entity, spec);
                    var tierInput = ResolveTierInput(entity, spec);
                    limbProfile = ResolveLimbProfile(moduleType.ValueRO.Value, spec.Class, qualityInput, tierInput, hasLimbCatalog, limbCatalog);
                    ecb.AddComponent(entity, limbProfile);
                }
                else
                {
                    limbProfile = _limbProfileLookup[entity];
                }

                if (!_limbStateLookup.HasBuffer(entity))
                {
                    var buffer = ecb.AddBuffer<ModuleLimbState>(entity);
                    BuildDefaultLimbStates(buffer, limbProfile);
                }

                if (spec.Class != ModuleClass.Engine)
                {
                    var qualityInput = ResolveQualityInput(entity, spec);
                    var tierInput = ResolveTierInput(entity, spec);

                    if (spec.Class == ModuleClass.Bridge && !_bridgeProfileLookup.HasComponent(entity))
                    {
                        var techLevel = ResolveBridgeTechLevel(moduleType.ValueRO.Value, qualityInput, tierInput, hasBridgeCatalog, bridgeCatalog);
                        ecb.AddComponent(entity, new BridgeModuleProfile { TechLevel = techLevel });
                    }
                    else if (spec.Class == ModuleClass.Cockpit && !_cockpitProfileLookup.HasComponent(entity))
                    {
                        var cohesion = ResolveCockpitCohesion(moduleType.ValueRO.Value, qualityInput, tierInput, hasCockpitCatalog, cockpitCatalog);
                        ecb.AddComponent(entity, new CockpitModuleProfile { NavigationCohesion = cohesion });
                    }
                    else if (spec.Class == ModuleClass.Ammunition && !_ammoProfileLookup.HasComponent(entity))
                    {
                        var capacity = ResolveAmmoCapacity(moduleType.ValueRO.Value, spec.RequiredSize, hasAmmoCatalog, ammoCatalog);
                        ecb.AddComponent(entity, new AmmoModuleProfile { AmmoCapacity = capacity });
                    }
                    else if (spec.Class == ModuleClass.Shield && !_shieldProfileLookup.HasComponent(entity))
                    {
                        var profile = ResolveShieldProfile(moduleType.ValueRO.Value, spec.RequiredSize, qualityInput, tierInput, hasShieldCatalog, shieldCatalog);
                        ecb.AddComponent(entity, profile);
                    }
                    else if (spec.Class == ModuleClass.Armor && !_armorProfileLookup.HasComponent(entity))
                    {
                        var profile = ResolveArmorProfile(moduleType.ValueRO.Value, spec.RequiredSize, qualityInput, tierInput, hasArmorCatalog, armorCatalog);
                        ecb.AddComponent(entity, profile);
                    }
                    else if (spec.Class == ModuleClass.Scanner && !_sensorProfileLookup.HasComponent(entity))
                    {
                        var profile = ResolveSensorProfile(moduleType.ValueRO.Value, spec.RequiredSize, qualityInput, tierInput, hasSensorCatalog, sensorCatalog);
                        ecb.AddComponent(entity, profile);
                    }
                    else if (IsWeaponModuleClass(spec.Class) && !_weaponProfileLookup.HasComponent(entity))
                    {
                        var profile = ResolveWeaponProfile(moduleType.ValueRO.Value, spec.Class, qualityInput, tierInput, hasWeaponCatalog, weaponCatalog);
                        ecb.AddComponent(entity, profile);
                    }

                    continue;
                }

                if (!_engineProfileLookup.HasComponent(entity))
                {
                    var profile = EngineProfile.Default;
                    profile.VectoringMode = EngineVectoringMode.Vectored;
                    if (hasEngineCatalog)
                    {
                        ref var engineModules = ref engineCatalog.Catalog.Value.Modules;
                        if (TryGetEngineSpec(ref engineModules, moduleType.ValueRO.Value, out var engineSpec))
                        {
                            ApplyEngineSpec(ref profile, engineSpec);
                        }
                    }
                    ecb.AddComponent(entity, profile);
                }

                if (!_runtimeSpecLookup.HasComponent(entity))
                {
                    ecb.AddComponent(entity, new RuntimeModuleSpec { Spec = ResolveDefaultSpec(ModuleCapabilityKind.ThrustAuthority) });
                }

                if (!_runtimeStateLookup.HasComponent(entity))
                {
                    ecb.AddComponent(entity, new ModuleRuntimeState
                    {
                        Posture = ModulePosture.Standby,
                        NormalizedOutput = 0f,
                        TargetOutput = 0.35f,
                        TimeInState = 0f
                    });
                }

                if (!_powerRequestLookup.HasComponent(entity))
                {
                    var specBlob = ResolveDefaultSpec(ModuleCapabilityKind.ThrustAuthority);
                    ecb.AddComponent(entity, new ModulePowerRequest
                    {
                        RequestedPower = ResolvePowerDraw(ModulePosture.Standby, specBlob.Value)
                    });
                }

                if (!_powerAllocationLookup.HasComponent(entity))
                {
                    ecb.AddComponent(entity, new ModulePowerAllocation
                    {
                        AllocatedPower = 0f,
                        SupplyRatio = 1f
                    });
                }

                if (!_commandLookup.HasBuffer(entity))
                {
                    ecb.AddBuffer<ModuleCommand>(entity);
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private static bool TryGetModuleSpec(ref BlobArray<Space4X.Registry.ModuleSpec> modules, in FixedString64Bytes moduleId, out Space4X.Registry.ModuleSpec spec)
        {
            spec = default;
            for (int i = 0; i < modules.Length; i++)
            {
                if (modules[i].Id == moduleId)
                {
                    spec = modules[i];
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetEngineSpec(ref BlobArray<EngineModuleSpec> modules, in FixedString64Bytes moduleId, out EngineModuleSpec spec)
        {
            spec = default;
            for (int i = 0; i < modules.Length; i++)
            {
                if (modules[i].ModuleId == moduleId)
                {
                    spec = modules[i];
                    return true;
                }
            }

            return false;
        }

        private static void ApplyEngineSpec(ref EngineProfile profile, in EngineModuleSpec spec)
        {
            profile.Class = spec.EngineClass;
            profile.FuelType = spec.FuelType;
            profile.IntakeType = spec.IntakeType;
            profile.VectoringMode = spec.VectoringMode;
            if (spec.TechLevel > 0f)
            {
                profile.TechLevel = math.saturate(spec.TechLevel);
            }
            if (spec.Quality > 0f)
            {
                profile.Quality = math.saturate(spec.Quality);
            }
            if (spec.ThrustScalar > 0f)
            {
                profile.ThrustScalar = spec.ThrustScalar;
            }
            if (spec.TurnScalar > 0f)
            {
                profile.TurnScalar = spec.TurnScalar;
            }
            if (spec.ResponseRating > 0f)
            {
                profile.ResponseRating = math.saturate(spec.ResponseRating);
            }
            if (spec.EfficiencyRating > 0f)
            {
                profile.EfficiencyRating = math.saturate(spec.EfficiencyRating);
            }
            if (spec.BoostRating > 0f)
            {
                profile.BoostRating = math.saturate(spec.BoostRating);
            }
            if (spec.VectoringRating > 0f)
            {
                profile.VectoringRating = math.saturate(spec.VectoringRating);
            }
        }

        private float ResolveQualityInput(Entity entity, in Space4X.Registry.ModuleSpec spec)
        {
            var quality = _qualityLookup.HasComponent(entity)
                ? math.saturate(_qualityLookup[entity].Value)
                : math.saturate(spec.Quality);
            return quality > 0f ? quality : 0.5f;
        }

        private float ResolveTierInput(Entity entity, in Space4X.Registry.ModuleSpec spec)
        {
            var tier = _tierLookup.HasComponent(entity)
                ? math.saturate(_tierLookup[entity].Value / 255f)
                : math.saturate(spec.Tier / 255f);
            return tier > 0f ? tier : 0.5f;
        }

        private static bool TryGetBridgeSpec(ref BlobArray<BridgeModuleSpec> modules, in FixedString64Bytes moduleId, out BridgeModuleSpec spec)
        {
            spec = default;
            for (int i = 0; i < modules.Length; i++)
            {
                if (modules[i].ModuleId == moduleId)
                {
                    spec = modules[i];
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetCockpitSpec(ref BlobArray<CockpitModuleSpec> modules, in FixedString64Bytes moduleId, out CockpitModuleSpec spec)
        {
            spec = default;
            for (int i = 0; i < modules.Length; i++)
            {
                if (modules[i].ModuleId == moduleId)
                {
                    spec = modules[i];
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetAmmoSpec(ref BlobArray<AmmoModuleSpec> modules, in FixedString64Bytes moduleId, out AmmoModuleSpec spec)
        {
            spec = default;
            for (int i = 0; i < modules.Length; i++)
            {
                if (modules[i].ModuleId == moduleId)
                {
                    spec = modules[i];
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetLimbSpec(ref BlobArray<ModuleLimbSpec> modules, in FixedString64Bytes moduleId, out ModuleLimbSpec spec)
        {
            spec = default;
            for (int i = 0; i < modules.Length; i++)
            {
                if (modules[i].ModuleId == moduleId)
                {
                    spec = modules[i];
                    return true;
                }
            }

            return false;
        }

        private static float ResolveBridgeTechLevel(in FixedString64Bytes moduleId, float qualityInput, float tierInput, bool hasCatalog, BridgeModuleCatalogSingleton catalog)
        {
            if (hasCatalog)
            {
                ref var modules = ref catalog.Catalog.Value.Modules;
                if (TryGetBridgeSpec(ref modules, moduleId, out var spec) && spec.TechLevel > 0f)
                {
                    return math.saturate(spec.TechLevel);
                }
            }

            return math.clamp(0.35f + tierInput * 0.5f + qualityInput * 0.15f, 0f, 1f);
        }

        private static float ResolveCockpitCohesion(in FixedString64Bytes moduleId, float qualityInput, float tierInput, bool hasCatalog, CockpitModuleCatalogSingleton catalog)
        {
            if (hasCatalog)
            {
                ref var modules = ref catalog.Catalog.Value.Modules;
                if (TryGetCockpitSpec(ref modules, moduleId, out var spec) && spec.NavigationCohesion > 0f)
                {
                    return math.saturate(spec.NavigationCohesion);
                }
            }

            return math.clamp(0.4f + qualityInput * 0.4f + tierInput * 0.2f, 0f, 1f);
        }

        private static float ResolveAmmoCapacity(in FixedString64Bytes moduleId, MountSize size, bool hasCatalog, AmmoModuleCatalogSingleton catalog)
        {
            if (hasCatalog)
            {
                ref var modules = ref catalog.Catalog.Value.Modules;
                if (TryGetAmmoSpec(ref modules, moduleId, out var spec) && spec.AmmoCapacity > 0f)
                {
                    return math.max(0f, spec.AmmoCapacity);
                }
            }

            return ResolveDefaultAmmoCapacity(size);
        }

        private static float ResolveDefaultAmmoCapacity(MountSize size)
        {
            return size switch
            {
                MountSize.S => 120f,
                MountSize.M => 300f,
                MountSize.L => 600f,
                _ => 120f
            };
        }

        private static ModuleLimbProfile ResolveLimbProfile(
            in FixedString64Bytes moduleId,
            ModuleClass moduleClass,
            float qualityInput,
            float tierInput,
            bool hasCatalog,
            ModuleLimbCatalogSingleton catalog)
        {
            var found = false;
            var profile = default(ModuleLimbProfile);

            if (hasCatalog)
            {
                ref var modules = ref catalog.Catalog.Value.Modules;
                if (TryGetLimbSpec(ref modules, moduleId, out var spec))
                {
                    profile = spec.Profile;
                    found = true;
                }
            }

            if (!found)
            {
                profile = moduleClass switch
                {
                    ModuleClass.Laser => new ModuleLimbProfile { Cooling = 0.6f, Sensors = 0.45f, Lensing = 0.7f, Power = 0.4f },
                    ModuleClass.Kinetic => new ModuleLimbProfile { Cooling = 0.55f, Sensors = 0.4f, Lensing = 0.45f, Guidance = 0.3f, Power = 0.35f },
                    ModuleClass.Missile => new ModuleLimbProfile { Cooling = 0.45f, Sensors = 0.35f, Guidance = 0.6f, Power = 0.35f },
                    ModuleClass.PointDefense => new ModuleLimbProfile { Cooling = 0.55f, Sensors = 0.55f, Lensing = 0.4f, Guidance = 0.4f, Power = 0.35f },
                    ModuleClass.Scanner => new ModuleLimbProfile { Sensors = 0.9f, Cooling = 0.3f, Power = 0.2f },
                    ModuleClass.Shield => new ModuleLimbProfile { Cooling = 0.5f, Projector = 0.6f, Structural = 0.5f, Power = 0.45f },
                    ModuleClass.Armor => new ModuleLimbProfile { Structural = 0.8f },
                    ModuleClass.Engine => new ModuleLimbProfile { Cooling = 0.55f, Actuator = 0.5f, Structural = 0.4f, Power = 0.25f },
                    ModuleClass.Reactor => new ModuleLimbProfile { Cooling = 0.7f, Structural = 0.6f, Power = 0.85f },
                    ModuleClass.Bridge => new ModuleLimbProfile { Sensors = 0.6f, Actuator = 0.4f, Structural = 0.4f, Power = 0.25f },
                    ModuleClass.Cockpit => new ModuleLimbProfile { Sensors = 0.55f, Actuator = 0.45f, Structural = 0.35f, Power = 0.2f },
                    _ => new ModuleLimbProfile { Structural = 0.4f }
                };
            }

            var qualityScale = math.lerp(0.8f, 1.1f, math.saturate(qualityInput));
            var tierScale = math.lerp(0.85f, 1.15f, math.saturate(tierInput));
            var scale = qualityScale * tierScale;

            profile.Cooling = math.saturate(profile.Cooling * scale);
            profile.Sensors = math.saturate(profile.Sensors * scale);
            profile.Lensing = math.saturate(profile.Lensing * scale);
            profile.Projector = math.saturate(profile.Projector * scale);
            profile.Guidance = math.saturate(profile.Guidance * scale);
            profile.Actuator = math.saturate(profile.Actuator * scale);
            profile.Structural = math.saturate(profile.Structural * scale);
            profile.Power = math.saturate(profile.Power * scale);

            return profile;
        }

        private static void BuildDefaultLimbStates(DynamicBuffer<ModuleLimbState> buffer, in ModuleLimbProfile profile)
        {
            AddLimb(buffer, ModuleLimbFamily.Cooling, ModuleLimbId.Heatsink, profile.Cooling, 0.65f, 0.7f);
            AddLimb(buffer, ModuleLimbFamily.Cooling, ModuleLimbId.CoolantManifold, profile.Cooling, 0.45f, 0.5f);

            AddLimb(buffer, ModuleLimbFamily.Sensors, ModuleLimbId.SensorArray, profile.Sensors, 0.7f, 0.6f);
            AddLimb(buffer, ModuleLimbFamily.Sensors, ModuleLimbId.FireControl, profile.Sensors, 0.5f, 0.5f);

            AddLimb(buffer, ModuleLimbFamily.Lensing, ModuleLimbId.Lens, profile.Lensing, 0.7f, 0.55f);
            AddLimb(buffer, ModuleLimbFamily.Lensing, ModuleLimbId.FocusCoil, profile.Lensing, 0.45f, 0.4f);

            AddLimb(buffer, ModuleLimbFamily.Projector, ModuleLimbId.ProjectorEmitter, profile.Projector, 0.75f, 0.6f);

            AddLimb(buffer, ModuleLimbFamily.Guidance, ModuleLimbId.GuidanceCore, profile.Guidance, 0.7f, 0.55f);

            AddLimb(buffer, ModuleLimbFamily.Actuator, ModuleLimbId.ActuatorMotor, profile.Actuator, 0.7f, 0.45f);

            AddLimb(buffer, ModuleLimbFamily.Structural, ModuleLimbId.StructuralFrame, profile.Structural, 1f, 0.35f);
            AddLimb(buffer, ModuleLimbFamily.Structural, ModuleLimbId.Barrel, profile.Structural, 0.55f, 0.6f);

            AddLimb(buffer, ModuleLimbFamily.Power, ModuleLimbId.Capacitor, profile.Power, 0.7f, 0.4f);
            AddLimb(buffer, ModuleLimbFamily.Power, ModuleLimbId.PowerCoupler, profile.Power, 0.5f, 0.3f);
        }

        private static void AddLimb(
            DynamicBuffer<ModuleLimbState> buffer,
            ModuleLimbFamily family,
            ModuleLimbId limbId,
            float coverage,
            float weight,
            float exposure)
        {
            if (coverage <= 0f || weight <= 0f)
            {
                return;
            }

            buffer.Add(new ModuleLimbState
            {
                LimbId = limbId,
                Family = family,
                Integrity = math.saturate(coverage * weight),
                Exposure = math.saturate(exposure)
            });
        }

        private static bool TryGetShieldSpec(ref BlobArray<ShieldModuleSpec> modules, in FixedString64Bytes moduleId, out ShieldModuleSpec spec)
        {
            spec = default;
            for (int i = 0; i < modules.Length; i++)
            {
                if (modules[i].ModuleId == moduleId)
                {
                    spec = modules[i];
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetArmorSpec(ref BlobArray<ArmorModuleSpec> modules, in FixedString64Bytes moduleId, out ArmorModuleSpec spec)
        {
            spec = default;
            for (int i = 0; i < modules.Length; i++)
            {
                if (modules[i].ModuleId == moduleId)
                {
                    spec = modules[i];
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetSensorSpec(ref BlobArray<SensorModuleSpec> modules, in FixedString64Bytes moduleId, out SensorModuleSpec spec)
        {
            spec = default;
            for (int i = 0; i < modules.Length; i++)
            {
                if (modules[i].ModuleId == moduleId)
                {
                    spec = modules[i];
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetWeaponSpec(ref BlobArray<WeaponModuleSpec> modules, in FixedString64Bytes moduleId, out WeaponModuleSpec spec)
        {
            spec = default;
            for (int i = 0; i < modules.Length; i++)
            {
                if (modules[i].ModuleId == moduleId)
                {
                    spec = modules[i];
                    return true;
                }
            }

            return false;
        }

        private static ShieldModuleProfile ResolveShieldProfile(
            in FixedString64Bytes moduleId,
            MountSize size,
            float qualityInput,
            float tierInput,
            bool hasCatalog,
            ShieldModuleCatalogSingleton catalog)
        {
            if (hasCatalog)
            {
                ref var modules = ref catalog.Catalog.Value.Modules;
                if (TryGetShieldSpec(ref modules, moduleId, out var spec))
                {
                    var profile = new ShieldModuleProfile
                    {
                        Capacity = math.max(0f, spec.Capacity),
                        RechargePerSecond = math.max(0f, spec.RechargePerSecond),
                        RegenDelaySeconds = math.max(0f, spec.RegenDelaySeconds),
                        ArcDegrees = math.clamp(spec.ArcDegrees, 0f, 360f),
                        KineticResist = math.saturate(spec.KineticResist),
                        EnergyResist = math.saturate(spec.EnergyResist),
                        ThermalResist = math.saturate(spec.ThermalResist),
                        EMResist = math.saturate(spec.EMResist),
                        RadiationResist = math.saturate(spec.RadiationResist),
                        ExplosiveResist = math.saturate(spec.ExplosiveResist),
                        CausticResist = math.saturate(spec.CausticResist)
                    };

                    ApplyHardening(ref profile, spec.HardenedType, spec.HardenedBonus, spec.HardenedPenalty);
                    return profile;
                }
            }

            var baseCap = size switch
            {
                MountSize.S => 180f,
                MountSize.M => 420f,
                MountSize.L => 820f,
                _ => 180f
            };
            var scale = math.lerp(0.85f, 1.2f, qualityInput) * math.lerp(0.9f, 1.3f, tierInput);
            var capacity = math.max(40f, baseCap * scale);
            var recharge = capacity * math.lerp(0.0125f, 0.022f, qualityInput);
            var delay = math.clamp(3.5f - qualityInput * 1.2f, 1.5f, 4.5f);

            return new ShieldModuleProfile
            {
                Capacity = capacity,
                RechargePerSecond = recharge,
                RegenDelaySeconds = delay,
                ArcDegrees = 360f,
                KineticResist = 1f,
                EnergyResist = 1f,
                ThermalResist = 1f,
                EMResist = 1f,
                RadiationResist = 1f,
                ExplosiveResist = 1f,
                CausticResist = 1f
            };
        }

        private static ArmorModuleProfile ResolveArmorProfile(
            in FixedString64Bytes moduleId,
            MountSize size,
            float qualityInput,
            float tierInput,
            bool hasCatalog,
            ArmorModuleCatalogSingleton catalog)
        {
            if (hasCatalog)
            {
                ref var modules = ref catalog.Catalog.Value.Modules;
                if (TryGetArmorSpec(ref modules, moduleId, out var spec))
                {
                    var profile = new ArmorModuleProfile
                    {
                        HullBonus = math.max(0f, spec.HullBonus),
                        DamageReduction = math.saturate(spec.DamageReduction),
                        KineticResist = math.saturate(spec.KineticResist),
                        EnergyResist = math.saturate(spec.EnergyResist),
                        ThermalResist = math.saturate(spec.ThermalResist),
                        EMResist = math.saturate(spec.EMResist),
                        RadiationResist = math.saturate(spec.RadiationResist),
                        ExplosiveResist = math.saturate(spec.ExplosiveResist),
                        CausticResist = math.saturate(spec.CausticResist),
                        RepairRateMultiplier = math.max(0f, spec.RepairRateMultiplier)
                    };

                    ApplyHardening(ref profile, spec.HardenedType, spec.HardenedBonus, spec.HardenedPenalty);
                    return profile;
                }
            }

            var baseThickness = size switch
            {
                MountSize.S => 18f,
                MountSize.M => 38f,
                MountSize.L => 70f,
                _ => 18f
            };
            var scale = math.lerp(0.9f, 1.25f, qualityInput) * math.lerp(0.9f, 1.2f, tierInput);

            return new ArmorModuleProfile
            {
                HullBonus = baseThickness * scale,
                DamageReduction = math.clamp(0.2f + tierInput * 0.35f, 0.1f, 0.75f),
                KineticResist = 1f,
                EnergyResist = 1f,
                ThermalResist = 1f,
                EMResist = 1f,
                RadiationResist = 1f,
                ExplosiveResist = 1f,
                CausticResist = 1f,
                RepairRateMultiplier = 1f
            };
        }

        private static void ApplyHardening(ref ShieldModuleProfile profile, Space4XDamageType type, float bonus, float penalty)
        {
            if (type == Space4XDamageType.Unknown || (bonus <= 0f && penalty <= 0f))
            {
                return;
            }

            var targetBonus = math.max(0f, bonus);
            var tradeoff = math.max(0f, penalty);

            ApplyResistanceShift(type, targetBonus, tradeoff,
                ref profile.KineticResist,
                ref profile.EnergyResist,
                ref profile.ThermalResist,
                ref profile.EMResist,
                ref profile.RadiationResist,
                ref profile.CausticResist,
                ref profile.ExplosiveResist);
        }

        private static void ApplyHardening(ref ArmorModuleProfile profile, Space4XDamageType type, float bonus, float penalty)
        {
            if (type == Space4XDamageType.Unknown || (bonus <= 0f && penalty <= 0f))
            {
                return;
            }

            var targetBonus = math.max(0f, bonus);
            var tradeoff = math.max(0f, penalty);

            ApplyResistanceShift(type, targetBonus, tradeoff,
                ref profile.KineticResist,
                ref profile.EnergyResist,
                ref profile.ThermalResist,
                ref profile.EMResist,
                ref profile.RadiationResist,
                ref profile.CausticResist,
                ref profile.ExplosiveResist);
        }

        private static void ApplyResistanceShift(
            Space4XDamageType hardenedType,
            float bonus,
            float penalty,
            ref float kinetic,
            ref float energy,
            ref float thermal,
            ref float em,
            ref float radiation,
            ref float caustic,
            ref float explosive)
        {
            switch (hardenedType)
            {
                case Space4XDamageType.Kinetic:
                    kinetic = math.saturate(kinetic + bonus);
                    break;
                case Space4XDamageType.Energy:
                    energy = math.saturate(energy + bonus);
                    break;
                case Space4XDamageType.Thermal:
                    thermal = math.saturate(thermal + bonus);
                    break;
                case Space4XDamageType.EM:
                    em = math.saturate(em + bonus);
                    break;
                case Space4XDamageType.Radiation:
                    radiation = math.saturate(radiation + bonus);
                    break;
                case Space4XDamageType.Caustic:
                    caustic = math.saturate(caustic + bonus);
                    break;
                case Space4XDamageType.Explosive:
                    explosive = math.saturate(explosive + bonus);
                    break;
            }

            if (penalty <= 0f)
            {
                return;
            }

            if (hardenedType != Space4XDamageType.Kinetic) kinetic = math.saturate(kinetic - penalty);
            if (hardenedType != Space4XDamageType.Energy) energy = math.saturate(energy - penalty);
            if (hardenedType != Space4XDamageType.Thermal) thermal = math.saturate(thermal - penalty);
            if (hardenedType != Space4XDamageType.EM) em = math.saturate(em - penalty);
            if (hardenedType != Space4XDamageType.Radiation) radiation = math.saturate(radiation - penalty);
            if (hardenedType != Space4XDamageType.Caustic) caustic = math.saturate(caustic - penalty);
            if (hardenedType != Space4XDamageType.Explosive) explosive = math.saturate(explosive - penalty);
        }

        private static SensorModuleProfile ResolveSensorProfile(
            in FixedString64Bytes moduleId,
            MountSize size,
            float qualityInput,
            float tierInput,
            bool hasCatalog,
            SensorModuleCatalogSingleton catalog)
        {
            if (hasCatalog)
            {
                ref var modules = ref catalog.Catalog.Value.Modules;
                if (TryGetSensorSpec(ref modules, moduleId, out var spec))
                {
                    return new SensorModuleProfile
                    {
                        Range = math.max(0f, spec.Range),
                        RefreshSeconds = math.max(0.02f, spec.RefreshSeconds),
                        Resolution = math.saturate(spec.Resolution),
                        JamResistance = math.saturate(spec.JamResistance),
                        PassiveSignature = math.saturate(spec.PassiveSignature)
                    };
                }
            }

            var baseRange = size switch
            {
                MountSize.S => 360f,
                MountSize.M => 520f,
                MountSize.L => 700f,
                _ => 360f
            };
            var range = baseRange * math.lerp(0.85f, 1.2f, qualityInput) * math.lerp(0.9f, 1.2f, tierInput);
            var refresh = math.clamp(0.3f - qualityInput * 0.12f - tierInput * 0.06f, 0.08f, 0.6f);
            var resolution = math.clamp(0.45f + qualityInput * 0.4f, 0.2f, 1f);

            return new SensorModuleProfile
            {
                Range = range,
                RefreshSeconds = refresh,
                Resolution = resolution,
                JamResistance = math.clamp(0.2f + tierInput * 0.4f, 0f, 1f),
                PassiveSignature = 0.2f
            };
        }

        private static WeaponModuleProfile ResolveWeaponProfile(
            in FixedString64Bytes moduleId,
            ModuleClass moduleClass,
            float qualityInput,
            float tierInput,
            bool hasCatalog,
            WeaponModuleCatalogSingleton catalog)
        {
            if (hasCatalog)
            {
                ref var modules = ref catalog.Catalog.Value.Modules;
                if (TryGetWeaponSpec(ref modules, moduleId, out var spec))
                {
                    return new WeaponModuleProfile
                    {
                        WeaponId = spec.WeaponId,
                        FireArcDegrees = math.max(0f, spec.FireArcDegrees),
                        FireArcOffsetDeg = spec.FireArcOffsetDeg,
                        AccuracyBonus = spec.AccuracyBonus,
                        TrackingBonus = spec.TrackingBonus
                    };
                }
            }

            var arc = moduleClass switch
            {
                ModuleClass.PointDefense => 220f,
                ModuleClass.Missile => 140f,
                ModuleClass.Kinetic => 120f,
                _ => 180f
            };

            return new WeaponModuleProfile
            {
                WeaponId = moduleId,
                FireArcDegrees = arc,
                FireArcOffsetDeg = 0f,
                AccuracyBonus = (qualityInput - 0.5f) * 0.1f,
                TrackingBonus = (tierInput - 0.5f) * 0.12f
            };
        }

        private static bool IsWeaponModuleClass(ModuleClass moduleClass)
        {
            return moduleClass == ModuleClass.Laser
                   || moduleClass == ModuleClass.Kinetic
                   || moduleClass == ModuleClass.Missile
                   || moduleClass == ModuleClass.PointDefense;
        }

        private BlobAssetReference<ModuleSpecBlob> ResolveDefaultSpec(ModuleCapabilityKind capability)
        {
            if (capability == ModuleCapabilityKind.TurnAuthority)
            {
                if (!_turnSpec.IsCreated)
                {
                    _turnSpec = BuildSpec(0f, 1.5f, 4f, 6f, 4f, 1.5f, 2f, 3f, 1f, 0f, capability);
                }

                return _turnSpec;
            }

            if (!_thrustSpec.IsCreated)
            {
                _thrustSpec = BuildSpec(0f, 2f, 6f, 8f, 6f, 2f, 3f, 4f, 1f, 0f, ModuleCapabilityKind.ThrustAuthority);
            }

            return _thrustSpec;
        }

        private static BlobAssetReference<ModuleSpecBlob> BuildSpec(
            float powerDrawOff,
            float powerDrawStandby,
            float powerDrawOnline,
            float powerDrawEmergency,
            float tauColdToOnline,
            float tauWarmToOnline,
            float tauOnlineToStandby,
            float tauStandbyToOff,
            float maxOutput,
            float rampRateLimit,
            ModuleCapabilityKind capability)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<ModuleSpecBlob>();

            root.PowerDrawOff = math.max(0f, powerDrawOff);
            root.PowerDrawStandby = math.max(0f, powerDrawStandby);
            root.PowerDrawOnline = math.max(0f, powerDrawOnline);
            root.PowerDrawEmergency = math.max(0f, powerDrawEmergency);
            root.TauColdToOnline = math.max(0.01f, tauColdToOnline);
            root.TauWarmToOnline = math.max(0.01f, tauWarmToOnline);
            root.TauOnlineToStandby = math.max(0.01f, tauOnlineToStandby);
            root.TauStandbyToOff = math.max(0.01f, tauStandbyToOff);
            root.MaxOutput = math.max(0f, maxOutput);
            root.RampRateLimit = math.max(0f, rampRateLimit);
            root.Capability = capability;

            var blob = builder.CreateBlobAssetReference<ModuleSpecBlob>(Allocator.Persistent);
            builder.Dispose();
            return blob;
        }

        private static float ResolvePowerDraw(ModulePosture posture, in ModuleSpecBlob spec)
        {
            return posture switch
            {
                ModulePosture.Off => spec.PowerDrawOff,
                ModulePosture.Standby => spec.PowerDrawStandby,
                ModulePosture.Online => spec.PowerDrawOnline,
                ModulePosture.Emergency => spec.PowerDrawEmergency,
                _ => spec.PowerDrawOff
            };
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(AISystemGroup))]
    public partial struct Space4XEngineProfileDerivationSystem : ISystem
    {
        private ComponentLookup<ModuleQuality> _qualityLookup;
        private ComponentLookup<ModuleTier> _tierLookup;
        private ComponentLookup<ModuleRarityComponent> _rarityLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EngineProfile>();

            _qualityLookup = state.GetComponentLookup<ModuleQuality>(true);
            _tierLookup = state.GetComponentLookup<ModuleTier>(true);
            _rarityLookup = state.GetComponentLookup<ModuleRarityComponent>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _qualityLookup.Update(ref state);
            _tierLookup.Update(ref state);
            _rarityLookup.Update(ref state);

            var hasTuning = SystemAPI.TryGetSingleton<Space4XEngineTuningSingleton>(out var tuningSingleton) &&
                            tuningSingleton.Blob.IsCreated;
            var tuningBlob = tuningSingleton.Blob;

            foreach (var (profile, entity) in SystemAPI.Query<RefRW<EngineProfile>>().WithEntityAccess())
            {
                var qualityInput = _qualityLookup.HasComponent(entity) ? math.saturate(_qualityLookup[entity].Value) : 0.5f;
                var tierInput = _tierLookup.HasComponent(entity) ? math.saturate(_tierLookup[entity].Value / 255f) : 0.5f;
                var rarity = _rarityLookup.HasComponent(entity) ? _rarityLookup[entity].Value : ModuleRarity.Common;
                var rarityBonus = ResolveRarityBonus(rarity);

                var profileData = profile.ValueRW;

                var classTuning = ResolveClassTuning(profileData.Class, hasTuning, tuningBlob);
                var fuelTuning = ResolveFuelTuning(profileData.FuelType, hasTuning, tuningBlob);
                var intakeTuning = ResolveIntakeTuning(profileData.IntakeType, hasTuning, tuningBlob);
                var vectoringTuning = ResolveVectoringTuning(profileData.VectoringMode, hasTuning, tuningBlob);

                var techLevel = profileData.TechLevel > 0f
                    ? math.saturate(profileData.TechLevel)
                    : ResolveTechLevel(tierInput, rarityBonus);
                var quality = profileData.Quality > 0f
                    ? math.saturate(profileData.Quality)
                    : ResolveQuality(qualityInput, rarityBonus);

                profileData.TechLevel = techLevel;
                profileData.Quality = quality;

                var techFactor = math.lerp(0.85f, 1.25f, techLevel);
                var qualityFactor = math.lerp(0.85f, 1.2f, quality);
                var baseScale = techFactor * qualityFactor;

                if (profileData.ThrustScalar <= 0f)
                {
                    var thrust = baseScale
                        * classTuning.ThrustMultiplier
                        * fuelTuning.ThrustMultiplier
                        * intakeTuning.ThrustMultiplier;
                    profileData.ThrustScalar = math.clamp(thrust, 0.5f, 1.6f);
                }

                if (profileData.TurnScalar <= 0f)
                {
                    var turn = baseScale
                        * classTuning.TurnMultiplier
                        * vectoringTuning.TurnMultiplier;
                    profileData.TurnScalar = math.clamp(turn, 0.45f, 1.7f);
                }

                if (profileData.ResponseRating <= 0f)
                {
                    var response = ResolveResponseRating(techLevel, quality, classTuning, fuelTuning, intakeTuning);
                    profileData.ResponseRating = response;
                }

                if (profileData.EfficiencyRating <= 0f)
                {
                    var efficiency = ResolveEfficiencyRating(techLevel, quality, classTuning, fuelTuning, intakeTuning);
                    profileData.EfficiencyRating = efficiency;
                }

                if (profileData.BoostRating <= 0f)
                {
                    var boost = ResolveBoostRating(techLevel, quality, classTuning, fuelTuning, intakeTuning);
                    profileData.BoostRating = boost;
                }

                if (profileData.VectoringRating <= 0f)
                {
                    var vectoring = ResolveVectoringRating(quality, vectoringTuning);
                    profileData.VectoringRating = vectoring;
                }

                profile.ValueRW = profileData;
            }
        }

        private static float ResolveRarityBonus(ModuleRarity rarity)
        {
            return rarity switch
            {
                ModuleRarity.Uncommon => 0.04f,
                ModuleRarity.Heroic => 0.1f,
                ModuleRarity.Prototype => 0.18f,
                _ => 0f
            };
        }

        private static float ResolveTechLevel(float tierNormalized, float rarityBonus)
        {
            return math.clamp(0.3f + tierNormalized * 0.6f + rarityBonus * 0.5f, 0f, 1f);
        }

        private static float ResolveQuality(float qualityInput, float rarityBonus)
        {
            return math.clamp(0.35f + qualityInput * 0.55f + rarityBonus * 0.2f, 0f, 1f);
        }

        private static EngineClassTuning ResolveClassTuning(EngineClass engineClass, bool hasTuning, BlobAssetReference<Space4XEngineTuningBlob> tuningBlob)
        {
            if (hasTuning)
            {
                var index = Space4XEngineTuningDefaults.ResolveClassIndex(engineClass);
                if ((uint)index < (uint)tuningBlob.Value.ClassTuning.Length)
                {
                    return Space4XEngineTuningDefaults.Sanitize(tuningBlob.Value.ClassTuning[index]);
                }
            }

            return Space4XEngineTuningDefaults.DefaultClassTuning(engineClass);
        }

        private static EngineFuelTuning ResolveFuelTuning(EngineFuelType fuelType, bool hasTuning, BlobAssetReference<Space4XEngineTuningBlob> tuningBlob)
        {
            if (hasTuning)
            {
                var index = Space4XEngineTuningDefaults.ResolveFuelIndex(fuelType);
                if ((uint)index < (uint)tuningBlob.Value.FuelTuning.Length)
                {
                    return Space4XEngineTuningDefaults.Sanitize(tuningBlob.Value.FuelTuning[index]);
                }
            }

            return Space4XEngineTuningDefaults.DefaultFuelTuning(fuelType);
        }

        private static EngineIntakeTuning ResolveIntakeTuning(EngineIntakeType intakeType, bool hasTuning, BlobAssetReference<Space4XEngineTuningBlob> tuningBlob)
        {
            if (hasTuning)
            {
                var index = Space4XEngineTuningDefaults.ResolveIntakeIndex(intakeType);
                if ((uint)index < (uint)tuningBlob.Value.IntakeTuning.Length)
                {
                    return Space4XEngineTuningDefaults.Sanitize(tuningBlob.Value.IntakeTuning[index]);
                }
            }

            return Space4XEngineTuningDefaults.DefaultIntakeTuning(intakeType);
        }

        private static EngineVectoringTuning ResolveVectoringTuning(EngineVectoringMode vectoringMode, bool hasTuning, BlobAssetReference<Space4XEngineTuningBlob> tuningBlob)
        {
            if (hasTuning)
            {
                var index = Space4XEngineTuningDefaults.ResolveVectoringIndex(vectoringMode);
                if ((uint)index < (uint)tuningBlob.Value.VectoringTuning.Length)
                {
                    return Space4XEngineTuningDefaults.Sanitize(tuningBlob.Value.VectoringTuning[index]);
                }
            }

            return Space4XEngineTuningDefaults.DefaultVectoringTuning(vectoringMode);
        }

        private static float ResolveResponseRating(float techLevel, float quality, in EngineClassTuning classTuning, in EngineFuelTuning fuelTuning, in EngineIntakeTuning intakeTuning)
        {
            var baseRating = 0.5f + (quality - 0.5f) * 0.4f + (techLevel - 0.5f) * 0.25f;
            var response = baseRating * classTuning.ResponseMultiplier
                           + fuelTuning.ResponseBias
                           + intakeTuning.ResponseBias;
            return math.clamp(response, 0.05f, 1f);
        }

        private static float ResolveEfficiencyRating(float techLevel, float quality, in EngineClassTuning classTuning, in EngineFuelTuning fuelTuning, in EngineIntakeTuning intakeTuning)
        {
            var baseRating = 0.5f + (techLevel - 0.5f) * 0.35f + (quality - 0.5f) * 0.2f;
            var efficiency = baseRating * classTuning.EfficiencyMultiplier
                             + fuelTuning.EfficiencyBias
                             + intakeTuning.EfficiencyBias;
            return math.clamp(efficiency, 0.05f, 1f);
        }

        private static float ResolveBoostRating(float techLevel, float quality, in EngineClassTuning classTuning, in EngineFuelTuning fuelTuning, in EngineIntakeTuning intakeTuning)
        {
            var baseRating = 0.5f + (techLevel - 0.5f) * 0.25f + (quality - 0.5f) * 0.15f;
            var boost = baseRating * classTuning.BoostMultiplier
                        + fuelTuning.BoostBias
                        + intakeTuning.BoostBias;
            return math.clamp(boost, 0.05f, 1f);
        }

        private static float ResolveVectoringRating(float quality, in EngineVectoringTuning vectoringTuning)
        {
            var baseRating = math.saturate(vectoringTuning.BaseVectoring);
            return math.clamp(baseRating * math.lerp(0.9f, 1.1f, quality), 0f, 1f);
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(AISystemGroup))]
    [UpdateBefore(typeof(Space4XModulePostureCommandSystem))]
    public partial struct Space4XModuleAttachmentSyncSystem : ISystem
    {
        private BufferLookup<ModuleAttachment> _attachmentLookup;
        private ComponentLookup<ModuleOwner> _ownerLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CarrierModuleSlot>();
            _attachmentLookup = state.GetBufferLookup<ModuleAttachment>(false);
            _ownerLookup = state.GetComponentLookup<ModuleOwner>(false);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _attachmentLookup.Update(ref state);
            _ownerLookup.Update(ref state);

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (slots, ownerEntity) in SystemAPI.Query<DynamicBuffer<CarrierModuleSlot>>().WithEntityAccess())
            {
                if (!_attachmentLookup.HasBuffer(ownerEntity))
                {
                    ecb.AddBuffer<ModuleAttachment>(ownerEntity);
                    continue;
                }

                var attachments = _attachmentLookup[ownerEntity];
                attachments.Clear();

                for (var i = 0; i < slots.Length; i++)
                {
                    var module = slots[i].CurrentModule;
                    if (module == Entity.Null)
                    {
                        continue;
                    }

                    attachments.Add(new ModuleAttachment { Module = module });

                    if (_ownerLookup.HasComponent(module))
                    {
                        var owner = _ownerLookup[module];
                        if (owner.Owner != ownerEntity)
                        {
                            owner.Owner = ownerEntity;
                            _ownerLookup[module] = owner;
                        }
                    }
                    else
                    {
                        ecb.AddComponent(module, new ModuleOwner { Owner = ownerEntity });
                    }
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(AISystemGroup))]
    [UpdateAfter(typeof(Space4XModuleAttachmentSyncSystem))]
    public partial struct Space4XBridgeCockpitAggregationSystem : ISystem
    {
        private ComponentLookup<BridgeModuleProfile> _bridgeProfileLookup;
        private ComponentLookup<CockpitModuleProfile> _cockpitProfileLookup;
        private ComponentLookup<BridgeTechLevel> _bridgeTechLookup;
        private ComponentLookup<NavigationCohesion> _navigationLookup;
        private ComponentLookup<BridgeTechLevelBase> _bridgeBaseLookup;
        private ComponentLookup<NavigationCohesionBase> _navigationBaseLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ModuleAttachment>();

            _bridgeProfileLookup = state.GetComponentLookup<BridgeModuleProfile>(true);
            _cockpitProfileLookup = state.GetComponentLookup<CockpitModuleProfile>(true);
            _bridgeTechLookup = state.GetComponentLookup<BridgeTechLevel>(false);
            _navigationLookup = state.GetComponentLookup<NavigationCohesion>(false);
            _bridgeBaseLookup = state.GetComponentLookup<BridgeTechLevelBase>(true);
            _navigationBaseLookup = state.GetComponentLookup<NavigationCohesionBase>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _bridgeProfileLookup.Update(ref state);
            _cockpitProfileLookup.Update(ref state);
            _bridgeTechLookup.Update(ref state);
            _navigationLookup.Update(ref state);
            _bridgeBaseLookup.Update(ref state);
            _navigationBaseLookup.Update(ref state);

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (modules, owner) in SystemAPI.Query<DynamicBuffer<ModuleAttachment>>().WithEntityAccess())
            {
                var baseBridge = ResolveBaseBridge(owner, ref ecb);
                var baseNavigation = ResolveBaseNavigation(owner, ref ecb);

                var bridge = baseBridge;
                var navigation = baseNavigation;

                for (var i = 0; i < modules.Length; i++)
                {
                    var module = modules[i].Module;
                    if (module == Entity.Null)
                    {
                        continue;
                    }

                    if (_bridgeProfileLookup.HasComponent(module))
                    {
                        bridge = math.max(bridge, math.saturate(_bridgeProfileLookup[module].TechLevel));
                    }

                    if (_cockpitProfileLookup.HasComponent(module))
                    {
                        navigation = math.max(navigation, math.saturate(_cockpitProfileLookup[module].NavigationCohesion));
                    }
                }

                bridge = math.saturate(bridge);
                navigation = math.saturate(navigation);

                if (_bridgeTechLookup.HasComponent(owner))
                {
                    _bridgeTechLookup[owner] = new BridgeTechLevel { Value = bridge };
                }
                else
                {
                    ecb.AddComponent(owner, new BridgeTechLevel { Value = bridge });
                }

                if (_navigationLookup.HasComponent(owner))
                {
                    _navigationLookup[owner] = new NavigationCohesion { Value = navigation };
                }
                else
                {
                    ecb.AddComponent(owner, new NavigationCohesion { Value = navigation });
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private float ResolveBaseBridge(Entity owner, ref EntityCommandBuffer ecb)
        {
            if (_bridgeBaseLookup.HasComponent(owner))
            {
                return math.saturate(_bridgeBaseLookup[owner].Value);
            }

            var baseValue = _bridgeTechLookup.HasComponent(owner)
                ? math.saturate(_bridgeTechLookup[owner].Value)
                : 0.5f;
            if (baseValue <= 0f)
            {
                baseValue = 0.5f;
            }

            ecb.AddComponent(owner, new BridgeTechLevelBase { Value = baseValue });
            return baseValue;
        }

        private float ResolveBaseNavigation(Entity owner, ref EntityCommandBuffer ecb)
        {
            if (_navigationBaseLookup.HasComponent(owner))
            {
                return math.saturate(_navigationBaseLookup[owner].Value);
            }

            var baseValue = _navigationLookup.HasComponent(owner)
                ? math.saturate(_navigationLookup[owner].Value)
                : 0.5f;
            if (baseValue <= 0f)
            {
                baseValue = 0.5f;
            }

            ecb.AddComponent(owner, new NavigationCohesionBase { Value = baseValue });
            return baseValue;
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(AISystemGroup))]
    [UpdateAfter(typeof(Space4XModuleAttachmentSyncSystem))]
    public partial struct Space4XAmmoCapacityAggregationSystem : ISystem
    {
        private ComponentLookup<AmmoModuleProfile> _ammoProfileLookup;
        private ComponentLookup<SupplyStatus> _supplyLookup;
        private ComponentLookup<AmmoCapacityBase> _ammoBaseLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ModuleAttachment>();

            _ammoProfileLookup = state.GetComponentLookup<AmmoModuleProfile>(true);
            _supplyLookup = state.GetComponentLookup<SupplyStatus>(false);
            _ammoBaseLookup = state.GetComponentLookup<AmmoCapacityBase>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _ammoProfileLookup.Update(ref state);
            _supplyLookup.Update(ref state);
            _ammoBaseLookup.Update(ref state);

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (modules, owner) in SystemAPI.Query<DynamicBuffer<ModuleAttachment>>().WithEntityAccess())
            {
                if (!_supplyLookup.HasComponent(owner))
                {
                    continue;
                }

                var baseAmmo = ResolveBaseAmmo(owner, ref ecb);
                var ammoCapacity = baseAmmo;

                for (var i = 0; i < modules.Length; i++)
                {
                    var module = modules[i].Module;
                    if (module == Entity.Null)
                    {
                        continue;
                    }

                    if (_ammoProfileLookup.HasComponent(module))
                    {
                        ammoCapacity += math.max(0f, _ammoProfileLookup[module].AmmoCapacity);
                    }
                }

                var status = _supplyLookup[owner];
                ammoCapacity = math.max(0f, ammoCapacity);
                if (math.abs(status.AmmunitionCapacity - ammoCapacity) > 0.01f)
                {
                    status.AmmunitionCapacity = ammoCapacity;
                    status.Ammunition = math.min(status.Ammunition, ammoCapacity);
                    _supplyLookup[owner] = status;
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private float ResolveBaseAmmo(Entity owner, ref EntityCommandBuffer ecb)
        {
            if (_ammoBaseLookup.HasComponent(owner))
            {
                return math.max(0f, _ammoBaseLookup[owner].Value);
            }

            var baseValue = _supplyLookup.HasComponent(owner)
                ? math.max(0f, _supplyLookup[owner].AmmunitionCapacity)
                : 0f;

            ecb.AddComponent(owner, new AmmoCapacityBase { Value = baseValue });
            return baseValue;
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(AISystemGroup))]
    [UpdateAfter(typeof(Space4XModuleAttachmentSyncSystem))]
    public partial struct Space4XDefenseModuleAggregationSystem : ISystem
    {
        private ComponentLookup<ShieldModuleProfile> _shieldProfileLookup;
        private ComponentLookup<ArmorModuleProfile> _armorProfileLookup;
        private ComponentLookup<Space4XShield> _shieldLookup;
        private ComponentLookup<Space4XArmor> _armorLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ModuleAttachment>();
            state.RequireForUpdate<TimeState>();

            _shieldProfileLookup = state.GetComponentLookup<ShieldModuleProfile>(true);
            _armorProfileLookup = state.GetComponentLookup<ArmorModuleProfile>(true);
            _shieldLookup = state.GetComponentLookup<Space4XShield>(false);
            _armorLookup = state.GetComponentLookup<Space4XArmor>(false);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var fixedDt = math.max(1e-5f, SystemAPI.GetSingleton<TimeState>().FixedDeltaTime);

            _shieldProfileLookup.Update(ref state);
            _armorProfileLookup.Update(ref state);
            _shieldLookup.Update(ref state);
            _armorLookup.Update(ref state);

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (modules, owner) in SystemAPI.Query<DynamicBuffer<ModuleAttachment>>().WithEntityAccess())
            {
                var needShield = !_shieldLookup.HasComponent(owner);
                var needArmor = !_armorLookup.HasComponent(owner);
                if (!needShield && !needArmor)
                {
                    continue;
                }

                float shieldCap = 0f;
                float shieldRecharge = 0f;
                float shieldDelay = 0f;
                float shieldWeight = 0f;
                float shieldK = 0f;
                float shieldE = 0f;
                float shieldT = 0f;
                float shieldEM = 0f;
                float shieldR = 0f;
                float shieldX = 0f;
                float shieldC = 0f;

                float armorThickness = 0f;
                float armorReduction = 0f;
                float armorWeight = 0f;
                float armorK = 0f;
                float armorE = 0f;
                float armorT = 0f;
                float armorEM = 0f;
                float armorR = 0f;
                float armorX = 0f;
                float armorC = 0f;

                for (var i = 0; i < modules.Length; i++)
                {
                    var module = modules[i].Module;
                    if (module == Entity.Null)
                    {
                        continue;
                    }

                    if (needShield && _shieldProfileLookup.HasComponent(module))
                    {
                        var shield = _shieldProfileLookup[module];
                        if (shield.Capacity > 0f)
                        {
                            shieldCap += shield.Capacity;
                            shieldRecharge += math.max(0f, shield.RechargePerSecond);
                            shieldDelay = math.max(shieldDelay, math.max(0f, shield.RegenDelaySeconds));

                            var weight = math.max(1f, shield.Capacity);
                            shieldWeight += weight;
                            shieldK += math.saturate(shield.KineticResist) * weight;
                            shieldE += math.saturate(shield.EnergyResist) * weight;
                            shieldT += math.saturate(shield.ThermalResist) * weight;
                            shieldEM += math.saturate(shield.EMResist) * weight;
                            shieldR += math.saturate(shield.RadiationResist) * weight;
                            shieldX += math.saturate(shield.ExplosiveResist) * weight;
                            shieldC += math.saturate(shield.CausticResist) * weight;
                        }
                    }

                    if (needArmor && _armorProfileLookup.HasComponent(module))
                    {
                        var armor = _armorProfileLookup[module];
                        var thickness = math.max(0f, armor.HullBonus);
                        if (thickness > 0f)
                        {
                            armorThickness += thickness;
                            var weight = math.max(1f, thickness);
                            armorWeight += weight;
                            armorReduction += math.saturate(armor.DamageReduction) * weight;
                            armorK += math.saturate(armor.KineticResist) * weight;
                            armorE += math.saturate(armor.EnergyResist) * weight;
                            armorT += math.saturate(armor.ThermalResist) * weight;
                            armorEM += math.saturate(armor.EMResist) * weight;
                            armorR += math.saturate(armor.RadiationResist) * weight;
                            armorX += math.saturate(armor.ExplosiveResist) * weight;
                            armorC += math.saturate(armor.CausticResist) * weight;
                        }
                    }
                }

                if (needShield && shieldCap > 0f)
                {
                    var resistK = shieldWeight > 0f ? shieldK / shieldWeight : 1f;
                    var resistE = shieldWeight > 0f ? shieldE / shieldWeight : 1f;
                    var resistT = shieldWeight > 0f ? shieldT / shieldWeight : resistE;
                    var resistEM = shieldWeight > 0f ? shieldEM / shieldWeight : resistE;
                    var resistR = shieldWeight > 0f ? shieldR / shieldWeight : resistE;
                    var resistX = shieldWeight > 0f ? shieldX / shieldWeight : 1f;
                    var resistC = shieldWeight > 0f ? shieldC / shieldWeight : resistT;
                    if (resistC <= 0f)
                    {
                        resistC = resistT;
                    }
                    var delayTicks = (ushort)math.clamp(math.round(shieldDelay / fixedDt), 0, ushort.MaxValue);
                    var rechargePerTick = math.max(0f, shieldRecharge * fixedDt);

                    ecb.AddComponent(owner, new Space4XShield
                    {
                        Type = ShieldType.Standard,
                        Current = shieldCap,
                        Maximum = shieldCap,
                        RechargeRate = rechargePerTick,
                        RechargeDelay = delayTicks,
                        CurrentDelay = 0,
                        EnergyResistance = (half)math.saturate(resistE),
                        ThermalResistance = (half)math.saturate(resistT),
                        EMResistance = (half)math.saturate(resistEM),
                        RadiationResistance = (half)math.saturate(resistR),
                        KineticResistance = (half)math.saturate(resistK),
                        ExplosiveResistance = (half)math.saturate(resistX),
                        CausticResistance = (half)math.saturate(resistC)
                    });
                }

                if (needArmor && armorThickness > 0f)
                {
                    var resistK = armorWeight > 0f ? armorK / armorWeight : 1f;
                    var resistE = armorWeight > 0f ? armorE / armorWeight : 1f;
                    var resistT = armorWeight > 0f ? armorT / armorWeight : resistE;
                    var resistEM = armorWeight > 0f ? armorEM / armorWeight : resistE;
                    var resistR = armorWeight > 0f ? armorR / armorWeight : resistE;
                    var resistX = armorWeight > 0f ? armorX / armorWeight : 1f;
                    var resistC = armorWeight > 0f ? armorC / armorWeight : resistT;
                    if (resistC <= 0f)
                    {
                        resistC = resistT;
                    }
                    var reduction = armorWeight > 0f ? armorReduction / armorWeight : 0.3f;

                    ecb.AddComponent(owner, new Space4XArmor
                    {
                        Type = ArmorType.Standard,
                        Thickness = math.max(1f, armorThickness),
                        PenetrationThreshold = (half)math.clamp(0.2f + reduction * 0.6f, 0.05f, 1f),
                        EnergyResistance = (half)math.saturate(resistE),
                        ThermalResistance = (half)math.saturate(resistT),
                        EMResistance = (half)math.saturate(resistEM),
                        RadiationResistance = (half)math.saturate(resistR),
                        KineticResistance = (half)math.saturate(resistK),
                        ExplosiveResistance = (half)math.saturate(resistX),
                        CausticResistance = (half)math.saturate(resistC)
                    });
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(AISystemGroup))]
    [UpdateAfter(typeof(Space4XModuleAttachmentSyncSystem))]
    public partial struct Space4XSensorModuleAggregationSystem : ISystem
    {
        private ComponentLookup<SensorModuleProfile> _sensorProfileLookup;
        private ComponentLookup<SenseCapability> _senseLookup;
        private BufferLookup<SenseOrganState> _organLookup;
        private BufferLookup<PerceivedEntity> _perceivedLookup;
        private ComponentLookup<PerceptionState> _perceptionLookup;
        private ComponentLookup<SignalPerceptionState> _signalLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ModuleAttachment>();

            _sensorProfileLookup = state.GetComponentLookup<SensorModuleProfile>(true);
            _senseLookup = state.GetComponentLookup<SenseCapability>(false);
            _organLookup = state.GetBufferLookup<SenseOrganState>(false);
            _perceivedLookup = state.GetBufferLookup<PerceivedEntity>(false);
            _perceptionLookup = state.GetComponentLookup<PerceptionState>(false);
            _signalLookup = state.GetComponentLookup<SignalPerceptionState>(false);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _sensorProfileLookup.Update(ref state);
            _senseLookup.Update(ref state);
            _organLookup.Update(ref state);
            _perceivedLookup.Update(ref state);
            _perceptionLookup.Update(ref state);
            _signalLookup.Update(ref state);

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (modules, owner) in SystemAPI.Query<DynamicBuffer<ModuleAttachment>>().WithEntityAccess())
            {
                if (_senseLookup.HasComponent(owner))
                {
                    continue;
                }

                var range = 0f;
                var refresh = 1f;
                var resolution = 0f;
                var found = false;

                for (var i = 0; i < modules.Length; i++)
                {
                    var module = modules[i].Module;
                    if (module == Entity.Null || !_sensorProfileLookup.HasComponent(module))
                    {
                        continue;
                    }

                    var profile = _sensorProfileLookup[module];
                    range = math.max(range, profile.Range);
                    if (profile.RefreshSeconds > 0f)
                    {
                        refresh = math.min(refresh, profile.RefreshSeconds);
                    }
                    resolution = math.max(resolution, profile.Resolution);
                    found = true;
                }

                if (!found || range <= 0f)
                {
                    continue;
                }

                var acuity = math.clamp(resolution, 0.2f, 1f);
                var updateInterval = math.clamp(refresh, 0.05f, 1.5f);
                var maxTracked = (byte)math.clamp(8 + (int)(resolution * 16f), 4, 32);

                ecb.AddComponent(owner, new SenseCapability
                {
                    EnabledChannels = PerceptionChannel.EM | PerceptionChannel.Gravitic,
                    Range = range,
                    FieldOfView = 360f,
                    Acuity = acuity,
                    UpdateInterval = updateInterval,
                    MaxTrackedTargets = maxTracked,
                    Flags = 0
                });

                if (!_organLookup.HasBuffer(owner))
                {
                    var organs = ecb.AddBuffer<SenseOrganState>(owner);
                    organs.Add(new SenseOrganState
                    {
                        OrganType = SenseOrganType.EMSuite,
                        Channels = PerceptionChannel.EM,
                        Gain = 1f,
                        Condition = acuity,
                        NoiseFloor = 1f - acuity,
                        RangeMultiplier = 1f
                    });
                    organs.Add(new SenseOrganState
                    {
                        OrganType = SenseOrganType.GraviticArray,
                        Channels = PerceptionChannel.Gravitic,
                        Gain = 1f,
                        Condition = acuity,
                        NoiseFloor = 1f - acuity,
                        RangeMultiplier = 1f
                    });
                }

                if (!_perceivedLookup.HasBuffer(owner))
                {
                    ecb.AddBuffer<PerceivedEntity>(owner);
                }

                if (!_perceptionLookup.HasComponent(owner))
                {
                    ecb.AddComponent(owner, new PerceptionState());
                }

                if (!_signalLookup.HasComponent(owner))
                {
                    ecb.AddComponent(owner, new SignalPerceptionState());
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(AISystemGroup))]
    [UpdateAfter(typeof(Space4XModuleAttachmentSyncSystem))]
    public partial struct Space4XWeaponModuleAggregationSystem : ISystem
    {
        private ComponentLookup<ModuleTypeId> _moduleTypeLookup;
        private ComponentLookup<WeaponModuleProfile> _weaponProfileLookup;
        private ComponentLookup<ModuleLimbProfile> _limbProfileLookup;
        private BufferLookup<WeaponMount> _weaponMountLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ModuleAttachment>();
            state.RequireForUpdate<ModuleCatalogSingleton>();

            _moduleTypeLookup = state.GetComponentLookup<ModuleTypeId>(true);
            _weaponProfileLookup = state.GetComponentLookup<WeaponModuleProfile>(true);
            _limbProfileLookup = state.GetComponentLookup<ModuleLimbProfile>(true);
            _weaponMountLookup = state.GetBufferLookup<WeaponMount>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<ModuleCatalogSingleton>(out var catalog) || !catalog.Catalog.IsCreated)
            {
                return;
            }

            var hasWeaponSpecCatalog = SystemAPI.TryGetSingleton<WeaponCatalogSingleton>(out var weaponSpecCatalog) &&
                                       weaponSpecCatalog.Catalog.IsCreated;
            var hasProjectileSpecCatalog = SystemAPI.TryGetSingleton<ProjectileCatalogSingleton>(out var projectileSpecCatalog) &&
                                           projectileSpecCatalog.Catalog.IsCreated;

            ref var moduleSpecs = ref catalog.Catalog.Value.Modules;

            _moduleTypeLookup.Update(ref state);
            _weaponProfileLookup.Update(ref state);
            _limbProfileLookup.Update(ref state);
            _weaponMountLookup.Update(ref state);

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (modules, owner) in SystemAPI.Query<DynamicBuffer<ModuleAttachment>>().WithEntityAccess())
            {
                if (_weaponMountLookup.HasBuffer(owner))
                {
                    continue;
                }

                DynamicBuffer<WeaponMount> weaponBuffer = default;
                var created = false;

                for (var i = 0; i < modules.Length; i++)
                {
                    var module = modules[i].Module;
                    if (module == Entity.Null || !_moduleTypeLookup.HasComponent(module))
                    {
                        continue;
                    }

                    var moduleId = _moduleTypeLookup[module].Value;
                    if (!TryGetModuleSpec(ref moduleSpecs, moduleId, out var spec))
                    {
                        continue;
                    }

                    if (!IsWeaponModuleClass(spec.Class))
                    {
                        continue;
                    }

                    if (!created)
                    {
                        weaponBuffer = ecb.AddBuffer<WeaponMount>(owner);
                        created = true;
                    }

                    var weapon = ResolveWeapon(spec.Class, spec.RequiredSize);
                    var arcOffset = 0f;
                    var weaponId = FixedString64Bytes.Empty;
                    var limb = _limbProfileLookup.HasComponent(module) ? _limbProfileLookup[module] : default;
                    var cooling = math.clamp(limb.Cooling, 0f, 1f);
                    var sensors = math.clamp(limb.Sensors, 0f, 1f);
                    var lensing = math.clamp(limb.Lensing, 0f, 1f);
                    var hasWeaponSpec = false;
                    WeaponSpec weaponSpec = default;

                    if (_weaponProfileLookup.HasComponent(module))
                    {
                        var profile = _weaponProfileLookup[module];
                        if (profile.FireArcDegrees > 0f)
                        {
                            weapon.FireArcDegrees = profile.FireArcDegrees;
                        }

                        arcOffset = profile.FireArcOffsetDeg;
                        weapon.BaseAccuracy = (half)math.saturate((float)weapon.BaseAccuracy + profile.AccuracyBonus + profile.TrackingBonus * 0.25f);
                        weapon.Tracking = (half)math.saturate((float)weapon.Tracking + profile.TrackingBonus);
                        weaponId = profile.WeaponId;
                    }

                    if (weaponId.IsEmpty)
                    {
                        weaponId = moduleId;
                    }

                    if (weapon.Family == WeaponFamily.Unknown)
                    {
                        weapon.Family = Space4XWeapon.ResolveFamily(weapon.Type);
                    }

                    if (weapon.DamageType == Space4XDamageType.Unknown)
                    {
                        weapon.DamageType = Space4XWeapon.ResolveDamageType(weapon.Type);
                    }

                    if (weapon.Delivery == WeaponDelivery.Unknown)
                    {
                        weapon.Delivery = Space4XWeapon.ResolveDelivery(weapon.Type);
                    }

                    if (hasWeaponSpecCatalog && !weaponId.IsEmpty)
                    {
                        ref var weaponSpecs = ref weaponSpecCatalog.Catalog.Value.Weapons;
                        if (TryGetWeaponCatalogSpec(ref weaponSpecs, weaponId, out weaponSpec))
                        {
                            hasWeaponSpec = true;
                            ApplyWeaponSpecDamage(ref weapon, weaponSpec, hasProjectileSpecCatalog, projectileSpecCatalog);
                        }
                    }

                    var rangeScale = math.lerp(0.8f, 1.25f, lensing);
                    weapon.OptimalRange *= rangeScale;
                    weapon.MaxRange *= rangeScale;

                    var accuracyScale = math.lerp(0.75f, 1.25f, sensors);
                    weapon.BaseAccuracy = (half)math.saturate((float)weapon.BaseAccuracy * accuracyScale);
                    var trackingScale = math.lerp(0.8f, 1.2f, sensors);
                    weapon.Tracking = (half)math.saturate((float)weapon.Tracking * trackingScale);

                    var cooldownScale = math.lerp(1.35f, 0.75f, cooling);
                    weapon.CooldownTicks = (ushort)math.clamp((int)math.round(weapon.CooldownTicks * cooldownScale), 1, ushort.MaxValue);

                    var heatPerShot = ResolveHeatPerShot(weapon, hasWeaponSpec, weaponSpec);
                    var heatCapacity = math.lerp(0.6f, 1.4f, cooling);
                    var heatDissipation = math.lerp(0.01f, 0.06f, cooling);

                    weaponBuffer.Add(new WeaponMount
                    {
                        Weapon = weapon,
                        CurrentTarget = Entity.Null,
                        FireArcCenterOffsetDeg = (half)arcOffset,
                        IsEnabled = 1,
                        SourceModule = module,
                        CoolingRating = (half)cooling,
                        Heat01 = 0f,
                        HeatCapacity = heatCapacity,
                        HeatDissipation = heatDissipation,
                        HeatPerShot = heatPerShot
                    });
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private static bool TryGetModuleSpec(ref BlobArray<Space4X.Registry.ModuleSpec> modules, in FixedString64Bytes moduleId, out Space4X.Registry.ModuleSpec spec)
        {
            spec = default;
            for (int i = 0; i < modules.Length; i++)
            {
                if (modules[i].Id == moduleId)
                {
                    spec = modules[i];
                    return true;
                }
            }

            return false;
        }

        private static bool IsWeaponModuleClass(ModuleClass moduleClass)
        {
            return moduleClass == ModuleClass.Laser
                   || moduleClass == ModuleClass.Kinetic
                   || moduleClass == ModuleClass.Missile
                   || moduleClass == ModuleClass.PointDefense;
        }

        private static bool TryGetWeaponCatalogSpec(ref BlobArray<WeaponSpec> weapons, in FixedString64Bytes weaponId, out WeaponSpec spec)
        {
            spec = default;
            for (int i = 0; i < weapons.Length; i++)
            {
                if (weapons[i].Id == weaponId)
                {
                    spec = weapons[i];
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetProjectileCatalogSpec(ref BlobArray<ProjectileSpec> projectiles, in FixedString64Bytes projectileId, out ProjectileSpec spec)
        {
            spec = default;
            for (int i = 0; i < projectiles.Length; i++)
            {
                if (projectiles[i].Id == projectileId)
                {
                    spec = projectiles[i];
                    return true;
                }
            }

            return false;
        }

        private static void ApplyWeaponSpecDamage(
            ref Space4XWeapon weapon,
            in WeaponSpec weaponSpec,
            bool hasProjectileCatalog,
            ProjectileCatalogSingleton projectileCatalog)
        {
            var damageType = weaponSpec.DamageType;

            if (damageType == Space4XDamageType.Unknown && hasProjectileCatalog && !weaponSpec.ProjectileId.IsEmpty)
            {
                ref var projectiles = ref projectileCatalog.Catalog.Value.Projectiles;
                if (TryGetProjectileCatalogSpec(ref projectiles, weaponSpec.ProjectileId, out var projectileSpec))
                {
                    damageType = projectileSpec.DamageType;
                    if (damageType == Space4XDamageType.Unknown)
                    {
                        damageType = ResolveDamageTypeFromChannels(projectileSpec.Damage);
                    }
                }
            }

            if (damageType == Space4XDamageType.Unknown)
            {
                damageType = Space4XWeapon.ResolveDamageType(weapon.Type);
            }

            weapon.DamageType = damageType;
            weapon.Family = ResolveFamilyFromDamageType(damageType, weapon.Type);
        }

        private static float ResolveHeatPerShot(in Space4XWeapon weapon, bool hasWeaponSpec, in WeaponSpec weaponSpec)
        {
            var baseHeat = hasWeaponSpec ? weaponSpec.HeatCost : (0.6f + 0.2f * (int)weapon.Size);
            return math.max(0.02f, baseHeat * 0.08f);
        }

        private static Space4XDamageType ResolveDamageTypeFromChannels(in DamageModel damage)
        {
            if (damage.Explosive >= damage.Kinetic && damage.Explosive >= damage.Energy && damage.Explosive > 0f)
            {
                return Space4XDamageType.Explosive;
            }

            if (damage.Kinetic >= damage.Energy && damage.Kinetic > 0f)
            {
                return Space4XDamageType.Kinetic;
            }

            if (damage.Energy > 0f)
            {
                return Space4XDamageType.Energy;
            }

            return Space4XDamageType.Unknown;
        }

        private static WeaponFamily ResolveFamilyFromDamageType(Space4XDamageType damageType, WeaponType fallbackType)
        {
            return damageType switch
            {
                Space4XDamageType.Energy => WeaponFamily.Energy,
                Space4XDamageType.Thermal => WeaponFamily.Energy,
                Space4XDamageType.EM => WeaponFamily.Energy,
                Space4XDamageType.Radiation => WeaponFamily.Energy,
                Space4XDamageType.Caustic => WeaponFamily.Energy,
                Space4XDamageType.Kinetic => WeaponFamily.Kinetic,
                Space4XDamageType.Explosive => WeaponFamily.Explosive,
                _ => Space4XWeapon.ResolveFamily(fallbackType)
            };
        }

        private static Space4XWeapon ResolveWeapon(ModuleClass moduleClass, MountSize size)
        {
            var weaponSize = ResolveWeaponSize(size);
            return moduleClass switch
            {
                ModuleClass.Kinetic => Space4XWeapon.Kinetic(weaponSize),
                ModuleClass.Missile => Space4XWeapon.Missile(weaponSize),
                ModuleClass.PointDefense => CreatePointDefenseWeapon(weaponSize),
                _ => Space4XWeapon.Laser(weaponSize)
            };
        }

        private static WeaponSize ResolveWeaponSize(MountSize size)
        {
            return size switch
            {
                MountSize.S => WeaponSize.Small,
                MountSize.M => WeaponSize.Medium,
                MountSize.L => WeaponSize.Large,
                _ => WeaponSize.Small
            };
        }

        private static Space4XWeapon CreatePointDefenseWeapon(WeaponSize size)
        {
            return new Space4XWeapon
            {
                Type = WeaponType.PointDefense,
                Size = size,
                Family = WeaponFamily.Kinetic,
                DamageType = Space4XDamageType.Kinetic,
                Flags = WeaponFlags.None,
                BaseDamage = 6f * (1 + (int)size),
                OptimalRange = 180f + 40f * (int)size,
                MaxRange = 260f + 60f * (int)size,
                BaseAccuracy = (half)0.9f,
                Tracking = (half)Space4XWeapon.ResolveTracking(WeaponType.PointDefense, size),
                CooldownTicks = (ushort)(6 + 2 * (int)size),
                CurrentCooldown = 0,
                AmmoPerShot = 1,
                ShieldModifier = (half)0.7f,
                ArmorPenetration = (half)0.4f,
                FireArcDegrees = 220f
            };
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(AISystemGroup))]
    [UpdateAfter(typeof(Space4XCaptainPolicySystem))]
    [UpdateAfter(typeof(Space4XModuleAttachmentSyncSystem))]
    public partial struct Space4XModulePostureCommandSystem : ISystem
    {
        private ComponentLookup<ModuleRuntimeState> _runtimeLookup;
        private BufferLookup<ModuleCommand> _commandLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ManeuverMode>();
            state.RequireForUpdate<ModuleAttachment>();

            _runtimeLookup = state.GetComponentLookup<ModuleRuntimeState>(true);
            _commandLookup = state.GetBufferLookup<ModuleCommand>(false);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _runtimeLookup.Update(ref state);
            _commandLookup.Update(ref state);

            foreach (var (mode, modules) in SystemAPI.Query<RefRO<ManeuverMode>, DynamicBuffer<ModuleAttachment>>())
            {
                var desiredPosture = mode.ValueRO.Mode switch
                {
                    ShipManeuverMode.Anchor => ModulePosture.Off,
                    ShipManeuverMode.Transit => ModulePosture.Standby,
                    ShipManeuverMode.Maneuver => ModulePosture.Online,
                    _ => ModulePosture.Standby
                };

                var desiredTarget = desiredPosture switch
                {
                    ModulePosture.Online => 1f,
                    ModulePosture.Standby => 0.35f,
                    _ => 0f
                };

                for (var i = 0; i < modules.Length; i++)
                {
                    var module = modules[i].Module;
                    if (module == Entity.Null || !_commandLookup.HasBuffer(module))
                    {
                        continue;
                    }

                    if (_runtimeLookup.HasComponent(module))
                    {
                        var runtime = _runtimeLookup[module];
                        if (runtime.Posture == desiredPosture &&
                            math.abs(runtime.TargetOutput - desiredTarget) <= 0.01f)
                        {
                            continue;
                        }
                    }

                    var buffer = _commandLookup[module];
                    buffer.Add(new ModuleCommand
                    {
                        Posture = desiredPosture,
                        TargetOutput = desiredTarget,
                        Flags = ModuleCommandFlags.Posture | ModuleCommandFlags.TargetOutput
                    });
                }
            }
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(GameplaySystemGroup))]
    [UpdateAfter(typeof(PureDOTS.Systems.Modules.ModuleEffectsSystem))]
    public partial struct Space4XEngineAuthorityFallbackSystem : ISystem
    {
        private ComponentLookup<EnginePerformanceOutput> _engineOutputLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ModuleCapabilityOutput>();
            state.RequireForUpdate<EnginePerformanceOutput>();

            _engineOutputLookup = state.GetComponentLookup<EnginePerformanceOutput>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _engineOutputLookup.Update(ref state);

            foreach (var (capability, entity) in SystemAPI.Query<RefRW<ModuleCapabilityOutput>>().WithEntityAccess())
            {
                if (!_engineOutputLookup.HasComponent(entity))
                {
                    continue;
                }

                var engine = _engineOutputLookup[entity];
                if (engine.ThrustAuthority <= 0f)
                {
                    continue;
                }

                if (capability.ValueRO.TurnAuthority > 0.01f)
                {
                    continue;
                }

                var vectoring = math.saturate(engine.Vectoring);
                var fallback = engine.ThrustAuthority * math.lerp(0.35f, 0.95f, vectoring);
                capability.ValueRW.TurnAuthority = math.max(capability.ValueRO.TurnAuthority, math.max(0.05f, fallback));
            }
        }
    }
}

using PureDOTS.Runtime.Logistics.Components;
using Space4X.Runtime;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    /// <summary>
    /// Adds baseline non-capital customization components to strike craft, miners, and haulers.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct Space4XCraftCustomizationBootstrapSystem : ISystem
    {
        private ComponentLookup<StrikeCraftPilotLink> _strikePilotLookup;
        private ComponentLookup<VesselPilotLink> _vesselPilotLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _strikePilotLookup = state.GetComponentLookup<StrikeCraftPilotLink>(true);
            _vesselPilotLookup = state.GetComponentLookup<VesselPilotLink>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _strikePilotLookup.Update(ref state);
            _vesselPilotLookup.Update(ref state);

            var em = state.EntityManager;
            var query = SystemAPI.QueryBuilder()
                .WithAny<StrikeCraftProfile, MiningVessel, Space4XHaulerShuttleState, HaulerTag>()
                .WithNone<Prefab>()
                .Build();
            if (query.IsEmptyIgnoreFilter)
            {
                return;
            }

            using var entities = query.ToEntityArray(Allocator.Temp);
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            for (var i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                var kind = ResolveCraftKind(em, entity);
                var controlMode = ResolveControlMode(kind, entity, ref _strikePilotLookup, ref _vesselPilotLookup);
                var defaultMaxMass = ResolveDefaultMaxMass(em, entity, kind);

                if (!em.HasComponent<NonCapitalCraftProfile>(entity))
                {
                    var profile = Space4XCraftCustomizationUtility.CreateDefaultProfile(kind, controlMode, defaultMaxMass);
                    ecb.AddComponent(entity, profile);
                }

                if (!em.HasComponent<CraftInternalState>(entity))
                {
                    var massClass = Space4XCraftCustomizationUtility.ResolveMassClassFromCap(defaultMaxMass);
                    ecb.AddComponent(entity, CraftInternalState.ForMassClass(massClass));
                }

                if (!em.HasComponent<CraftCustomizationPolicy>(entity))
                {
                    ecb.AddComponent(entity, CraftCustomizationPolicy.Default);
                }

                if (!em.HasComponent<CraftLoadoutAggregate>(entity))
                {
                    ecb.AddComponent<CraftLoadoutAggregate>(entity);
                }

                if (!em.HasComponent<CraftPerformanceBaseline>(entity))
                {
                    ecb.AddComponent<CraftPerformanceBaseline>(entity);
                }

                if (!em.HasBuffer<CraftModuleInstance>(entity))
                {
                    ecb.AddBuffer<CraftModuleInstance>(entity);
                }

                if (!em.HasBuffer<CraftModuleSlot>(entity))
                {
                    var slots = ecb.AddBuffer<CraftModuleSlot>(entity);
                    PopulateDefaultSlots(kind, ref slots);
                }
                else
                {
                    var slots = em.GetBuffer<CraftModuleSlot>(entity);
                    if (slots.Length == 0)
                    {
                        PopulateDefaultSlots(kind, ref slots);
                    }
                }
            }

            ecb.Playback(em);
            ecb.Dispose();
        }

        private static NonCapitalCraftKind ResolveCraftKind(EntityManager em, Entity entity)
        {
            if (em.HasComponent<StrikeCraftProfile>(entity))
            {
                return NonCapitalCraftKind.StrikeCraft;
            }

            if (em.HasComponent<MiningVessel>(entity))
            {
                return NonCapitalCraftKind.MiningVessel;
            }

            if (em.HasComponent<HaulerTag>(entity) || em.HasComponent<Space4XHaulerShuttleState>(entity))
            {
                return NonCapitalCraftKind.Hauler;
            }

            return NonCapitalCraftKind.Utility;
        }

        private static NonCapitalCraftControlMode ResolveControlMode(
            NonCapitalCraftKind kind,
            Entity entity,
            ref ComponentLookup<StrikeCraftPilotLink> strikePilotLookup,
            ref ComponentLookup<VesselPilotLink> vesselPilotLookup)
        {
            switch (kind)
            {
                case NonCapitalCraftKind.StrikeCraft:
                    if (strikePilotLookup.HasComponent(entity) &&
                        strikePilotLookup[entity].Pilot != Entity.Null)
                    {
                        return NonCapitalCraftControlMode.Piloted;
                    }
                    return NonCapitalCraftControlMode.RemoteDrone;
                case NonCapitalCraftKind.MiningVessel:
                    if (vesselPilotLookup.HasComponent(entity) &&
                        vesselPilotLookup[entity].Pilot != Entity.Null)
                    {
                        return NonCapitalCraftControlMode.Piloted;
                    }
                    return NonCapitalCraftControlMode.RemoteDrone;
                case NonCapitalCraftKind.Hauler:
                    return NonCapitalCraftControlMode.AutonomousDrone;
                default:
                    return NonCapitalCraftControlMode.RemoteDrone;
            }
        }

        private static float ResolveDefaultMaxMass(EntityManager em, Entity entity, NonCapitalCraftKind kind)
        {
            if (kind == NonCapitalCraftKind.StrikeCraft && em.HasComponent<StrikeCraftProfile>(entity))
            {
                var role = em.GetComponentData<StrikeCraftProfile>(entity).Role;
                return role switch
                {
                    StrikeCraftRole.Interceptor => 40f,
                    StrikeCraftRole.Bomber => 68f,
                    StrikeCraftRole.Recon => 38f,
                    StrikeCraftRole.Suppression => 72f,
                    StrikeCraftRole.EWar => 45f,
                    _ => 50f
                };
            }

            return kind switch
            {
                NonCapitalCraftKind.MiningVessel => 90f,
                NonCapitalCraftKind.Hauler => 120f,
                NonCapitalCraftKind.Drone => 20f,
                _ => 48f
            };
        }

        private static void PopulateDefaultSlots(NonCapitalCraftKind kind, ref DynamicBuffer<CraftModuleSlot> slots)
        {
            slots.Clear();
            switch (kind)
            {
                case NonCapitalCraftKind.StrikeCraft:
                    slots.Add(new CraftModuleSlot { SlotId = 0, Category = CraftModuleCategory.Propulsion, SizeBudget = 2, IsFixed = 1 });
                    slots.Add(new CraftModuleSlot { SlotId = 1, Category = CraftModuleCategory.Weapon, SizeBudget = 2, IsFixed = 0 });
                    slots.Add(new CraftModuleSlot { SlotId = 2, Category = CraftModuleCategory.Weapon, SizeBudget = 2, IsFixed = 0 });
                    slots.Add(new CraftModuleSlot { SlotId = 3, Category = CraftModuleCategory.Defense, SizeBudget = 2, IsFixed = 0 });
                    slots.Add(new CraftModuleSlot { SlotId = 4, Category = CraftModuleCategory.Utility, SizeBudget = 1, IsFixed = 0 });
                    break;
                case NonCapitalCraftKind.MiningVessel:
                    slots.Add(new CraftModuleSlot { SlotId = 0, Category = CraftModuleCategory.Propulsion, SizeBudget = 2, IsFixed = 1 });
                    slots.Add(new CraftModuleSlot { SlotId = 1, Category = CraftModuleCategory.MiningTool, SizeBudget = 2, IsFixed = 0 });
                    slots.Add(new CraftModuleSlot { SlotId = 2, Category = CraftModuleCategory.MiningTool, SizeBudget = 2, IsFixed = 0 });
                    slots.Add(new CraftModuleSlot { SlotId = 3, Category = CraftModuleCategory.Cargo, SizeBudget = 3, IsFixed = 0 });
                    slots.Add(new CraftModuleSlot { SlotId = 4, Category = CraftModuleCategory.Utility, SizeBudget = 2, IsFixed = 0 });
                    break;
                case NonCapitalCraftKind.Hauler:
                    slots.Add(new CraftModuleSlot { SlotId = 0, Category = CraftModuleCategory.Propulsion, SizeBudget = 2, IsFixed = 1 });
                    slots.Add(new CraftModuleSlot { SlotId = 1, Category = CraftModuleCategory.Cargo, SizeBudget = 3, IsFixed = 0 });
                    slots.Add(new CraftModuleSlot { SlotId = 2, Category = CraftModuleCategory.Cargo, SizeBudget = 3, IsFixed = 0 });
                    slots.Add(new CraftModuleSlot { SlotId = 3, Category = CraftModuleCategory.Support, SizeBudget = 2, IsFixed = 0 });
                    slots.Add(new CraftModuleSlot { SlotId = 4, Category = CraftModuleCategory.Utility, SizeBudget = 1, IsFixed = 0 });
                    break;
                default:
                    slots.Add(new CraftModuleSlot { SlotId = 0, Category = CraftModuleCategory.Propulsion, SizeBudget = 1, IsFixed = 1 });
                    slots.Add(new CraftModuleSlot { SlotId = 1, Category = CraftModuleCategory.Utility, SizeBudget = 1, IsFixed = 0 });
                    break;
            }
        }
    }

    /// <summary>
    /// Aggregates craft internals/modules into deterministic runtime multipliers and projects them onto active craft stats.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(Space4XStrikeCraftMovementSystem))]
    [UpdateBefore(typeof(Space4X.Systems.AI.VesselMovementSystem))]
    public partial struct Space4XCraftCustomizationSystem : ISystem
    {
        private BufferLookup<CraftModuleSlot> _slotLookup;
        private BufferLookup<ModuleLimbState> _limbLookup;
        private ComponentLookup<VesselMovement> _movementLookup;
        private ComponentLookup<MiningVessel> _miningLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NonCapitalCraftProfile>();
            _slotLookup = state.GetBufferLookup<CraftModuleSlot>(true);
            _limbLookup = state.GetBufferLookup<ModuleLimbState>(true);
            _movementLookup = state.GetComponentLookup<VesselMovement>(false);
            _miningLookup = state.GetComponentLookup<MiningVessel>(false);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _slotLookup.Update(ref state);
            _limbLookup.Update(ref state);
            _movementLookup.Update(ref state);
            _miningLookup.Update(ref state);

            foreach (var (profileRef, internalRef, policyRef, aggregateRef, baselineRef, modules, entity) in
                     SystemAPI.Query<
                         RefRW<NonCapitalCraftProfile>,
                         RefRW<CraftInternalState>,
                         RefRO<CraftCustomizationPolicy>,
                         RefRW<CraftLoadoutAggregate>,
                         RefRW<CraftPerformanceBaseline>,
                         DynamicBuffer<CraftModuleInstance>>()
                         .WithEntityAccess())
            {
                var profile = profileRef.ValueRO;
                var internals = internalRef.ValueRO;
                var policy = policyRef.ValueRO;
                var baseline = baselineRef.ValueRO;

                profile.MaxMassTons = math.max(8f, profile.MaxMassTons);
                profile.MassClass = Space4XCraftCustomizationUtility.ResolveMassClassFromCap(profile.MaxMassTons);
                if (profile.BaseMassTons <= 0f)
                {
                    profile.BaseMassTons = profile.MaxMassTons * 0.45f;
                }
                profileRef.ValueRW = profile;

                var coreIntegrity = math.saturate((internals.CoreIntegrity + internals.EngineIntegrity + internals.AvionicsIntegrity + internals.LifeSupportIntegrity) * 0.25f);
                var limbIntegrity = ResolveAverageLimbIntegrity(entity, ref _limbLookup);
                coreIntegrity *= limbIntegrity;

                var totalMass = math.max(0f, profile.BaseMassTons) + math.max(0f, internals.InternalMassTons);
                var heatLoad = math.max(0f, internals.BaseHeatLoad);
                var heatDissipation = math.max(0f, internals.HeatDissipation);
                var thrustBonus = 0f;
                var armorBonus = 0f;
                var hullBonus = 0f;
                var miningBonus = 0f;
                var cargoBonus = 0f;
                var transferBonus = 0f;
                var evasionBonus = 0f;

                var hasPropulsion = internals.EngineIntegrity > 0.05f;
                var hasControlCore = internals.AvionicsIntegrity > 0.05f || profile.ControlMode != NonCapitalCraftControlMode.Piloted;
                var slotMismatchCount = 0;

                for (var i = 0; i < modules.Length; i++)
                {
                    var module = modules[i];
                    totalMass += math.max(0f, module.MassTons);
                    heatLoad += math.max(0f, module.HeatLoad);
                    heatDissipation += math.max(0f, module.HeatDissipation);
                    thrustBonus += module.ThrustBonus;
                    armorBonus += module.ArmorBonus;
                    hullBonus += module.HullBonus;
                    miningBonus += module.MiningYieldBonus;
                    cargoBonus += module.CargoBonus;
                    transferBonus += module.TransferBonus;
                    evasionBonus += module.EvasionBonus;

                    if (module.Category == CraftModuleCategory.Propulsion)
                    {
                        hasPropulsion = true;
                    }

                    if (module.Category == CraftModuleCategory.Sensor ||
                        module.Category == CraftModuleCategory.Utility ||
                        module.Category == CraftModuleCategory.ElectronicWarfare)
                    {
                        hasControlCore = true;
                    }

                    if (!IsModuleCompatibleWithSlots(entity, module, ref _slotLookup))
                    {
                        slotMismatchCount++;
                    }
                }

                var massUtilization = totalMass / profile.MaxMassTons;
                var overMassRatio = math.max(0f, massUtilization - 1f);
                var overMassTons = math.max(0f, totalMass - profile.MaxMassTons);
                var heatBalance = heatDissipation - heatLoad;
                var heatDeficitRatio = math.max(0f, -heatBalance) / math.max(0.1f, heatLoad);
                var isHeatDeficit = heatDeficitRatio > math.max(0f, policy.HeatDeficitTolerance);

                var speedMultiplier = (1f + thrustBonus * 0.2f) * coreIntegrity;
                speedMultiplier *= math.max(0.2f, 1f - overMassRatio * math.max(0f, policy.OverMassSpeedPenaltyPerRatio));
                speedMultiplier *= math.max(0.35f, 1f - heatDeficitRatio * math.max(0f, policy.HeatPenaltyScale));

                var turnMultiplier = (1f + (thrustBonus * 0.08f + evasionBonus * 0.12f)) * coreIntegrity;
                turnMultiplier *= math.max(0.2f, 1f - overMassRatio * math.max(0f, policy.OverMassTurnPenaltyPerRatio));
                turnMultiplier *= math.max(0.45f, 1f - heatDeficitRatio * math.max(0f, policy.HeatPenaltyScale) * 0.65f);

                speedMultiplier = math.clamp(speedMultiplier, 0.2f, 2.5f);
                turnMultiplier = math.clamp(turnMultiplier, 0.2f, 2.5f);

                var miningMultiplier = math.clamp(1f + miningBonus * 0.3f, 0.4f, 3f);
                var cargoMultiplier = math.clamp(1f + cargoBonus * 0.4f, 0.4f, 3f);
                var transferMultiplier = math.clamp(1f + transferBonus * 0.35f + cargoBonus * 0.1f, 0.4f, 3f);
                var evasionMultiplier = math.clamp(
                    Space4XCraftCustomizationUtility.ResolveMassClassEvasionBase(profile.MassClass) + evasionBonus * 0.25f - overMassRatio * 0.2f,
                    0.35f,
                    2.2f);

                var violations = CraftBuildViolation.None;
                if (overMassRatio > 1e-5f)
                {
                    violations |= CraftBuildViolation.OverMass;
                }

                if (!hasPropulsion)
                {
                    violations |= CraftBuildViolation.MissingPropulsion;
                }

                if (!hasControlCore)
                {
                    violations |= CraftBuildViolation.MissingControlCore;
                }

                if (isHeatDeficit)
                {
                    violations |= CraftBuildViolation.HeatDeficit;
                }

                if (slotMismatchCount > 0)
                {
                    violations |= CraftBuildViolation.SlotMismatch;
                }

                if (policy.AllowUnsafeBuilds == 0 && violations != CraftBuildViolation.None)
                {
                    speedMultiplier = math.min(speedMultiplier, 0.75f);
                    turnMultiplier = math.min(turnMultiplier, 0.8f);
                }

                aggregateRef.ValueRW = new CraftLoadoutAggregate
                {
                    TotalMassTons = totalMass,
                    MassUtilization = massUtilization,
                    OverMassTons = overMassTons,
                    CoreIntegrity = coreIntegrity,
                    HeatLoad = heatLoad,
                    HeatDissipation = heatDissipation,
                    HeatBalance = heatBalance,
                    EffectiveSpeedMultiplier = speedMultiplier,
                    EffectiveTurnMultiplier = turnMultiplier,
                    MiningYieldMultiplier = miningMultiplier,
                    CargoMultiplier = cargoMultiplier,
                    TransferMultiplier = transferMultiplier,
                    EvasionMultiplier = evasionMultiplier,
                    BonusHull = hullBonus,
                    BonusArmor = armorBonus,
                    Violations = violations
                };

                if (profile.Kind != NonCapitalCraftKind.StrikeCraft && _movementLookup.HasComponent(entity))
                {
                    var movement = _movementLookup[entity];
                    if (baseline.VesselBaseCaptured == 0 || baseline.VesselBaseSpeed <= 0f)
                    {
                        baseline.VesselBaseCaptured = 1;
                        baseline.VesselBaseSpeed = math.max(0.1f, movement.BaseSpeed);
                    }

                    movement.BaseSpeed = math.max(0.1f, baseline.VesselBaseSpeed * speedMultiplier);
                    _movementLookup[entity] = movement;
                }

                if (_miningLookup.HasComponent(entity))
                {
                    var mining = _miningLookup[entity];
                    if (baseline.MiningBaseCaptured == 0)
                    {
                        baseline.MiningBaseCaptured = 1;
                        baseline.MiningBaseSpeed = math.max(0.1f, mining.Speed);
                        baseline.MiningBaseEfficiency = math.max(0.01f, mining.MiningEfficiency);
                        baseline.MiningBaseCargoCapacity = math.max(1f, mining.CargoCapacity);
                    }

                    mining.Speed = math.max(0.1f, baseline.MiningBaseSpeed * speedMultiplier);
                    mining.MiningEfficiency = math.max(0.01f, baseline.MiningBaseEfficiency * miningMultiplier);
                    mining.CargoCapacity = math.max(1f, baseline.MiningBaseCargoCapacity * cargoMultiplier);
                    mining.CurrentCargo = math.min(mining.CurrentCargo, mining.CargoCapacity);
                    _miningLookup[entity] = mining;
                }

                baselineRef.ValueRW = baseline;
            }
        }

        private static bool IsModuleCompatibleWithSlots(
            Entity entity,
            in CraftModuleInstance module,
            ref BufferLookup<CraftModuleSlot> slotLookup)
        {
            if (!slotLookup.HasBuffer(entity))
            {
                return false;
            }

            var slots = slotLookup[entity];
            for (var i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                if (slot.SlotId != module.SlotId)
                {
                    continue;
                }

                return slot.Category == module.Category ||
                       slot.Category == CraftModuleCategory.Utility ||
                       slot.Category == CraftModuleCategory.Support;
            }

            return false;
        }

        private static float ResolveAverageLimbIntegrity(
            Entity entity,
            ref BufferLookup<ModuleLimbState> limbLookup)
        {
            if (!limbLookup.HasBuffer(entity))
            {
                return 1f;
            }

            var limbs = limbLookup[entity];
            if (limbs.Length == 0)
            {
                return 1f;
            }

            var sum = 0f;
            for (var i = 0; i < limbs.Length; i++)
            {
                sum += math.saturate(limbs[i].Integrity);
            }

            return math.saturate(sum / limbs.Length);
        }
    }
}

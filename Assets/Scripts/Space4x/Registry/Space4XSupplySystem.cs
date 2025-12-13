using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Registry
{
    using Debug = UnityEngine.Debug;

    
    /// <summary>
    /// Consumes supplies based on activity level.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct Space4XSupplyConsumptionSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SupplyStatus>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (status, rates, entity) in
                SystemAPI.Query<RefRW<SupplyStatus>, RefRO<SupplyConsumptionRates>>()
                    .WithEntityAccess())
            {
                // Calculate consumption based on activity
                float fuelConsumption = SupplyUtility.CalculateFuelConsumption(rates.ValueRO, status.ValueRO.Activity);

                // Apply consumption
                status.ValueRW.Fuel = math.max(0, status.ValueRO.Fuel - fuelConsumption);
                status.ValueRW.Provisions = math.max(0, status.ValueRO.Provisions - rates.ValueRO.ProvisionsBase);
                status.ValueRW.LifeSupport = math.max(0, status.ValueRO.LifeSupport - rates.ValueRO.LifeSupportBase);

                // Ammo only consumed in combat
                if (status.ValueRO.Activity == ActivityLevel.Combat)
                {
                    status.ValueRW.Ammunition = math.max(0, status.ValueRO.Ammunition - rates.ValueRO.AmmoCombat);
                }

                status.ValueRW.TicksSinceResupply++;

                // Check for critical status
                bool isCritical = status.ValueRO.FuelRatio < 0.1f ||
                                  status.ValueRO.ProvisionsRatio < 0.1f ||
                                  status.ValueRO.LifeSupportRatio < 0.1f;

                if (isCritical && !SystemAPI.HasComponent<SupplyCriticalTag>(entity))
                {
                    ecb.AddComponent<SupplyCriticalTag>(entity);
                }
                else if (!isCritical && SystemAPI.HasComponent<SupplyCriticalTag>(entity))
                {
                    ecb.RemoveComponent<SupplyCriticalTag>(entity);
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }

    /// <summary>
    /// Generates supply alerts for low resources.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XSupplyConsumptionSystem))]
    public partial struct Space4XSupplyAlertSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SupplyStatus>();
            state.RequireForUpdate<SupplyAlert>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var currentTick = (uint)SystemAPI.Time.ElapsedTime;

            foreach (var (status, rates, alerts, entity) in
                SystemAPI.Query<RefRO<SupplyStatus>, RefRO<SupplyConsumptionRates>, DynamicBuffer<SupplyAlert>>()
                    .WithEntityAccess())
            {
                var alertsBuffer = alerts;

                alertsBuffer.Clear();

                // Check fuel
                byte fuelSeverity = SupplyUtility.GetAlertSeverity(status.ValueRO.FuelRatio);
                if (fuelSeverity < 255)
                {
                    float consumption = SupplyUtility.CalculateFuelConsumption(rates.ValueRO, status.ValueRO.Activity);
                    alertsBuffer.Add(new SupplyAlert
                    {
                        Type = SupplyType.Fuel,
                        Severity = fuelSeverity,
                        CurrentRatio = (half)status.ValueRO.FuelRatio,
                        TicksUntilDepletion = SupplyUtility.CalculateTicksUntilDepletion(status.ValueRO.Fuel, consumption),
                        AlertTick = currentTick
                    });
                }

                // Check ammunition
                byte ammoSeverity = SupplyUtility.GetAlertSeverity(status.ValueRO.AmmoRatio);
                if (ammoSeverity < 255)
                {
                    alertsBuffer.Add(new SupplyAlert
                    {
                        Type = SupplyType.Ammunition,
                        Severity = ammoSeverity,
                        CurrentRatio = (half)status.ValueRO.AmmoRatio,
                        TicksUntilDepletion = SupplyUtility.CalculateTicksUntilDepletion(status.ValueRO.Ammunition, rates.ValueRO.AmmoCombat),
                        AlertTick = currentTick
                    });
                }

                // Check provisions
                byte provSeverity = SupplyUtility.GetAlertSeverity(status.ValueRO.ProvisionsRatio);
                if (provSeverity < 255)
                {
                    alertsBuffer.Add(new SupplyAlert
                    {
                        Type = SupplyType.Provisions,
                        Severity = provSeverity,
                        CurrentRatio = (half)status.ValueRO.ProvisionsRatio,
                        TicksUntilDepletion = SupplyUtility.CalculateTicksUntilDepletion(status.ValueRO.Provisions, rates.ValueRO.ProvisionsBase),
                        AlertTick = currentTick
                    });
                }

                // Check life support
                byte lsSeverity = SupplyUtility.GetAlertSeverity(status.ValueRO.LifeSupportRatio);
                if (lsSeverity < 255)
                {
                    alertsBuffer.Add(new SupplyAlert
                    {
                        Type = SupplyType.LifeSupport,
                        Severity = lsSeverity,
                        CurrentRatio = (half)status.ValueRO.LifeSupportRatio,
                        TicksUntilDepletion = SupplyUtility.CalculateTicksUntilDepletion(status.ValueRO.LifeSupport, rates.ValueRO.LifeSupportBase),
                        AlertTick = currentTick
                    });
                }
            }
        }
    }

    /// <summary>
    /// Tracks and validates supply routes.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct Space4XSupplyRouteSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SupplyRoute>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (route, entity) in
                SystemAPI.Query<RefRW<SupplyRoute>>()
                    .WithEntityAccess())
            {
                // Skip inactive routes
                if (route.ValueRO.Status == SupplyRouteStatus.None ||
                    route.ValueRO.Status == SupplyRouteStatus.Completed ||
                    route.ValueRO.Status == SupplyRouteStatus.Cancelled)
                {
                    continue;
                }

                // Validate source exists
                if (route.ValueRO.Source == Entity.Null || !SystemAPI.Exists(route.ValueRO.Source))
                {
                    route.ValueRW.Status = SupplyRouteStatus.Disrupted;
                    continue;
                }

                // Validate destination exists
                if (route.ValueRO.Destination == Entity.Null || !SystemAPI.Exists(route.ValueRO.Destination))
                {
                    route.ValueRW.Status = SupplyRouteStatus.Cancelled;
                    continue;
                }

                // Update distance
                if (SystemAPI.HasComponent<LocalTransform>(route.ValueRO.Source) &&
                    SystemAPI.HasComponent<LocalTransform>(route.ValueRO.Destination))
                {
                    var sourcePos = SystemAPI.GetComponent<LocalTransform>(route.ValueRO.Source).Position;
                    var destPos = SystemAPI.GetComponent<LocalTransform>(route.ValueRO.Destination).Position;
                    route.ValueRW.Distance = math.distance(sourcePos, destPos);

                    // Estimate ETA based on distance
                    route.ValueRW.ETAResupply = (uint)(route.ValueRO.Distance / 10f); // Assume speed of 10
                }

                // Check source availability
                if (SystemAPI.HasComponent<SupplySource>(route.ValueRO.Source))
                {
                    var source = SystemAPI.GetComponent<SupplySource>(route.ValueRO.Source);
                    if (source.IsAvailable == 0)
                    {
                        route.ValueRW.Status = SupplyRouteStatus.Disrupted;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Handles resupply delivery when routes complete.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XSupplyRouteSystem))]
    public partial struct Space4XSupplyDeliverySystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SupplyRoute>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (route, entity) in
                SystemAPI.Query<RefRW<SupplyRoute>>()
                    .WithEntityAccess())
            {
                if (route.ValueRO.Status != SupplyRouteStatus.Delivering)
                {
                    continue;
                }

                // Get destination supply status
                if (!SystemAPI.HasComponent<SupplyStatus>(route.ValueRO.Destination))
                {
                    route.ValueRW.Status = SupplyRouteStatus.Cancelled;
                    continue;
                }

                // Get source info
                if (!SystemAPI.HasComponent<SupplySource>(route.ValueRO.Source))
                {
                    route.ValueRW.Status = SupplyRouteStatus.Cancelled;
                    continue;
                }

                var source = SystemAPI.GetComponent<SupplySource>(route.ValueRO.Source);
                var destStatus = SystemAPI.GetComponent<SupplyStatus>(route.ValueRO.Destination);

                // Transfer supplies
                float fuelTransfer = math.min(source.TransferRate, source.AvailableFuel);
                float fuelNeeded = destStatus.FuelCapacity - destStatus.Fuel;
                fuelTransfer = math.min(fuelTransfer, fuelNeeded);

                float ammoTransfer = math.min(source.TransferRate * 0.5f, source.AvailableAmmo);
                float ammoNeeded = destStatus.AmmunitionCapacity - destStatus.Ammunition;
                ammoTransfer = math.min(ammoTransfer, ammoNeeded);

                float provTransfer = math.min(source.TransferRate * 0.2f, source.AvailableProvisions);
                float provNeeded = destStatus.ProvisionsCapacity - destStatus.Provisions;
                provTransfer = math.min(provTransfer, provNeeded);

                // Apply transfers
                destStatus.Fuel += fuelTransfer;
                destStatus.Ammunition += ammoTransfer;
                destStatus.Provisions += provTransfer;
                destStatus.TicksSinceResupply = 0;

                SystemAPI.SetComponent(route.ValueRO.Destination, destStatus);

                // Update source
                var sourceUpdated = source;
                sourceUpdated.AvailableFuel -= fuelTransfer;
                sourceUpdated.AvailableAmmo -= ammoTransfer;
                sourceUpdated.AvailableProvisions -= provTransfer;
                SystemAPI.SetComponent(route.ValueRO.Source, sourceUpdated);

                // Check if delivery complete
                bool destFull = destStatus.FuelRatio >= 0.95f &&
                               destStatus.AmmoRatio >= 0.95f &&
                               destStatus.ProvisionsRatio >= 0.95f;

                bool sourceDepleted = source.AvailableFuel <= 0 &&
                                     source.AvailableAmmo <= 0 &&
                                     source.AvailableProvisions <= 0;

                if (destFull || sourceDepleted)
                {
                    route.ValueRW.Status = SupplyRouteStatus.Completed;
                }
            }
        }
    }

    /// <summary>
    /// Handles emergency harvest operations.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct Space4XEmergencyHarvestSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EmergencyHarvest>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (harvest, status, transform, entity) in
                SystemAPI.Query<RefRW<EmergencyHarvest>, RefRW<SupplyStatus>, RefRO<LocalTransform>>()
                    .WithEntityAccess())
            {
                if (harvest.ValueRO.IsHarvesting == 0)
                {
                    continue;
                }

                // Check if still near target
                if (harvest.ValueRO.Target != Entity.Null && SystemAPI.HasComponent<LocalTransform>(harvest.ValueRO.Target))
                {
                    var targetPos = SystemAPI.GetComponent<LocalTransform>(harvest.ValueRO.Target).Position;
                    float distance = math.distance(transform.ValueRO.Position, targetPos);

                    if (distance > 100f)
                    {
                        harvest.ValueRW.IsHarvesting = 0;
                        continue;
                    }
                }

                // Progress harvest
                float progressIncrement = harvest.ValueRO.HarvestRate / 100f;
                harvest.ValueRW.Progress = (half)math.min(1f, (float)harvest.ValueRO.Progress + progressIncrement);

                // Complete cycle
                if ((float)harvest.ValueRO.Progress >= 1f)
                {
                    // Apply yield based on harvest type
                    switch (harvest.ValueRO.Type)
                    {
                        case EmergencyHarvestType.GasGiantFuel:
                            status.ValueRW.Fuel = math.min(status.ValueRO.FuelCapacity,
                                status.ValueRO.Fuel + harvest.ValueRO.FuelPerCycle);
                            break;

                        case EmergencyHarvestType.PlanetaryProvisions:
                            status.ValueRW.Provisions = math.min(status.ValueRO.ProvisionsCapacity,
                                status.ValueRO.Provisions + harvest.ValueRO.ProvisionsPerCycle);
                            break;

                        case EmergencyHarvestType.AsteroidMaterials:
                            status.ValueRW.RepairParts = math.min(status.ValueRO.RepairPartsCapacity,
                                status.ValueRO.RepairParts + harvest.ValueRO.FuelPerCycle * 0.5f);
                            break;
                    }

                    // Reset progress for next cycle
                    harvest.ValueRW.Progress = (half)0f;

                    // Check if capacity full
                    bool shouldStop = harvest.ValueRO.Type switch
                    {
                        EmergencyHarvestType.GasGiantFuel => status.ValueRO.FuelRatio >= 0.9f,
                        EmergencyHarvestType.PlanetaryProvisions => status.ValueRO.ProvisionsRatio >= 0.9f,
                        _ => false
                    };

                    if (shouldStop)
                    {
                        harvest.ValueRW.IsHarvesting = 0;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Updates morale based on supply status.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XSupplyConsumptionSystem))]
    public partial struct Space4XSupplyMoraleSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SupplyStatus>();
            state.RequireForUpdate<MoraleState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var currentTick = (uint)SystemAPI.Time.ElapsedTime;

            foreach (var (status, morale, modifiers, entity) in
                SystemAPI.Query<RefRO<SupplyStatus>, RefRW<MoraleState>, DynamicBuffer<MoraleModifier>>()
                    .WithEntityAccess())
            {
                var modifiersBuffer = modifiers;
                float penalty = SupplyUtility.CalculateSupplyMoralePenalty(status.ValueRO);

                if (penalty > 0)
                {
                    // Check if supply modifier already exists
                    bool found = false;
                    for (int i = 0; i < modifiersBuffer.Length; i++)
                    {
                        if (modifiersBuffer[i].Source == MoraleModifierSource.SupplyShortage)
                        {
                            var mod = modifiersBuffer[i];
                            mod.Strength = (half)(-penalty);
                            modifiersBuffer[i] = mod;
                            found = true;
                            break;
                        }
                    }

                    if (!found && modifiersBuffer.Length < modifiersBuffer.Capacity)
                    {
                        modifiersBuffer.Add(new MoraleModifier
                        {
                            Strength = (half)(-penalty),
                            Source = MoraleModifierSource.SupplyShortage,
                            RemainingTicks = 0, // Persistent until supplies improve
                            AppliedTick = currentTick
                        });
                    }
                }
            }
        }
    }

    /// <summary>
    /// Telemetry for supply system.
    /// </summary>
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    public partial struct Space4XSupplyTelemetrySystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SupplyStatus>();
        }

        public void OnUpdate(ref SystemState state)
        {
            int totalEntities = 0;
            int criticalEntities = 0;
            float avgFuelRatio = 0f;
            float avgProvisionsRatio = 0f;
            int activeRoutes = 0;
            int harvestingEntities = 0;

            foreach (var status in SystemAPI.Query<RefRO<SupplyStatus>>())
            {
                totalEntities++;
                avgFuelRatio += status.ValueRO.FuelRatio;
                avgProvisionsRatio += status.ValueRO.ProvisionsRatio;

                if (status.ValueRO.FuelRatio < 0.1f || status.ValueRO.ProvisionsRatio < 0.1f)
                {
                    criticalEntities++;
                }
            }

            foreach (var route in SystemAPI.Query<RefRO<SupplyRoute>>())
            {
                if (route.ValueRO.Status == SupplyRouteStatus.Active ||
                    route.ValueRO.Status == SupplyRouteStatus.InTransit ||
                    route.ValueRO.Status == SupplyRouteStatus.Delivering)
                {
                    activeRoutes++;
                }
            }

            foreach (var harvest in SystemAPI.Query<RefRO<EmergencyHarvest>>())
            {
                if (harvest.ValueRO.IsHarvesting == 1)
                {
                    harvestingEntities++;
                }
            }

            if (totalEntities > 0)
            {
                avgFuelRatio /= totalEntities;
                avgProvisionsRatio /= totalEntities;
            }

            // Would emit to telemetry stream
            // UnityEngine.Debug.Log($"[Supply] Entities: {totalEntities}, Critical: {criticalEntities}, AvgFuel: {avgFuelRatio:P0}, AvgProv: {avgProvisionsRatio:P0}, Routes: {activeRoutes}, Harvesting: {harvestingEntities}");
        }
    }
}


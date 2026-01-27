using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Resource;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Processes harvest commands for fruiting vegetation and delivers yield to villager inventories.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(VegetationSystemGroup))]
    [UpdateAfter(typeof(VegetationGrowthSystem))]
    public partial struct VegetationHarvestSystem : ISystem
    {
        private BufferLookup<VillagerInventoryItem> _villagerInventoryLookup;
        private BufferLookup<VillagerJobCarryItem> _villagerCarryLookup;
        private BufferLookup<VegetationHistoryEvent> _historyLookup;
        private ComponentLookup<VegetationProduction> _productionLookup;
        private ComponentLookup<VegetationReadyToHarvestTag> _readyTagLookup;
        private ComponentLookup<VegetationSpeciesIndex> _speciesIndexLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _villagerInventoryLookup = state.GetBufferLookup<VillagerInventoryItem>(false);
            _villagerCarryLookup = state.GetBufferLookup<VillagerJobCarryItem>(false);
            _historyLookup = state.GetBufferLookup<VegetationHistoryEvent>(false);
            _productionLookup = state.GetComponentLookup<VegetationProduction>(false);
            _readyTagLookup = state.GetComponentLookup<VegetationReadyToHarvestTag>(false);
            _speciesIndexLookup = state.GetComponentLookup<VegetationSpeciesIndex>(true);

            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<VegetationSpeciesLookup>();
            state.RequireForUpdate<VegetationHarvestCommandQueue>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton(out TimeState timeState) ||
                !SystemAPI.TryGetSingleton(out RewindState rewindState))
            {
                return;
            }

            if (timeState.IsPaused || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var queueEntity = SystemAPI.GetSingletonEntity<VegetationHarvestCommandQueue>();
            var commands = state.EntityManager.GetBuffer<VegetationHarvestCommand>(queueEntity);
            if (commands.Length == 0)
            {
                return;
            }

            var receipts = state.EntityManager.GetBuffer<VegetationHarvestReceipt>(queueEntity);
            receipts.Clear();

            _villagerInventoryLookup.Update(ref state);
            _villagerCarryLookup.Update(ref state);
            _historyLookup.Update(ref state);
            _productionLookup.Update(ref state);
            _readyTagLookup.Update(ref state);
            _speciesIndexLookup.Update(ref state);

            var speciesLookup = SystemAPI.GetSingleton<VegetationSpeciesLookup>();
            ref var catalog = ref speciesLookup.CatalogBlob.Value;

            var resourceCatalogRef = SystemAPI.GetSingleton<ResourceTypeIndex>();
            if (!resourceCatalogRef.Catalog.IsCreated)
            {
                return;
            }
            ref var resourceCatalog = ref resourceCatalogRef.Catalog.Value;

            var deltaSeconds = timeState.FixedDeltaTime;
            var currentSeconds = timeState.Tick * deltaSeconds;
            var commandArray = commands.AsNativeArray();

            for (int i = 0; i < commandArray.Length; i++)
            {
                var command = commandArray[i];
                var result = VegetationHarvestResult.Success;
                float harvestedAmount = 0f;

                if (!state.EntityManager.Exists(command.Vegetation) ||
                    !_productionLookup.HasComponent(command.Vegetation) ||
                    !_readyTagLookup.HasComponent(command.Vegetation))
                {
                    result = VegetationHarvestResult.InvalidEntities;
                    receipts.Add(CreateReceipt(command, default, 0f, result, timeState.Tick));
                    continue;
                }

                if (!state.EntityManager.Exists(command.Villager) ||
                    !_villagerInventoryLookup.HasBuffer(command.Villager))
                {
                    result = VegetationHarvestResult.NoInventory;
                    receipts.Add(CreateReceipt(command, default, 0f, result, timeState.Tick));
                    continue;
                }

                var production = _productionLookup[command.Vegetation];
                var resourceTypeId = production.ResourceTypeId;
                if (resourceTypeId.IsEmpty)
                {
                    result = VegetationHarvestResult.InvalidEntities;
                    receipts.Add(CreateReceipt(command, resourceTypeId, 0f, result, timeState.Tick));
                    continue;
                }

                var resourceTypeIndex = resourceCatalog.LookupIndex(resourceTypeId);
                if (resourceTypeIndex < 0 || resourceTypeIndex > ushort.MaxValue)
                {
                    result = VegetationHarvestResult.InvalidEntities;
                    receipts.Add(CreateReceipt(command, resourceTypeId, 0f, result, timeState.Tick));
                    continue;
                }
                var ushortTypeIndex = (ushort)resourceTypeIndex;

                if (!_readyTagLookup.IsComponentEnabled(command.Vegetation))
                {
                    result = VegetationHarvestResult.NotReady;
                    receipts.Add(CreateReceipt(command, resourceTypeId, 0f, result, timeState.Tick));
                    continue;
                }

                ushort speciesIndex = command.SpeciesIndex;
                if (_speciesIndexLookup.HasComponent(command.Vegetation))
                {
                    speciesIndex = _speciesIndexLookup[command.Vegetation].Value;
                }

                if (speciesIndex >= catalog.Species.Length)
                {
                    result = VegetationHarvestResult.InvalidEntities;
                    receipts.Add(CreateReceipt(command, resourceTypeId, 0f, result, timeState.Tick));
                    continue;
                }

                ref var species = ref catalog.Species[speciesIndex];

                var cooldownElapsed = currentSeconds - production.LastHarvestTime;
                if (cooldownElapsed < math.max(0f, species.HarvestCooldown))
                {
                    result = VegetationHarvestResult.Cooldown;
                    receipts.Add(CreateReceipt(command, resourceTypeId, 0f, result, timeState.Tick));
                    continue;
                }

                var availableProduction = math.max(0f, production.CurrentProduction);
                if (availableProduction <= 0f)
                {
                    result = VegetationHarvestResult.Empty;
                    _readyTagLookup.SetComponentEnabled(command.Vegetation, false);
                    receipts.Add(CreateReceipt(command, resourceTypeId, 0f, result, timeState.Tick));
                    continue;
                }

                var requested = command.RequestedAmount > 0f ? command.RequestedAmount : species.MaxYieldPerCycle;
                requested = math.max(0f, requested);

                harvestedAmount = math.min(requested, availableProduction);
                harvestedAmount = math.min(harvestedAmount, species.MaxYieldPerCycle);

                if (harvestedAmount <= 0f)
                {
                    result = VegetationHarvestResult.Empty;
                    receipts.Add(CreateReceipt(command, resourceTypeId, 0f, result, timeState.Tick));
                    continue;
                }

                production.CurrentProduction = math.max(0f, availableProduction - harvestedAmount);

                if (production.CurrentProduction > 0f && species.PartialHarvestPenalty > 0f && harvestedAmount < species.MaxYieldPerCycle)
                {
                    var penaltyLoss = species.PartialHarvestPenalty * harvestedAmount;
                    production.CurrentProduction = math.max(0f, production.CurrentProduction - penaltyLoss);
                }

                production.LastHarvestTime = currentSeconds;
                _productionLookup[command.Vegetation] = production;

                if (production.CurrentProduction <= 0f)
                {
                    _readyTagLookup.SetComponentEnabled(command.Vegetation, false);
                }

                // Deliver yield to villager inventory
                var inventory = _villagerInventoryLookup[command.Villager];
                var delivered = TryAddToInventory(ref inventory, ushortTypeIndex, harvestedAmount);
                harvestedAmount = delivered;

                if (harvestedAmount > 0f && _villagerCarryLookup.HasBuffer(command.Villager))
                {
                    var carry = _villagerCarryLookup[command.Villager];
                    var added = false;
                    for (int c = 0; c < carry.Length; c++)
                    {
                        if (carry[c].ResourceTypeIndex == (ushort)resourceTypeIndex)
                        {
                            var item = carry[c];
                            item.Amount += harvestedAmount;
                            item.TierId = item.TierId == 0 ? (byte)ResourceQualityTier.Unknown : item.TierId;
                            item.AverageQuality = item.AverageQuality == 0 ? (ushort)200 : item.AverageQuality;
                            carry[c] = item;
                            added = true;
                            break;
                        }
                    }

                    if (!added)
                    {
                        carry.Add(new VillagerJobCarryItem
                        {
                            ResourceTypeIndex = (ushort)resourceTypeIndex,
                            Amount = harvestedAmount,
                            TierId = (byte)ResourceQualityTier.Unknown,
                            AverageQuality = 200
                        });
                    }
                }

                if (harvestedAmount > 0f)
                {
                    if (_historyLookup.HasBuffer(command.Vegetation))
                    {
                        var history = _historyLookup[command.Vegetation];
                        history.Add(new VegetationHistoryEvent
                        {
                            EventTick = timeState.Tick,
                            Type = VegetationHistoryEvent.EventType.Harvested,
                            Value = harvestedAmount
                        });
                    }
                }
                else
                {
                    result = VegetationHarvestResult.NoInventory;
                }

                receipts.Add(CreateReceipt(command, resourceTypeId, harvestedAmount, result, timeState.Tick));
            }

            commands.Clear();
        }

        private static VegetationHarvestReceipt CreateReceipt(
            VegetationHarvestCommand command,
            FixedString64Bytes resourceTypeId,
            float harvestedAmount,
            VegetationHarvestResult result,
            uint processedTick)
        {
            return new VegetationHarvestReceipt
            {
                Result = result,
                Villager = command.Villager,
                Vegetation = command.Vegetation,
                ResourceTypeId = resourceTypeId,
                HarvestedAmount = harvestedAmount,
                IssuedTick = command.IssuedTick,
                ProcessedTick = processedTick
            };
        }

        private static float TryAddToInventory(
            ref DynamicBuffer<VillagerInventoryItem> inventory,
            ushort resourceTypeIndex,
            float amount)
        {
            if (amount <= 0f)
            {
                return 0f;
            }

            for (var i = 0; i < inventory.Length; i++)
            {
                var item = inventory[i];
                if (item.ResourceTypeIndex != resourceTypeIndex)
                {
                    continue;
                }

                item.Amount += amount;
                inventory[i] = item;
                return amount;
            }

            inventory.Add(new VillagerInventoryItem
            {
                ResourceTypeIndex = resourceTypeIndex,
                Amount = amount,
                MaxCarryCapacity = math.max(50f, amount)
            });

            return amount;
        }
    }
}

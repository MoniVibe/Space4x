using PureDOTS.Runtime;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Economy.Production;
using PureDOTS.Runtime.Economy.Resources;
using Space4X.Registry;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Systems.Economy
{
    /// <summary>
    /// Converts research output items into tech diffusion upgrades.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(PureDOTS.Runtime.Economy.Production.ProductionJobCompletionSystem))]
    public partial struct Space4XResearchTechBridgeSystem : ISystem
    {
        private const float ResearchDrainPerTick = 8f;
        private const float DiffusionBaseSeconds = 25f;

        private ComponentLookup<TechResearchPool> _researchPoolLookup;
        private ComponentLookup<TechLevel> _techLookup;
        private ComponentLookup<TechDiffusionState> _diffusionLookup;
        private ComponentLookup<BusinessInventory> _inventoryLookup;
        private BufferLookup<InventoryItem> _itemsLookup;

        private FixedString64Bytes _researchId;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ScenarioState>();
            state.RequireForUpdate<TickTimeState>();
            state.RequireForUpdate<RewindState>();

            _researchPoolLookup = state.GetComponentLookup<TechResearchPool>(false);
            _techLookup = state.GetComponentLookup<TechLevel>(true);
            _diffusionLookup = state.GetComponentLookup<TechDiffusionState>(false);
            _inventoryLookup = state.GetComponentLookup<BusinessInventory>(true);
            _itemsLookup = state.GetBufferLookup<InventoryItem>(false);

            _researchId = new FixedString64Bytes("space4x_research");
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton(out ScenarioState scenario) ||
                !scenario.IsInitialized ||
                !scenario.EnableEconomy)
            {
                return;
            }

            var tickTime = SystemAPI.GetSingleton<TickTimeState>();
            if (tickTime.IsPaused)
            {
                return;
            }

            var rewind = SystemAPI.GetSingleton<RewindState>();
            if (rewind.Mode != RewindMode.Record)
            {
                return;
            }

            _researchPoolLookup.Update(ref state);
            _techLookup.Update(ref state);
            _diffusionLookup.Update(ref state);
            _inventoryLookup.Update(ref state);
            _itemsLookup.Update(ref state);

            var deltaTime = math.max(0f, tickTime.FixedDeltaTime * math.max(0.01f, tickTime.CurrentSpeedMultiplier));
            var drainBudget = ResearchDrainPerTick * deltaTime;

            foreach (var (link, facility) in SystemAPI.Query<RefRO<ColonyFacilityLink>>().WithEntityAccess())
            {
                if (link.ValueRO.FacilityClass != FacilityBusinessClass.Research)
                {
                    continue;
                }

                var colony = link.ValueRO.Colony;
                if (colony == Entity.Null || !_researchPoolLookup.HasComponent(colony))
                {
                    continue;
                }

                if (!_inventoryLookup.HasComponent(facility))
                {
                    continue;
                }

                var inventoryEntity = _inventoryLookup[facility].InventoryEntity;
                if (inventoryEntity == Entity.Null || !_itemsLookup.HasBuffer(inventoryEntity))
                {
                    continue;
                }

                var items = _itemsLookup[inventoryEntity];
                var drained = ConsumeItem(ref items, _researchId, drainBudget);
                if (drained <= 0f)
                {
                    continue;
                }

                var pool = _researchPoolLookup[colony];
                pool.Stored += drained;
                pool.LastUpdateTick = tickTime.Tick;
                _researchPoolLookup[colony] = pool;
            }

            foreach (var (pool, entity) in SystemAPI.Query<RefRW<TechResearchPool>>().WithEntityAccess())
            {
                if (!_techLookup.HasComponent(entity) || !_diffusionLookup.HasComponent(entity))
                {
                    continue;
                }

                var diffusion = _diffusionLookup[entity];
                if (diffusion.Active != 0)
                {
                    continue;
                }

                var threshold = math.max(1f, pool.ValueRO.Threshold);
                if (pool.ValueRO.Stored < threshold)
                {
                    continue;
                }

                var tech = _techLookup[entity];
                var currentTier = math.max((int)tech.MiningTech,
                    math.max((int)tech.CombatTech, math.max((int)tech.HaulingTech, (int)tech.ProcessingTech)));
                var nextTier = (byte)math.min((int)pool.ValueRO.MaxTier, currentTier + 1);
                if (nextTier <= currentTier)
                {
                    continue;
                }

                diffusion.SourceEntity = entity;
                diffusion.DiffusionProgressSeconds = 0f;
                diffusion.DiffusionDurationSeconds = DiffusionBaseSeconds * (1f + 0.35f * nextTier);
                diffusion.TargetMiningTech = nextTier;
                diffusion.TargetCombatTech = nextTier;
                diffusion.TargetHaulingTech = nextTier;
                diffusion.TargetProcessingTech = nextTier;
                diffusion.Active = 1;
                diffusion.DiffusionStartTick = 0u;
                _diffusionLookup[entity] = diffusion;

                pool.ValueRW.Stored = math.max(0f, pool.ValueRO.Stored - threshold);
                pool.ValueRW.LastUpdateTick = tickTime.Tick;
            }
        }

        private static float ConsumeItem(ref DynamicBuffer<InventoryItem> items, in FixedString64Bytes itemId, float amount)
        {
            if (amount <= 0f)
            {
                return 0f;
            }

            var remaining = amount;
            var consumed = 0f;

            for (int i = items.Length - 1; i >= 0 && remaining > 0f; i--)
            {
                if (!items[i].ItemId.Equals(itemId))
                {
                    continue;
                }

                var item = items[i];
                var take = math.min(item.Quantity, remaining);
                item.Quantity -= take;
                remaining -= take;
                consumed += take;

                if (item.Quantity <= 0f)
                {
                    items.RemoveAt(i);
                }
                else
                {
                    items[i] = item;
                }
            }

            return consumed;
        }
    }
}

using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Economy.Resources;
using PureDOTS.Runtime.Logistics.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Logistics.Systems
{
    /// <summary>
    /// Handles living cargo and personnel.
    /// Generates events for failed storage conditions (deaths, escapes, infections).
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct LivingCargoSystem : ISystem
    {
        private BufferLookup<CargoItem> _cargoBufferLookup;
        private BufferLookup<CargoContainerSlot> _containerBufferLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TickTimeState>();
            state.RequireForUpdate<ItemSpecCatalog>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<ScenarioState>();
            _cargoBufferLookup = state.GetBufferLookup<CargoItem>(false);
            _containerBufferLookup = state.GetBufferLookup<CargoContainerSlot>(false);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<ScenarioState>(out var scenario)
                || !scenario.IsInitialized
                || !scenario.EnableEconomy)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState)
                || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<ItemSpecCatalog>(out var itemCatalog))
            {
                return;
            }

            ref var itemCatalogBlob = ref itemCatalog.Catalog.Value;

            _cargoBufferLookup.Update(ref state);
            _containerBufferLookup.Update(ref state);

            var ecb = new EntityCommandBuffer(Allocator.TempJob);

            // Process living cargo on haulers
            foreach (var (haulerTag, haulerEntity) in SystemAPI.Query<RefRO<HaulerTag>>()
                .WithAll<CargoItem>()
                .WithEntityAccess())
            {
                if (!_cargoBufferLookup.HasBuffer(haulerEntity))
                {
                    continue;
                }

                var cargoBuffer = _cargoBufferLookup[haulerEntity];
                for (int i = 0; i < cargoBuffer.Length; i++)
                {
                    var cargo = cargoBuffer[i];

                    // Check if item is living/personnel
                    if (!TryFindItemSpec(cargo.ResourceId, ref itemCatalogBlob, out var itemSpec))
                    {
                        continue;
                    }

                    // Check if item requires living storage (would check ResourceDef.StorageClass)
                    // For now, use item tags as proxy
                    bool isLiving = (itemSpec.Tags & ItemTags.Food) != 0 && 
                                    itemSpec.Category == ItemCategory.Food;
                    // In practice, would check StorageClass.Living or StorageClass.Personnel

                    if (!isLiving)
                    {
                        continue;
                    }

                    // Check container conditions
                    bool hasProperContainer = false;
                    float containerProtection = 0f;

                    if (cargo.ContainerSlotIndex >= 0 && 
                        _containerBufferLookup.HasBuffer(haulerEntity))
                    {
                        var containers = _containerBufferLookup[haulerEntity];
                        if (cargo.ContainerSlotIndex < containers.Length)
                        {
                            var container = containers[cargo.ContainerSlotIndex];
                            // Get container def to check SafetyFactor, TempControl
                            // For now, assume basic container
                            hasProperContainer = true;
                            containerProtection = 0.7f; // Placeholder
                        }
                    }

                    // Check for storage failures
                    // No power, damaged container, etc. would reduce protection
                    // For now, simplified check
                    if (!hasProperContainer || containerProtection < 0.5f)
                    {
                        // Storage conditions failed - generate events
                        // In practice, would create event entities or trigger callbacks
                        // For now, reduce cargo amount to simulate deaths/escapes
                        float failureRate = 0.01f; // 1% per tick without proper storage
                        float lostAmount = cargo.Amount * failureRate;

                        if (lostAmount > 0f)
                        {
                            cargo.Amount -= lostAmount;
                            cargoBuffer[i] = cargo;

                            // TODO: Generate event entities:
                            // - DeathEvent (for deaths)
                            // - EscapeEvent (for escapes)
                            // - InfectionEvent (for infections)
                        }
                    }
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        [BurstCompile]
        private static bool TryFindItemSpec(in FixedString64Bytes itemId, ref ItemSpecCatalogBlob catalog, out ItemSpecBlob spec)
        {
            for (int i = 0; i < catalog.Items.Length; i++)
            {
                if (catalog.Items[i].ItemId.Equals(itemId))
                {
                    spec = catalog.Items[i];
                    return true;
                }
            }

            spec = default;
            return false;
        }
    }
}


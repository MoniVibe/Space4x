using PureDOTS.Runtime;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Resource;
using PureDOTS.Runtime.Time;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Systems.Facility
{
    /// <summary>
    /// Processes Facility entities to convert inputs to outputs based on recipes.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct FacilityProductionSystem : ISystem
    {
        private BufferLookup<ResourceStack> _inventoryLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<ScenarioState>();

            _inventoryLookup = state.GetBufferLookup<ResourceStack>(false);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var scenarioState = SystemAPI.GetSingleton<ScenarioState>();
            // Facilities work for both Godgame and Space4X, so we only skip if both are disabled
            if (!scenarioState.EnableGodgame && !scenarioState.EnableSpace4x)
            {
                return;
            }

            var deltaTime = timeState.FixedDeltaTime;
            var ecb = new EntityCommandBuffer(Allocator.TempJob);
            _inventoryLookup.Update(ref state);

            foreach (var (facility, entity) in SystemAPI.Query<RefRW<PureDOTS.Runtime.Facility.Facility>>().WithEntityAccess())
            {
                if (!_inventoryLookup.HasBuffer(entity))
                    continue;

                var inventory = _inventoryLookup[entity];

                // Get recipe for this facility archetype
                if (!PureDOTS.Runtime.Facility.FacilityRecipes.TryGetRecipe(facility.ValueRO.ArchetypeId, out var recipe))
                {
                    continue;
                }

                // Check if we have enough inputs
                bool hasAllInputs = true;
                for (int i = 0; i < recipe.InputResourceIds.Length; i++)
                {
                    int resourceId = recipe.InputResourceIds[i];
                    float requiredAmount = recipe.InputAmounts[i];
                    
                    // Linear search through buffer
                    float available = 0f;
                    for (int j = 0; j < inventory.Length; j++)
                    {
                        if (inventory[j].ResourceTypeId == resourceId)
                        {
                            available = inventory[j].Amount;
                            break;
                        }
                    }
                    
                    if (available < requiredAmount)
                    {
                        hasAllInputs = false;
                        break;
                    }
                }

                if (!hasAllInputs)
                {
                    // Reset progress if we don't have inputs
                    facility.ValueRW.WorkProgress = 0f;
                    continue;
                }

                // Advance work progress
                ref var facilityRef = ref facility.ValueRW;
                facilityRef.WorkProgress += deltaTime / recipe.WorkRequired;

                // Check if work is complete
                if (facilityRef.WorkProgress >= 1f)
                {
                    // Consume inputs
                    for (int i = 0; i < recipe.InputResourceIds.Length; i++)
                    {
                        int resourceId = recipe.InputResourceIds[i];
                        float amount = recipe.InputAmounts[i];
                        
                        // Find and consume from buffer
                        for (int j = inventory.Length - 1; j >= 0; j--)
                        {
                            if (inventory[j].ResourceTypeId == resourceId)
                            {
                                var stack = inventory[j];
                                stack.Amount -= amount;
                                if (stack.Amount <= 0f)
                                {
                                    inventory.RemoveAtSwapBack(j);
                                }
                                else
                                {
                                    inventory[j] = stack;
                                }
                                break;
                            }
                        }
                    }

                    // Add outputs
                    for (int i = 0; i < recipe.OutputResourceIds.Length; i++)
                    {
                        int resourceId = recipe.OutputResourceIds[i];
                        float amount = recipe.OutputAmounts[i];
                        
                        // Find existing stack or add new
                        bool found = false;
                        for (int j = 0; j < inventory.Length; j++)
                        {
                            if (inventory[j].ResourceTypeId == resourceId)
                            {
                                var stack = inventory[j];
                                stack.Amount += amount;
                                inventory[j] = stack;
                                found = true;
                                break;
                            }
                        }
                        
                        if (!found)
                        {
                            inventory.Add(new ResourceStack
                            {
                                ResourceTypeId = resourceId,
                                Amount = amount
                            });
                        }
                    }

                    // Reset progress
                    facilityRef.WorkProgress = 0f;
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}


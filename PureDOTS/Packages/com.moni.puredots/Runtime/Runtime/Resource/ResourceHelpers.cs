using PureDOTS.Runtime.Components;
using Unity.Collections;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Resource
{
    /// <summary>
    /// Burst-friendly helper functions for managing ResourceStack buffers.
    /// Uses linear search for small-scale inventories.
    /// </summary>
    public static class ResourceHelpers
    {
        /// <summary>
        /// Adds or increases the amount of a resource in the inventory.
        /// </summary>
        public static void AddResource(NativeList<ResourceStack> inventory, int resourceTypeId, float amount)
        {
            if (amount <= 0f)
            {
                return;
            }

            // Try to find existing stack
            for (int i = 0; i < inventory.Length; i++)
            {
                if (inventory[i].ResourceTypeId == resourceTypeId)
                {
                    var stack = inventory[i];
                    stack.Amount += amount;
                    inventory[i] = stack;
                    return;
                }
            }

            // No existing stack, add new one
            inventory.Add(new ResourceStack
            {
                ResourceTypeId = resourceTypeId,
                Amount = amount
            });
        }

        /// <summary>
        /// Tries to consume the specified amount of a resource.
        /// Returns true if successful, false if insufficient.
        /// </summary>
        public static bool TryConsumeResource(NativeList<ResourceStack> inventory, int resourceTypeId, float amount, out float consumed)
        {
            consumed = 0f;

            if (amount <= 0f)
            {
                return true;
            }

            for (int i = 0; i < inventory.Length; i++)
            {
                if (inventory[i].ResourceTypeId == resourceTypeId)
                {
                    var stack = inventory[i];
                    float available = stack.Amount;
                    consumed = math.min(available, amount);
                    stack.Amount -= consumed;

                    if (stack.Amount <= 0f)
                    {
                        // Remove empty stack
                        inventory.RemoveAtSwapBack(i);
                    }
                    else
                    {
                        inventory[i] = stack;
                    }

                    return consumed >= amount;
                }
            }

            return false;
        }

        /// <summary>
        /// Gets the current amount of a resource in the inventory.
        /// Returns 0 if not found.
        /// </summary>
        public static float GetResourceAmount(NativeArray<ResourceStack> inventory, int resourceTypeId)
        {
            for (int i = 0; i < inventory.Length; i++)
            {
                if (inventory[i].ResourceTypeId == resourceTypeId)
                {
                    return inventory[i].Amount;
                }
            }

            return 0f;
        }

        /// <summary>
        /// Checks if the inventory has at least the specified amount of a resource.
        /// </summary>
        public static bool HasResource(NativeArray<ResourceStack> inventory, int resourceTypeId, float amount)
        {
            return GetResourceAmount(inventory, resourceTypeId) >= amount;
        }
    }
}

using PureDOTS.Runtime.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Resource
{
    /// <summary>
    /// API helpers for storehouse deposit/withdraw operations.
    /// Used by job execution systems for conservation checks.
    /// </summary>
    public static class StorehouseApi
    {
        /// <summary>
        /// Attempts to deposit resources into a storehouse by resource ID.
        /// Returns true if deposit was successful.
        /// </summary>
        public static bool TryDeposit(
            Entity storehouseEntity,
            FixedString64Bytes resourceTypeId,
            float amount,
            ref StorehouseInventory inventory,
            DynamicBuffer<StorehouseInventoryItem> items,
            out float depositedAmount)
        {
            depositedAmount = 0f;

            if (amount <= 0f)
            {
                return false;
            }

            var capacityRemaining = math.max(0f, inventory.TotalCapacity - inventory.TotalStored);
            if (capacityRemaining <= 0f)
            {
                return false;
            }

            var amountToDeposit = math.min(amount, capacityRemaining);
            int itemIndex = -1;

            for (int i = 0; i < items.Length; i++)
            {
                if (items[i].ResourceTypeId.Equals(resourceTypeId))
                {
                    itemIndex = i;
                    break;
                }
            }

            if (itemIndex >= 0)
            {
                var item = items[itemIndex];
                item.Amount += amountToDeposit;
                items[itemIndex] = item;
            }
            else
            {
                items.Add(new StorehouseInventoryItem
                {
                    ResourceTypeId = resourceTypeId,
                    Amount = amountToDeposit,
                    Reserved = 0f,
                    TierId = 0,
                    AverageQuality = 0
                });
            }

            inventory.TotalStored += amountToDeposit;
            depositedAmount = amountToDeposit;
            return depositedAmount > 0f;
        }

        /// <summary>
        /// Attempts to withdraw resources from a storehouse.
        /// Returns true if withdrawal was successful.
        /// </summary>
        public static bool TryWithdraw(
            Entity storehouseEntity,
            ushort resourceTypeIndex,
            float amount,
            ref StorehouseInventory inventory,
            DynamicBuffer<StorehouseInventoryItem> items,
            out float withdrawnAmount)
        {
            withdrawnAmount = 0f;
            
            if (amount <= 0f)
            {
                return false;
            }
            
            // Find inventory item for this resource type
            for (int i = 0; i < items.Length; i++)
            {
                var item = items[i];
                // Simplified: check if item matches (full implementation would map index to ID)
                if (item.Amount > 0f && item.Reserved < item.Amount)
                {
                    var available = item.Amount - item.Reserved;
                    withdrawnAmount = math.min(amount, available);
                    
                    if (withdrawnAmount > 0f)
                    {
                        item.Amount -= withdrawnAmount;
                        items[i] = item;
                        inventory.TotalStored -= withdrawnAmount;
                        return true;
                    }
                }
            }
            
            return false;
        }
    }
}

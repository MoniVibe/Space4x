using PureDOTS.Runtime.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Resource
{
    public static class StorehouseMutationService
    {
        public static bool TryReserveOut(
            ushort resourceTypeIndex,
            float requestedAmount,
            bool allowPartial,
            BlobAssetReference<ResourceTypeIndexBlob> catalog,
            DynamicBuffer<StorehouseInventoryItem> items,
            out float reservedAmount)
        {
            reservedAmount = 0f;
            if (!TryResolveResourceId(resourceTypeIndex, catalog, out var resourceId) ||
                requestedAmount <= 0f)
            {
                return false;
            }

            for (int i = 0; i < items.Length; i++)
            {
                var item = items[i];
                if (!item.ResourceTypeId.Equals(resourceId))
                {
                    continue;
                }

                var available = math.max(0f, item.Amount - item.Reserved);
                if (available <= 0f)
                {
                    return false;
                }

                reservedAmount = allowPartial
                    ? math.min(requestedAmount, available)
                    : (available >= requestedAmount ? requestedAmount : 0f);

                if (reservedAmount <= 0f)
                {
                    return false;
                }

                item.Reserved = math.min(item.Amount, item.Reserved + reservedAmount);
                items[i] = item;
                return true;
            }

            return false;
        }

        public static bool CancelReserveOut(
            ushort resourceTypeIndex,
            float amount,
            BlobAssetReference<ResourceTypeIndexBlob> catalog,
            DynamicBuffer<StorehouseInventoryItem> items)
        {
            if (!TryResolveResourceId(resourceTypeIndex, catalog, out var resourceId) ||
                amount <= 0f)
            {
                return false;
            }

            for (int i = 0; i < items.Length; i++)
            {
                var item = items[i];
                if (!item.ResourceTypeId.Equals(resourceId))
                {
                    continue;
                }

                item.Reserved = math.max(0f, item.Reserved - amount);
                items[i] = item;
                return true;
            }

            return false;
        }

        public static bool CommitWithdrawReservedOut(
            ushort resourceTypeIndex,
            float amount,
            BlobAssetReference<ResourceTypeIndexBlob> catalog,
            ref StorehouseInventory inventory,
            DynamicBuffer<StorehouseInventoryItem> items,
            out float withdrawnAmount)
        {
            withdrawnAmount = 0f;
            if (!TryResolveResourceId(resourceTypeIndex, catalog, out var resourceId) ||
                amount <= 0f)
            {
                return false;
            }

            for (int i = 0; i < items.Length; i++)
            {
                var item = items[i];
                if (!item.ResourceTypeId.Equals(resourceId))
                {
                    continue;
                }

                var maxWithdraw = math.min(item.Amount, amount);
                if (item.Reserved > 0f)
                {
                    maxWithdraw = math.min(maxWithdraw, item.Reserved);
                }

                if (maxWithdraw <= 0f)
                {
                    return false;
                }

                item.Amount -= maxWithdraw;
                item.Reserved = math.max(0f, item.Reserved - maxWithdraw);
                withdrawnAmount = maxWithdraw;

                if (item.Amount <= 0f)
                {
                    items.RemoveAt(i);
                }
                else
                {
                    items[i] = item;
                }

                inventory.TotalStored = math.max(0f, inventory.TotalStored - withdrawnAmount);
                inventory.ItemTypeCount = items.Length;
                return true;
            }

            return false;
        }

        public static bool TryConsumeUnreserved(
            ushort resourceTypeIndex,
            float amount,
            BlobAssetReference<ResourceTypeIndexBlob> catalog,
            ref StorehouseInventory inventory,
            DynamicBuffer<StorehouseInventoryItem> items)
        {
            if (!TryResolveResourceId(resourceTypeIndex, catalog, out var resourceId) ||
                amount <= 0f)
            {
                return false;
            }

            for (int i = 0; i < items.Length; i++)
            {
                var item = items[i];
                if (!item.ResourceTypeId.Equals(resourceId))
                {
                    continue;
                }

                var available = math.max(0f, item.Amount - item.Reserved);
                if (available + 1e-3f < amount)
                {
                    return false;
                }

                item.Amount = math.max(0f, item.Amount - amount);
                if (item.Amount <= 1e-3f && item.Reserved <= 1e-3f)
                {
                    items.RemoveAt(i);
                }
                else
                {
                    items[i] = item;
                }

                inventory.TotalStored = math.max(0f, inventory.TotalStored - amount);
                inventory.ItemTypeCount = items.Length;
                return true;
            }

            return false;
        }

        public static bool TryReserveIn(
            ushort resourceTypeIndex,
            float requestedAmount,
            bool allowPartial,
            BlobAssetReference<ResourceTypeIndexBlob> catalog,
            DynamicBuffer<StorehouseCapacityElement> capacities,
            DynamicBuffer<StorehouseInventoryItem> items,
            DynamicBuffer<StorehouseReservationItem> reservations,
            ref StorehouseJobReservation totals,
            out float reservedAmount)
        {
            reservedAmount = 0f;
            if (!TryResolveResourceId(resourceTypeIndex, catalog, out var resourceId) ||
                requestedAmount <= 0f)
            {
                return false;
            }

            if (!TryGetCapacity(capacities, resourceId, out var capacity))
            {
                return false;
            }

            var stored = GetStoredAmount(items, resourceId);
            var reservedIn = GetReservedIn(reservations, resourceTypeIndex);
            var available = math.max(0f, capacity - stored - reservedIn);

            reservedAmount = allowPartial
                ? math.min(requestedAmount, available)
                : (available >= requestedAmount ? requestedAmount : 0f);

            if (reservedAmount <= 0f)
            {
                return false;
            }

            ApplyReserveIn(reservations, resourceTypeIndex, reservedAmount);
            totals.ReservedCapacity = math.max(0f, totals.ReservedCapacity + reservedAmount);
            return true;
        }

        public static bool CancelReserveIn(
            ushort resourceTypeIndex,
            float amount,
            BlobAssetReference<ResourceTypeIndexBlob> catalog,
            DynamicBuffer<StorehouseReservationItem> reservations,
            ref StorehouseJobReservation totals)
        {
            if (!TryResolveResourceId(resourceTypeIndex, catalog, out _) ||
                amount <= 0f)
            {
                return false;
            }

            for (int i = 0; i < reservations.Length; i++)
            {
                var entry = reservations[i];
                if (entry.ResourceTypeIndex != resourceTypeIndex)
                {
                    continue;
                }

                var newReserved = math.max(0f, entry.Reserved - amount);
                totals.ReservedCapacity = math.max(0f, totals.ReservedCapacity - (entry.Reserved - newReserved));

                if (newReserved <= 0f)
                {
                    reservations.RemoveAt(i);
                }
                else
                {
                    entry.Reserved = newReserved;
                    reservations[i] = entry;
                }

                return true;
            }

            return false;
        }

        public static bool HasCapacityForDeposit(
            ushort resourceTypeIndex,
            float amount,
            BlobAssetReference<ResourceTypeIndexBlob> catalog,
            DynamicBuffer<StorehouseInventoryItem> items,
            DynamicBuffer<StorehouseCapacityElement> capacities,
            DynamicBuffer<StorehouseReservationItem> reservations)
        {
            if (!TryResolveResourceId(resourceTypeIndex, catalog, out var resourceId) ||
                amount <= 0f)
            {
                return false;
            }

            if (!TryGetCapacity(capacities, resourceId, out var capacity))
            {
                return false;
            }

            var stored = GetStoredAmount(items, resourceId);
            var reservedIn = GetReservedIn(reservations, resourceTypeIndex);
            var available = math.max(0f, capacity - stored - reservedIn);
            return available + 1e-3f >= amount;
        }

        public static bool TryDepositWithPerTypeCapacity(
            ushort resourceTypeIndex,
            float amount,
            BlobAssetReference<ResourceTypeIndexBlob> catalog,
            ref StorehouseInventory inventory,
            DynamicBuffer<StorehouseInventoryItem> items,
            DynamicBuffer<StorehouseCapacityElement> capacities,
            DynamicBuffer<StorehouseReservationItem> reservations,
            out float depositedAmount)
        {
            depositedAmount = 0f;
            if (!TryResolveResourceId(resourceTypeIndex, catalog, out var resourceId) ||
                amount <= 0f)
            {
                return false;
            }

            if (!TryGetCapacity(capacities, resourceId, out var capacity))
            {
                return false;
            }

            var stored = GetStoredAmount(items, resourceId);
            var reservedIn = GetReservedIn(reservations, resourceTypeIndex);
            var available = math.max(0f, capacity - stored - reservedIn);
            if (available <= 0f)
            {
                return false;
            }

            depositedAmount = math.min(amount, available);
            if (depositedAmount <= 0f)
            {
                return false;
            }

            for (int i = 0; i < items.Length; i++)
            {
                var item = items[i];
                if (!item.ResourceTypeId.Equals(resourceId))
                {
                    continue;
                }

                item.Amount += depositedAmount;
                items[i] = item;
                inventory.TotalStored = math.max(0f, inventory.TotalStored + depositedAmount);
                inventory.ItemTypeCount = items.Length;
                return true;
            }

            items.Add(new StorehouseInventoryItem
            {
                ResourceTypeId = resourceId,
                Amount = depositedAmount,
                Reserved = 0f,
                TierId = 0,
                AverageQuality = 0
            });

            inventory.TotalStored = math.max(0f, inventory.TotalStored + depositedAmount);
            inventory.ItemTypeCount = items.Length;
            return true;
        }

        private static bool TryResolveResourceId(
            ushort resourceTypeIndex,
            BlobAssetReference<ResourceTypeIndexBlob> catalog,
            out FixedString64Bytes resourceId)
        {
            resourceId = default;
            if (!catalog.IsCreated || resourceTypeIndex >= catalog.Value.Ids.Length)
            {
                return false;
            }

            resourceId = catalog.Value.Ids[resourceTypeIndex];
            return resourceId.Length > 0;
        }

        private static bool TryGetCapacity(
            DynamicBuffer<StorehouseCapacityElement> capacities,
            FixedString64Bytes resourceId,
            out float capacity)
        {
            capacity = 0f;
            for (int i = 0; i < capacities.Length; i++)
            {
                var entry = capacities[i];
                if (entry.ResourceTypeId.Equals(resourceId))
                {
                    capacity = entry.MaxCapacity;
                    return true;
                }
            }

            return false;
        }

        private static float GetStoredAmount(
            DynamicBuffer<StorehouseInventoryItem> items,
            FixedString64Bytes resourceId)
        {
            for (int i = 0; i < items.Length; i++)
            {
                if (items[i].ResourceTypeId.Equals(resourceId))
                {
                    return items[i].Amount;
                }
            }

            return 0f;
        }

        private static float GetReservedIn(
            DynamicBuffer<StorehouseReservationItem> reservations,
            ushort resourceTypeIndex)
        {
            for (int i = 0; i < reservations.Length; i++)
            {
                if (reservations[i].ResourceTypeIndex == resourceTypeIndex)
                {
                    return reservations[i].Reserved;
                }
            }

            return 0f;
        }

        private static void ApplyReserveIn(
            DynamicBuffer<StorehouseReservationItem> reservations,
            ushort resourceTypeIndex,
            float amount)
        {
            for (int i = 0; i < reservations.Length; i++)
            {
                var entry = reservations[i];
                if (entry.ResourceTypeIndex != resourceTypeIndex)
                {
                    continue;
                }

                entry.Reserved += amount;
                reservations[i] = entry;
                return;
            }

            reservations.Add(new StorehouseReservationItem
            {
                ResourceTypeIndex = resourceTypeIndex,
                Reserved = amount
            });
        }
    }
}

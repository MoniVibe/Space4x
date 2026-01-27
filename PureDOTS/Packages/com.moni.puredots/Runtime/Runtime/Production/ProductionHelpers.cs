using PureDOTS.Runtime.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Production
{
    public static class ProductionUsageHelpers
    {
        public static void Allocate(ref ProductionFacilityUsage usage, in ProductionJob job)
        {
            usage.LanesInUse = (byte)math.min(byte.MaxValue, usage.LanesInUse + job.RequiredLanes);
            usage.SeatsInUse = (byte)math.min(byte.MaxValue, usage.SeatsInUse + job.RequiredSeats);
            usage.PowerInUse = math.max(0f, usage.PowerInUse + job.RequiredPower);
        }

        public static void Release(ref ProductionFacilityUsage usage, in ProductionJob job)
        {
            usage.LanesInUse = (byte)math.max(0, usage.LanesInUse - job.RequiredLanes);
            usage.SeatsInUse = (byte)math.max(0, usage.SeatsInUse - job.RequiredSeats);
            usage.PowerInUse = math.max(0f, usage.PowerInUse - job.RequiredPower);
        }
    }

    public static class ProductionScoreHelpers
    {
        public static float ComputeScore(float baseValue, float quality01, float deliveredAmount, uint timeCostTicks)
        {
            var safeTicks = math.max(1u, timeCostTicks);
            var clampedQuality = math.clamp(quality01, 0f, 1f);
            return (baseValue * clampedQuality * deliveredAmount) / safeTicks;
        }
    }

    public static class ProductionStorageHelpers
    {
        public static bool TryDepositWithQuality(
            FixedString64Bytes resourceId,
            float amount,
            ushort averageQuality,
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
            if (amountToDeposit <= 0f)
            {
                return false;
            }

            for (int i = 0; i < items.Length; i++)
            {
                if (!items[i].ResourceTypeId.Equals(resourceId))
                {
                    continue;
                }

                var item = items[i];
                var totalAmount = item.Amount + amountToDeposit;
                var weightedQuality = item.AverageQuality * item.Amount + averageQuality * amountToDeposit;
                var newAverage = totalAmount > 0f ? weightedQuality / totalAmount : averageQuality;
                item.Amount = totalAmount;
                item.AverageQuality = (ushort)math.clamp(math.round(newAverage), 0f, ushort.MaxValue);
                items[i] = item;
                inventory.TotalStored = math.max(0f, inventory.TotalStored + amountToDeposit);
                inventory.ItemTypeCount = items.Length;
                depositedAmount = amountToDeposit;
                return true;
            }

            items.Add(new StorehouseInventoryItem
            {
                ResourceTypeId = resourceId,
                Amount = amountToDeposit,
                Reserved = 0f,
                TierId = 0,
                AverageQuality = averageQuality
            });

            inventory.TotalStored = math.max(0f, inventory.TotalStored + amountToDeposit);
            inventory.ItemTypeCount = items.Length;
            depositedAmount = amountToDeposit;
            return true;
        }
    }
}

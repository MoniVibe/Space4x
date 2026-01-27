using Unity.Entities;

namespace PureDOTS.Runtime.Resource
{
    /// <summary>
    /// Shared payload descriptor that keeps resource amount, quality and tier bundled for transport systems.
    /// </summary>
    public struct ResourcePayloadSummary
    {
        public ushort ResourceTypeIndex;
        public float Amount;
        public byte TierId;
        public ushort AverageQuality;
    }

    public static class ResourcePayloadUtility
    {
        public static ResourcePayloadSummary Create(ushort resourceTypeIndex, float amount, byte tierId, ushort averageQuality)
        {
            return new ResourcePayloadSummary
            {
                ResourceTypeIndex = resourceTypeIndex,
                Amount = amount,
                TierId = tierId,
                AverageQuality = averageQuality
            };
        }

        /// <summary>
        /// Blends two payloads that represent the same resource type.
        /// </summary>
        public static void Merge(ref ResourcePayloadSummary destination, in ResourcePayloadSummary addition)
        {
            if (addition.Amount <= 0f)
            {
                return;
            }

            if (destination.Amount <= 0f)
            {
                destination = addition;
                return;
            }

            destination.AverageQuality = ResourceQualityUtility.BlendQuality(
                destination.AverageQuality,
                destination.Amount,
                addition.AverageQuality,
                addition.Amount);
            destination.Amount += addition.Amount;
            destination.TierId = addition.TierId;
        }
    }
}

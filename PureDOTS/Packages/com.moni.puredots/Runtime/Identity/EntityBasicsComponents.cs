using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Identity
{
    /// <summary>
    /// Optional display name for any entity. Works for individuals, buildings, items, etc.
    /// </summary>
    public struct EntityName : IComponentData
    {
        public FixedString64Bytes Value;
    }

    /// <summary>
    /// Optional semantic kind identifier (e.g. Villager, SentientBuilding, Spirit, Artifact).
    /// </summary>
    public struct EntityKind : IComponentData
    {
        public FixedString64Bytes Value;
    }

    /// <summary>
    /// Capability tier + confidence for a capability tag.
    /// </summary>
    public struct CapabilityTier
    {
        /// <summary>Tier/intensity (0 = nominal). Semantics authored per capability.</summary>
        public byte Tier;

        /// <summary>Confidence/quality (0-100). Optional metadata for planners.</summary>
        public byte Confidence;
    }

    /// <summary>
    /// Capability tags describe what systems are allowed to do with an entity.
    /// Example tags: IsUndead, IsStructure, Sentient, CanCast, AcceptsFocus.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct CapabilityTag : IBufferElementData
    {
        public FixedString64Bytes Id;
        public CapabilityTier Tier;
    }

    /// <summary>
    /// Helpers for working with capability buffers without allocations.
    /// </summary>
    public static class CapabilityBufferExtensions
    {
        public static bool HasCapability(this DynamicBuffer<CapabilityTag> capabilityBuffer, in FixedString64Bytes capabilityId, byte minimumTier = 0)
        {
            for (var i = 0; i < capabilityBuffer.Length; i++)
            {
                var tag = capabilityBuffer[i];
                if (tag.Id.Equals(capabilityId) && tag.Tier.Tier >= minimumTier)
                {
                    return true;
                }
            }

            return false;
        }

        public static bool TryGetCapability(this DynamicBuffer<CapabilityTag> capabilityBuffer, in FixedString64Bytes capabilityId, out CapabilityTier tier)
        {
            for (var i = 0; i < capabilityBuffer.Length; i++)
            {
                var tag = capabilityBuffer[i];
                if (tag.Id.Equals(capabilityId))
                {
                    tier = tag.Tier;
                    return true;
                }
            }

            tier = default;
            return false;
        }

        public static void UpsertCapability(this DynamicBuffer<CapabilityTag> capabilityBuffer, in FixedString64Bytes capabilityId, CapabilityTier tier)
        {
            for (var i = 0; i < capabilityBuffer.Length; i++)
            {
                var tag = capabilityBuffer[i];
                if (tag.Id.Equals(capabilityId))
                {
                    tag.Tier = tier;
                    capabilityBuffer[i] = tag;
                    return;
                }
            }

            capabilityBuffer.Add(new CapabilityTag
            {
                Id = capabilityId,
                Tier = tier
            });
        }
    }
}


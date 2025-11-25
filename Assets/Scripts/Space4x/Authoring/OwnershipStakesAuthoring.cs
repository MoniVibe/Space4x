using System;
using System.Collections.Generic;
using Space4X.Registry;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace Space4X.Authoring
{
    /// <summary>
    /// Authoring component for ownership stakes in facilities/manufacturers.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Space4X/Ownership Stakes")]
    public sealed class OwnershipStakesAuthoring : MonoBehaviour
    {
        [Serializable]
        public class StakeEntry
        {
            [Tooltip("Asset type (Facility, Manufacturer, etc.)")]
            public string assetType = string.Empty;
            [Tooltip("Asset ID")]
            public string assetId = string.Empty;
            [Tooltip("Ownership percentage (0-1)")]
            [Range(0f, 1f)]
            public float ownershipPercentage = 0f;
        }

        [Tooltip("Ownership stakes (multiple allowed)")]
        public List<StakeEntry> stakes = new List<StakeEntry>();

        public sealed class Baker : Unity.Entities.Baker<OwnershipStakesAuthoring>
        {
            public override void Bake(OwnershipStakesAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                var buffer = AddBuffer<Registry.OwnershipStake>(entity);

                if (authoring.stakes != null)
                {
                    foreach (var entry in authoring.stakes)
                    {
                        if (!string.IsNullOrWhiteSpace(entry.assetId))
                        {
                            buffer.Add(new Registry.OwnershipStake
                            {
                                AssetType = new FixedString64Bytes(entry.assetType ?? string.Empty),
                                AssetId = new FixedString64Bytes(entry.assetId),
                                OwnershipPercentage = entry.ownershipPercentage
                            });
                        }
                    }
                }
            }
        }
    }
}


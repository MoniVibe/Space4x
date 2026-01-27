using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace PureDOTS.Runtime.Components
{
    [BurstCompile]
    public struct ResourceTypeIndexBlob
    {
        public BlobArray<FixedString64Bytes> Ids;
        public BlobArray<BlobString> DisplayNames;
        public BlobArray<Color32> Colors;

        /// <summary>
        /// Lookup resource type index by FixedString64Bytes. Returns -1 if not found.
        /// </summary>
        public int LookupIndex(FixedString64Bytes searchId)
        {
            for (int i = 0; i < Ids.Length; i++)
            {
                if (Ids[i].Equals(searchId))
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>
        /// Get display name for resource type index. Returns empty string if invalid.
        /// </summary>
        public BlobString GetDisplayName(int index)
        {
            if (index >= 0 && index < DisplayNames.Length)
            {
                return DisplayNames[index];
            }
            return default;
        }

        /// <summary>
        /// Get display color for resource type index. Returns transparent if invalid.
        /// </summary>
        public Color32 GetColor(int index)
        {
            if (index >= 0 && index < Colors.Length)
            {
                return Colors[index];
            }
            return new Color32(0, 0, 0, 0);
        }
    }
}

using System;
using System.Collections.Generic;
using PureDOTS.Runtime.Stats;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace PureDOTS.Authoring.Stats
{
    /// <summary>
    /// ScriptableObject that defines trait axis metadata (ID, ranges, semantic tags).
    /// </summary>
    [CreateAssetMenu(fileName = "TraitAxisCatalog", menuName = "PureDOTS/Stats/Trait Axis Catalog")]
    public sealed class TraitAxisCatalogAsset : ScriptableObject
    {
        [Serializable]
        public struct AxisDefinitionEntry
        {
            [Tooltip("Unique axis identifier (e.g., LawfulChaotic, Cohesion, Xenophobia).")]
            public string axisId;

            [Tooltip("UI-facing display name.")]
            public string displayName;

            [Tooltip("Minimum allowed value (bipolar axes typically -100).")]
            public float minValue;

            [Tooltip("Maximum allowed value (bipolar axes typically +100).")]
            public float maxValue;

            [Tooltip("Default/neutral value (0 for bipolar, 50 for unipolar).")]
            public float defaultValue;

            [Tooltip("Semantic tags for filtering. Alignment, Behavior, Outlook, Stat, Communication, Cooperation.")]
            public TraitAxisTag tags;

            [Tooltip("Negative pole label (for bipolar axes).")]
            public string negativePoleLabel;

            [Tooltip("Neutral label (optional).")]
            public string neutralLabel;

            [Tooltip("Positive pole label (for bipolar axes).")]
            public string positivePoleLabel;
        }

        [SerializeField]
        private AxisDefinitionEntry[] axes = Array.Empty<AxisDefinitionEntry>();

        /// <summary>
        /// Number of axes defined.
        /// </summary>
        public int AxisCount => axes?.Length ?? 0;

        /// <summary>
        /// Build a blob asset for runtime consumption.
        /// </summary>
        public BlobAssetReference<TraitAxisCatalogBlob> BuildBlobAsset(Allocator allocator)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref var catalog = ref builder.ConstructRoot<TraitAxisCatalogBlob>();

            var axisArray = builder.Allocate(ref catalog.Axes, AxisCount);

            for (int i = 0; i < axisArray.Length; i++)
            {
                var entry = axes[i];
                var axisId = string.IsNullOrWhiteSpace(entry.axisId) ? $"Axis_{i}" : entry.axisId.Trim();
                var displayName = string.IsNullOrWhiteSpace(entry.displayName) ? axisId : entry.displayName.Trim();

                float minValue = entry.minValue;
                float maxValue = math.max(minValue + 0.0001f, entry.maxValue);
                float defaultValue = math.clamp(entry.defaultValue, minValue, maxValue);

                axisArray[i] = new TraitAxisDefinition
                {
                    AxisId = new FixedString32Bytes(axisId),
                    DisplayName = new FixedString64Bytes(displayName),
                    MinValue = minValue,
                    MaxValue = maxValue,
                    DefaultValue = defaultValue,
                    Tags = entry.tags,
                    NegativePoleLabel = new FixedString32Bytes(entry.negativePoleLabel ?? string.Empty),
                    NeutralLabel = new FixedString32Bytes(entry.neutralLabel ?? string.Empty),
                    PositivePoleLabel = new FixedString32Bytes(entry.positivePoleLabel ?? string.Empty)
                };
            }

            var blobAsset = builder.CreateBlobAssetReference<TraitAxisCatalogBlob>(allocator);
            builder.Dispose();
            return blobAsset;
        }

        /// <summary>
        /// Try to find the definition for a specific axis ID.
        /// </summary>
        public bool TryGetDefinition(string axisId, out AxisDefinitionEntry definition)
        {
            if (string.IsNullOrWhiteSpace(axisId) || axes == null)
            {
                definition = default;
                return false;
            }

            var trimmedId = axisId.Trim();

            for (int i = 0; i < axes.Length; i++)
            {
                if (string.Equals(axes[i].axisId?.Trim(), trimmedId, StringComparison.OrdinalIgnoreCase))
                {
                    definition = axes[i];
                    return true;
                }
            }

            definition = default;
            return false;
        }

        /// <summary>
        /// Enumerate all axis definitions.
        /// </summary>
        public IReadOnlyList<AxisDefinitionEntry> GetAxes() => axes;

        private void OnValidate()
        {
            if (axes == null)
            {
                axes = Array.Empty<AxisDefinitionEntry>();
                return;
            }

            var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < axes.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(axes[i].axisId))
                {
                    axes[i].axisId = $"Axis_{i}";
                }

                if (string.IsNullOrWhiteSpace(axes[i].displayName))
                {
                    axes[i].displayName = axes[i].axisId;
                }

                var id = axes[i].axisId.Trim();
                if (seenIds.Contains(id))
                {
                    Debug.LogWarning($"TraitAxisCatalogAsset [{name}] has duplicate axis ID: {id}");
                }

                seenIds.Add(id);
            }
        }
    }
}




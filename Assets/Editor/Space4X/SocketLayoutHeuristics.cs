using System.Collections.Generic;
using Space4X.Authoring;
using Space4X.Registry;
using UnityEngine;

namespace Space4X.Editor
{
    /// <summary>
    /// Heuristics for socket layout per hull category. Provides default positioning strategies
    /// with optional manual overrides.
    /// </summary>
    public static class SocketLayoutHeuristics
    {
        /// <summary>
        /// Socket layout configuration for a hull category.
        /// </summary>
        public class LayoutConfig
        {
            public Vector3 BasePosition;
            public Vector3 Spacing;
            public LayoutPattern Pattern;
            public Dictionary<MountType, Vector3> TypeOffsets; // Per-mount-type offsets
        }

        public enum LayoutPattern
        {
            Linear,      // Simple linear arrangement
            Radial,     // Circular/radial arrangement
            Grid,       // Grid-based arrangement
            Symmetric,  // Symmetric arrangement (left/right, front/back)
            Clustered   // Clustered by mount type
        }

        private static readonly Dictionary<HullCategory, LayoutConfig> DefaultLayouts = new Dictionary<HullCategory, LayoutConfig>
        {
            [HullCategory.CapitalShip] = new LayoutConfig
            {
                BasePosition = Vector3.zero,
                Spacing = new Vector3(2f, 0f, 2f),
                Pattern = LayoutPattern.Symmetric,
                TypeOffsets = new Dictionary<MountType, Vector3>
                {
                    [MountType.Core] = new Vector3(0f, 0f, 0f),
                    [MountType.Engine] = new Vector3(0f, 0f, -5f),
                    [MountType.Weapon] = new Vector3(3f, 0f, 0f),
                    [MountType.Defense] = new Vector3(-3f, 0f, 0f),
                    [MountType.Utility] = new Vector3(0f, 1f, 0f),
                    [MountType.Hangar] = new Vector3(0f, -1f, 0f)
                }
            },
            [HullCategory.Carrier] = new LayoutConfig
            {
                BasePosition = Vector3.zero,
                Spacing = new Vector3(1.5f, 0f, 1.5f),
                Pattern = LayoutPattern.Clustered,
                TypeOffsets = new Dictionary<MountType, Vector3>
                {
                    [MountType.Core] = new Vector3(0f, 0f, 0f),
                    [MountType.Engine] = new Vector3(0f, 0f, -4f),
                    [MountType.Hangar] = new Vector3(0f, 0f, 2f),
                    [MountType.Weapon] = new Vector3(2f, 0f, 0f),
                    [MountType.Defense] = new Vector3(-2f, 0f, 0f),
                    [MountType.Utility] = new Vector3(0f, 1f, 0f)
                }
            },
            [HullCategory.Station] = new LayoutConfig
            {
                BasePosition = Vector3.zero,
                Spacing = new Vector3(3f, 0f, 3f),
                Pattern = LayoutPattern.Radial,
                TypeOffsets = new Dictionary<MountType, Vector3>
                {
                    [MountType.Core] = new Vector3(0f, 0f, 0f),
                    [MountType.Utility] = new Vector3(0f, 2f, 0f),
                    [MountType.Hangar] = new Vector3(0f, -2f, 0f),
                    [MountType.Weapon] = new Vector3(4f, 0f, 0f),
                    [MountType.Defense] = new Vector3(-4f, 0f, 0f),
                    [MountType.Engine] = new Vector3(0f, 0f, 4f)
                }
            },
            [HullCategory.Escort] = new LayoutConfig
            {
                BasePosition = Vector3.zero,
                Spacing = new Vector3(1f, 0f, 1f),
                Pattern = LayoutPattern.Linear,
                TypeOffsets = new Dictionary<MountType, Vector3>
                {
                    [MountType.Core] = new Vector3(0f, 0f, 0f),
                    [MountType.Engine] = new Vector3(0f, 0f, -2f),
                    [MountType.Weapon] = new Vector3(1.5f, 0f, 0f),
                    [MountType.Defense] = new Vector3(-1.5f, 0f, 0f),
                    [MountType.Utility] = new Vector3(0f, 0.5f, 0f),
                    [MountType.Hangar] = Vector3.zero
                }
            },
            [HullCategory.Freighter] = new LayoutConfig
            {
                BasePosition = Vector3.zero,
                Spacing = new Vector3(1.5f, 0f, 1.5f),
                Pattern = LayoutPattern.Grid,
                TypeOffsets = new Dictionary<MountType, Vector3>
                {
                    [MountType.Core] = new Vector3(0f, 0f, 0f),
                    [MountType.Engine] = new Vector3(0f, 0f, -3f),
                    [MountType.Utility] = new Vector3(0f, 0f, 2f),
                    [MountType.Weapon] = new Vector3(2f, 0f, 0f),
                    [MountType.Defense] = new Vector3(-2f, 0f, 0f),
                    [MountType.Hangar] = Vector3.zero
                }
            }
        };

        /// <summary>
        /// Get layout configuration for a hull category. Returns default if not found.
        /// </summary>
        public static LayoutConfig GetLayoutConfig(HullCategory category)
        {
            if (DefaultLayouts.TryGetValue(category, out var config))
            {
                return config;
            }
            // Default fallback
            return new LayoutConfig
            {
                BasePosition = Vector3.zero,
                Spacing = new Vector3(2f, 0f, 2f),
                Pattern = LayoutPattern.Linear,
                TypeOffsets = new Dictionary<MountType, Vector3>()
            };
        }

        /// <summary>
        /// Calculate socket positions based on layout heuristics.
        /// </summary>
        public static List<Vector3> CalculateSocketPositions(
            List<HullCatalogAuthoring.HullSlotData> slots,
            HullCategory category,
            Dictionary<string, Vector3> manualOverrides = null)
        {
            var positions = new List<Vector3>();
            var config = GetLayoutConfig(category);
            var typeCounts = new Dictionary<MountType, int>();

            // Count sockets per type
            foreach (var slot in slots)
            {
                if (!typeCounts.ContainsKey(slot.type))
                {
                    typeCounts[slot.type] = 0;
                }
                typeCounts[slot.type]++;
            }

            // Calculate positions based on pattern
            var currentIndex = new Dictionary<MountType, int>();
            foreach (var slot in slots)
            {
                if (!currentIndex.ContainsKey(slot.type))
                {
                    currentIndex[slot.type] = 0;
                }

                var index = currentIndex[slot.type];
                currentIndex[slot.type]++;

                // Check for manual override
                var socketKey = $"Socket_{slot.type}_{slot.size}_{index}";
                if (manualOverrides != null && manualOverrides.TryGetValue(socketKey, out var overridePos))
                {
                    positions.Add(overridePos);
                    continue;
                }

                // Calculate position based on pattern
                Vector3 position = CalculatePositionForSlot(slot, index, typeCounts[slot.type], config);
                positions.Add(position);
            }

            return positions;
        }

        private static Vector3 CalculatePositionForSlot(
            HullCatalogAuthoring.HullSlotData slot,
            int index,
            int totalOfType,
            LayoutConfig config)
        {
            var baseOffset = config.TypeOffsets.TryGetValue(slot.type, out var offset) ? offset : Vector3.zero;
            var position = config.BasePosition + baseOffset;

            switch (config.Pattern)
            {
                case LayoutPattern.Linear:
                    position += config.Spacing * index;
                    break;

                case LayoutPattern.Radial:
                    if (totalOfType > 1)
                    {
                        var angle = (360f / totalOfType) * index * Mathf.Deg2Rad;
                        var radius = config.Spacing.magnitude;
                        position += new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                    }
                    break;

                case LayoutPattern.Grid:
                    var gridX = index % 3;
                    var gridZ = index / 3;
                    position += new Vector3(gridX * config.Spacing.x, 0f, gridZ * config.Spacing.z);
                    break;

                case LayoutPattern.Symmetric:
                    if (totalOfType > 1)
                    {
                        var side = index % 2 == 0 ? 1f : -1f;
                        var depth = index / 2;
                        position += new Vector3(side * config.Spacing.x, 0f, depth * config.Spacing.z);
                    }
                    break;

                case LayoutPattern.Clustered:
                    // Clustered by type - just use base offset with small spacing
                    position += config.Spacing * index * 0.5f;
                    break;
            }

            // Apply size-based offset
            switch (slot.size)
            {
                case MountSize.S:
                    position += new Vector3(0f, -0.1f, 0f);
                    break;
                case MountSize.M:
                    // No offset
                    break;
                case MountSize.L:
                    position += new Vector3(0f, 0.1f, 0f);
                    break;
            }

            return position;
        }
    }
}


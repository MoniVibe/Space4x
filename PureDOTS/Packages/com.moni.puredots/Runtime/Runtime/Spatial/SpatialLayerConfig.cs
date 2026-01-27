using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Spatial
{
    /// <summary>
    /// Configuration for spatial navigation layers (2D surface, 3D volume, underground, etc.).
    /// Allows multiple navigation layers to coexist with different cell sizes and cost fields.
    /// </summary>
    public struct SpatialLayerConfig : IComponentData
    {
        public byte LayerId; // 0 = default/ground, 1 = underground, 2 = air, etc.
        public FixedString32Bytes LayerName;
        public float LayerHeight; // Y-axis offset for this layer
        public float LayerHeightTolerance; // Tolerance for layer transitions
        public bool Is3D; // True for full 3D volumes, false for 2D surface projection
        public float CostMultiplier; // Cost field multiplier for pathfinding
    }
    
    /// <summary>
    /// Tag component marking entities that belong to a specific spatial layer.
    /// </summary>
    public struct SpatialLayerTag : IComponentData
    {
        public byte LayerId;
    }
}


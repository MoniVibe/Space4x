using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Environment
{
    /// <summary>
    /// Per-cell moisture data stored in blob asset.
    /// </summary>
    public struct MoistureCell
    {
        /// <summary>Moisture level (0-1, where 1 = saturated).</summary>
        public float Moisture;

        /// <summary>Drainage rate (moisture loss per tick).</summary>
        public float DrainageRate;

        /// <summary>Absorption rate (moisture gain per tick from sources).</summary>
        public float AbsorptionRate;

        /// <summary>Last update tick for this cell.</summary>
        public uint LastUpdateTick;
    }

    /// <summary>
    /// Blob asset containing moisture grid cells.
    /// </summary>
    public struct MoistureCellBlob
    {
        /// <summary>Array of moisture cells (aligned with SpatialGridConfig).</summary>
        public BlobArray<MoistureCell> Cells;
    }

    /// <summary>
    /// Global moisture grid state for a world/planet.
    /// Singleton component tracking soil/ground moisture per spatial cell.
    /// </summary>
    public struct MoistureGridState : IComponentData
    {
        /// <summary>Reference to moisture cell blob asset.</summary>
        public BlobAssetReference<MoistureCellBlob> Grid;

        /// <summary>Grid width (matches SpatialGridConfig).</summary>
        public int Width;

        /// <summary>Grid height (matches SpatialGridConfig).</summary>
        public int Height;

        /// <summary>Cell size in world units (matches SpatialGridConfig).</summary>
        public float CellSize;

        /// <summary>Last update tick for moisture grid.</summary>
        public uint LastUpdateTick;
    }

    /// <summary>
    /// Configuration for moisture grid behavior.
    /// Singleton component defining moisture update parameters.
    /// </summary>
    public struct MoistureConfig : IComponentData
    {
        /// <summary>Base evaporation rate (moisture loss per tick).</summary>
        public float BaseEvaporationRate;

        /// <summary>Base absorption rate (moisture gain per tick from rain).</summary>
        public float BaseAbsorptionRate;

        /// <summary>Drainage factor (how fast moisture flows/drains).</summary>
        public float DrainageFactor;

        /// <summary>Temperature influence on evaporation (multiplier).</summary>
        public float TemperatureEvaporationMultiplier;

        /// <summary>Wind influence on evaporation (multiplier).</summary>
        public float WindEvaporationMultiplier;

        /// <summary>Update frequency (update every N ticks).</summary>
        public uint UpdateFrequency;

        /// <summary>
        /// Default configuration with sensible values.
        /// </summary>
        public static MoistureConfig Default => new MoistureConfig
        {
            BaseEvaporationRate = 0.001f, // Slow evaporation
            BaseAbsorptionRate = 0.01f, // Fast absorption from rain
            DrainageFactor = 0.0005f, // Slow drainage
            TemperatureEvaporationMultiplier = 0.1f, // Higher temp = more evaporation
            WindEvaporationMultiplier = 0.05f, // Higher wind = more evaporation
            UpdateFrequency = 1u // Update every tick
        };
    }
}

























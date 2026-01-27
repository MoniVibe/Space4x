using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Power
{
    /// <summary>
    /// Type of power source generator.
    /// </summary>
    public enum PowerSourceKind : byte
    {
        Solar,
        Wind,
        FuelBurner,   // generic generator burning a fuel resource
        Reactor,      // fission/fusion/antimatter/etc
        RTG,
        Geothermal,
        ZeroPoint,
        Megastructure // dyson, stellar lasso, etc
    }

    /// <summary>
    /// Definition of a power source type (blob asset).
    /// </summary>
    public struct PowerSourceDefBlob
    {
        public int SourceDefId;
        public PowerSourceKind Kind;

        public float RatedOutput;        // MW baseline at ideal conditions
        public float Efficiency;         // generic overall efficiency [0..1]
        public byte TechTier;            // tech progression
        public byte QualityTier;         // manufacturer/material quality

        public float MinOperatingFraction;   // e.g. 0.2 = cannot throttle below 20%
        public float MaxOverdriveFraction;   // e.g. 1.5 = 150% with stress

        public int FuelTypeId;           // used by FuelBurner/Reactor only (0 = none)
        public float FuelUsePerMW;       // baseline consumption

        public byte Flags;               // bitfield: Clean, Volatile, Portable, etc.
    }

    /// <summary>
    /// Runtime state of a power generator.
    /// </summary>
    public struct PowerSourceState : IComponentData
    {
        public int SourceDefId;
        public float CurrentOutput;
        public float MaxOutput;         // after environment + wear
        public float Wear;              // 0..1, 0 = new
        public float MaintenanceDebt;   // 0..1
        public byte Online;             // 0/1
    }

    /// <summary>
    /// Type-specific parameters for solar sources (blob asset).
    /// </summary>
    public struct SolarSourceParamsBlob
    {
        public int SourceDefId;
        public float PanelArea;         // m^2
        public float PanelEfficiency;   // 0..1
        public float TrackingFactor;    // 0..1 (fixed vs sun-tracking)
    }

    /// <summary>
    /// Type-specific parameters for wind sources (blob asset).
    /// </summary>
    public struct WindSourceParamsBlob
    {
        public int SourceDefId;
        public float RotorArea;
        public float CutInSpeed;
        public float RatedSpeed;
        public float CutOutSpeed;
        public float TurbineEfficiency;
    }

    /// <summary>
    /// Type-specific parameters for geothermal sources (blob asset).
    /// </summary>
    public struct GeothermalSourceParamsBlob
    {
        public int SourceDefId;
        public float SiteGrade;         // 0..1, location quality
    }

    /// <summary>
    /// Type-specific parameters for RTG sources (blob asset).
    /// </summary>
    public struct RTGSourceParamsBlob
    {
        public int SourceDefId;
        public float InitialOutput;
        public float HalfLifeTicks;
    }

    /// <summary>
    /// Type-specific parameters for zero-point sources (blob asset).
    /// </summary>
    public struct ZeroPointSourceParamsBlob
    {
        public int SourceDefId;
        public float Stability;        // meltdown/anomaly risk
    }

    /// <summary>
    /// Type-specific parameters for megastructure sources (blob asset).
    /// </summary>
    public struct MegastructureSourceParamsBlob
    {
        public int SourceDefId;
        public float RadiusAU;         // Dyson swarm radius
        public float CaptureEfficiency;
        public float Coverage;         // fraction of star captured
    }

    /// <summary>
    /// Registry blob asset containing all power source definitions.
    /// </summary>
    public struct PowerSourceDefRegistryBlob
    {
        public BlobArray<PowerSourceDefBlob> SourceDefs;
        public BlobArray<SolarSourceParamsBlob> SolarParams;
        public BlobArray<WindSourceParamsBlob> WindParams;
        public BlobArray<GeothermalSourceParamsBlob> GeothermalParams;
        public BlobArray<RTGSourceParamsBlob> RTGParams;
        public BlobArray<ZeroPointSourceParamsBlob> ZeroPointParams;
        public BlobArray<MegastructureSourceParamsBlob> MegastructureParams;
    }

    /// <summary>
    /// Singleton component exposing compiled power source definitions.
    /// </summary>
    public struct PowerSourceDefRegistry : IComponentData
    {
        public BlobAssetReference<PowerSourceDefRegistryBlob> Value;
    }
}


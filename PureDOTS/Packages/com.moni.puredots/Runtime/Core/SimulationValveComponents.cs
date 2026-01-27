using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Core
{
    /// <summary>
    /// Feature flags for enabling/disabling subsystems.
    /// Uses bitfield for efficient checking.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct SimulationFeatureFlags : IComponentData
    {
        /// <summary>Bitfield of enabled features.</summary>
        public uint Flags;

        // Feature flag constants
        public const uint CommsEnabled = 1 << 0;
        public const uint PerceptionEnabled = 1 << 1;
        public const uint AIScriptsEnabled = 1 << 2;
        public const uint CombatEnabled = 1 << 3;
        public const uint PowerSimEnabled = 1 << 4;
        public const uint RewindEnabled = 1 << 5;
        public const uint FaunaSentienceEnabled = 1 << 6;
        public const uint LegacySensorSystemEnabled = 1 << 7;
        public const uint LegacyCommunicationDispatchEnabled = 1 << 8;
        public const uint SensorCommsScalingPrototype = 1 << 9;
        public const uint ComplexEntitiesEnabled = 1 << 10;
        public const uint ComplexEntityOperationalExpansionEnabled = 1 << 11;
        public const uint ComplexEntityNarrativeExpansionEnabled = 1 << 12;

        /// <summary>Default: all features enabled.</summary>
        public static SimulationFeatureFlags Default => new SimulationFeatureFlags
        {
            Flags = CommsEnabled | PerceptionEnabled | AIScriptsEnabled | CombatEnabled | PowerSimEnabled | RewindEnabled | FaunaSentienceEnabled
        };
    }

    /// <summary>
    /// Global scalar multipliers for simulation parameters.
    /// Applied at calculation time, not baked into components.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct SimulationScalars : IComponentData
    {
        /// <summary>Time scale multiplier (0 = frozen, 1 = normal, >1 = fast-forward).</summary>
        public float TimeScale;
        /// <summary>Mass multiplier (0 = massless, 1 = normal).</summary>
        public float MassMultiplier;
        /// <summary>Size multiplier for visual/collision scale.</summary>
        public float SizeMultiplier;
        /// <summary>Perception range multiplier (0 = blind, 1 = normal).</summary>
        public float PerceptionRangeMult;
        /// <summary>Rewind window multiplier (1 = normal, 2 = double length).</summary>
        public float RewindWindowMult;

        /// <summary>Default: all scalars = 1.0 (normal).</summary>
        public static SimulationScalars Default => new SimulationScalars
        {
            TimeScale = 1.0f,
            MassMultiplier = 1.0f,
            SizeMultiplier = 1.0f,
            PerceptionRangeMult = 1.0f,
            RewindWindowMult = 1.0f
        };
    }

    /// <summary>
    /// Hard overrides for specific parameters.
    /// When an override is active, it takes precedence over scalars.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct SimulationOverrides : IComponentData
    {
        [MarshalAs(UnmanagedType.U1)]
        public bool OverrideMass;
        public float MassOverride;

        [MarshalAs(UnmanagedType.U1)]
        public bool OverridePerception;
        public float PerceptionOverride;

        [MarshalAs(UnmanagedType.U1)]
        public bool OverrideTimeScale;
        public float TimeScaleOverride;

        /// <summary>Default: no overrides active.</summary>
        public static SimulationOverrides Default => new SimulationOverrides
        {
            OverrideMass = false,
            MassOverride = 0f,
            OverridePerception = false,
            PerceptionOverride = 0f,
            OverrideTimeScale = false,
            TimeScaleOverride = 0f
        };
    }

    /// <summary>
    /// Sandbox-specific scalar flags for experimental features.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct SimulationSandboxFlags : IComponentData
    {
        /// <summary>Fauna sentience level (0 = none, 1 = rare, >1 = common).</summary>
        public float FaunaSentienceLevel;

        /// <summary>Default: no sentience.</summary>
        public static SimulationSandboxFlags Default => new SimulationSandboxFlags
        {
            FaunaSentienceLevel = 0f
        };
    }

    /// <summary>
    /// Parameter kind for UI generation metadata.
    /// </summary>
    public enum ValveParamKind : byte
    {
        Bool = 0,
        Float = 1
    }

    /// <summary>
    /// Metadata definition for a valve parameter.
    /// Used by editor/UI to auto-generate controls.
    /// Values live in SimulationScalars/SimulationOverrides, not here.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct ValveParamDef
    {
        public int Id;
        public ValveParamKind Kind;
        public FixedString64Bytes Name;
        public FixedString128Bytes Description;
        public float Min;
        public float Max;
        public float DefaultValue;
    }

    /// <summary>
    /// Profile data for saving/loading valve configurations.
    /// Blob-compatible for efficient storage.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct SimulationValveProfile
    {
        public FixedString64Bytes Name;
        public SimulationFeatureFlags FeatureFlags;
        public SimulationScalars Scalars;
        public SimulationOverrides Overrides;
        public SimulationSandboxFlags SandboxFlags;
    }
}


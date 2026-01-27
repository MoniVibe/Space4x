using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Movement
{
    /// <summary>
    /// Movement kind enumeration - defines the type of movement model.
    /// </summary>
    public enum MovementKind : byte
    {
        Ground2D = 0,
        Hover2D = 1,
        Omni3D = 2,
        Forward3D = 3,
        VTOL3D = 4,
        Drift3D = 5
    }

    /// <summary>
    /// Movement capability flags - quick gates for allowed movement axes.
    /// </summary>
    [System.Flags]
    public enum MovementCaps : uint
    {
        None = 0,
        Forward = 1 << 0,
        Strafe = 1 << 1,
        Vertical = 1 << 2,
        Reverse = 1 << 3,
        TurnYaw = 1 << 4,
        TurnPitch = 1 << 5,
        TurnRoll = 1 << 6,
        Boost = 1 << 7,
        Drift = 1 << 8
    }

    /// <summary>
    /// 1D curve for Burst-friendly linear interpolation.
    /// Knots array stores evenly spaced samples (0..1 mapped to array indices).
    /// </summary>
    public struct Curve1D
    {
        public BlobArray<float> Knots;
    }

    /// <summary>
    /// Movement model specification - defines kinematic behavior for a vessel/entity.
    /// Baked from catalog, immutable at runtime.
    /// </summary>
    public struct MovementModelSpec
    {
        public FixedString32Bytes Id;
        public MovementKind Kind;
        public MovementCaps Caps; // Quick capability gates
        public byte Dim; // 2 for Godgame (terrain), 3 for Space4X (space)

        // Kinematics curves (sampled at runtime based on throttle/mass ratio)
        public Curve1D MaxSpeed; // vs throttle (0..1) or vs mass ratio
        public Curve1D AccelForward; // m/s^2
        public Curve1D AccelStrafe; // m/s^2 (0 for Forward3D)
        public Curve1D AccelVertical; // m/s^2 (hover/vtol)

        // Turn rate curves (deg/s)
        public Curve1D TurnRateYaw;
        public Curve1D TurnRatePitch;
        public Curve1D TurnRateRoll;

        public float JerkClamp; // m/s^3 - maximum rate of acceleration change

        // Energy/heat costs (Space4X), stamina (Godgame)
        public float EnergyPerAccel; // per m/s^2
        public float HeatPerAccel;

        // Terrain constraints (used when Dim==2 or Ground2D/Hover2D)
        public float MaxSlopeDeg; // Maximum slope angle before refusal
        public float GroundFriction; // Friction coefficient for ground movement
    }

    /// <summary>
    /// Catalog blob containing all movement model specifications.
    /// </summary>
    public struct MovementModelCatalogBlob
    {
        public BlobArray<MovementModelSpec> Entries;
    }
}


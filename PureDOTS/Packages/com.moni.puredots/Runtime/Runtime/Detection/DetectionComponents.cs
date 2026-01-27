using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Detection
{
    /// <summary>
    /// Detection states for entities.
    /// </summary>
    public enum VisibilityState : byte
    {
        Exposed = 0,      // Fully visible (0% stealth bonus)
        Concealed = 1,    // Behind cover, in crowds (+25%)
        Hidden = 2,       // Actively sneaking (+50%)
        Invisible = 3     // Magical/tech invisibility (+75%)
    }

    /// <summary>
    /// Stealth capabilities of an entity.
    /// </summary>
    public struct StealthStats : IComponentData
    {
        public float BaseStealthRating;     // Skill-based hiding ability
        public float EquipmentBonus;        // Cloaking device, shadow cloak
        public VisibilityState CurrentState;
        public float MovementPenalty;       // Running = harder to hide
        public float EnvironmentBonus;      // Darkness, fog, etc.
        public float EffectiveRating;       // Calculated total
    }

    /// <summary>
    /// Perception capabilities of an entity.
    /// </summary>
    public struct PerceptionStats : IComponentData
    {
        public float BasePerceptionRating;  // Detection ability
        public float EquipmentBonus;        // Night vision, sensors
        public float AlertnessLevel;        // 0=sleeping, 1=alert, 2=searching
        public float DetectionRadius;       // How far can detect
        public float EffectiveRating;       // Calculated total
    }

    /// <summary>
    /// Result of detecting an entity.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct DetectionResult : IBufferElementData
    {
        public Entity DetectedEntity;
        public float Confidence;            // 0-1, how certain of detection
        public float3 LastKnownPosition;
        public uint DetectionTick;
        public byte IsCurrentlyVisible;     // Still in sight?
    }

    /// <summary>
    /// Environmental visibility modifier zone.
    /// </summary>
    public struct VisibilityZone : IComponentData
    {
        public float3 Center;
        public float Radius;
        public float StealthModifier;       // +0.3 = easier to hide, -0.3 = harder
        public FixedString32Bytes ZoneType; // "darkness", "fog", "crowd"
    }

    /// <summary>
    /// Configuration for detection checks.
    /// </summary>
    public struct DetectionConfig : IComponentData
    {
        public float BaseSuccessChance;     // 50% baseline
        public float DistanceFalloff;       // Harder to detect at range
        public float MovementDetectionBonus;// Moving targets easier to spot
        public float AlertnessMultiplier;   // Alert guards detect better
        public uint CheckIntervalTicks;     // How often to roll detection
    }

    /// <summary>
    /// Tracking state for an observer.
    /// </summary>
    public struct ObserverState : IComponentData
    {
        public uint LastCheckTick;
        public byte IsSearching;            // Actively looking for hidden
        public float SearchDuration;        // How long been searching
        public Entity PrimaryTarget;        // Main entity being tracked
    }

    /// <summary>
    /// Alert state when suspicious activity detected.
    /// </summary>
    public struct AlertState : IComponentData
    {
        public float AlertLevel;            // 0=calm, 1=suspicious, 2=alarmed
        public float3 LastAlertPosition;
        public uint AlertStartTick;
        public uint AlertDecayTick;         // When alert starts decaying
        public byte IsInvestigating;
    }

    /// <summary>
    /// Cover point for stealth gameplay.
    /// </summary>
    public struct CoverPoint : IComponentData
    {
        public float3 Position;
        public float CoverValue;            // 0-1, how much cover provided
        public float3 CoverDirection;       // Direction cover faces
        public byte IsOccupied;
    }
}


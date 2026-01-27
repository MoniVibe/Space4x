using System;
using Unity.Entities;

namespace PureDOTS.Runtime.Agency
{
    /// <summary>
    /// Domains (areas) of control/agency that can be independently contested.
    /// A domain is a bitmask so multiple domains may be granted to a single controller link.
    /// </summary>
    [Flags]
    public enum AgencyDomain : uint
    {
        None = 0u,

        // Core (shared) domains
        SelfBody = 1u << 0,         // bodily autonomy, self-preservation, internal actions
        Movement = 1u << 1,         // locomotion/navigation
        Work = 1u << 2,             // labor/operations (mining, building, crafting)
        Combat = 1u << 3,           // violence/weapons use
        Communications = 1u << 4,   // comms/signals/broadcasting

        // Operations (common in vehicles/ships, but still generic)
        Sensors = 1u << 5,
        Logistics = 1u << 6,
        FlightOps = 1u << 7,

        // Governance (macro aggregates)
        Governance = 1u << 8,
        Construction = 1u << 9,
        Security = 1u << 10,

        All = 0xFFFFFFFFu
    }

    /// <summary>
    /// Origin tag for control links so bridge systems can manage their own links safely.
    /// </summary>
    public enum ControlLinkSourceKind : byte
    {
        None = 0,
        Claim = 1,
        Custody = 2
    }

    /// <summary>
    /// Controlled entity's baseline autonomy state used in control contests.
    /// Values are normalized (typically 0..1) and are intended to be driven by other modules (needs, morale, ideology).
    /// </summary>
    public struct AgencySelf : IComponentData
    {
        /// <summary>Baseline resistance to external control (0..1).</summary>
        public float BaseResistance;

        /// <summary>
        /// Urgency of self-needs (0..1). Higher increases resistance when a controller is hostile to the body-mind.
        /// </summary>
        public float SelfNeedUrgency;

        /// <summary>
        /// General affinity for surrendering autonomy (0..1). Enables willing domination / hive-mind behavior.
        /// </summary>
        public float DominationAffinity;
    }

    /// <summary>
    /// A controller link from this entity to an external controlling entity, with tunable contest parameters.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct ControlLink : IBufferElementData
    {
        public Entity Controller;
        public AgencyDomain Domains;

        /// <summary>Raw pressure applied by the controller (0..1+).</summary>
        public float Pressure;

        /// <summary>How legitimate/accepted the controller is (0..1).</summary>
        public float Legitimacy;

        /// <summary>How hostile the controller is to the body-mind (0..1).</summary>
        public float Hostility;

        /// <summary>Local consent toward this controller (0..1). Higher reduces resistance.</summary>
        public float Consent;

        public uint EstablishedTick;

        public ControlLinkSourceKind SourceKind;
        public byte Reserved0;
        public ushort Reserved1;
    }

    /// <summary>
    /// Derived per-domain resolved controller. Controller == Entity.Null implies self-control.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct ResolvedControl : IBufferElementData
    {
        /// <summary>A single-bit domain mask.</summary>
        public AgencyDomain Domain;
        public Entity Controller;
        public float Score;
    }

    public static class AgencyDefaults
    {
        public static AgencySelf ToolSelf() =>
            new()
            {
                BaseResistance = 0.1f,
                SelfNeedUrgency = 0.05f,
                DominationAffinity = 0.85f
            };

        public static AgencySelf SentientSelf() =>
            new()
            {
                BaseResistance = 0.65f,
                SelfNeedUrgency = 0.45f,
                DominationAffinity = 0.1f
            };

        public static AgencySelf DefaultSelf() =>
            new()
            {
                BaseResistance = 0.5f,
                SelfNeedUrgency = 0.25f,
                DominationAffinity = 0f
            };
    }
}

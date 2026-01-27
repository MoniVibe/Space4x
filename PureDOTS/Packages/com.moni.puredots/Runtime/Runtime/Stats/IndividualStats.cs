using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Stats
{
    /// <summary>
    /// Core individual stats for Space4X entities (captains, officers, crew).
    /// Values range from 0-100.
    /// </summary>
    public struct IndividualStats : IComponentData
    {
        /// <summary>
        /// Command stat (0-100). Affects max pilots/crews, morale bonuses, command point replenishment, formation coordination.
        /// </summary>
        public half Command;

        /// <summary>
        /// Tactics stat (0-100). Affects special ability count/cooldowns, targeting accuracy, engagement timing.
        /// </summary>
        public half Tactics;

        /// <summary>
        /// Logistics stat (0-100). Affects cargo transfer speed, utility vessel speeds, dock/hangar throughput.
        /// </summary>
        public half Logistics;

        /// <summary>
        /// Diplomacy stat (0-100). Affects agreement success rates, relation modifiers, interception/stance decisions.
        /// </summary>
        public half Diplomacy;

        /// <summary>
        /// Engineering stat (0-100). Affects repair/refit speeds, costs, jam chance reduction, system complexity reduction.
        /// </summary>
        public half Engineering;

        /// <summary>
        /// Resolve stat (0-100). Affects morale thresholds, recall thresholds, action speed, risk tolerance.
        /// </summary>
        public half Resolve;
    }

    /// <summary>
    /// Physique, Finesse, and Will attributes with inclinations and GeneralXP pool.
    /// Values range from 1-10 for attributes, inclinations are 1-10.
    /// </summary>
    public struct PhysiqueFinesseWill : IComponentData
    {
        /// <summary>
        /// Physique attribute (1-10). Affects strike craft performance, physical tasks.
        /// </summary>
        public half Physique;

        /// <summary>
        /// Finesse attribute (1-10). Affects crew task efficiency, precision tasks.
        /// </summary>
        public half Finesse;

        /// <summary>
        /// Will attribute (1-10). Affects psionic abilities, mental fortitude.
        /// </summary>
        public half Will;

        /// <summary>
        /// Physique inclination (1-10). Preference toward physique-based activities.
        /// </summary>
        public half PhysiqueInclination;

        /// <summary>
        /// Finesse inclination (1-10). Preference toward finesse-based activities.
        /// </summary>
        public half FinesseInclination;

        /// <summary>
        /// Will inclination (1-10). Preference toward will-based activities.
        /// </summary>
        public half WillInclination;

        /// <summary>
        /// General XP pool that can be allocated to any attribute.
        /// </summary>
        public float GeneralXP;
    }

    /// <summary>
    /// Expertise entry in a buffer. Tracks expertise type and tier.
    /// </summary>
    public struct ExpertiseEntry : IBufferElementData
    {
        /// <summary>
        /// Expertise type identifier (CarrierCommand, Espionage, Logistics, Psionic, Beastmastery, etc.).
        /// </summary>
        public FixedString32Bytes Type;

        /// <summary>
        /// Expertise tier (0-255).
        /// </summary>
        public byte Tier;
    }

    /// <summary>
    /// Service trait entry in a buffer. Represents special abilities or modifiers.
    /// </summary>
    public struct ServiceTrait : IBufferElementData
    {
        /// <summary>
        /// Trait identifier (ReactorWhisperer, StrikeWingMentor, TacticalSavant, LogisticsMaestro, PirateBane, etc.).
        /// </summary>
        public FixedString32Bytes Id;
    }
}


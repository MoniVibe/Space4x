using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Motivation
{
    /// <summary>
    /// Definition of a single motivation archetype.
    /// This is just data; interpretation is left to the game.
    /// </summary>
    public struct MotivationSpec
    {
        /// <summary>Unique identifier for this motivation spec.</summary>
        public short SpecId;

        /// <summary>Layer/type of this motivation.</summary>
        public MotivationLayer Layer;

        /// <summary>Scope (individual, aggregate, or either).</summary>
        public MotivationScope Scope;

        /// <summary>High-level category tag.</summary>
        public MotivationTag Tag;

        /// <summary>
        /// 0–255. Baseline importance if not overridden at runtime.
        /// </summary>
        public byte BaseImportance;

        /// <summary>
        /// 0–255. How "expensive" it is in initiative to pursue.
        /// </summary>
        public byte BaseInitiativeCost;

        /// <summary>
        /// How many entities can reasonably hold this at once
        /// (e.g. global mythic ambitions vs local personal dreams).
        /// 0 = unlimited.
        /// </summary>
        public byte MaxConcurrentHolders;

        /// <summary>
        /// Minimum loyalty bias required to prioritize this over self-interest (0–200).
        /// Use 0 for purely selfish goals, 200 for pure martyrdom-level goals.
        /// </summary>
        public byte RequiredLoyalty;

        /// <summary>
        /// Minimum corrupt/pure alignment required (-100..100).
        /// Reserved for future alignment hooks.
        /// </summary>
        public sbyte MinCorruptPure;

        /// <summary>
        /// Minimum lawful/chaotic alignment required (-100..100).
        /// Reserved for future alignment hooks.
        /// </summary>
        public sbyte MinLawChaos;

        /// <summary>
        /// Minimum good/evil alignment required (-100..100).
        /// Reserved for future alignment hooks.
        /// </summary>
        public sbyte MinGoodEvil;
    }

    /// <summary>
    /// Blob asset catalog containing all motivation specs.
    /// Built from ScriptableObjects/JSON and merged at bootstrap.
    /// </summary>
    public struct MotivationCatalog
    {
        /// <summary>Array of motivation specs.</summary>
        public BlobArray<MotivationSpec> Specs;
    }

    /// <summary>
    /// Simulation-wide knobs for the Motivation system.
    /// One singleton in the world, set per-game during bootstrap.
    /// </summary>
    public struct MotivationConfigState : IComponentData
    {
        /// <summary>Reference to the motivation catalog blob asset.</summary>
        public BlobAssetReference<MotivationCatalog> Catalog;

        /// <summary>
        /// How many sim ticks between dream/goal refresh passes.
        /// </summary>
        public uint TicksBetweenRefresh;

        /// <summary>
        /// Default number of dream slots per entity (can be overridden in authoring).
        /// </summary>
        public byte DefaultDreamSlots;

        /// <summary>
        /// Default number of aspiration slots per entity.
        /// </summary>
        public byte DefaultAspirationSlots;

        /// <summary>
        /// Default number of wish slots per entity.
        /// </summary>
        public byte DefaultWishSlots;

        /// <summary>
        /// Default configuration with sensible values.
        /// </summary>
        public static MotivationConfigState Default => new MotivationConfigState
        {
            Catalog = default,
            TicksBetweenRefresh = 100u, // Refresh every 100 ticks
            DefaultDreamSlots = 3,
            DefaultAspirationSlots = 2,
            DefaultWishSlots = 2
        };
    }

    /// <summary>
    /// Configurable scoring weights for intent selection.
    /// Allows mods/game modes to tweak how goals are prioritized.
    /// </summary>
    public struct MotivationScoringConfig : IComponentData
    {
        /// <summary>Weight multiplier for Importance in scoring.</summary>
        public float ImportanceWeight;

        /// <summary>Weight multiplier for Initiative in scoring.</summary>
        public float InitiativeWeight;

        /// <summary>Weight multiplier for Loyalty in scoring.</summary>
        public float LoyaltyWeight;

        /// <summary>
        /// Default scoring configuration.
        /// </summary>
        public static MotivationScoringConfig Default => new MotivationScoringConfig
        {
            ImportanceWeight = 1.0f,
            InitiativeWeight = 1.0f,
            LoyaltyWeight = 0.5f
        };
    }
}

























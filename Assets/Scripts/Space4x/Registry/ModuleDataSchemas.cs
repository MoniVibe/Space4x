using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    public enum MountType : byte
    {
        Core = 0,
        Engine = 1,
        Weapon = 2,
        Defense = 3,
        Utility = 4,
        Hangar = 5
    }

    public enum MountSize : byte
    {
        S = 0,
        M = 1,
        L = 2
    }

    public enum ModuleClass : byte
    {
        Reactor = 0,
        Engine = 1,
        Laser = 2,
        Kinetic = 3,
        Missile = 4,
        PointDefense = 5,
        Shield = 6,
        Armor = 7,
        Hangar = 8,
        RepairDrones = 9,
        Scanner = 10,
        Cargo = 11,
        Tractor = 12,
        Bridge = 13,
        Cockpit = 14,
        Ammunition = 15
    }

    public enum HullCategory : byte
    {
        CapitalShip = 0,
        Carrier = 1,
        Station = 2,
        Escort = 3,
        Freighter = 4,
        Other = 255
    }

    public enum ModuleFunction : byte
    {
        None = 0,
        HangarCapacity = 1,
        CargoCapacity = 2,
        Manufacturing = 3,
        Research = 4,
        RefitFacility = 5,
        RepairFacility = 6,
        ResourceProcessing = 7,
        Habitat = 8,
        Command = 9,
        Other = 255
    }

    public enum FacilityArchetype : byte
    {
        None = 0,
        Refinery = 1,
        Fabricator = 2,
        Foundry = 3,
        Bioprocessor = 4,
        ResearchLab = 5,
        LogisticsHub = 6,
        HabitatModule = 7,
        TitanForge = 8,
        MobileFabricationBay = 9,
        ExpeditionLab = 10,
        OrbitalDrydock = 11,
        TradeNexus = 12,
        DefenceGridControl = 13,
        TerraformingPlant = 14,
        CivicWorks = 15,
        CulturalArchive = 16,
        StellarManipulator = 17,
        SupercarrierHangar = 18
    }

    public enum FacilityTier : byte
    {
        Small = 0,
        Medium = 1,
        Large = 2,
        Massive = 3,
        Titanic = 4
    }

    public enum ModuleRarity : byte
    {
        Common = 0,
        Uncommon = 1,
        Heroic = 2,
        Prototype = 3
    }

    public enum HullVariant : byte
    {
        Common = 0,
        Uncommon = 1,
        Heroic = 2,
        Prototype = 3
    }

    // Weapon & Projectile enums
    public enum WeaponClass : byte
    {
        Laser = 0,
        Kinetic = 1,
        Missile = 2,
        Beam = 3,
        Plasma = 4
    }

    public enum ProjectileKind : byte
    {
        Ballistic = 0,
        BeamTick = 1,
        Missile = 2,
        AoE = 3
    }

    public struct ModuleSpec
    {
        public FixedString64Bytes Id;
        public ModuleClass Class;
        public MountType RequiredMount;
        public MountSize RequiredSize;
        public float MassTons;
        public float PowerDrawMW;
        public byte OffenseRating;
        public byte DefenseRating;
        public byte UtilityRating;
        public float DefaultEfficiency;
        // Prefab generation metadata
        public ModuleFunction Function;
        public float FunctionCapacity; // e.g., hangar capacity, cargo capacity
        public FixedString64Bytes FunctionDescription; // Human-readable description
        // Quality/rarity/tier/manufacturer metadata
        public float Quality; // 0-1, fine control over spread/dispersion, misfire risk, maintenance load
        public ModuleRarity Rarity; // Common, Uncommon, Heroic, Prototype
        public byte Tier; // 0-255, drives baseline performance, reliability
        public FixedString64Bytes ManufacturerId; // References manufacturer catalog
        // Facility archetype/tier metadata (for facility modules)
        public FacilityArchetype FacilityArchetype; // Refinery, Fabricator, etc.
        public FacilityTier FacilityTier; // Small, Medium, Large, Massive, Titanic
    }

    public struct HullSlot
    {
        public MountType Type;
        public MountSize Size;
    }

    public struct HullSpec
    {
        public FixedString64Bytes Id;
        public float BaseMassTons;
        public bool FieldRefitAllowed;
        public BlobArray<HullSlot> Slots;
        // Prefab generation metadata
        public HullCategory Category;
        public float HangarCapacity; // Total hangar capacity (sum of hangar modules)
        public FixedString64Bytes PresentationArchetype; // e.g., "capital-ship", "carrier", "station"
        public StyleTokens DefaultStyleTokens; // Default style tokens for this hull
        // Variant metadata
        public HullVariant Variant; // Common, Uncommon, Heroic, Prototype
        public BlobArray<FixedString64Bytes> BuiltInModuleLoadouts; // Pre-configured module IDs
    }

    public struct RefitRepairTuning
    {
        public float BaseRefitSeconds;
        public float MassSecPerTon;
        public float SizeMultS;
        public float SizeMultM;
        public float SizeMultL;
        public float StationTimeMult;
        public float FieldTimeMult;
        public bool GlobalFieldRefitEnabled;
        public float RepairRateEffPerSecStation;
        public float RepairRateEffPerSecField;
        public float RewirePenaltySeconds;
    }

    public struct ModuleCatalogBlob
    {
        public BlobArray<ModuleSpec> Modules;
    }

    public struct HullCatalogBlob
    {
        public BlobArray<HullSpec> Hulls;
    }

    public struct ModuleCatalogSingleton : IComponentData
    {
        public BlobAssetReference<ModuleCatalogBlob> Catalog;
    }

    public struct HullCatalogSingleton : IComponentData
    {
        public BlobAssetReference<HullCatalogBlob> Catalog;
    }

    public struct RefitRepairTuningSingleton : IComponentData
    {
        public BlobAssetReference<RefitRepairTuning> Tuning;
    }

    public struct RefitFacilityTag : IComponentData { }

    public struct FacilityZone : IComponentData
    {
        public float RadiusMeters;
    }

    public struct InRefitFacilityTag : IComponentData { }

    // Runtime components for prefab identification
    public struct HullId : IComponentData
    {
        public FixedString64Bytes Id;
    }

    public struct ModuleId : IComponentData
    {
        public FixedString64Bytes Id;
    }

    public struct StationId : IComponentData
    {
        public FixedString64Bytes Id;
    }

    public struct MountRequirement : IComponentData
    {
        public MountType Type;
        public MountSize Size;
    }

    public struct StyleTokens : IComponentData
    {
        public byte Palette;
        public byte Roughness;
        public byte Pattern;
    }

    public struct HullSocketTag : IComponentData { }

    // Runtime components for prefab attributes
    public struct HangarCapacity : IComponentData
    {
        public float Capacity; // Total hangar capacity
    }

    public struct ModuleFunctionData : IComponentData
    {
        public ModuleFunction Function;
        public float Capacity; // Function-specific capacity
        public FixedString64Bytes Description;
    }

    // Runtime components for module quality attributes
    public struct ModuleQuality : IComponentData
    {
        public float Value; // 0-1, fine control over spread/dispersion, misfire risk, maintenance load
    }

    public struct ModuleRarityComponent : IComponentData
    {
        public ModuleRarity Value; // Common, Uncommon, Heroic, Prototype
    }

    public struct ModuleTier : IComponentData
    {
        public byte Value; // 0-255, drives baseline performance, reliability
    }

    public struct ModuleManufacturer : IComponentData
    {
        public FixedString64Bytes ManufacturerId; // References manufacturer catalog
    }

    // Runtime components for facility archetype/tier
    public struct FacilityArchetypeComponent : IComponentData
    {
        public FacilityArchetype Value;
    }

    public struct FacilityTierComponent : IComponentData
    {
        public FacilityTier Value;
    }

    // Runtime component for hull variant
    public struct HullVariantComponent : IComponentData
    {
        public HullVariant Value;
    }

    public struct CapitalShipTag : IComponentData { }

    public struct CarrierTag : IComponentData { }

    public enum Space4XStationSpecialization : byte
    {
        General = 0,
        Industrial = 1,
        Military = 2,
        Scientific = 3,
        Logistics = 4,
        Trade = 5
    }

    [Flags]
    public enum Space4XStationServiceFlags : uint
    {
        None = 0,
        Refit = 1u << 0,
        Repair = 1u << 1,
        Trade = 1u << 2,
        Manufacturing = 1u << 3,
        Research = 1u << 4,
        Recruitment = 1u << 5
    }

    public struct Space4XStationServiceProfileOverride : IComponentData
    {
        public byte Enabled;
        public Space4XStationSpecialization Specialization;
        public Space4XStationServiceFlags Services;
        public byte Tier;
        public float ServiceScale;
    }

    public struct Space4XStationAccessPolicyOverride : IComponentData
    {
        public byte Enabled;
        public float MinStandingForApproach;
        public float MinStandingForDock;
        public float WarningRadiusMeters;
        public float NoFlyRadiusMeters;
        public byte EnforceNoFlyZone;
        public byte DenyDockingWithoutStanding;
    }

    // Additional catalog types for North Star expansion
    public struct StationSpec
    {
        public FixedString64Bytes Id;
        public bool HasRefitFacility;
        public float FacilityZoneRadius;
        public FixedString64Bytes PresentationArchetype;
        public Space4XStationSpecialization Specialization;
        public Space4XStationServiceFlags Services;
        public byte Tier;
        public float ServiceScale;
        public byte HasServiceProfileOverride;
        public float MinStandingForApproach;
        public float MinStandingForDock;
        public float WarningRadiusMeters;
        public float NoFlyRadiusMeters;
        public byte EnforceNoFlyZone;
        public byte DenyDockingWithoutStanding;
        public byte HasAccessPolicyOverride;
        public StyleTokens DefaultStyleTokens;
    }

    public struct ResourceSpec
    {
        public FixedString64Bytes Id;
        public ResourceType Type; // Maps to existing ResourceType enum
        public FixedString64Bytes PresentationArchetype;
        public StyleTokens DefaultStyleTokens;
    }

    public struct ProductSpec
    {
        public FixedString64Bytes Id;
        public FixedString64Bytes DisplayName;
        public byte RequiredTechTier; // Tech gate
        public FixedString64Bytes PresentationArchetype;
        public StyleTokens DefaultStyleTokens;
    }

    public struct RecipeSpec
    {
        public FixedString64Bytes Id;
        public FixedString64Bytes OutputProductId;
        public BlobArray<FixedString64Bytes> InputResourceIds; // Input resource IDs
        public float ProductionTimeSeconds;
        public byte RequiredTechTier; // Tech gate
        public float EnergyCostMW;
    }

    public struct AggregateSpec
    {
        public FixedString64Bytes Id;
        public byte Alignment; // Alignment token (0-255)
        public byte Outlook; // Outlook token (0-255)
        public byte Policy; // Policy token (0-255)
        public FixedString64Bytes PresentationArchetype;
        public StyleTokens DefaultStyleTokens;
    }

    public struct TechSpec
    {
        public FixedString64Bytes Id;
        public byte Tier; // Tech tier (0-255)
        public BlobArray<FixedString64Bytes> Unlocks; // IDs of things this tech unlocks
        public BlobArray<FixedString64Bytes> Requires; // IDs of prerequisite techs
    }

    public struct EffectSpec
    {
        public FixedString64Bytes Id;
        public FixedString64Bytes PresentationArchetype;
        public StyleTokens DefaultStyleTokens;
    }

    // Weapon & Projectile specs (data-driven, GameObject-free)
    public struct DamageModel
    {
        public float Kinetic;
        public float Energy;
        public float Explosive;
    }

    public struct EffectOp
    {
        public byte Kind; // Effect operation type
        public float Magnitude;
        public float Duration;
        public uint StatusId; // Status effect ID if applicable
    }

    public struct WeaponSpec
    {
        public FixedString64Bytes Id;
        public WeaponClass Class;
        public float FireRate; // shots/sec
        public byte BurstCount; // 1..N
        public float SpreadDeg; // cone spread in degrees
        public float EnergyCost;
        public float HeatCost;
        public float LeadBias; // 0..1 (aiming hint)
        public Space4XDamageType DamageType; // Optional override for damage type
        public FixedString32Bytes ProjectileId; // References ProjectileSpec
    }

    public struct ProjectileSpec
    {
        public FixedString64Bytes Id;
        public ProjectileKind Kind;
        public float Speed; // m/s (0 for hitscan beam)
        public float Lifetime; // s
        public float Gravity; // m/s^2 (0 space)
        public float TurnRateDeg; // homing for missiles
        public float SeekRadius; // homing acquisition radius
        public float Pierce; // how many targets can pass through
        public float ChainRange; // chaining arcs (0 = none)
        public float AoERadius; // explosion radius
        public DamageModel Damage;
        public Space4XDamageType DamageType; // Optional override for damage type
        public BlobArray<EffectOp> OnHit; // e.g., status, knockback, spawn subprojectile
    }

    public struct TurretSpec
    {
        public FixedString64Bytes Id;
        public float ArcLimitDeg; // Traverse arc limit
        public float TraverseSpeedDegPerSec; // Traverse speed
        public float ElevationMinDeg; // Minimum elevation
        public float ElevationMaxDeg; // Maximum elevation
        public float RecoilForce; // Recoil force
        public FixedString32Bytes SocketName; // Socket name for muzzle binding (e.g., "Socket_Muzzle")
    }

    // Catalog blobs
    public struct StationCatalogBlob
    {
        public BlobArray<StationSpec> Stations;
    }

    public struct ResourceCatalogBlob
    {
        public BlobArray<ResourceSpec> Resources;
    }

    public struct ProductCatalogBlob
    {
        public BlobArray<ProductSpec> Products;
    }

    public struct RecipeCatalogBlob
    {
        public BlobArray<RecipeSpec> Recipes;
    }

    public struct AggregateCatalogBlob
    {
        public BlobArray<AggregateSpec> Aggregates;
    }

    public struct TechCatalogBlob
    {
        public BlobArray<TechSpec> Techs;
    }

    public struct EffectCatalogBlob
    {
        public BlobArray<EffectSpec> Effects;
    }

    public struct WeaponCatalogBlob
    {
        public BlobArray<WeaponSpec> Weapons;
    }

    public struct ProjectileCatalogBlob
    {
        public BlobArray<ProjectileSpec> Projectiles;
    }

    public struct TurretCatalogBlob
    {
        public BlobArray<TurretSpec> Turrets;
    }

    // Catalog singletons
    public struct StationCatalogSingleton : IComponentData
    {
        public BlobAssetReference<StationCatalogBlob> Catalog;
    }

    public struct ResourceCatalogSingleton : IComponentData
    {
        public BlobAssetReference<ResourceCatalogBlob> Catalog;
    }

    public struct ProductCatalogSingleton : IComponentData
    {
        public BlobAssetReference<ProductCatalogBlob> Catalog;
    }

    public struct RecipeCatalogSingleton : IComponentData
    {
        public BlobAssetReference<RecipeCatalogBlob> Catalog;
    }

    public struct AggregateCatalogSingleton : IComponentData
    {
        public BlobAssetReference<AggregateCatalogBlob> Catalog;
    }

    public struct TechCatalogSingleton : IComponentData
    {
        public BlobAssetReference<TechCatalogBlob> Catalog;
    }

    public struct EffectCatalogSingleton : IComponentData
    {
        public BlobAssetReference<EffectCatalogBlob> Catalog;
    }

    public struct WeaponCatalogSingleton : IComponentData
    {
        public BlobAssetReference<WeaponCatalogBlob> Catalog;
    }

    public struct ProjectileCatalogSingleton : IComponentData
    {
        public BlobAssetReference<ProjectileCatalogBlob> Catalog;
    }

    public struct TurretCatalogSingleton : IComponentData
    {
        public BlobAssetReference<TurretCatalogBlob> Catalog;
    }

    // Additional runtime components for prefab identification
    public struct ResourceId : IComponentData
    {
        public FixedString64Bytes Id;
    }

    public struct ProductId : IComponentData
    {
        public FixedString64Bytes Id;
    }

    public struct AggregateId : IComponentData
    {
        public FixedString64Bytes Id;
    }

    public struct EffectId : IComponentData
    {
        public FixedString64Bytes Id;
    }

    public struct AggregateTags : IComponentData
    {
        public byte Alignment;
        public byte Outlook;
        public byte Policy;
    }

    // Runtime components for aggregate composition
    public struct AggregateType : IComponentData
    {
        public AffiliationType Value; // Dynasty, Guild, Corporation, Army, Band
    }

    public struct Reputation : IComponentData
    {
        public half ReputationScore; // 0-1
        public half PrestigeScore; // 0-1
    }

    public struct ComposedAggregateProfile : IComponentData
    {
        public FixedString32Bytes TemplateId;
        public FixedString32Bytes OutlookId;
        public FixedString32Bytes AlignmentId;
        public FixedString32Bytes PersonalityId;
        public FixedString32Bytes ThemeId;
    }

    // Runtime components for individual entities
    public struct IndividualStats : IComponentData
    {
        public half Command;
        public half Tactics;
        public half Logistics;
        public half Diplomacy;
        public half Engineering;
        public half Resolve;
    }

    public struct DerivedCapacities : IComponentData
    {
        public float Sight;
        public float Manipulation;
        public float Consciousness;
        public float ReactionTime;
        public float Boarding;
    }

    public struct CrewSkill : IComponentData
    {
        public float Value;
    }

    public enum AnatomyPartIds : ushort
    {
        Head = 1,
        EyeLeft = 2,
        EyeRight = 3,
        Brain = 4
    }

    [Flags]
    public enum AnatomyPartTags : ushort
    {
        None = 0,
        Internal = 1 << 0,
        Sensory = 1 << 1,
        Vital = 1 << 2
    }

    [InternalBufferCapacity(8)]
    public struct AnatomyPart : IBufferElementData
    {
        public AnatomyPartIds PartId;
        public int ParentIndex;
        public float Coverage;
        public AnatomyPartTags Tags;
    }

    [Flags]
    public enum ConditionFlags : ushort
    {
        None = 0,
        Missing = 1 << 0,
        OneEyeMissing = 1 << 1
    }

    [InternalBufferCapacity(8)]
    public struct Condition : IBufferElementData
    {
        public AnatomyPartIds TargetPartId;
        public float Severity;
        public byte StageId;
        public ConditionFlags Flags;
    }

    public struct PhysiqueFinesseWill : IComponentData
    {
        public half Physique;
        public half Finesse;
        public half Will;
        public byte PhysiqueInclination; // 1-10
        public byte FinesseInclination; // 1-10
        public byte WillInclination; // 1-10
        public float GeneralXP;
    }

    public enum ExpertiseType : byte
    {
        CarrierCommand = 0,
        Espionage = 1,
        Logistics = 2,
        Psionic = 3,
        Beastmastery = 4,
        Count = 5
    }

    [InternalBufferCapacity(5)]
    public struct ExpertiseEntry : IBufferElementData
    {
        public ExpertiseType Type;
        public byte Tier; // 0-255
    }

    public enum ServiceTraitId : ushort
    {
        None = 0,
        ReactorWhisperer = 1,
        StrikeWingMentor = 2,
        TacticalSavant = 3,
        LogisticsMaestro = 4,
        PirateBane = 5
    }

    [InternalBufferCapacity(5)]
    public struct ServiceTrait : IBufferElementData
    {
        public ServiceTraitId TraitId;
    }

    public enum PreordainTrack : byte
    {
        None = 0,
        CombatAce = 1,
        LogisticsMaven = 2,
        DiplomaticEnvoy = 3,
        EngineeringSavant = 4
    }

    public struct PreordainProfile : IComponentData
    {
        public PreordainTrack Track;
    }

    public enum TitleTier : byte
    {
        None = 0,
        Captain = 1,
        Admiral = 2,
        Governor = 3,
        HighMarshal = 4,
        StellarLord = 5,
        InterstellarLord = 6,
        Stellarch = 7,
        GrandStellarch = 8
    }

    public enum TitleType : byte
    {
        Hero = 0,      // Title as hero of a colony (founding/defending)
        Elite = 1,     // Title as elite living in colony/faction/empire (renown-based)
        Ruler = 2      // Title to rule over colony/worlds/systems
    }

    /// <summary>
    /// Title level/hierarchy - represents the scope and power of the title.
    /// From leader of an upstart band to ruler of multiple empires.
    /// Higher levels are more prestigious and powerful.
    /// </summary>
    public enum TitleLevel : byte
    {
        None = 0,
        BandLeader = 1,           // Leader of an upstart band
        SquadLeader = 2,          // Leader of a squad/unit
        CompanyLeader = 3,        // Leader of a company
        ColonyHero = 4,           // Hero of a single colony
        ColonyElite = 5,          // Elite of a single colony
        ColonyRuler = 6,          // Ruler of a single colony
        FactionHero = 7,          // Hero recognized across a faction
        FactionElite = 8,         // Elite member of a faction
        FactionRuler = 9,         // Ruler of a faction
        WorldRuler = 10,          // Ruler of a world/system
        MultiWorldRuler = 11,     // Ruler of multiple worlds/systems
        EmpireHero = 12,          // Hero recognized across an empire
        EmpireElite = 13,         // Elite member of an empire
        EmpireRuler = 14,         // Ruler of a single empire
        MultiEmpireRuler = 15     // Ruler of multiple empires
    }

    /// <summary>
    /// Title state - represents whether a title is currently held or has been lost.
    /// Former titles carry diminished prestige but are still remembered.
    /// </summary>
    public enum TitleState : byte
    {
        Active = 0,               // Currently held title
        Lost = 1,                 // Title was lost (general case)
        Usurped = 2,              // Title was taken by force
        Disinherited = 3,         // Title was removed through inheritance dispute
        Revoked = 4,              // Title was officially revoked
        Former = 5                // Former title (carries prestige but is shadow of proper title)
    }

    [InternalBufferCapacity(5)]
    public struct TitleEntry : IBufferElementData
    {
        public TitleTier Tier;
        public TitleType Type;
        public TitleLevel Level;                  // Hierarchical level of the title
        public TitleState State;                  // Current state of the title (Active, Lost, Former, etc.)
        public FixedString64Bytes DisplayName;
        public FixedString64Bytes ColonyId;      // Associated colony (if applicable)
        public FixedString64Bytes FactionId;     // Associated faction (if applicable)
        public FixedString64Bytes EmpireId;      // Associated empire (if applicable)
        public uint AcquiredTick;                 // When title was acquired
        public uint LostTick;                     // When title was lost (0 if still active)
        public FixedString64Bytes AcquisitionReason; // "Founded", "Defended", "Renown", etc.
        public FixedString64Bytes LossReason;     // "Broken", "Fallen", "Usurped", "Revoked", etc.
    }

    /// <summary>
    /// Component that tracks the highest level ACTIVE title for presentation purposes.
    /// Former/lost titles are not considered for highest title calculation.
    /// Automatically updated when titles change.
    /// </summary>
    public struct HighestTitle : IComponentData
    {
        public TitleTier Tier;
        public TitleType Type;
        public TitleLevel Level;
        public TitleState State;                  // Should always be Active for highest title
        public FixedString64Bytes DisplayName;
    }

    /// <summary>
    /// Component that tracks the highest level FORMER title (if any).
    /// Former titles carry prestige but are shadows of proper titles.
    /// </summary>
    public struct HighestFormerTitle : IComponentData
    {
        public TitleTier Tier;
        public TitleType Type;
        public TitleLevel Level;
        public TitleState State;                  // Lost, Usurped, Disinherited, Revoked, or Former
        public FixedString64Bytes DisplayName;
        public FixedString64Bytes LossReason;
    }

    public struct LineageId : IComponentData
    {
        public FixedString64Bytes Id;
    }

    public enum ContractType : byte
    {
        Fleet = 0,
        Manufacturer = 1,
        MercenaryGuild = 2,
        Corporation = 3
    }

    public struct Contract : IComponentData
    {
        public ContractType Type;
        public FixedString64Bytes EmployerId;
        public uint ExpirationTick;
    }

    // Runtime components for entity relations
    [InternalBufferCapacity(3)]
    public struct LoyaltyScore : IBufferElementData
    {
        public AffiliationType TargetType;
        public FixedString64Bytes TargetId;
        public half Loyalty; // 0-1
    }

    [InternalBufferCapacity(5)]
    public struct OwnershipStake : IBufferElementData
    {
        public FixedString64Bytes AssetType; // Facility, Manufacturer, etc.
        public FixedString64Bytes AssetId;
        public float OwnershipPercentage; // 0-1
    }

    public struct Mentor : IComponentData
    {
        public FixedString64Bytes MentorId;
    }

    [InternalBufferCapacity(5)]
    public struct Mentee : IBufferElementData
    {
        public FixedString64Bytes MenteeId;
    }

    [InternalBufferCapacity(5)]
    public struct PatronageMembership : IBufferElementData
    {
        public AffiliationType AggregateType;
        public FixedString64Bytes AggregateId;
        public FixedString64Bytes Role;
    }

    public enum SuccessorType : byte
    {
        Heir = 0,
        Protege = 1
    }

    [InternalBufferCapacity(3)]
    public struct Successor : IBufferElementData
    {
        public FixedString64Bytes SuccessorId;
        public float InheritancePercentage; // 0-1, e.g., 0.5 = 50% of expertise
        public SuccessorType Type;
    }

    // Runtime components for augmentation system
    public struct SentientAnatomy : IComponentData
    {
        public FixedString64Bytes SpeciesId;
    }

    [InternalBufferCapacity(8)]
    public struct InstalledAugmentation : IBufferElementData
    {
        public FixedString64Bytes SlotId;
        public FixedString64Bytes AugmentId;
        public float Quality; // 0-1
        public byte Tier; // 0-255
        public ModuleRarity Rarity;
        public FixedString64Bytes ManufacturerId;
        public uint StatusFlags;
    }

    public struct AugmentationStats : IComponentData
    {
        public float PhysiqueModifier;
        public float FinesseModifier;
        public float WillModifier;
        public float GeneralModifier;
        public float TotalUpkeepCost;
        public float AggregatedRiskFactor; // 0-1
    }

    public enum InstallerType : byte
    {
        Doc = 0,      // Licensed medtech
        Ripper = 1    // Illicit surgeon
    }

    public enum LegalStatus : byte
    {
        Licensed = 0,
        Rogue = 1,
        BlackMarket = 2
    }

    [InternalBufferCapacity(8)]
    public struct AugmentationContract : IBufferElementData
    {
        public FixedString64Bytes AugmentId;
        public InstallerType InstallerType;
        public FixedString64Bytes InstallerId;
        public uint WarrantyDurationTicks;
        public LegalStatus LegalStatus;
    }

    public struct TechTier : IComponentData
    {
        public byte Tier;
    }

    public struct RequiredTechTier : IComponentData
    {
        public byte Tier; // Minimum tech tier required
    }

    // AI Command & Order Components
    public enum AIOrderType : byte
    {
        None = 0,
        Mine = 1,
        Haul = 2,
        Explore = 3,
        Patrol = 4,
        Engage = 5,
        Escort = 6,
        Construct = 7,
        Repair = 8,
        Retreat = 9,
        Dock = 10
    }

    public enum AIOrderStatus : byte
    {
        Pending = 0,
        PreFlightCheck = 1,
        ThreatEvaluation = 2,
        Executing = 3,
        Completed = 4,
        Failed = 5,
        Cancelled = 6,
        Escalated = 7
    }

    public enum VesselStanceMode : byte
    {
        Balanced = 0,
        Defensive = 1,
        Aggressive = 2,
        Evasive = 3,
        Neutral = Balanced
    }

    [InternalBufferCapacity(8)]
    public struct AIOrder : IBufferElementData
    {
        public AIOrderType Type;
        public AIOrderStatus Status;
        public Entity TargetEntity;
        public float3 TargetPosition;
        public Entity IssuerEntity; // Player, faction, empire that issued the order
        public uint IssueTick;
        public uint ExpirationTick;
        public byte Priority; // 0-255, higher = more urgent
        public half ThreatTolerance; // 0-1, how much threat captain will accept
    }

    public struct AICommandQueue : IComponentData
    {
        public uint LastProcessedTick;
    }

    public struct PreFlightCheck : IComponentData
    {
        public half ProvisionsLevel; // 0-1
        public half CrewMorale; // 0-1
        public half HullIntegrity; // 0-1
        public byte CheckPassed; // 0 = failed, 1 = passed
        public uint CheckTick;
    }

    public struct ThreatAssessment : IComponentData
    {
        public half LocalThreatLevel; // 0-1
        public half RouteThreatLevel; // 0-1
        public half DefensiveCapability; // 0-1
        public byte CanProceed; // 0 = unsafe, 1 = safe
        public uint AssessmentTick;
    }

    public struct VesselStanceComponent : IComponentData
    {
        // Backing bytes keep the component trivially blittable for ComponentLookup/RefRW.
        public VesselStanceMode CurrentStance
        {
            readonly get => (VesselStanceMode)_currentStance;
            set => _currentStance = (byte)value;
        }

        public VesselStanceMode DesiredStance
        {
            readonly get => (VesselStanceMode)_desiredStance;
            set => _desiredStance = (byte)value;
        }

        public uint StanceChangeTick;

        private byte _currentStance;
        private byte _desiredStance;
    }

    public struct FormationData : IComponentData
    {
        public half FormationTightness; // 0-1, based on alignment/outlook
        public float FormationRadius;
        public Entity FormationLeader; // Null if this is the leader
        public uint FormationUpdateTick;
    }

    public struct StrikeCraftState : IComponentData
    {
        public enum State : byte
        {
            Docked = 0,
            FormingUp = 1,
            Approaching = 2,
            Engaging = 3,
            Disengaging = 4,
            Returning = 5
        }

        public State CurrentState;
        public Entity TargetEntity;
        public float3 TargetPosition;
        public float Experience; // 0-1, affects behavior quality
        public uint StateStartTick;
        public byte KamikazeActive;
        public uint KamikazeStartTick;
        public StrikeCraftDogfightPhase DogfightPhase;
        public uint DogfightPhaseStartTick;
        public uint DogfightLastFireTick;
        public Entity DogfightWingLeader;
    }

    public struct ChildVesselTether : IComponentData
    {
        public Entity ParentCarrier;
        public float MaxTetherRange;
        public byte CanPatrol; // 0 = must stay tethered, 1 = can patrol independently
    }

    public enum ThreatProfileType : byte
    {
        Pirate = 0,
        SpaceFauna = 1,
        RivalEmpire = 2,
        Environmental = 3,
        Unknown = 4
    }

    public struct ThreatProfile : IComponentData
    {
        public ThreatProfileType Type;
        public AlignmentTriplet Alignment;
        public half AggressionLevel; // 0-1
        public half RiskRewardRatio; // 0-1, for pirates
        public byte CanNegotiate; // 0 = no diplomacy, 1 = can negotiate
        public Entity TargetEntity; // Current target for engagement
        public uint LastEngagementTick; // Last successful engagement tick
        public uint EngagementCount; // Number of engagements
        public uint DefeatCount; // Number of defeats (for relocation logic)
    }

    // Profile structures for aggregate composition
    public struct OutlookProfile
    {
        public FixedString32Bytes Id;
        public float Aggression;
        public float TradeBias;
        public float Diplomacy;
        public float DoctrineMissile;
        public float DoctrineLaser;
        public float DoctrineHangar;
        public float FieldRefitMult;
    }

    public struct AlignmentProfile
    {
        public FixedString32Bytes Id;
        public float Ethics;
        public float Order;
        public float CollateralLimit;
        public float PiracyTolerance;
        public float DiplomacyBias;
    }

    public struct PersonalityArchetype
    {
        public FixedString32Bytes Id;
        public float Risk;
        public float Opportunism;
        public float Caution;
        public float Zeal;
        public float CooldownMult;
    }

    public struct ThemeProfile
    {
        public FixedString32Bytes Id;
        public byte Palette;
        public byte Emblem;
        public byte Pattern;
    }

    public struct AggregateTemplate
    {
        public FixedString32Bytes Id;
        public byte HullLightPct;    // 0..100
        public byte HullCarrierPct;  // 0..100
        public byte HullHeavyPct;    // 0..100 (sum should be 100)
        public float TechFloor;
        public float TechCap;
        public float CrewGradeMean;
        public float LogisticsTolerance;
    }

    // Composed aggregate spec (runtime product)
    public struct ComposedAggregateSpec
    {
        public uint AggregateId32; // Hash(Template, Outlook, Alignment, Personality, Theme)
        public FixedString32Bytes TemplateId;
        public FixedString32Bytes OutlookId;
        public FixedString32Bytes AlignmentId;
        public FixedString32Bytes PersonalityId;
        public FixedString32Bytes ThemeId;
        
        // Resolved policy fields
        public float Aggression;
        public float TradeBias;
        public float Diplomacy;
        public float DoctrineMissile;
        public float DoctrineLaser;
        public float DoctrineHangar;
        public float FieldRefitMult;
        public float Ethics;
        public float Order;
        public float CollateralLimit;
        public float PiracyTolerance;
        public float DiplomacyBias;
        public float Risk;
        public float Opportunism;
        public float Caution;
        public float Zeal;
        public float CooldownMult;
        public float TechFloor;
        public float TechCap;
        public float CrewGradeMean;
        public float LogisticsTolerance;
        public StyleTokens StyleTokens;
    }

    // Profile catalog blobs
    public struct OutlookProfileCatalogBlob
    {
        public BlobArray<OutlookProfile> Profiles;
    }

    public struct AlignmentProfileCatalogBlob
    {
        public BlobArray<AlignmentProfile> Profiles;
    }

    public struct PersonalityArchetypeCatalogBlob
    {
        public BlobArray<PersonalityArchetype> Archetypes;
    }

    public struct ThemeProfileCatalogBlob
    {
        public BlobArray<ThemeProfile> Profiles;
    }

    public struct AggregateTemplateCatalogBlob
    {
        public BlobArray<AggregateTemplate> Templates;
    }

    public struct AggregateComboTableBlob
    {
        public BlobArray<ComposedAggregateSpec> Combos;
    }

    // Profile catalog singletons
    public struct OutlookProfileCatalogSingleton : IComponentData
    {
        public BlobAssetReference<OutlookProfileCatalogBlob> Catalog;
    }

    public struct AlignmentProfileCatalogSingleton : IComponentData
    {
        public BlobAssetReference<AlignmentProfileCatalogBlob> Catalog;
    }

    public struct PersonalityArchetypeCatalogSingleton : IComponentData
    {
        public BlobAssetReference<PersonalityArchetypeCatalogBlob> Catalog;
    }

    public struct ThemeProfileCatalogSingleton : IComponentData
    {
        public BlobAssetReference<ThemeProfileCatalogBlob> Catalog;
    }

    public struct AggregateTemplateCatalogSingleton : IComponentData
    {
        public BlobAssetReference<AggregateTemplateCatalogBlob> Catalog;
    }

    public struct AggregateComboTableSingleton : IComponentData
    {
        public BlobAssetReference<AggregateComboTableBlob> Table;
    }
}

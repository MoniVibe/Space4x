# Prefab Maker Requirements Assessment

This document assesses what the Space4X Prefab Maker needs to generate prefabs according to the game vision, based on analysis of vision documents, mechanics, catalog schemas, and existing implementation.

## Step 1: Source Vision Collection

### Vision Documents Reviewed

#### Game Vision (`Docs/Conceptualization/GameVision.md`)
- **Core Concept**: Carrier-first 4X where players command through orders rather than avatars
- **Key Entities**: Carriers, stations, colonies, fleets, modules, hulls, resources, products, aggregates, effects
- **Modular Architecture**: Capital ships feature Battletech-style variants with modular sections, hulls, hardpoints, and modules
- **Starting Modes**: Highly configurable, shifting available carriers, facility tiers, and political standing
- **Scale Target**: Approximately one million active entities

#### Carrier Customization (`Docs/Conceptualization/Mechanics/CarrierCustomization.md`)
- **Variants**: Common, Uncommon, Heroic, Prototype variants with unique names and built-in systems
- **Modular Architecture**:
  - Sections: Forward, mid, aft, and spine segments configurable independently
  - Hulls: Swappable plating influences durability, mass, and signatures
  - Hardpoints: Weapon/utility slots by size (S/M/L); variant defaults can be overridden
  - Modules: Hangars, warp cores, manufacturing bays, habitat pods, labs
- **Service Traits**: Carriers accrue service history, unlocking traits
- **Refit Permissions**: Captains propose refits; approval depends on ownership

#### Construction Loop (`Docs/Conceptualization/Mechanics/ConstructionLoop.md`)
- **Facility Tiers**: Small, Medium, Large, Massive, Titanic
- **Mounting Rules**: Small–Massive on carriers/stations; Titanic only on megastructures
- **Colony Support**: Development level dictates facility tier caps
- **Processing**: Mined resources → facilities → refined goods → construction sites
- **Cargo Holds**: Track capacity per resource type with overflow/degradation

#### Facility Archetypes (`Docs/Conceptualization/Mechanics/FacilityArchetypes.md`)
- **Shared Archetypes**: Refinery, Fabricator, Bioprocessor, Research Lab, Logistics Hub, Habitat Module
- **Carrier-Focused**: Mobile Fabrication Bays, Expedition Labs
- **Station-Focused**: Orbital Drydocks, Trade Nexus modules, Defence Grid Control
- **Colony-Focused**: Terraforming Plants, Civic Works, Cultural Archives
- **Titan-Focused**: Titan Forge, Stellar Manipulators, Supercarrier Hangars
- **Manufacturer Specialization**: Multiple manufacturers deliver same archetype with divergent tuning

#### Entity Hierarchy (`Docs/Conceptualization/Mechanics/EntityHierarchy.md`)
- **Ownership Layers**: Individual → Crew/Household → Fleet/Ship → Colony → Faction → Empire
- **Asset Ownership**: Deterministic links between entities and registry entries
- **Allegiance & Splintering**: Entities can declare independence, forming factions/empires
- **Data Representation**: Dynamic buffers for allegiances per entity (colony, faction, empire, guild, company, fleet, ship)

#### Mod Support (`Docs/Conceptualization/Mechanics/ModSupport.md`)
- **Data Packs**: Editing resources, tech trees, facilities, missions via configuration files
- **Custom Missions/Events**: Scripting hooks for dynamic events, situations, contract chains
- **Factions & Cultures**: New outlook mixtures, signature tech, starting conditions
- **Assets & Prefabs**: Unity authoring for custom ships, megastructures, HUD layouts
- **Tooling Roadmap**: Phase 1 (data-driven mod packs), Phase 2 (mission/event scripting), Phase 3 (Unity tooling integration, prefab export)

### Prefab Pipeline Documents Reviewed

#### PrefabMaker Godgame Patterns (`Docs/PrefabMaker_GodgamePatterns.md`)
- **Patterns Adopted**:
  - PlaceholderPrefabUtility for creating placeholder visuals
  - Visuals as child GameObjects named "Visual"
  - Primitive meshes with appropriate scales per prefab type
  - Clean authoring component structure
  - Folder structure consistency (`Assets/Prefabs/Space4X/`)
- **Space4X-Specific**:
  - Catalog-driven generation (reads from `HullCatalogAuthoring`/`ModuleCatalogAuthoring`)
  - Socket generation from catalog slot data
  - Binding JSON/blob from prefabs for runtime spawning

#### Prefab Checklist (`PureDOTS/Docs/TODO/Space4X_PrefabChecklist.md`)
- **Prefab Types**: Systems, Vessels, Carriers, Asteroids, Colonies, Fleets
- **Visual Representation**: Carriers/vessels/asteroids use `PlaceholderVisualAuthoring`; registry entities are data-only
- **Bulk Authoring**: Many entities created via bulk authoring components
- **Component Requirements**: ID components, mount requirements, style tokens, category tags

### Catalog Schema Analysis

#### ModuleDataSchemas (`Assets/Scripts/Space4x/Registry/ModuleDataSchemas.cs`)
- **Enums**: `MountType` (Core, Engine, Weapon, Defense, Utility, Hangar), `MountSize` (S, M, L), `ModuleClass`, `HullCategory`, `ModuleFunction`
- **ModuleSpec**: ID, class, mount requirements, mass, power, ratings, function metadata
- **HullSpec**: ID, base mass, field refit allowed, slots array, category, hangar capacity, presentation archetype, style tokens
- **StationSpec**: ID, refit facility flag, facility zone radius, presentation archetype, style tokens
- **Additional Catalogs**: ResourceSpec, ProductSpec, RecipeSpec, AggregateSpec, TechSpec, EffectSpec
- **Runtime Components**: `HullId`, `ModuleId`, `StationId`, `MountRequirement`, `StyleTokens`, `HullSocketTag`, `HangarCapacity`, `ModuleFunctionData`

#### Authoring Components (`Assets/Scripts/Space4x/Authoring/`)
- **ID Components**: `HullIdAuthoring`, `ModuleIdAuthoring`, `StationIdAuthoring`, `ResourceIdAuthoring`, `ProductIdAuthoring`, `AggregateIdAuthoring`, `EffectIdAuthoring`
- **Mount/Socket**: `MountRequirementAuthoring`, `HullSocketAuthoring`
- **Category Tags**: `CapitalShipAuthoring`, `CarrierAuthoring`
- **Functionality**: `ModuleFunctionAuthoring`, `HangarCapacityAuthoring`, `StyleTokensAuthoring`
- **Catalogs**: `HullCatalogAuthoring`, `ModuleCatalogAuthoring`, `StationCatalogAuthoring`, `ResourceCatalogAuthoring`, `ProductCatalogAuthoring`, `AggregateCatalogAuthoring`, `EffectCatalogAuthoring`

## Step 2: Vision → Prefab Generator Responsibilities

### Entity Coverage Requirements

#### 1. Hull Prefabs
**Generator**: `HullGenerator` (exists)
**Responsibilities**:
- Generate prefabs for all hulls in `HullCatalogAuthoring`
- Organize by category: `Hulls/`, `CapitalShips/`, `Carriers/`, `Stations/`
- Add `HullIdAuthoring` with catalog ID
- Add `HullSocketAuthoring` with `autoCreateFromCatalog = true`
- Create socket child transforms from catalog slot data (naming: `Socket_{MountType}_{MountSize}_{Index:D2}`)
- Add category-specific components (`CapitalShipAuthoring`, `CarrierAuthoring`)
- Add `HangarCapacityAuthoring` if `hangarCapacity > 0`
- Add `StyleTokensAuthoring` from catalog defaults
- Add placeholder visual (Capsule for hulls/capital ships, Cube for carriers, Cylinder for stations)

**Vision Alignment**:
- Supports modular architecture (sockets for module attachment)
- Enables variant system (category-based organization)
- Facilitates refit system (sockets define attachment points)

#### 2. Module Prefabs
**Generator**: `ModuleGenerator` (exists)
**Responsibilities**:
- Generate prefabs for all modules in `ModuleCatalogAuthoring`
- Add `ModuleIdAuthoring` with catalog ID
- Add `MountRequirementAuthoring` (type + size from catalog)
- Add `ModuleFunctionAuthoring` if function != None (function, capacity, description)
- Add placeholder visual (Cube, scaled by mount size)

**Vision Alignment**:
- Supports modular customization (modules attach to hull sockets)
- Enables facility archetypes (function defines module purpose)
- Facilitates manufacturer specialization (module class/ratings from catalog)

#### 3. Station Prefabs
**Generator**: `StationGenerator` (exists)
**Responsibilities**:
- Generate prefabs for all stations in `StationCatalogAuthoring`
- Add `StationIdAuthoring` with refit facility flags and zone radius
- Add `StyleTokensAuthoring` from catalog defaults
- Add placeholder visual (Cylinder)

**Vision Alignment**:
- Supports construction loop (stations host facilities)
- Enables refit system (refit facility zones)
- Facilitates facility archetypes (station-specific modules)

#### 4. Resource Prefabs
**Generator**: `ResourceGenerator` (exists)
**Responsibilities**:
- Generate prefabs for all resources in `ResourceCatalogAuthoring`
- Add `ResourceIdAuthoring` with catalog ID
- Add `StyleTokensAuthoring` from catalog defaults
- Add placeholder visual (Sphere, small scale)

**Vision Alignment**:
- Supports mining/haul loops (resources are extracted and transported)
- Enables modding (resources are data-driven)

#### 5. Product Prefabs
**Generator**: `ProductGenerator` (exists)
**Responsibilities**:
- Generate prefabs for all products in `ProductCatalogAuthoring`
- Add `ProductIdAuthoring` with catalog ID
- Add `RequiredTechTier` component if tech tier specified
- Add `StyleTokensAuthoring` from catalog defaults
- Add placeholder visual (Sphere, small scale)

**Vision Alignment**:
- Supports construction loop (products are construction outputs)
- Enables tech progression (tech tier gating)

#### 6. Aggregate Prefabs
**Generator**: `AggregateGenerator` (exists)
**Responsibilities**:
- Generate prefabs for all aggregates in `AggregateCatalogAuthoring`
- Add `AggregateIdAuthoring` with catalog ID
- Add `AggregateTags` component (alignment, outlook, policy)
- Add `StyleTokensAuthoring` from catalog defaults
- Add placeholder visual (Quad)

**Vision Alignment**:
- Supports entity hierarchy (aggregates represent factions/empires)
- Enables alignment/compliance systems (tags drive behavior)

#### 7. Effect Prefabs
**Generator**: `EffectGenerator` (exists)
**Responsibilities**:
- Generate prefabs for all effects in `EffectCatalogAuthoring`
- Add `EffectIdAuthoring` with catalog ID
- Add `StyleTokensAuthoring` from catalog defaults
- Add placeholder visual (Quad)

**Vision Alignment**:
- Supports VFX/visual effects system
- Enables modding (effects are data-driven)

### Catalog Input Requirements

#### Required Catalog Prefabs
The generator must locate and load:
- `{CatalogPath}/HullCatalog.prefab` → `HullCatalogAuthoring`
- `{CatalogPath}/ModuleCatalog.prefab` → `ModuleCatalogAuthoring`
- `{CatalogPath}/StationCatalog.prefab` → `StationCatalogAuthoring`
- `{CatalogPath}/ResourceCatalog.prefab` → `ResourceCatalogAuthoring`
- `{CatalogPath}/ProductCatalog.prefab` → `ProductCatalogAuthoring`
- `{CatalogPath}/AggregateCatalog.prefab` → `AggregateCatalogAuthoring`
- `{CatalogPath}/EffectCatalog.prefab` → `EffectCatalogAuthoring`

#### Catalog Data Mapping
- **Hull Catalog**: `id`, `category`, `slots[]`, `hangarCapacity`, `defaultPalette/roughness/pattern`, `presentationArchetype`
- **Module Catalog**: `id`, `requiredMount`, `requiredSize`, `function`, `functionCapacity`, `functionDescription`
- **Station Catalog**: `id`, `hasRefitFacility`, `facilityZoneRadius`, `defaultPalette/roughness/pattern`
- **Resource/Product/Aggregate/Effect Catalogs**: `id`, `presentationArchetype`, `defaultPalette/roughness/pattern`

### Output Requirements

#### Prefab Directory Structure
```
Assets/Prefabs/Space4X/
├── Hulls/              # Generic hulls (HullCategory.Other)
├── CapitalShips/       # HullCategory.CapitalShip
├── Carriers/           # HullCategory.Carrier
├── Stations/           # HullCategory.Station + StationCatalog
├── Modules/            # All modules
├── Resources/          # Resource entities
├── Products/           # Product entities
├── Aggregates/         # Aggregate entities
├── FX/                 # Effect entities
├── Weapons/            # Weapon presentation tokens (optional, visual-only)
├── Projectiles/        # Projectile presentation tokens (optional, visual-only)
└── Turrets/            # Turret presentation tokens (optional, visual-only)
```

#### Binding Output
- Generate `Assets/Space4X/Bindings/Space4XPresentationBinding.json` (and `.asset` ScriptableObject)
- Maps catalog IDs → prefab paths for runtime spawning
- Categories: Hulls, Modules, Stations, Resources, Products, Aggregates, FX, Individuals, Weapons, Projectiles, Turrets
- Supports Minimal/Fancy binding sets (switchable at runtime)

### Authoring Component Requirements

#### Core Components (All Prefabs)
- Appropriate `*IdAuthoring` component (HullId, ModuleId, StationId, etc.)
- `StyleTokensAuthoring` (if catalog provides defaults)
- Placeholder visual child GameObject (if `PlaceholdersOnly` option)

#### Hull-Specific Components
- `HullIdAuthoring` (required)
- `HullSocketAuthoring` (required, `autoCreateFromCatalog = true`)
- `CapitalShipAuthoring` OR `CarrierAuthoring` (based on category)
- `HangarCapacityAuthoring` (if `hangarCapacity > 0`)
- Socket child transforms (from catalog slots)

#### Module-Specific Components
- `ModuleIdAuthoring` (required)
- `MountRequirementAuthoring` (required, from catalog mount type/size)
- `ModuleFunctionAuthoring` (if function != None)

#### Station-Specific Components
- `StationIdAuthoring` (required, with refit facility flags)

### Validation Requirements

#### Catalog Consistency
- All catalog entries must have corresponding prefabs
- Prefab IDs must match catalog IDs exactly
- Mount requirements must match catalog data

#### Socket Validation
- Hull prefabs must have socket transforms matching catalog slot counts
- Socket naming must follow pattern: `Socket_{MountType}_{MountSize}_{Index:D2}`
- Socket positions/rotations should be set (currently defaults to zero)

#### Component Validation
- Required authoring components must be present
- Category tags must match catalog category
- Function components must match catalog function data

## Step 3: Gaps & Enhancements

### Current Implementation Status

#### ✅ Implemented
- **HullGenerator**: Generates hull prefabs with sockets, category tags, style tokens
- **ModuleGenerator**: Generates module prefabs with mount requirements, function data
- **StationGenerator**: Generates station prefabs with refit facility flags
- **ResourceGenerator, ProductGenerator, AggregateGenerator, EffectGenerator**: Basic generators exist
- **PlaceholderPrefabUtility**: Creates child visual GameObjects with appropriate primitives
- **Binding Generation**: Creates JSON mapping catalog IDs → prefab paths
- **Validation Framework**: `Validate()` methods on generators, `ValidateAll()` in PrefabMaker

#### ⚠️ Partial Implementation
- **Socket Positioning**: Sockets are created but positions default to zero (no layout algorithm)
- **Binding Format**: JSON only; full blob asset pending
- **Validation UI**: Validation exists but no rich diagnostics panel (mentioned in Godgame patterns as future)

### Identified Gaps

#### 1. Alignment/Ownership Data in Prefabs
**Gap**: Vision requires ownership layers and alignment data (`AlignmentTriplet`, `RaceId`, `CultureId`, `DynamicBuffer<EthicAxisValue>`, `DynamicBuffer<StanceEntry>`, `DynamicBuffer<AffiliationTag>`)

**Current State**: No authoring components for alignment/ownership data in prefabs

**Enhancement Needed**:
- Add `AlignmentAuthoring` component for crews/fleets/colonies
- Add `OwnershipAuthoring` component for assets
- Add `AffiliationAuthoring` component for multi-layer allegiances
- Generate these components in prefabs based on catalog data or default values

**Priority**: High (Agent A alignment/compliance systems depend on this)

#### 2. Facility Archetype Support
**Gap**: Vision defines facility archetypes (Refinery, Fabricator, Foundry, Bioprocessor, Research Lab, Logistics Hub, Habitat Module, Titan Forge) with tier scaling (Small → Titanic)

**Current State**: `ModuleFunctionAuthoring` exists but doesn't capture archetype/tier relationship

**Enhancement Needed**:
- Extend `ModuleCatalogAuthoring` to include facility archetype and tier
- Add `FacilityArchetypeAuthoring` component to module prefabs
- Add `FacilityTierAuthoring` component (Small/Medium/Large/Massive/Titanic)
- Validate tier compatibility with host entity (carrier vs station vs colony vs titan)

**Priority**: Medium (construction loop depends on facility archetypes)

#### 3. Socket Layout Algorithm
**Gap**: Vision requires sockets positioned on hulls for visual module attachment

**Current State**: Sockets created but positions default to zero (no spatial layout)

**Enhancement Needed**:
- Implement socket layout algorithm (spherical/cylindrical distribution based on hull category)
- Allow manual socket positioning override in catalog
- Visualize socket positions in editor (gizmos/sphere markers)

**Priority**: Medium (visual polish, doesn't block gameplay)

#### 4. Variant System Support
**Gap**: Vision requires variant system (Common, Uncommon, Heroic, Prototype) with unique names and built-in systems

**Current State**: No variant metadata in catalogs or prefabs

**Enhancement Needed**:
- Extend `HullCatalogAuthoring` to support variants (variant type, unique name, built-in module IDs)
- Add `VariantAuthoring` component to hull prefabs
- Generate variant-specific prefabs or variant metadata
- Support variant-specific default module loadouts

**Priority**: Low (nice-to-have, can be added later)

#### 5. Service Traits Support
**Gap**: Vision requires carriers to accrue service history, unlocking traits

**Current State**: No trait system in prefabs

**Enhancement Needed**:
- Add `ServiceTraitsAuthoring` component (empty at generation, populated at runtime)
- Define trait catalog/authoring system
- Generate trait-ready prefabs

**Priority**: Low (runtime system, prefab generation not critical)

#### 6. Modding Hooks
**Gap**: Vision requires modding support for custom ships, megastructures, HUD layouts

**Current State**: Catalog-driven generation supports modding, but no explicit modding workflow

**Enhancement Needed**:
- Document modding workflow (create catalog prefab → run Prefab Maker → customize prefab)
- Add modding validation (check for mod conflicts, missing dependencies)
- Support mod-specific prefab folders (`Assets/Prefabs/Mods/{ModName}/`)

**Priority**: Medium (mod support is Phase 3 goal)

#### 7. Binding Blob Asset
**Gap**: Binding currently outputs JSON; vision may require blob asset for runtime performance

**Current State**: `GenerateBindingBlob()` creates JSON only

**Enhancement Needed**:
- Create `Space4XPresentationBinding` ScriptableObject asset
- Generate blob asset reference for runtime lookups
- Maintain JSON as fallback/editor convenience

**Priority**: Low (JSON works for now, blob asset optimization later)

#### 8. Validation UI Enhancement
**Gap**: Godgame patterns mention rich diagnostics panel with "why invalid" explanations

**Current State**: Validation exists but no UI; only console logs

**Enhancement Needed**:
- Create `PrefabMakerWindow` with validation panel
- Show validation issues with severity, prefab path, explanation
- Provide "fix" suggestions (e.g., "Add missing socket", "Update mount requirement")
- Highlight invalid prefabs in Project window

**Priority**: Medium (improves developer experience)

#### 9. Crew/Alignment Component Generation
**Gap**: Agent A requires alignment buffers in crew prefabs (`DynamicBuffer<EthicAxisValue>`, `DynamicBuffer<StanceEntry>`, `DynamicBuffer<AffiliationTag>`)

**Current State**: No crew prefab generator; crews are runtime entities

**Enhancement Needed**:
- Create `CrewGenerator` if crews need prefabs
- Add alignment authoring components to crew prefabs
- Generate default alignment values from catalog or templates

**Priority**: High (Agent A dependency)

#### 10. Facility Mounting Validation
**Gap**: Vision requires facility tier compatibility checks (Small–Massive on carriers/stations; Titanic only on megastructures)

**Current State**: No validation for facility tier vs host entity compatibility

**Enhancement Needed**:
- Add validation rule: module facility tier must be compatible with host hull category
- Generate warnings when incompatible modules are assigned to hulls
- Document tier compatibility matrix

**Priority**: Medium (prevents runtime errors)

### Additional Gaps Identified (Post-Initial Assessment)

#### 11. Individual Entity Prefabs (Captains, Officers, Crew)
**Gap**: Vision requires individual entity prefabs for captains, ace officers, and crew specialists with stats, traits, expertise, and progression data.

**Current State**: No individual entity prefab generator; crews are aggregated at runtime.

**Requirements from Docs**:
- **Individual Stats**: `Command`, `Tactics`, `Logistics`, `Diplomacy`, `Engineering`, `Resolve` (AceOfficerProgression)
- **Shared Stat Contract**: `Physique`, `Finesse`, `Will`, general XP pool with inclination modifiers (1-10) per stat
- **Expertise Vectors**: `CarrierCommand`, `Espionage`, `Logistics`, `Psionic`, `Beastmastery` (LineageAndAggregates)
- **Progression Stages**: Crew Specialist → Junior Officer → Ace Officer → Legend (AceOfficerProgression)
- **Service Traits**: Reactor Whisperer, Strike Wing Mentor, etc. (AceOfficerProgression)
- **Preordain Tracks**: Combat Ace, Logistics Maven, Diplomatic Envoy, Engineering Savant (AceOfficerProgression)
- **Titles & Ranks**: Culture-aware title ladder (Captain → Admiral → Governor → Stellar Lord → Stellarch) (AceOfficerProgression)
- **Lineage/Dynasty**: `LineageId`, dynasty membership, heir pools (LineageAndAggregates)
- **Contracts**: Service contracts (1-5 years) with fleets, manufacturers, mercenary guilds (AceOfficerProgression)

**Enhancement Needed**:
- Create `IndividualEntityGenerator` for captains/officers/crew specialists
- Add `IndividualStatsAuthoring` component (Command, Tactics, Logistics, Diplomacy, Engineering, Resolve)
- Add `PhysiqueFinesseWillAuthoring` component (Physique, Finesse, Will, inclination modifiers)
- Add `ExpertiseAuthoring` component (expertise vectors with type/tier)
- Add `ServiceTraitsAuthoring` component (trait list)
- Add `PreordainTrackAuthoring` component (career path focus)
- Add `TitleAuthoring` component (current title, rank tier)
- Add `LineageAuthoring` component (lineage ID, dynasty membership)
- Add `ContractAuthoring` component (contract type, expiry, employer)
- Generate prefabs in `Assets/Prefabs/Space4X/Individuals/` (Captains/, Officers/, Crew/)

**Priority**: High (core progression system, narrative hooks)

#### 12. Augmentation/Implant System
**Gap**: Vision requires augmentation/implants for individual entities with slot-based installation, quality/tier/rarity/manufacturer attributes.

**Current State**: No augmentation authoring components or prefab generation.

**Requirements from Docs**:
- **Augmentation Schema**: `SentientAnatomy` (species-defined limb/organ slots), `AugmentationInventory` (installed augments), `AugmentationStats` (aggregated modifiers), `AugmentationExperience` (usage XP), `AugmentationContracts` (installer provenance, warranty, legal status) (AmmunitionAndAugments)
- **Augment Attributes**: `SlotId`, `AugmentId`, `Quality`, `Tier`, `ManufacturerId`, `StatusFlags` (AmmunitionAndAugments)
- **Augment Types**: Combat augments (cybernetic musculature, recoil dampeners), Finesse augments (neural accelerators, sensor uplinks), Will augments (psionic amplifiers, synaptic shields), General augments (longevity, resilience) (AceOfficerProgression)
- **Installers**: Docs (licensed medtechs) vs Rippers (illicit surgeons) (AceOfficerProgression, AmmunitionAndAugments)
- **Species Compatibility**: Cross-species augmentation with compatibility tables (AmmunitionAndAugments)

**Enhancement Needed**:
- Create `AugmentationCatalogAuthoring` with augment definitions
- Add `AugmentationAuthoring` component to individual entity prefabs
- Add `SentientAnatomyAuthoring` component (species, limb/organ slots)
- Add `AugmentationInventoryAuthoring` component (installed augments buffer)
- Add `AugmentationStatsAuthoring` component (Physique/Finesse/Will modifiers)
- Generate augmentation prefabs in `Assets/Prefabs/Space4X/Augmentations/`
- Support quality/tier/rarity/manufacturer attributes in augmentation catalog

**Priority**: Medium (progression enhancement, not blocking)

#### 13. Module Quality/Rarity/Tier/Manufacturer Attributes
**Gap**: Vision requires module quality, rarity, tier, and manufacturer attributes that affect performance, availability, and legendary production runs.

**Current State**: `ModuleCatalogAuthoring` has ratings but no explicit quality/rarity/tier/manufacturer fields.

**Requirements from Docs**:
- **Quality**: Fine control over spread/dispersion, misfire risk, maintenance load (AmmunitionAndAugments)
- **Rarity**: Common, Uncommon, Heroic, Prototype - availability, black-market value, diplomatic leverage (CarrierCustomization, AmmunitionAndAugments)
- **Tier**: Drives baseline performance, reliability (AmmunitionAndAugments)
- **Manufacturer**: Signature traits (fire rate, ammo/fuel type, damage profile, mount size, energy footprint, maintenance costs) that scale with manufacturer experience; veteran shops unlock exclusive variants and legendary production runs (FacilityArchetypes, AmmunitionAndAugments)
- **Legendary Runs**: Require manufacturer XP milestones plus officer endorsements (AmmunitionAndAugments)

**Enhancement Needed**:
- Extend `ModuleCatalogAuthoring.ModuleSpecData` to include:
  - `quality` (float, 0-1 or enum)
  - `rarity` (enum: Common, Uncommon, Heroic, Prototype)
  - `tier` (byte or enum)
  - `manufacturerId` (string, references manufacturer catalog)
- Add `ModuleQualityAuthoring` component to module prefabs
- Add `ModuleRarityAuthoring` component to module prefabs
- Add `ModuleTierAuthoring` component to module prefabs
- Add `ModuleManufacturerAuthoring` component to module prefabs
- Create `ManufacturerCatalogAuthoring` for manufacturer definitions
- Generate manufacturer prefabs in `Assets/Prefabs/Space4X/Manufacturers/`
- Update `ModuleGenerator` to emit quality/rarity/tier/manufacturer components

**Priority**: High (core customization system, affects all modules)

#### 14. Weapon & Projectile Prefab Strategy
**Status**: ✅ Implemented

**Design Philosophy**: Weapons, projectiles, and turrets are **data-driven** (specs baked to blobs) with **optional thin presentation tokens** (visual-only prefabs). This preserves determinism, avoids asset debt, and keeps gameplay free of GameObject ties.

**What Should Be Data (Always)**:
- `WeaponSpec` and `ProjectileSpec` baked to blobs; systems read IDs + numbers
- `TurretSpec` for arc limits, traverse speed, elevation, recoil, socket name
- All specs authored in ScriptableObjects → baked to blobs. No GameObject references

**What Can Be Prefabs (Thin & Optional)**:
- Muzzle flash, beam line, tracer, impact FX, missile shell (visual only)
- Components: `WeaponIdAuthoring`/`ProjectileIdAuthoring`, `StyleTokensAuthoring`, optional sockets (`Socket_Muzzle`)
- Turret token (if you need a visible mount) with named sockets; no logic

**Prefab Maker: What to Generate**:
- **Specs & Blobs**: Bake `WeaponSpec`, `ProjectileSpec`, `TurretSpec` catalogs
- **Validation**: Cross-refs (every `WeaponSpec` points to a valid `ProjectileSpec`), sanity checks (FireRate ≥ 0, Lifetime ≥ 0, Speed ≥ 0, TurnRateDeg 0..720, AoERadius ≥ 0), behavior constraints (beam = Speed=0, ProjectileKind=BeamTick; missiles require TurnRateDeg>0), damage budget caps per class
- **Presentation Bindings (Minimal/Fancy sets)**: `WeaponId` → muzzle FX, SFX; `ProjectileId` → tracer/beam prefab + impact FX/SFX; `TurretSpec` → socket name ↔ muzzle binding
- **Thin Tokens (Optional)**: Generate placeholder prefabs for tracers/beam segments/impact puffs using Unity primitives

**Runtime Systems**:
- `FireSystem` (FixedStep): reads `WeaponSpec`, checks cooldown/energy/heat, computes lead, enqueues projectile entities with `ProjectileSpec` via ECB
- `ProjectileAdvanceSystem` (FixedStep): integrates ballistic motion or homing, performs hits, applies `EffectOp`s, decrements pierce/chain, queues impact FX requests
- `BeamSystem` (FixedStep): sample hitscan each tick; apply tick damage; fire beam FX requests
- Presentation systems: read request buffers → play muzzle/beam/impact visuals

**Tests to Land**:
- Idempotency: run generator twice → identical hashes (blobs + prefabs)
- Determinism: seeded firing at 30/60/120 FPS → same hit and damage totals
- Homing bounds: missiles never NaN; angle clamp respected
- Pierce/Chain invariants: no extra hits beyond limits
- Binding optionality: remove all presentation bindings → simulation still runs; only visuals disappear

**Priority**: High (core combat system)

#### 15. Aggregate Outlook/Alignment Composition
**Gap**: Vision requires aggregate entities (dynasties, guilds, corporations, armies) with composed outlook/alignment profiles, not just simple tags.

**Current State**: `AggregateGenerator` adds `AggregateTags` (alignment, outlook, policy bytes) but doesn't support composed profiles.

**Requirements from Docs**:
- **Composed Aggregate Spec**: `ComposedAggregateSpec` with `TemplateId`, `OutlookId`, `AlignmentId`, `PersonalityId`, `ThemeId` plus resolved policy fields (Aggression, TradeBias, Diplomacy, DoctrineMissile, DoctrineLaser, DoctrineHangar, FieldRefitMult, Ethics, Order, CollateralLimit, PiracyTolerance, DiplomacyBias, Risk, Opportunism, Caution, Zeal, CooldownMult, TechFloor, TechCap, CrewGradeMean, LogisticsTolerance) (ModuleDataSchemas)
- **Profile Catalogs**: `OutlookProfileCatalog`, `AlignmentProfileCatalog`, `PersonalityArchetypeCatalog`, `ThemeProfileCatalog`, `AggregateTemplateCatalog` (ModuleDataSchemas)
- **Aggregate Types**: Dynasty, Guild, Corporation, Army, Band (LineageAndAggregates)
- **Aggregate Members**: Individual entities and sub-aggregates (LineageAndAggregates)
- **Aggregate Assets**: Ships, stations, contracts (LineageAndAggregates)
- **Reputation/Prestige**: Aggregate-level reputation and prestige scores (LineageAndAggregates)

**Enhancement Needed**:
- Extend `AggregateCatalogAuthoring` to support composed aggregates:
  - `templateId`, `outlookId`, `alignmentId`, `personalityId`, `themeId` fields
  - Resolved policy fields (or reference to profile catalogs)
- Add `AggregateTypeAuthoring` component (Dynasty, Guild, Corporation, Army, Band)
- Add `AggregateMembersAuthoring` component (member entity IDs buffer)
- Add `AggregateAssetsAuthoring` component (owned ships/stations/contracts)
- Add `ReputationAuthoring` component (reputation/prestige scores)
- Create profile catalog generators for Outlook, Alignment, Personality, Theme, Template
- Update `AggregateGenerator` to generate composed aggregate prefabs with profile references

**Priority**: High (Agent A dependency, core social/political system)

#### 15. Individual Entity Relations
**Gap**: Vision requires individual entities to track relations (loyalty scores, contracts, ownership stakes, mentorship).

**Current State**: No relation tracking in individual entity prefabs.

**Requirements from Docs**:
- **Loyalty Scores**: `LoyaltyScores` (Empire, Lineage, Guild) (LineageAndAggregates)
- **Ownership Stakes**: Officers can own stakes in facilities/manufacturers (AceOfficerProgression)
- **Mentorship**: High-expertise individuals can train juniors (LineageAndAggregates)
- **Patronage Webs**: Individuals belong to one or more aggregates (LineageAndAggregates)
- **Succession**: Notable individuals have heirs or proteges who inherit partial expertise (LineageAndAggregates)

**Enhancement Needed**:
- Add `LoyaltyScoresAuthoring` component (empire, lineage, guild loyalty values)
- Add `OwnershipStakesAuthoring` component (facility/manufacturer stakes buffer)
- Add `MentorshipAuthoring` component (mentor/mentee relationships)
- Add `PatronageWebAuthoring` component (aggregate memberships buffer)
- Add `SuccessionAuthoring` component (heir/protege IDs, inheritance percentage)
- Update `IndividualEntityGenerator` to emit relation components

**Priority**: Medium (social system enhancement)

### Recommended Implementation Order (Updated)

1. **Phase 1 (Critical - Agent A/Progression)**:
   - Alignment/Ownership authoring components (Agent A dependency)
   - Aggregate outlook/alignment composition (Agent A dependency)
   - Module quality/rarity/tier/manufacturer attributes (core customization)
   - Individual entity prefabs (captains/officers/crew) with stats/traits/expertise

2. **Phase 2 (High Value)**:
   - Facility archetype/tier support (construction loop dependency)
   - Facility mounting validation (prevents errors)
   - Individual entity relations (loyalty, ownership, mentorship)
   - Binding blob asset (performance optimization)

3. **Phase 3 (Polish)**:
   - Augmentation/implant system (progression enhancement)
   - Socket layout algorithm (visual polish)
   - Validation UI enhancement (developer experience)
   - Variant system support (nice-to-have)

4. **Phase 4 (Future)**:
   - Service traits support (runtime system)
   - Modding hooks enhancement (Phase 3 mod support goal)

## Summary

The Prefab Maker currently implements core functionality for generating catalog-driven prefabs (hulls, modules, stations, resources, products, aggregates, effects) with appropriate authoring components, placeholder visuals, and binding output. 

**Major gaps identified**:
1. **Individual entity prefabs** (captains, officers, crew) with stats, traits, expertise, progression stages, titles, lineage, contracts
2. **Module quality/rarity/tier/manufacturer** attributes for customization and legendary production runs
3. **Aggregate outlook/alignment composition** with profile catalogs (Outlook, Alignment, Personality, Theme, Template)
4. **Augmentation/implant system** for individual entities with slot-based installation
5. **Individual entity relations** (loyalty, ownership, mentorship, patronage, succession)

The generator architecture is sound and extensible; these enhancements can be added incrementally. Priority should focus on individual entities and module quality attributes as they are core to the progression and customization systems.

---

## Stat System Audit & Runtime Usage Gap Analysis

**Date**: 2025-01-XX  
**Status**: In Progress - Sprint Implementation

### Current Stat Definitions

#### IndividualStats (ModuleDataSchemas.cs:585-593)
- **Command**: Used only in `Space4XFleetCoordinationAISystem` for admiral selection (line 127). No other systems read this stat.
- **Tactics**: Defined but never read by any system. Should influence targeting accuracy, engagement timing, special ability cooldowns.
- **Logistics**: Defined but never read. Should affect cargo transfer speed, vessel speeds, dock/hangar throughput.
- **Diplomacy**: Defined but never read. Should influence interception/stance decisions, agreement success rates, relation modifiers.
- **Engineering**: Defined but never read. Should affect repair/refit speeds, costs, jam chance reduction, system complexity.
- **Resolve**: Defined but never read. Should adjust morale thresholds, recall thresholds, action speed, risk tolerance.

#### PhysiqueFinesseWill (ModuleDataSchemas.cs:595-604)
- **Physique**: Defined with authoring component but never read. Should affect strike craft performance, boarding strength, hull plating.
- **Finesse**: Defined but never read. Should affect accuracy, maneuver precision, tactical awareness, targeting.
- **Will**: Defined but never read. Should affect psionic abilities, energy regen, aura potency, education gains.
- **Inclinations**: Defined (1-10 scale) but never used. Should scale XP gain and skill costs.
- **GeneralXP**: Defined but never accumulated or spent. Should feed cross-discipline abilities.

#### ExpertiseEntry Buffer (ModuleDataSchemas.cs:616-621)
- **ExpertiseType**: CarrierCommand, Espionage, Logistics, Psionic, Beastmastery defined but no systems query this buffer.
- **Tier**: 0-255 tier system defined but unused.

#### ServiceTrait Buffer (ModuleDataSchemas.cs:633-637)
- **ServiceTraitId**: ReactorWhisperer, StrikeWingMentor, TacticalSavant, LogisticsMaestro, PirateBane defined but no systems read traits.

#### PreordainProfile (ModuleDataSchemas.cs:648-651)
- **PreordainTrack**: CombatAce, LogisticsMaven, DiplomaticEnvoy, EngineeringSavant defined but never used to guide career paths.

### Required Stat Usage (Per Plan Goals)

#### Command
- **Current**: Admiral selection only
- **Required**: Max pilots/crews, morale bonuses, command point replenishment (mana-like for special abilities)
- **Systems to Update**: `Space4XFleetCoordinationAISystem` (expand), `Space4XStrikeCraftBehaviorSystem` (max craft), morale systems (TBD)

#### Tactics
- **Current**: Unused
- **Required**: Special ability count/cooldowns, targeting accuracy, engagement timing
- **Systems to Update**: `Space4XStrikeCraftBehaviorSystem`, `VesselTargetingSystem`, combat systems (TBD)

#### Logistics
- **Current**: Unused
- **Required**: Cargo transfer speed, utility vessel speeds, dock/hangar throughput
- **Systems to Update**: `VesselDepositSystem`, `VesselGatheringSystem`, carrier systems

#### Diplomacy
- **Current**: Unused
- **Required**: Agreement success rates, relation modifiers, interception/stance decisions
- **Systems to Update**: `Space4XAIDiplomacySystem`, `Space4XFleetCoordinationAISystem`, interception systems

#### Engineering
- **Current**: Unused
- **Required**: Repair/refit speeds, costs, jam chance reduction, system complexity reduction
- **Systems to Update**: `Space4XFieldRepairSystem`, `Space4XCarrierModuleRefitSystem`, module systems

#### Resolve
- **Current**: Unused
- **Required**: Morale thresholds, recall thresholds, action speed, risk tolerance
- **Systems to Update**: `Space4XStrikeCraftBehaviorSystem` (disengage thresholds), `Space4XThreatBehaviorSystem`, morale systems (TBD)

#### Physique/Finesse/Will
- **Current**: Unused
- **Required**: Strike craft performance, crew task efficiency, psionic abilities
- **Systems to Update**: `Space4XStrikeCraftBehaviorSystem`, crew aggregation systems, psionic systems (TBD)

### Data Flow Gaps

1. **No Stat Aggregation**: Individual entities with stats are not aggregated to vessel/carrier level for system queries.
2. **No Stat Progression**: XP pools exist but no systems accumulate or spend XP to increase stats.
3. **No Trait Application**: Service traits defined but never applied as modifiers to stat checks.
4. **No Expertise Queries**: Expertise buffer exists but no systems check expertise before allowing actions.
5. **No Preordain Guidance**: Preordain tracks defined but no systems use them to guide career progression.

### Implementation Priority

1. **High**: Wire IndividualStats into existing systems (Command, Tactics, Logistics, Engineering, Resolve, Diplomacy)
2. **High**: Wire Physique/Finesse/Will into strike craft and crew systems
3. **Medium**: Add stat aggregation for vessel-level queries
4. **Medium**: Create authoring components for ExpertiseEntry and ServiceTrait buffers
5. **Low**: Implement XP accumulation/spending (future expansion)
6. **Low**: Implement preordain track guidance (future expansion)



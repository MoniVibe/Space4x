# Production Chain Concepts

## Overview
- Provide a shared data-driven production pipeline spanning raw extraction, processing tiers, intermediate materials, and manufactured goods (ships, wagons, carriers).
- Support both Godgame and Space4x by abstracting resource categories, recipes, and logistics triggers.
- Tie into existing registries (Resource, Storehouse, LogisticsRequest) while remaining extensible for new services (economy, trade, tech).

## Data Model
- `ResourceTypeRegistry`: classifies resources into categories:
  - Extraction (ores, timber, fish, crops, livestock, stone, rare herbs)
  - Harvestables/Lootables (fallen loot, raid spoils, relics)
  - Processed Materials (ingots, planks, cloth, chemicals, alloys, food rations)
  - Components (hulls, rigging, wheels, weapon fittings, magical cores)
  - Final Products (ships, wagons, siege engines, carriers, trade goods)
- `ProductionRecipe` blob:
  - Inputs: `ResourceRef`, quantity, quality tier requirement
  - Outputs: `ResourceRef`, quantity, optional byproducts
  - Time cost, workforce requirement (`SkillTag`, `WorkCrewSize`)
  - Facility requirement (building type, service flags, environment)
  - Tech/culture requirements from shared services
- `ProductionChainDescriptor`:
  - Graph linking multiple `ProductionRecipe` nodes; supports alternate branches (e.g., timber -> plank -> wagon body; ore -> ingot -> armor plate).
  - Metadata for trade value, logistics priority, moral/faith modifiers.

## Systems
- `ExtractionSystemGroup`:
  - Handles raw gathering (mines, farms, fishing docks, hunting).
  - Emits `ResourceIncrement` events to the resource registry.
- `ProcessingSystemGroup`:
  - Runs recipe jobs in parallel, consuming inputs from storehouses, applying time/workforce costs, and producing intermediates.
  - Integrates with `VillagerJobSystems` for worker assignment and `Education service` for skill gating.
- `ManufacturingSystemGroup`:
  - Assembles intermediate components into final products (ships, wagons, carriers) and registers with `ConstructionRegistry` or `TransportRegistry`.
  - Supports queueing via `ProductionOrderBuffer` (requested by economy, trade, or military services).
- `LootIntegrationSystem`:
  - Converts battle loot or event rewards into resource entries/progression triggers; handles salvage recipes (break down ships into components).
- `LogisticsIntegrationSystem`:
  - Reads production orders, schedules delivery routes, and updates `LogisticsRequestRegistry`.

## Authoring
- ScriptableObject catalogs:
  - `ResourceCatalog` for defining categories, stack limits, spoilage rules.
  - `ProductionRecipeCatalog` grouped by facility type and tech tier.
  - `ProductionChainCatalog` describing canonical chains per faction/biome.
- Bakers translate catalogs into blob assets consumed by production systems.

## Services Integration
- `Economy/Trade`: adjusts production priorities, applies price multipliers, manages market demand.
- `Tech/Culture`: unlocks recipes, improves yields, introduces new chain branches.
- `Population Traits`: modifies worker efficiency, determines available skills.
- `Military`: requests equipment (weapons, armor, siege engines) and consumes outputs.
- `Narrative Situations`: trigger special recipes (festival goods, relief supplies, elite regalia) with unique effects.

## Analytics & Telemetry
- Track throughput per chain node (inputs/outputs per tick).
- Monitor workforce utilization, bottlenecks, spoilage.
- Emit events for chain completion (ship launched, convoy assembled) to analytics and narrative systems.

## Implementation Notes
- **Data Layout**: store production state in SoA buffers (separate arrays for timers, input counts, worker slots); use AoSoA batching for high-frequency recipes to improve Burst vectorization.
- **Registries**: mirror resource outputs in dedicated registries (`ProductionOrderRegistry`, `ManufacturedGoodsRegistry`) following the deterministic builder contract.
- **Authoring & Baking**: bakers translate catalog ScriptableObjects into blob assets; ensure conversion hooks validate recipe dependencies and facility requirements.
- **Behavior Trees/AI**: extend villager/job AI with nodes that query production orders, evaluate skill match, and reserve workstations.
- **Scheduler**: integrate with service scheduler to tick long-running productions deterministically across rewind.
- **Requirements & Gating**:
  - Skill levels: gate recipe access and yield modifiers behind worker skills provided by population traits/education services.
  - Facility capabilities: require building upgrades or module components before advanced recipes activate.
  - Resource quality: enforce minimum grade or blessed variants for elite products (relics, flagship hulls).
  - Culture/tech prerequisites: query shared services to confirm alignment or research milestones before production queues accept orders.

---

## Detailed Production Chain Examples

### Core Resource Processing Chains

#### 1. Water & Life Support Chain (Both Modes)

##### Godgame: Ice → Water → Consumption

```yaml
Chain: Water Supply
- Extract: Ice (from ice deposits, frozen lakes)
  - Worker: Laborer (skill: Mining 0)
  - Time: 100 ticks per unit
  - Output: 1 Ice

- Process: Ice Treatment Plant
  - Input: 1 Ice
  - Worker: Plant Operator (skill: Processing 20)
  - Facility: Water Treatment Plant
  - Time: 50 ticks
  - Output: 5 Water
  - Byproduct: 0.1 Mineral Residue (10% chance)

- Consume: Water Usage
  - Villager Consumption: 2 Water per day
  - Crafting Input: Used in brewing, cooking, alchemy
  - Agriculture: Irrigation (3 Water per crop tile per day)
  - Livestock: 1 Water per animal per day

Alternate Source: Well (infinite, slow production)
- Worker: Well Keeper
- Time: 200 ticks per unit
- Output: 3 Water (no ice required)
```

##### Space4X: Ice → Water → Life Support

```yaml
Chain: Life Support System
- Extract: Ice (from asteroids, comets, ice planets)
  - Worker: Mining Drone / Crew
  - Facility: Mining Bay
  - Time: 150 ticks per unit
  - Output: 1 Ice Unit (100kg)

- Process: Electrolysis Plant
  - Input: 1 Ice Unit
  - Worker: Engineer (skill: Systems 30)
  - Facility: Life Support Module
  - Time: 80 ticks
  - Output: 8 Water, 2 Oxygen
  - Byproduct: 0.05 Hydrogen (fuel)

- Consume: Ship Supplies
  - Crew Consumption: 3 Water per crew per day
  - Hydroponics: 5 Water per growing bay per day
  - Coolant: 2 Water per reactor per day (recycled at 90%)
```

#### 2. Metal Refinement Chain (Godgame)

```yaml
Chain: Metal Production
- Extract: Iron Ore
  - Worker: Miner (skill: Mining 10)
  - Facility: Mine
  - Time: 200 ticks
  - Output: 1 Iron Ore
  - Byproduct: 0.2 Stone

- Refine: Smelting
  - Input: 1 Iron Ore, 0.5 Coal/Charcoal
  - Worker: Smelter (skill: Metallurgy 30)
  - Facility: Refinery / Blast Furnace
  - Time: 400 ticks
  - Output: 1 Iron Ingot
  - Quality: Depends on smelter skill (Common to Legendary)

- Alloy: Advanced Metallurgy
  - Input: 2 Iron Ingot, 1 Coal, 0.5 Rare Metal (Tin, Zinc, etc.)
  - Worker: Master Smelter (skill: Metallurgy 60)
  - Facility: Advanced Refinery
  - Time: 600 ticks
  - Output: 2 Steel Alloy / 2 Bronze Alloy
  - Note: Alloy type depends on rare metal used

Alternate Alloys:
- Bronze: 3 Copper + 1 Tin → 3 Bronze
- Steel: 2 Iron + 1 Coal (high temp) → 2 Steel
- Mithril Alloy: 1 Mithril Ore + 2 Steel → 2 Mithril Alloy (requires Metallurgy 80)
```

#### 3. Armor Production Chain (Godgame)

```yaml
Chain: Personal Armor
- Source: Metal Alloy (from refinement chain)

- Craft: Plate Armor
  - Input: 4 Steel Alloy, 2 Leather, 1 Cloth
  - Worker: Armorsmith (skill: Armorsmithing 50)
  - Facility: Armory / Smithy
  - Time: 1200 ticks
  - Output: 1 Plate Armor (Quality: depends on skill)
  - Properties:
    - Armor Rating: 12-18 (based on quality & material)
    - Weight: 25kg
    - Durability: 100

- Craft: Chainmail
  - Input: 2 Iron Ingot, 1 Leather
  - Worker: Armorsmith (skill: Armorsmithing 30)
  - Facility: Smithy
  - Time: 800 ticks
  - Output: 1 Chainmail
  - Properties:
    - Armor Rating: 8-12
    - Weight: 15kg
    - Durability: 80

Material Substitution Effects (from MaterialPropertiesSystem.md):
- Bronze Plate Armor: -20% armor rating (softer), -30% durability
- Mithril Plate Armor: +40% armor rating, -50% weight, +200% cost
- Adamantine Plate Armor: +60% armor rating, -20% weight, +500% cost
```

#### 4. Ship Plating Chain (Space4X)

```yaml
Chain: Ship Hull Plates
- Extract: Metal Ore (Iron, Titanium, Rare Metals)
  - Worker: Mining Drone / Crew
  - Facility: Asteroid Mining Station
  - Time: 300 ticks per unit
  - Output: 1 Ore Unit (500kg)

- Refine: Ore → Ingot
  - Input: 1 Ore Unit
  - Worker: Refinery Technician (skill: Metallurgy 40)
  - Facility: Orbital Refinery
  - Time: 500 ticks
  - Output: 1 Metal Ingot (400kg, 20% waste)

- Alloy: Advanced Ship Alloy
  - Input: 3 Titanium Ingot, 1 Rare Metal, 0.5 Carbon Fiber
  - Worker: Materials Engineer (skill: Advanced Metallurgy 70)
  - Facility: Materials Lab
  - Time: 800 ticks
  - Output: 3 Starship Alloy
  - Properties: High tensile strength, radiation resistant

- Fabricate: Hull Plates
  - Input: 5 Starship Alloy
  - Worker: Ship Fabricator (skill: Shipbuilding 60)
  - Facility: Shipyard Fabrication Bay
  - Time: 1500 ticks
  - Output: 1 Hull Plate (covers 10m² of ship surface)
  - Quality: Legendary plates have +30% armor, +20% heat resistance

Ship Assembly:
- Frigate: 50 Hull Plates, 20 Internal Frames, 10 Bulkheads
- Cruiser: 200 Hull Plates, 80 Internal Frames, 40 Bulkheads
- Capital Ship: 800 Hull Plates, 300 Internal Frames, 150 Bulkheads
```

#### 5. Ammunition Chain: Wood & Metal → Arrows (Godgame)

```yaml
Chain: Arrow Production
- Extract: Wood
  - Worker: Lumberjack (skill: Forestry 5)
  - Facility: Logging Camp
  - Time: 150 ticks
  - Output: 1 Timber Log

- Process: Timber → Planks
  - Input: 1 Timber Log
  - Worker: Sawyer (skill: Woodworking 15)
  - Facility: Sawmill
  - Time: 200 ticks
  - Output: 4 Wood Planks
  - Byproduct: 1 Sawdust

- Craft: Arrow Shafts
  - Input: 1 Wood Plank
  - Worker: Fletcher (skill: Fletching 25)
  - Facility: Fletcher's Workshop
  - Time: 300 ticks
  - Output: 10 Arrow Shafts
  - Quality: Straightness affects accuracy

- Craft: Arrow Heads
  - Input: 0.5 Iron Ingot (or other metal)
  - Worker: Blacksmith (skill: Smithing 20)
  - Facility: Smithy
  - Time: 150 ticks
  - Output: 10 Arrow Heads
  - Material: Iron (standard), Steel (+10% damage), Bronze (-10% damage)

- Craft: Fletching
  - Input: 1 Feather (from hunting/farming)
  - Worker: Fletcher (skill: Fletching 15)
  - Facility: Fletcher's Workshop
  - Time: 100 ticks
  - Output: 10 Fletchings

- Assemble: Arrows
  - Input: 10 Arrow Shafts, 10 Arrow Heads, 10 Fletchings
  - Worker: Fletcher (skill: Fletching 30)
  - Facility: Fletcher's Workshop
  - Time: 400 ticks
  - Output: 10 Arrows (bundled)
  - Quality: Affects accuracy, damage, durability

Special Arrow Types:
- Bodkin Arrows: +1 Steel per 10 arrows, +40% armor penetration
- Fire Arrows: +0.5 Pitch per 10 arrows, sets targets on fire
- Poison Arrows: +0.2 Venom per 10 arrows, applies poison DOT
```

#### 6. Component Chain: Tool Parts (Godgame)

```yaml
Chain: Tool Components
- Source: Alloys (from refinement chain)

- Craft: Screws & Bolts
  - Input: 0.2 Iron Ingot
  - Worker: Tool Maker (skill: Tool Making 20)
  - Facility: Tool Shop
  - Time: 200 ticks
  - Output: 20 Screws, 10 Bolts
  - Used in: Complex tools, siege engines, wagons

- Craft: Rails & Tracks
  - Input: 5 Steel Alloy
  - Worker: Metalworker (skill: Metalworking 40)
  - Facility: Foundry
  - Time: 800 ticks
  - Output: 1 Rail Section (10m)
  - Used in: Mine carts, rail systems

- Craft: Gears & Mechanisms
  - Input: 1 Steel Alloy, 5 Screws, 2 Bolts
  - Worker: Clockmaker (skill: Precision Engineering 50)
  - Facility: Workshop
  - Time: 1000 ticks
  - Output: 1 Gear Assembly
  - Used in: Clockwork, windmills, advanced machinery

- Craft: Tool Handles
  - Input: 1 Wood Plank
  - Worker: Carpenter (skill: Carpentry 15)
  - Facility: Carpenter's Shop
  - Time: 150 ticks
  - Output: 5 Tool Handles
  - Material affects durability: Oak (best), Pine (standard), Birch (light)

- Assemble: Complete Tools
  - Pickaxe: 1 Iron Head, 1 Handle → Mining tool
  - Hammer: 1 Steel Head, 1 Handle → Construction tool
  - Saw: 1 Steel Blade, 2 Handles, 4 Screws → Woodworking tool
  - Complex Mechanism: 3 Gear Assemblies, 10 Screws, 5 Bolts → Clockwork device
```

#### 7. Food Production Chain (Godgame)

```yaml
Chain: Food Processing
- Extract: Raw Food
  - Farming: Wheat, Vegetables (300 ticks per harvest)
  - Hunting: Meat, Hides (variable)
  - Fishing: Fish (200 ticks per catch)
  - Livestock: Milk, Eggs (daily production)

- Process: Grain Milling
  - Input: 1 Wheat Bundle
  - Worker: Miller (skill: Milling 15)
  - Facility: Mill (windmill/watermill)
  - Time: 250 ticks
  - Output: 3 Flour
  - Byproduct: 0.5 Bran (animal feed)

- Process: Meat Processing
  - Input: 1 Raw Meat
  - Worker: Butcher (skill: Butchering 20)
  - Facility: Butcher Shop
  - Time: 200 ticks
  - Output: 2 Processed Meat, 1 Hide, 0.3 Bone
  - Spoilage: 5% per day without preservation

- Craft: Bread
  - Input: 2 Flour, 0.5 Water, 0.1 Salt, 0.05 Yeast
  - Worker: Baker (skill: Baking 25)
  - Facility: Bakery
  - Time: 400 ticks
  - Output: 5 Bread Loaves
  - Quality affects nutrition and morale bonus

- Craft: Cooked Meals
  - Input: 1 Processed Meat, 1 Vegetable, 0.5 Water, 0.1 Spices
  - Worker: Cook (skill: Cooking 30)
  - Facility: Kitchen
  - Time: 300 ticks
  - Output: 3 Cooked Meals
  - Morale bonus: +2 to +8 (based on quality and variety)

- Craft: Preserved Food
  - Input: 2 Meat, 1 Salt
  - Worker: Preserver (skill: Preservation 25)
  - Facility: Smokehouse / Salt House
  - Time: 600 ticks
  - Output: 2 Salted Meat (no spoilage for 30 days)
```

#### 8. Advanced Materials Chain (Space4X)

```yaml
Chain: Quantum Components
- Extract: Rare Elements
  - Worker: Specialized Mining Crew
  - Facility: Deep Space Mining Station
  - Time: 1000 ticks
  - Output: 1 Exotic Matter Crystal

- Refine: Quantum Substrate
  - Input: 3 Exotic Matter, 5 Titanium Alloy, 2 Coolant
  - Worker: Quantum Engineer (skill: Quantum Physics 80)
  - Facility: Quantum Lab
  - Time: 2000 ticks
  - Output: 1 Quantum Substrate
  - Failure Rate: 20% (destroyed on failure)

- Fabricate: Quantum Core
  - Input: 1 Quantum Substrate, 10 Starship Alloy, 5 Advanced Electronics
  - Worker: Master Engineer (skill: Advanced Engineering 90)
  - Facility: High-Tech Fabrication Bay
  - Time: 3000 ticks
  - Output: 1 Quantum Core
  - Required for: FTL drives, quantum computers, advanced weapons

- Assemble: FTL Drive
  - Input: 1 Quantum Core, 20 Hull Plates, 50 Advanced Electronics, 10 Coolant Systems
  - Worker: Shipwright (skill: Shipbuilding 85)
  - Facility: Capital Shipyard
  - Time: 5000 ticks
  - Output: 1 FTL Drive Module
  - Enables: Faster-than-light travel
```

### Production Chain Dependencies

```yaml
Dependency Tree Example: Plate Armor

Raw Resources:
├─ Iron Ore (Mining)
├─ Coal (Mining)
├─ Animal Hide (Hunting/Farming)
└─ Flax (Farming)

Tier 1 Processing:
├─ Iron Ore + Coal → Iron Ingot (Refinery)
├─ Animal Hide → Leather (Tannery)
└─ Flax → Linen Cloth (Weaver)

Tier 2 Processing:
└─ Iron Ingot + Coal → Steel Alloy (Advanced Refinery)

Tier 3 Manufacturing:
└─ Steel Alloy + Leather + Cloth → Plate Armor (Armory)

Bottlenecks:
- Requires 4 Steel Alloy = 8 Iron Ingot = 8 Iron Ore + 8 Coal
- Time: ~4000 ticks from ore to armor (if all facilities ready)
- Workers: Miner × 2, Smelter × 2, Refiner × 1, Armorsmith × 1
```

### Facility Requirements Matrix

```yaml
Facilities by Production Tier:

Extraction (Tier 0):
- Mine: Iron Ore, Coal, Stone, Gems
- Logging Camp: Timber
- Farm: Crops, Livestock
- Hunting Lodge: Meat, Hides, Feathers
- Fishing Dock: Fish
- Ice Quarry: Ice

Basic Processing (Tier 1):
- Refinery: Ore → Ingot
- Sawmill: Timber → Planks
- Tannery: Hide → Leather
- Weaver: Flax → Cloth
- Mill: Grain → Flour
- Water Treatment Plant: Ice → Water

Advanced Processing (Tier 2):
- Advanced Refinery: Ingot → Alloy
- Tool Shop: Ingot → Components
- Butcher: Meat → Processed Meat
- Bakery: Flour → Bread
- Smokehouse: Meat → Preserved Food

Manufacturing (Tier 3):
- Smithy: Metal → Tools, Weapons
- Armory: Alloy → Armor
- Fletcher's Workshop: Wood + Metal → Arrows
- Shipyard: Components → Ships
- Wagon Workshop: Wood + Metal → Wagons
- Siege Workshop: Alloy + Wood → Siege Engines

Advanced Manufacturing (Tier 4):
- Master Smithy: Exotic Materials → Legendary Equipment
- Capital Shipyard: Advanced Components → Capital Ships
- Enchanter's Tower: Equipment + Magic → Enchanted Items
- Quantum Lab: Exotic Matter → Quantum Components
```

### Skill Requirements by Chain

```yaml
Water Supply Chain:
- Ice Extraction: Mining 0
- Water Treatment: Processing 20

Basic Metalworking:
- Ore Extraction: Mining 10
- Smelting: Metallurgy 30
- Smithing: Smithing 20

Advanced Metalworking:
- Alloy Creation: Metallurgy 60
- Armorsmithing: Armorsmithing 50
- Master Smithing: Smithing 80

Ammunition Production:
- Logging: Forestry 5
- Sawing: Woodworking 15
- Fletching: Fletching 25-30
- Smithing (heads): Smithing 20

Component Manufacturing:
- Tool Making: Tool Making 20-50
- Metalworking: Metalworking 40
- Precision Engineering: Precision Engineering 50

Food Production:
- Farming: Farming 10
- Milling: Milling 15
- Butchering: Butchering 20
- Cooking: Cooking 30
- Preservation: Preservation 25

Space4X Advanced:
- Quantum Engineering: Quantum Physics 80
- Advanced Fabrication: Advanced Engineering 90
- Capital Shipbuilding: Shipbuilding 85
```

---

## Technology & Knowledge Gating System

### Tech Level Hierarchy

**CRITICAL**: Production chains are gated primarily by **tech level** and **facility availability**, NOT by individual crew skill. Skill affects quality and efficiency, but tech access is cultural/institutional knowledge.

```csharp
// Tech level tied to faction/culture
public struct FactionTechLevel : IComponentData
{
    public Entity FactionEntity;
    public TechTier CurrentTier;
    public DynamicBuffer<UnlockedTechnology> Technologies;
    public DynamicBuffer<ResearchProgress> ActiveResearch;
}

public enum TechTier : byte
{
    Primitive,          // Tier 0: Stone tools, basic farming, fire
    Bronze,             // Tier 1: Bronze working, basic irrigation, pottery
    Iron,               // Tier 2: Iron working, advanced farming, literacy
    Medieval,           // Tier 3: Steel, windmills, advanced architecture
    Renaissance,        // Tier 4: Printing press, gunpowder, advanced metallurgy
    Industrial,         // Tier 5: Steam power, factories, rail
    Modern,             // Tier 6: Electricity, combustion, mass production
    Information,        // Tier 7: Computers, automation, space flight
    Fusion,             // Tier 8: Fusion power, advanced materials, FTL theory
    Quantum,            // Tier 9: Quantum tech, matter manipulation, wormholes
    Transcendent        // Tier 10: Post-scarcity, reality manipulation
}

public struct UnlockedTechnology : IBufferElementData
{
    public FixedString64Bytes TechID;           // "SteelSmelting", "FTLDrive", "QuantumComputing"
    public TechTier RequiredTier;
    public Entity DiscoveredBy;                 // Culture/colony that discovered it
    public ushort DiscoveryTick;
    public bool IsSecretTech;                   // Actively kept secret
    public float KnowledgeFragility;            // 0.0-1.0 (how easily lost)
}

public struct ResearchProgress : IBufferElementData
{
    public FixedString64Bytes TechID;
    public float ProgressPercent;               // 0.0-1.0
    public ushort TicksInvested;
    public DynamicBuffer<Entity> Researchers;   // Scientists working on this
}
```

### Production Chain Tech Requirements

```yaml
Tech Gating Examples:

Water Treatment Plant:
- Required Tech: "BasicPlumbing" (Tier 2: Iron Age)
- Required Facility: Water Treatment Plant (upgraded well)
- Crew Skill: Processing 20 (affects efficiency, not access)

Advanced Refinery:
- Required Tech: "AdvancedMetallurgy" (Tier 4: Renaissance)
- Required Facility: Advanced Refinery building
- Crew Skill: Metallurgy 60 (quality modifier)

Quantum Lab:
- Required Tech: "QuantumManipulation" (Tier 9: Quantum Age)
- Required Facility: Quantum Lab (capital investment)
- Crew Skill: Quantum Physics 80 (failure rate modifier)

Key Principle:
- Tech unlocks the RECIPE (you can build it)
- Facility unlocks the CAPABILITY (you have infrastructure)
- Skill affects QUALITY and EFFICIENCY (how well you make it)
```

### Cultural Knowledge Preservation

```csharp
// Culture maintains tech knowledge independently
public struct CulturalTechKnowledge : IComponentData
{
    public Entity CultureEntity;
    public TechTier HighestTier;                // Highest tech this culture knows
    public DynamicBuffer<CulturalTech> KnownTechnologies;
    public float KnowledgeStability;            // 0.0-1.0 (resist loss during crisis)
}

public struct CulturalTech : IBufferElementData
{
    public FixedString64Bytes TechID;
    public TechCategory Category;
    public float MasteryLevel;                  // 0.0-1.0 (how well understood)
    public ushort PractitionerCount;            // Number of experts
    public KnowledgeEndangerment Endangerment;  // Safe, Vulnerable, Critical, Lost
}

public enum TechCategory : byte
{
    Agriculture,            // Farming, irrigation, crop rotation
    Metallurgy,             // Mining, smelting, alloys
    Construction,           // Buildings, infrastructure
    Military,               // Weapons, armor, siege
    Naval,                  // Ships, navigation
    Medicine,               // Healing, surgery, alchemy
    Arcane,                 // Magic, rituals, enchanting
    Spaceflight,            // Rockets, FTL, life support
    QuantumPhysics,         // Advanced physics
    Manufacturing           // Production chains, automation
}

// Knowledge endangerment (matches MaterialPropertiesSystem.md)
public enum KnowledgeEndangerment : byte
{
    Widespread,             // 10+ experts (safe)
    Common,                 // 5-10 experts (stable)
    Uncommon,               // 2-5 experts (vulnerable)
    Endangered,             // 1 expert (could be lost)
    CriticallyEndangered,   // 0 experts, 1+ apprentices (incomplete)
    Lost                    // No practitioners (extinct)
}
```

### Empire Collapse & Knowledge Loss

```csharp
// When empire collapses, knowledge scatters
public struct EmpireCollapseEvent : IComponentData
{
    public Entity FormerEmpire;
    public DynamicBuffer<Entity> SuccessorCultures;
    public CollapseType Type;
    public float KnowledgeLossRate;             // 0.0-1.0 (how much tech lost)
}

public enum CollapseType : byte
{
    GradualDecline,         // 20-40% knowledge loss (slow decay)
    CivilWar,               // 30-50% loss (destruction, brain drain)
    ExternalConquest,       // 10-30% loss (conquerors preserve some)
    Cataclysm,              // 60-90% loss (plague, disaster)
    RapidFragmentation      // 40-60% loss (chaos, no coordination)
}

// System to handle knowledge distribution after collapse
public struct EmpireCollapseKnowledgeSystem : ISystem
{
    public void OnCollapseEvent(Entity empire, CollapseType collapseType)
    {
        var empireTech = GetComponent<FactionTechLevel>(empire);
        var cultures = GetComponent<EmpireCollapseEvent>(empire).SuccessorCultures;

        float lossRate = CalculateKnowledgeLossRate(collapseType);

        foreach (var tech in empireTech.Technologies)
        {
            // Determine if tech survives
            if (Random.value < lossRate)
            {
                // Tech lost to all successor cultures
                continue;
            }

            // Tech survives, distribute to cultures
            int culturesReceiving = CalculateCulturesReceivingTech(
                tech,
                cultures.Length,
                collapseType);

            // Randomly select which cultures retain knowledge
            var selectedCultures = SelectRandomCultures(cultures, culturesReceiving);

            foreach (var culture in selectedCultures)
            {
                var culturalTech = GetComponent<CulturalTechKnowledge>(culture);

                // Determine mastery level (usually degraded)
                float masteryDegradation = Random.Range(0.2f, 0.6f);
                float newMastery = tech.MasteryLevel * (1f - masteryDegradation);

                AddTechToCulture(culture, tech.TechID, newMastery);
            }
        }
    }

    private int CalculateCulturesReceivingTech(
        UnlockedTechnology tech,
        int totalCultures,
        CollapseType collapseType)
    {
        // Common tech spreads to more cultures
        float baseProbability = tech.KnowledgeFragility; // Higher = more fragile = fewer cultures

        float collapseModifier = collapseType switch
        {
            CollapseType.GradualDecline => 0.7f,        // Most cultures retain
            CollapseType.CivilWar => 0.4f,              // Half cultures retain
            CollapseType.ExternalConquest => 0.6f,      // Conquerors preserve
            CollapseType.Cataclysm => 0.2f,             // Few survivors
            CollapseType.RapidFragmentation => 0.5f,    // Random distribution
            _ => 0.5f
        };

        int receiving = (int)(totalCultures * collapseModifier * (1f - baseProbability));
        return math.max(1, receiving); // At least 1 culture retains
    }
}
```

### Cultural Schism & Knowledge Retention

```csharp
// When culture splits, subcultural groups retain partial knowledge
public struct CulturalSchismEvent : IComponentData
{
    public Entity ParentCulture;
    public DynamicBuffer<Entity> Subcultures;
    public SchismType Type;
    public float KnowledgeRetention;            // 0.0-1.0 (how much tech kept)
}

public enum SchismType : byte
{
    PeacefulSeparation,     // 80-95% retention (amicable split)
    IdeologicalSplit,       // 70-85% retention (philosophical differences)
    ViolentRevolt,          // 50-70% retention (conflict disrupts learning)
    ForcedExpulsion,        // 40-60% retention (expelled group loses access)
    Diaspora                // 60-80% retention (scattered but connected)
}

// Subculture inherits tech from parent
public struct SubcultureKnowledgeInheritance : ISystem
{
    public void OnSchismEvent(Entity parentCulture, SchismType schismType)
    {
        var parentTech = GetComponent<CulturalTechKnowledge>(parentCulture);
        var subcultures = GetComponent<CulturalSchismEvent>(parentCulture).Subcultures;

        float retentionRate = CalculateRetentionRate(schismType);

        foreach (var subculture in subcultures)
        {
            var subcultureTech = GetComponent<CulturalTechKnowledge>(subculture);

            foreach (var tech in parentTech.KnownTechnologies)
            {
                // Determine if subculture retains this tech
                if (Random.value < retentionRate)
                {
                    // Retained, but possibly at lower mastery
                    float masteryRetention = Random.Range(0.6f, 1.0f);
                    float newMastery = tech.MasteryLevel * masteryRetention;

                    AddTechToSubculture(subculture, tech.TechID, newMastery);
                }
                else
                {
                    // Lost during schism
                    LogKnowledgeLoss(subculture, tech.TechID, schismType);
                }
            }
        }
    }
}
```

### Colony Tech Discovery & Secrecy

```csharp
// Colony discovers new technology
public struct ColonyTechDiscovery : IComponentData
{
    public Entity Colony;
    public FixedString64Bytes DiscoveredTech;
    public DiscoveryMethod Method;
    public bool KeepSecret;                     // Colony hoards discovery
    public float SecrecyStrength;               // 0.0-1.0 (how well hidden)
    public ushort SecretDuration;               // Ticks since discovery
}

public enum DiscoveryMethod : byte
{
    Research,               // Systematic research program
    Excavation,             // Unearthed ancient ruins/artifacts
    Observation,            // Observed natural phenomenon
    ReverseEngineering,     // Analyzed foreign tech
    Accident,               // Serendipitous discovery
    Espionage,              // Stolen from another faction
    AlienContact            // Learned from aliens
}

// Secret tech spreads slowly (or not at all)
public struct SecretTechSpreadSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        foreach (var (discovery, colony) in
                 SystemAPI.Query<RefRW<ColonyTechDiscovery>,
                                RefRO<ColonyData>>())
        {
            if (!discovery.ValueRO.KeepSecret)
                continue; // Not secret, normal spread

            // Check for leaks
            float leakChance = CalculateLeakChance(
                discovery.ValueRO.SecrecyStrength,
                discovery.ValueRO.SecretDuration,
                colony.ValueRO);

            if (Random.value < leakChance)
            {
                // Secret leaked!
                LeakTechnology(discovery.ValueRO, colony.ValueRO);
            }
        }
    }

    private float CalculateLeakChance(
        float secrecyStrength,
        ushort duration,
        ColonyData colony)
    {
        // Base leak chance increases over time
        float baseChance = 0.001f; // 0.1% per tick baseline

        // Time modifier (secrets harder to keep over time)
        float timeModifier = duration / 100000f; // +1% per 100k ticks

        // Secrecy strength reduces leak chance
        float secrecyModifier = 1f - secrecyStrength;

        // Colony corruption increases leaks (spies, bribery)
        var corruption = GetComponent<AggregatePurity>(colony.ParentFaction);
        float corruptionModifier = corruption.CorruptionScore;

        // Trade connections increase leak risk (more contacts)
        var tradeRoutes = GetComponent<TradeRouteRegistry>(colony.Entity);
        float tradeModifier = tradeRoutes.ActiveRoutes.Length * 0.05f;

        float totalChance = baseChance +
                           (timeModifier * secrecyModifier) +
                           (corruptionModifier * 0.01f) +
                           tradeModifier;

        return math.clamp(totalChance, 0f, 0.5f); // Max 50% per tick
    }
}

// Tech spread through espionage and observation
public struct TechSpreadMechanism : IComponentData
{
    public FixedString64Bytes TechID;
    public Entity SourceFaction;
    public SpreadMethod Method;
    public float SpreadRate;                    // 0.0-1.0 (ticks to spread)
    public DynamicBuffer<Entity> AwareFactionsBuffer;
}

public enum SpreadMethod : byte
{
    OfficialSharing,        // Diplomatic/trade agreement (fast)
    NaturalDiffusion,       // Cultural contact (medium)
    Espionage,              // Spies steal secrets (slow)
    ReverseEngineering,     // Analyze captured equipment (very slow)
    BrainDrain,             // Recruit enemy experts (medium)
    PublicDemonstration     // Witnessed in action (slow)
}
```

### Tech Discovery Examples

```yaml
Scenario 1: Colony Discovers FTL Drive

Event:
- Frontier Colony excavates ancient alien ruins
- Discovers partial FTL Drive schematics
- Colony leadership decides: Keep secret or share?

Option A: Keep Secret (Economic Advantage)
- Colony maintains monopoly on FTL tech
- SecrecyStrength: 0.8 (high security)
- Colony builds FTL ships, dominates trade routes
- Leak chance: 0.15% per tick (increases over time)
- After 50,000 ticks: Spy from rival faction steals data
- Tech spreads via Espionage to 3 nearby factions

Option B: Share with Empire (Loyalty Bonus)
- Colony reports discovery to imperial authorities
- Empire grants colony tax exemptions, prestige
- Tech spreads to all imperial colonies within 10,000 ticks
- Rival empires begin espionage campaigns
- Tech spreads to rivals within 50,000 ticks via spies

Result Comparison:
- Secret: 50k ticks monopoly, then uncontrolled spread
- Shared: No monopoly, but empire gains unified advantage
```

```yaml
Scenario 2: Empire Collapse - Knowledge Scattering

Setup:
- Grand Empire controls 20 cultures
- Tech Level: Tier 7 (Information Age)
- Technologies: 50 unlocked (FTL, Fusion, Advanced Materials)
- Collapse Type: CivilWar (KnowledgeLossRate 40%)

Knowledge Distribution:
- 20 technologies lost entirely (40% loss rate)
- 30 technologies survive, distributed among cultures

Culture Distribution:
- Core Culture (capital region):
  - Retains 18 techs (60% of survivors)
  - TechTier remains 7
  - Mastery: 0.6-0.8 (degraded but functional)

- Frontier Cultures (5 colonies):
  - Each retains 8-12 techs (40% of survivors)
  - TechTier drops to 6
  - Mastery: 0.4-0.6 (incomplete knowledge)

- Backwater Cultures (10 rural):
  - Each retains 3-6 techs (20% of survivors)
  - TechTier drops to 5-6
  - Mastery: 0.2-0.4 (fragmentary knowledge)

Lost Technologies:
- Quantum Computing: Lost entirely (no survivors)
- Advanced AI: Retained by Core only (1/20 cultures)
- Fusion Power: Retained by Core + 2 Frontiers (3/20 cultures)
- FTL Drive: Retained by Core + 4 Frontiers (5/20 cultures)

Recovery Timeline:
- Core Culture: Research to restore lost tech (20k-100k ticks per tech)
- Frontier: Rediscover or trade with Core (50k-200k ticks)
- Backwater: Dependent on trade/teaching (100k-500k ticks)

50 Years Later:
- Core Culture: Tier 7 (restored to 80% pre-collapse)
- Frontier Cultures: Tier 6-7 (learned from Core via trade)
- Backwater Cultures: Tier 5-6 (slow recovery)
- 5 Technologies permanently lost (Quantum Computing extinct)
```

```yaml
Scenario 3: Cultural Schism - Tech Retention

Setup:
- Unified Dwarf Culture (TechTier 4: Renaissance)
- 40 known technologies (metallurgy, mining, engineering)
- Schism: IdeologicalSplit (75% retention rate)
- Reason: Clan dispute over steam technology

Split Groups:
- Traditionalist Dwarves (45% population):
  - Retains 32 techs (80% - higher due to conservative values)
  - Rejects steam technology entirely
  - Focus: Manual craftsmanship, traditional methods
  - Lost: SteamPower, AdvancedGears, RailSystems

- Progressive Dwarves (55% population):
  - Retains 28 techs (70% - lost traditional knowledge)
  - Embraces steam and innovation
  - Focus: Industrialization, automation
  - Lost: AncientRunicSmithing, TraditionalForging, MasterCrafting

Knowledge Divergence:
- Both groups retain core metallurgy (shared foundation)
- Traditionalists master legendary crafting (Smithing 90)
- Progressives master mass production (Manufacturing 80)

100 Years Later:
- Traditionalists: Tier 4 (peak craftsmanship, no factories)
- Progressives: Tier 5 (early industrial, inferior craftsmanship)
- Trade emerges: Progressive tools + Traditionalist weapons
- Cultural reunion unlikely (ideological differences persist)
```

### Tech Spread Rate by Method

```yaml
Official Tech Sharing:
- Diplomatic Agreement: 1,000-5,000 ticks
- Trade Treaty: 5,000-10,000 ticks
- Alliance Pact: 500-2,000 ticks
- Conditions: Both parties willing, infrastructure exists

Natural Diffusion:
- Adjacent Cultures: 20,000-50,000 ticks
- Trade Route Contact: 30,000-80,000 ticks
- Cultural Exchange: 50,000-100,000 ticks
- Requires: Regular contact, peaceful relations

Espionage:
- Targeted Spying: 10,000-30,000 ticks (if successful)
- Embedded Agent: 5,000-15,000 ticks
- Data Theft: 2,000-8,000 ticks
- Risk: Detection, diplomatic incident, war

Reverse Engineering:
- Captured Equipment: 50,000-150,000 ticks
- Salvaged Components: 80,000-200,000 ticks
- Observed Technology: 100,000-300,000 ticks
- Requires: High intelligence, research facilities

Brain Drain:
- Recruit Expert: 15,000-40,000 ticks (expert teaches)
- Defection: 10,000-25,000 ticks
- Headhunting: 20,000-50,000 ticks
- Requires: Attractive offer, expert willing

Public Demonstration:
- Witnessed in Battle: 40,000-100,000 ticks
- Trade Fair Display: 60,000-120,000 ticks
- Cultural Festival: 80,000-150,000 ticks
- Limited: Only observable aspects (not theory)
```

---

## Production Efficiency & Alternative Methods

**Core Principle**: Factions can develop **alternative production modules** with different resource costs. Shortages drive innovation, creating multiple paths to the same outcome with trade-offs between cost, quality, and bonuses.

### Alternative Production Modules

```csharp
// Multiple ways to produce the same item
public struct ProductionModule : IComponentData
{
    public FixedString64Bytes ModuleID;         // "LaserWeapon_6Lens", "LaserWeapon_3Lens", "LaserWeapon_Cheap"
    public FixedString64Bytes BaseItemID;       // "LaserWeapon" (what it produces)
    public ModuleVariant Variant;
    public DynamicBuffer<ResourceCost> Costs;   // Resource requirements
    public DynamicBuffer<StatModifier> Bonuses; // Performance differences
    public float ProductionTimeMultiplier;      // 1.0 = standard, 0.8 = 20% faster
    public Entity DiscoveredBy;                 // Faction that developed this
    public bool IsSecretMethod;                 // Can be stolen
}

public enum ModuleVariant : byte
{
    Standard,           // Original design (balanced)
    Efficient,          // Reduced resource cost, same performance
    CheapCornerCut,     // Much cheaper, worse performance
    Premium,            // More resources, better performance
    Experimental,       // Unproven, risky benefits
    Improvised,         // Emergency design (shortage response)
    Optimized           // Research breakthrough (best efficiency)
}

public struct ResourceCost : IBufferElementData
{
    public FixedString64Bytes ResourceID;
    public float Quantity;
    public bool IsCritical;                     // Must have this resource (no substitution)
}

public struct StatModifier : IBufferElementData
{
    public FixedString64Bytes StatName;         // "Damage", "Accuracy", "Range", "PowerDraw"
    public float Multiplier;                    // 1.0 = baseline, 1.2 = +20%
}
```

### Efficiency Research System

```csharp
// Research to reduce production costs
public struct EfficiencyResearch : IComponentData
{
    public FixedString64Bytes TargetItemID;     // What we're optimizing
    public Entity ResearchingFaction;
    public EfficiencyGoal Goal;
    public float ProgressPercent;               // 0.0-1.0
    public ushort TicksInvested;
    public DynamicBuffer<Entity> Researchers;
}

public enum EfficiencyGoal : byte
{
    ReduceMaterialCost,     // Use fewer resources (maintain performance)
    ImprovePerformance,     // Better output (same resources)
    ReduceTime,             // Faster production (same cost)
    SubstituteMaterial,     // Replace scarce resource with common one
    SimplifyProcess,        // Reduce complexity (easier to make)
    ScaleProduction         // Optimize for mass production
}

// Successful research creates new module
public struct EfficiencyBreakthrough : IComponentData
{
    public Entity OriginalModule;
    public Entity NewModule;
    public EfficiencyGain Improvements;
    public float DevelopmentCost;               // R&D investment
}

public struct EfficiencyGain : IComponentData
{
    public float ResourceSavings;               // 0.0-1.0 (0.3 = 30% cheaper)
    public float PerformanceChange;             // -0.2 to +0.5 (can be negative)
    public float ProductionSpeedChange;         // -0.3 to +0.4
    public DynamicBuffer<ResourceSubstitution> Substitutions;
}

public struct ResourceSubstitution : IBufferElementData
{
    public FixedString64Bytes OriginalResource;
    public FixedString64Bytes NewResource;
    public float QuantityRatio;                 // 1.5 = need 50% more of new resource
}
```

### Corner-Cutting & Emergency Production

```csharp
// Shortages trigger emergency alternatives
public struct EmergencyProductionModule : IComponentData
{
    public FixedString64Bytes BaseItemID;
    public Entity ParentModule;                 // Original design
    public ShortageReason Reason;
    public DynamicBuffer<ResourceCost> ReducedCosts;
    public DynamicBuffer<QualityPenalty> Penalties;
    public bool IsTemporary;                    // Revert when shortage ends
}

public enum ShortageReason : byte
{
    ResourceScarcity,       // Critical resource unavailable
    WarEmergency,           // Need production ASAP
    EconomicCrisis,         // Can't afford standard production
    SupplyBlockade,         // Trade routes cut off
    DisasterResponse        // Catastrophe requires improvisation
}

public struct QualityPenalty : IBufferElementData
{
    public FixedString64Bytes StatName;
    public float PenaltyMultiplier;             // 0.7 = -30% performance
    public bool IsAcceptable;                   // Faction willing to use despite penalty
}

// System to evaluate if corner-cutting is acceptable
public struct CornerCuttingDecisionSystem : ISystem
{
    public bool ShouldUseCheapVersion(
        Entity faction,
        ProductionModule standardModule,
        EmergencyProductionModule cheapModule)
    {
        var economy = GetComponent<FactionEconomy>(faction);
        var military = GetComponent<MilitaryDoctrine>(faction);

        // Calculate resource availability
        float resourceAvailability = CalculateResourceAvailability(
            faction,
            standardModule.Costs);

        // Critical shortage: forced to corner-cut
        if (resourceAvailability < 0.3f)
            return true;

        // War emergency: quantity over quality
        if (military.IsAtWar && military.NeedsUrgentProduction)
        {
            // Willing to accept up to 40% performance loss for 3× production
            float efficiencyGain = standardModule.Costs.Sum() / cheapModule.ReducedCosts.Sum();
            if (efficiencyGain > 2.0f)
                return true;
        }

        // Economic crisis: can't afford standard
        if (economy.AvailableFunds < standardModule.ProductionCost * 0.5f)
            return true;

        return false; // Use standard production
    }
}
```

### Example: Laser Weapon Production Variants

```yaml
Laser Weapon Production Modules:

Module 1: Standard 6-Lens Design
- Resources:
  - 6 Focused Lenses (rare)
  - 2 Power Cells
  - 1 Targeting Computer
  - 5 Starship Alloy
- Time: 1000 ticks
- Stats:
  - Damage: 100 (baseline)
  - Accuracy: 100 (baseline)
  - Range: 100 (baseline)
  - Power Draw: 100 (baseline)
- Cost: 5000 credits

Module 2: Efficient 3-Lens Design (Research Breakthrough)
- Research Required: "OpticalEfficiency" (50k ticks, 10k credits)
- Resources:
  - 3 Focused Lenses (50% reduction!)
  - 2 Power Cells
  - 1 Advanced Targeting Computer (compensates with software)
  - 5 Starship Alloy
- Time: 1000 ticks (same)
- Stats:
  - Damage: 95 (-5%, acceptable trade-off)
  - Accuracy: 100 (maintained)
  - Range: 95 (-5%, acceptable)
  - Power Draw: 90 (-10%, bonus efficiency!)
- Cost: 3500 credits (30% cheaper)
- Note: Can still use 6 lenses for premium version

Module 3: Premium 6-Lens Optimized Design
- Available after researching Module 2
- Resources:
  - 6 Focused Lenses (keeps all 6 for bonuses)
  - 2 Power Cells
  - 1 Advanced Targeting Computer (uses new tech)
  - 5 Starship Alloy
- Time: 1000 ticks
- Stats:
  - Damage: 120 (+20%, benefits from optimization)
  - Accuracy: 110 (+10%)
  - Range: 115 (+15%)
  - Power Draw: 90 (-10%, efficiency from research)
- Cost: 5500 credits (slightly more expensive)
- Note: Best of both worlds (uses efficiency research + premium materials)

Module 4: Corner-Cut Emergency Design (Shortage Response)
- Created during lens shortage / war emergency
- Resources:
  - 2 Standard Lenses (inferior, but available)
  - 1 Power Cell (reduced)
  - 1 Basic Targeting Computer (downgraded)
  - 3 Starship Alloy (cheaper frame)
- Time: 600 ticks (40% faster)
- Stats:
  - Damage: 60 (-40%, significant penalty)
  - Accuracy: 70 (-30%)
  - Range: 80 (-20%)
  - Power Draw: 120 (+20%, inefficient)
- Cost: 1500 credits (70% cheaper)
- Note: Emergency use only, acceptable during crisis

Module 5: Mass Production Design (Scale Optimization)
- Research Required: "ManufacturingAutomation" (80k ticks, 15k credits)
- Resources:
  - 4 Standard Lenses (compromise between quality and cost)
  - 2 Power Cells
  - 1 Targeting Computer
  - 4 Starship Alloy (standardized components)
- Time: 500 ticks (50% faster via automation)
- Stats:
  - Damage: 90 (-10%, acceptable)
  - Accuracy: 95 (-5%)
  - Range: 95 (-5%)
  - Power Draw: 100 (baseline)
- Cost: 3000 credits (40% cheaper)
- Benefit: Can produce 2× quantity in same time
- Note: Best for large-scale fleet production

Production Strategy Decision:
- Peacetime: Module 1 (Standard) or Module 3 (Premium for elite units)
- Research Investment: Module 2 (Efficient) - long-term savings
- War Emergency: Module 4 (Corner-Cut) for rapid deployment
- Large Fleet: Module 5 (Mass Production) for quantity
```

### Knowledge Theft & Reverse Engineering

```csharp
// Steal efficiency methods from other factions
public struct StolenProductionKnowledge : IComponentData
{
    public Entity SourceFaction;                // Who developed this
    public Entity ThiefFaction;                 // Who stole it
    public FixedString64Bytes ModuleID;
    public TheftMethod Method;
    public float KnowledgeCompleteness;         // 0.0-1.0 (partial knowledge)
    public ushort TicksToImplement;             // Time to integrate
}

public enum TheftMethod : byte
{
    Espionage,              // Spy stole blueprints
    CapturedEquipment,      // Reverse-engineered from salvage
    DefectorExpert,         // Engineer defected with knowledge
    CorporateTheft,         // Bribed factory worker
    DataBreach,             // Hacked databases
    ObservationOnly         // Saw it work, reverse-engineer from scratch
}

// Implementing stolen knowledge
public struct StolenKnowledgeImplementation : ISystem
{
    public void OnImplement(Entity thiefFaction, StolenProductionKnowledge stolen)
    {
        var sourceModule = GetComponent<ProductionModule>(stolen.ModuleID);
        var thiefTech = GetComponent<FactionTechLevel>(thiefFaction);

        // Check if thief has tech base to use this
        if (thiefTech.CurrentTier < sourceModule.RequiredTechTier)
        {
            // Can't implement, tech too advanced
            MarkAsUnusable(stolen);
            return;
        }

        // Create stolen version with degraded performance
        var stolenModule = CreateStolenModule(
            sourceModule,
            stolen.KnowledgeCompleteness,
            stolen.Method);

        // Stolen modules usually worse than original
        float performancePenalty = CalculateTheftPenalty(
            stolen.Method,
            stolen.KnowledgeCompleteness);

        ApplyPerformancePenalty(stolenModule, performancePenalty);
        RegisterModule(thiefFaction, stolenModule);
    }

    private float CalculateTheftPenalty(
        TheftMethod method,
        float completeness)
    {
        // Base penalty by theft method
        float basePenalty = method switch
        {
            TheftMethod.Espionage => 0.05f,             // 5% penalty (good blueprints)
            TheftMethod.DefectorExpert => 0.02f,        // 2% penalty (expert knows tricks)
            TheftMethod.CapturedEquipment => 0.15f,     // 15% penalty (reverse-engineer)
            TheftMethod.DataBreach => 0.10f,            // 10% penalty (partial data)
            TheftMethod.CorporateTheft => 0.08f,        // 8% penalty (insider knowledge)
            TheftMethod.ObservationOnly => 0.30f,       // 30% penalty (guesswork)
            _ => 0.20f
        };

        // Knowledge completeness modifier
        float completenessPenalty = (1f - completeness) * 0.2f;

        return basePenalty + completenessPenalty;
    }
}
```

### Efficiency Competition & Arms Race

```csharp
// Factions compete to develop most efficient production
public struct EfficiencyCompetition : IComponentData
{
    public FixedString64Bytes ItemCategory;     // "LaserWeapons", "ShipHulls", "PowerCores"
    public DynamicBuffer<FactionEfficiency> Rankings;
    public Entity CurrentLeader;                // Most efficient producer
    public float LeaderAdvantage;               // 0.3 = 30% cheaper than average
}

public struct FactionEfficiency : IBufferElementData
{
    public Entity Faction;
    public float ResourceEfficiency;            // 0.0-2.0 (1.0 = standard, 0.7 = 30% cheaper)
    public float ProductionSpeed;               // 0.0-2.0 (1.0 = standard, 1.5 = 50% faster)
    public float QualityLevel;                  // 0.0-2.0 (1.0 = standard, 1.2 = 20% better)
    public ushort ModuleVariants;               // Number of alternative designs
}

// Economic advantage from efficiency
public struct EfficiencyAdvantage : IComponentData
{
    public Entity Faction;
    public float CostSavingsPerUnit;            // Credits saved per item
    public float ProductionVolumeBonus;         // Can produce more with same resources
    public float ExportCompetitiveness;         // Can undercut other factions in trade
}
```

### Scenario Examples

```yaml
Scenario 1: Lens Shortage Crisis

Setup:
- Faction relies on 6-Lens Laser Weapons (standard production)
- Focused Lens supplier destroyed in war
- Lens availability drops 80% (20 → 4 units per day)
- Military needs 100 laser weapons ASAP

Response Options:

Option A: Corner-Cut Emergency Production
- Switch to Module 4 (2-lens corner-cut design)
- Can produce 50 weapons per day (vs 3 with standard design)
- Performance: 60% damage, 70% accuracy
- Duration: 20 days → 1000 emergency weapons
- Result: Fleet equipped with inferior weapons but operational
- Risk: Losses increase 40% in combat due to poor quality

Option B: Research 3-Lens Efficiency
- Emergency research program (accelerated: 30k ticks vs 50k)
- Cost: 15k credits, 10 expert researchers
- Duration: 12 days to complete research
- Switch to Module 2 (3-lens efficient design)
- Performance: 95% damage, 100% accuracy (acceptable)
- Production: 6 weapons per day → 48 weapons (during research) + 150 weapons (after)
- Result: Fewer weapons initially, but higher quality fleet

Option C: Material Substitution Research
- Research alternative to Focused Lenses (synthetic crystals)
- Duration: 40 days
- Discover: "SyntheticFocusingCrystal" production chain
- Performance: 90% as effective as natural lenses
- Production: Unlimited (synthetic), 8 weapons per day
- Long-term: Independent of natural lens supply

Outcome Comparison:
- Option A: 1000 weak weapons (short-term survival)
- Option B: 198 good weapons (balanced approach)
- Option C: 320 acceptable weapons + future independence

Faction Choice: Depends on urgency and strategic outlook
```

```yaml
Scenario 2: Efficiency Espionage Success

Setup:
- Faction A: Pioneer in 3-Lens Efficient Laser Design
- Faction B: Uses standard 6-Lens design (50% more expensive)
- Faction B's spy steals partial blueprints (Completeness: 0.7)

Implementation:

Stolen Module Stats:
- Original (Faction A Module 2):
  - 3 Lenses, Damage 95, Cost 3500 credits
- Stolen Version (Faction B):
  - 3 Lenses, Damage 88 (-7% theft penalty)
  - Cost: 3800 credits (+8% due to incomplete knowledge)
  - Production Time: 1100 ticks (+10%, unfamiliar process)

Economic Impact:
- Faction B saves 24% cost vs their standard design (5000 → 3800)
- Still 8% more expensive than Faction A (intelligence gap)
- Can now compete in export markets

Faction A Response:
- Detects theft after 6 months (stolen weapons appear in Faction B fleet)
- Options:
  1. Accelerate research to next-gen 2-Lens design (stay ahead)
  2. Diplomatic protest (demand compensation)
  3. Counter-espionage (steal Faction B's module improvements)

Result:
- Arms race: Both factions invest in efficiency research
- Faction A develops 2-Lens Quantum-Focused design (further 30% reduction)
- Faction B continues reverse-engineering (always 1 generation behind)
```

```yaml
Scenario 3: Premium vs Efficient Production Choice

Setup:
- Faction has researched 3-Lens Efficient Design (Module 2)
- Can now produce either:
  - Module 2: 3 Lenses, 95% performance, 3500 credits
  - Module 3: 6 Lenses, 120% performance, 5500 credits

Strategic Decisions:

Fleet Composition:
- Core Fleet (20% of ships): Module 3 Premium (elite units)
  - Flagships, carriers, elite squadrons
  - Worth the cost for critical missions
  - Performance: 120% damage, 110% accuracy

- Main Fleet (60% of ships): Module 2 Efficient (backbone)
  - Standard frigates, destroyers
  - Good balance of cost and performance
  - Performance: 95% damage, 100% accuracy

- Reserve Fleet (20% of ships): Module 5 Mass Production (quantity)
  - Reserve forces, garrison ships
  - Acceptable performance, rapid deployment
  - Performance: 90% damage, 95% accuracy

Economic Analysis:
- Average cost per weapon: 3,800 credits (vs 5,000 standard)
- Fleet size increase: 32% more ships with same budget
- Combat effectiveness: 98% vs 100% (negligible difference)

Result: Diversified production strategy
- Elite units maintain performance edge
- Main fleet benefits from efficiency savings
- Overall fleet 32% larger for same cost
```

### Research Tree: Production Efficiency

```yaml
Efficiency Research Progression:

Tier 1: Basic Optimization
- Research: "ProcessStreamlining" (20k ticks, 5k credits)
- Effect: -10% production time, -5% resource cost
- Applies to: All Tier 2-3 production

Tier 2: Material Substitution
- Research: "AlternativeMaterials" (40k ticks, 10k credits)
- Effect: Can substitute rare materials with common ones
- Example: Use 3 Common Metal instead of 1 Rare Metal (-50% cost, -10% quality)

Tier 3: Advanced Efficiency
- Research: "OpticalEfficiency" (50k ticks, 10k credits)
- Effect: Laser weapons use 50% fewer lenses (3 vs 6)
- Performance: -5% damage (acceptable trade-off)

Tier 4: Manufacturing Automation
- Research: "AutomatedProduction" (80k ticks, 15k credits)
- Effect: -50% production time, standardized components
- Benefit: Can produce 2× quantity in same time

Tier 5: Breakthrough Optimization
- Research: "QuantumFocusing" (150k ticks, 30k credits)
- Effect: 2-Lens design with 110% performance
- Requires: Quantum Physics 80, OpticalEfficiency completed

Tier 6: Perfect Efficiency
- Research: "MaterialPerfection" (300k ticks, 60k credits)
- Effect: Premium performance with efficient costs
- Result: 6-Lens performance with 3-Lens cost (best of both)
```

---

# Generalized Environment & Ecosystem Framework

**Status:** Draft - <WIP: Abstract design pattern>  
**Category:** Meta - Reusable System Framework  
**Scope:** Cross-Project (Godgame, Space4X, any game with environmental simulation)  
**Created:** 2025-10-31  
**Last Updated:** 2025-10-31

**⚠️ PURPOSE:**
- Create **reusable environment system** for any game setting
- Works for fantasy worlds AND alien planets
- Scales from simple (static) to complex (full simulation)
- Pure design pattern, not implementation

---

## Core Concept

### What is Environment?

**Environment:** The living/reactive world layer beneath gameplay entities  
**Ecosystem:** Interconnected systems (climate, vegetation, resources, weather)  
**Biome/Region:** Spatial classification with distinct characteristics

**Universal Pattern:**
```
Region Classification (biome, planet type)
    ↓ Defines
Environmental Properties (climate, composition)
    ↓ Affected by
Dynamic Events (weather, disasters, player actions)
    ↓ Influences
Resource Availability (harvestables, yields)
    ↓ Affects
Entity Behavior (needs, strategies, survival)
```

---

## Three-Layer Architecture

### Layer 1: Region Classification (Biomes)

**Purpose:** Divide world into zones with distinct characteristics

**Examples Across Games:**
- **Godgame:** Temperate forest, grasslands, mountains, desert
- **Space4X:** Terran planet, arid world, frozen moon, toxic atmosphere, molten core
- **Survival:** Forest, tundra, jungle, ocean
- **City Builder:** Residential, industrial, commercial, parks

**Components:**
```
RegionId : IComponentData {
    ushort RegionType;         // Biome/planet type enum
    FixedString64Bytes Name;   // "Temperate Forest", "Ice World"
    float3 Center;             // Region center point
    float Radius;              // Region bounds (or use spatial grid cells)
}

RegionProperties : IComponentData {
    // Classification
    byte RegionCategory;       // Forest, Desert, Ocean, etc.
    
    // Base conditions
    float BaseTemperature;     // Default temp for this region
    float BaseHumidity;        // Default moisture
    float BaseFertility;       // Resource/vegetation density
    
    // Visual identity
    byte VisualBiomeId;        // Art asset set to use
}
```

---

### Layer 2: Environmental State (Climate/Conditions)

**Purpose:** Current state that changes over time

**Examples:**
- **Godgame:** Temperature, humidity, season
- **Space4X:** Atmospheric pressure, radiation, temperature, toxicity
- **Survival:** Weather, time of day, temperature
- **City Builder:** Pollution level, noise, air quality

**Components:**

**Global State (Singleton):**
```
EnvironmentState : IComponentData {
    // Current conditions (global or per-region)
    float Temperature;
    float Humidity;
    float Pressure;            // Atmosphere (space) or N/A (fantasy)
    float Toxicity;            // Pollutants (space/industrial) or N/A
    
    // Time cycles
    uint CurrentCycle;         // Season, day/night, orbit, etc.
    uint TicksIntoCycle;
    uint CycleLengthTicks;
}
```

**Spatial State (Grid-Based):**
```
EnvironmentGrid : IComponentData (singleton) {
    int Width, Height;
    float CellSize;
    BlobAssetReference<EnvironmentCellBlob> Cells;
}

EnvironmentCell : struct {
    float Temperature;         // Local temp (can vary from global)
    float Moisture;            // Ground wetness / resource availability
    float Fertility;           // Vegetation/resource density
    byte RegionType;           // Biome classification
}
```

---

### Layer 3: Dynamic Events (Weather/Disasters)

**Purpose:** Temporary state changes affecting environment

**Examples:**
- **Godgame:** Rain, drought, storm
- **Space4X:** Solar flares, meteor strikes, atmospheric storms
- **Survival:** Blizzard, sandstorm, earthquake
- **City Builder:** Smog alert, heatwave, flood

**Components:**
```
EnvironmentEvent : IComponentData {
    FixedString64Bytes EventType;  // "Rain", "SolarFlare", "Blizzard"
    uint StartTick;
    uint DurationTicks;
    float Intensity;                // 0-1
    float3 Position;                // Global or localized
    float Radius;                   // Area of effect
}

EventEffect : IComponentData {
    // What this event modifies
    float TemperatureDelta;    // ±change
    float MoistureDelta;       // ±change
    float FertilityDelta;      // ±change
    // Add more as needed per game
}
```

---

## Resource Layer (Harvestables)

**Purpose:** What entities can extract from environment

**Examples:**
- **Godgame:** Wood (trees), ore (mines), berries (bushes)
- **Space4X:** Minerals (deposits), energy (solar), gas (nebulae)
- **Survival:** Stone, metal, fiber, food
- **City Builder:** Land value, zoning capacity

**Components:**
```
HarvestableResource : IComponentData {
    ushort ResourceType;       // Wood, Ore, Minerals, etc.
    float Amount;              // Current available
    float MaxAmount;           // Capacity
    float RegenerationRate;    // Respawn speed (or 0 for non-renewable)
}

ResourceGrowth : IComponentData (optional - if dynamic) {
    float GrowthStage;         // 0-1 (seed to mature)
    float GrowthRate;          // Base speed
    
    // Environmental requirements
    float MoistureRequired;    // Optimal moisture range
    float TemperatureRequired; // Optimal temp range
    float FertilityMultiplier; // Soil quality factor
}
```

---

## Universal Patterns

### Climate → Resource Availability

**Pattern:**
```
Region Climate Properties
    ↓ Determines
Resource Distribution
    ↓ Affects
Harvesting Yields
    ↓ Influences
Entity Strategy
```

**Examples:**

**Godgame:**
```
Temperate Forest:
  Climate: Mild (15°C), Humid (60%)
  → Wood: High density (many trees)
  → Ore: Medium (hills)
  → Villager Strategy: Balanced economy
  
Mountain:
  Climate: Cold (5°C), Dry (30%)
  → Wood: Low (sparse pines)
  → Ore: High (exposed rock)
  → Villager Strategy: Mining-focused, import wood
```

**Space4X:**
```
Terran Planet:
  Climate: Moderate temp, breathable atmosphere
  → Minerals: Medium
  → Energy: Solar (good)
  → Pop Habitability: 100% (no gear needed)
  
Frozen Moon:
  Climate: -100°C, thin atmosphere
  → Minerals: High (exposed)
  → Energy: Low (distant from star)
  → Pop Habitability: 20% (need domes/suits)
```

---

### Weather → Temporary Modifiers

**Pattern:**
```
Normal Conditions (baseline)
    ↓
Weather Event Triggers
    ↓ Temporarily modifies
Environment State
    ↓ Affects
Resource Yield / Entity Needs
    ↓ Returns to
Normal (after duration)
```

**Examples:**

**Godgame:**
```
Rain Event:
  Duration: 60 seconds
  Effect: Moisture +0.3, Temperature -5°C
  Impact: Vegetation growth +20%, villagers seek shelter
  Visuals: Rain particles, wet ground
  
Drought Event:
  Duration: 10 minutes
  Effect: Moisture -0.5, Evaporation ×3
  Impact: Crop failure, water scarcity, famine risk
  Visuals: Brown grass, cracked earth
```

**Space4X:**
```
Solar Flare:
  Duration: 1 day (game time)
  Effect: Radiation +500%, Energy disruption
  Impact: Electronics damage, pop health -10%, shields needed
  Visuals: Orange glow, electrical arcs
  
Atmospheric Storm:
  Duration: 3 days
  Effect: Wind +300%, visibility 0
  Impact: Surface operations halted, ships grounded
  Visuals: Swirling clouds, lightning
```

---

### Moisture/Fertility → Growth/Yield

**Pattern:**
```
Environment Conditions
    ↓ Evaluated against
Resource Requirements
    ↓ Produces
Growth/Yield Multiplier
    ↓ Determines
Actual Amount/Speed
```

**Godgame Example:**
```
Tree growth:
  Moisture: 0.6 (good)
  Temperature: 20°C (optimal 15-25°C)
  Fertility: 0.8 (soil quality)
  
  Growth Rate = BaseRate × 
    MoistureFactor(0.6) = 1.0 ×
    TempFactor(20°C) = 1.0 ×
    FertilityFactor(0.8) = 0.8
  
  = BaseRate × 0.8 (slightly slower than optimal)
```

**Space4X Example:**
```
Mineral extraction:
  Richness: 0.9 (rich deposit)
  Temperature: 800°C (very hot)
  Toxicity: 0.3 (moderate)
  
  Yield = BaseYield ×
    RichnessFactor(0.9) = 1.5 ×
    TempPenalty(800°C) = 0.6 (too hot, equipment strain) ×
    ToxicPenalty(0.3) = 0.9
  
  = BaseYield × 0.81 (decent but challenging)
```

---

## Biome/Region Mapping

### Godgame Biomes → Space4X Planet Types

| Godgame Biome | Space4X Equivalent | Universal Pattern |
|---------------|-------------------|-------------------|
| Temperate Forest | Terran Planet | Optimal, balanced |
| Grasslands | Savanna World | Open, agricultural |
| Mountains | Rocky/High-G Planet | Resource-rich, harsh |
| Desert | Arid World | Water-scarce, extreme |
| Swamp | Tropical/Humid World | Disease risk, unique resources |
| Arctic | Frozen Moon | Extreme cold, mining |
| Volcanic | Molten Planet | Extreme heat, energy-rich |

**Pattern:** Each "biome" defines resource distribution + living conditions

---

## Grid Systems (Spatial Data)

### Universal Grid Pattern

**Any game with spatial environment uses grid:**

```
EnvironmentGrid : IComponentData {
    int Width, Height;         // Grid dimensions
    float CellSize;            // Meters per cell
    BlobAssetReference<CellData> Grid;
}

CellData : struct {
    // Configure what matters per game
    byte RegionType;           // Biome/terrain/planet type
    float ResourceDensity;     // 0-1
    float Condition1;          // Moisture (Godgame), Radiation (Space4X)
    float Condition2;          // Fertility (Godgame), Mineral richness (Space4X)
    float Condition3;          // Temperature (both games)
}
```

**Godgame Cells:**
```
Condition1 = Moisture (0-1)
Condition2 = Fertility (0-1)
Condition3 = Temperature (-50 to +50°C)
```

**Space4X Cells (Planet Surface):**
```
Condition1 = Radiation (0-1)
Condition2 = Mineral Richness (0-1)
Condition3 = Temperature (-200 to +500°C)
```

**Same grid structure, different meaning per game!**

---

## Dynamic vs Static (Complexity Tiers)

### Tier 0: No Environment (Pure Gameplay)
- No biomes, no weather, no growth
- Focus entirely on entities and actions
- **Use for:** Minimal viable product, focused mechanics

### Tier 1: Static Classification
- Biomes/regions defined but never change
- Fixed resource distribution
- No weather, no cycles
- **Use for:** Strategic diversity without simulation overhead

### Tier 2: Simple Dynamics
- Weather states (rain/clear)
- Basic resource regeneration
- Simple moisture/fertility tracking
- **Use for:** Balanced complexity, noticeable world reactivity

### Tier 3: Full Simulation
- Dynamic vegetation growth/death
- Seasonal cycles
- Weather events with consequences
- Moisture propagation
- Disaster events
- **Use for:** Rich simulation, ecosystem management core to gameplay

**Choose tier based on game focus!**

---

## Reusable Component Schema

### Region/Biome Classification

```csharp
// Works for any spatial zone type
RegionClassification : IComponentData {
    ushort RegionType;         // Game-specific enum
    float3 Center;
    float Radius;
    
    // Base properties (configure meaning per game)
    float Property1;           // Godgame: BaseTemperature | Space4X: Gravity
    float Property2;           // Godgame: BaseHumidity | Space4X: Atmosphere
    float Property3;           // Godgame: BaseFertility | Space4X: Richness
}
```

---

### Environment State

```csharp
// Global or per-region state
EnvironmentState : IComponentData {
    // Conditions (configure meaning per game)
    float Condition1;          // Temperature, Radiation, etc.
    float Condition2;          // Humidity, Pressure, etc.
    float Condition3;          // Fertility, Toxicity, etc.
    
    // Cycles (seasons, day/night, orbit)
    uint CurrentCycle;
    uint TicksIntoCycle;
}
```

---

### Spatial Grid

```csharp
// Universal environmental grid
EnvironmentGrid : IComponentData {
    int Width, Height;
    float CellSize;
    BlobAssetReference<EnvironmentCellBlob> Cells;
}

EnvironmentCell : struct {
    byte RegionType;           // Biome classification
    float Value1;              // Moisture, Mineral density, etc.
    float Value2;              // Fertility, Radiation, etc.
    float Value3;              // Temperature, Pressure, etc.
}
```

---

### Dynamic Events

```csharp
// Weather, disasters, phenomena
EnvironmentEvent : IComponentData {
    FixedString64Bytes EventType;  // Game-specific
    uint StartTick;
    uint DurationTicks;
    float Intensity;
    float3 Position;           // Epicenter
    float Radius;              // Area of effect
    
    // Effects (what it modifies)
    float DeltaCondition1;
    float DeltaCondition2;
    float DeltaCondition3;
}
```

---

### Harvestable Resources

```csharp
// Universal resource node/deposit
HarvestableEntity : IComponentData {
    ushort ResourceType;       // Game-specific
    float Amount;
    float MaxAmount;
    float RegenerationRate;
}

// Optional: Growth/depletion over time
ResourceDynamics : IComponentData {
    float GrowthStage;
    float GrowthRate;
    
    // Requirements (optimal conditions)
    float OptimalCondition1;
    float OptimalCondition2;
    float ToleranceRange;
}
```

---

## Cross-Game Applications

### Godgame (Fantasy Environment)

**Region Types:**
- Temperate Forest, Grasslands, Mountains, Desert, Swamp

**Environment Conditions:**
- Condition1 = Temperature (-50 to +50°C)
- Condition2 = Humidity (0-100%)
- Condition3 = Moisture (0-1, grid-based)

**Events:**
- Rain, Drought, Storm, Snow

**Harvestables:**
- Trees (wood), Ore nodes, Berry bushes, Crops

**Entity Impact:**
- Villagers need shelter from cold/heat
- Agriculture requires moisture
- Resources from vegetation harvesting

---

### Space 4X RTS (Alien Planets)

**Region Types:**
- Terran, Arid, Frozen, Toxic, Molten, Gas Giant, Barren

**Environment Conditions:**
- Condition1 = Temperature (-200 to +500°C)
- Condition2 = Atmospheric Pressure (0-10 atm)
- Condition3 = Radiation (0-1000 rads)

**Events:**
- Solar Flares, Meteor Storms, Volcanic Eruptions, Atmospheric Storms

**Harvestables:**
- Mineral Deposits, Gas Vents, Energy Crystals, Exotic Matter

**Entity Impact:**
- Pops need life support in hostile environments
- Mining yields vary by richness
- Habitability affects growth rate

---

### Survival Game (Wilderness)

**Region Types:**
- Forest, Tundra, Jungle, Desert, Ocean, Cave

**Conditions:**
- Condition1 = Temperature
- Condition2 = Danger Level (predators)
- Condition3 = Resource Abundance

**Events:**
- Storms, Animal migrations, Forest fires

**Harvestables:**
- Trees, Rocks, Plants, Animals

**Entity Impact:**
- Player survival (cold, hunger, predators)
- Seasonal resource availability

---

### City Builder (Urban Environment)

**Region Types:**
- Residential, Commercial, Industrial, Parks, Slums

**Conditions:**
- Condition1 = Pollution
- Condition2 = Noise Level
- Condition3 = Property Value

**Events:**
- Smog Alerts, Festivals, Riots, Construction Booms

**Harvestables:**
- Land (zones), Resources (abstract)

**Entity Impact:**
- Citizen happiness based on pollution/noise
- Property values affect tax income

---

## Environmental Cycles

### Universal Cycle Pattern

```
Cycle State: Phase index (0-N)
    ↓ Time advances
Phase Progression: Linear or circular
    ↓ Each phase has
Phase Properties: Environmental modifiers
    ↓ Affects
Entities and Resources
```

**Examples:**

**Godgame - Seasonal Cycle:**
```
Phase 0: Spring (renewal, growth fast)
Phase 1: Summer (peak, hot, abundant)
Phase 2: Fall (harvest, growth slows)
Phase 3: Winter (dormant, cold, consumption)
→ Back to Phase 0
```

**Space4X - Orbital Cycle:**
```
Phase 0: Perihelion (close to star, hot, high energy)
Phase 1: Aphelion (far from star, cold, low energy)
→ Back to Phase 0
```

**City Builder - Economic Cycle:**
```
Phase 0: Boom (growth, construction)
Phase 1: Plateau (stable)
Phase 2: Recession (decline)
Phase 3: Recovery (rebuilding)
→ Back to Phase 0
```

**Same pattern, different contexts!**

---

## Environment → Entity Needs

### Universal Modifier Pattern

**Environment conditions modify entity stats:**

```
EntityStat (base) × EnvironmentModifier = Actual Stat

Example:
  Base Work Rate = 100%
  Cold Environment (temp < 0°C) = ×0.7 modifier
  Actual Work Rate = 70% (slower in cold)
```

**Godgame Examples:**
```
Villager Work Rate:
  Temperature 20°C (optimal): ×1.0
  Temperature 35°C (hot): ×0.8 (tired from heat)
  Temperature -5°C (cold): ×0.7 (need warming breaks)
  
Vegetation Growth Rate:
  Moisture 0.5 (good): ×1.0
  Moisture 0.1 (drought): ×0.2 (struggling)
  Moisture 0.9 (saturated): ×0.7 (waterlogged)
```

**Space4X Examples:**
```
Pop Growth Rate:
  Habitability 100% (terran): ×1.0
  Habitability 50% (moderate): ×0.5
  Habitability 10% (hostile): ×0.1
  
Mining Yield:
  Richness 0.9: ×1.5
  Temperature 800°C: ×0.6 (equipment strain)
  Final: ×0.9 yield
```

---

## Event Propagation

### Universal Event Pattern

```
Event Triggers (natural, player, random)
    ↓
Event Spawns (entity or state change)
    ↓
Duration Active (ticks down)
    ↓
Effects Applied (modify environment)
    ↓
Environment Changes (temperature, moisture, etc.)
    ↓
Entities React (needs, yields, behavior)
    ↓
Event Ends (return to baseline or new stable state)
```

**Godgame - Rain Event:**
```
Rain Miracle Cast (player trigger)
    ↓
Rain Event spawns (60 second duration)
    ↓
Moisture +0.3 per second (affects grid)
    ↓
Vegetation growth accelerates
    ↓
Villagers seek shelter (behavior)
    ↓
Rain ends after 60s
    ↓
Moisture evaporates gradually (return to baseline over 5 minutes)
```

**Space4X - Solar Flare:**
```
Star activity spike (random trigger)
    ↓
Solar Flare event (24 hour duration)
    ↓
Radiation +500% (affects planets)
    ↓
Shields strain, electronics damage
    ↓
Pop health -10%, production -20%
    ↓
Flare ends
    ↓
Systems recover (repair time: 12 hours)
```

---

## Biome/Region Diversity Strategies

### Distinct Regions (Islands)
- Hard boundaries between biomes
- Each region pure type (forest, desert, etc.)
- **Use for:** Clear strategic choices, easy implementation

### Gradient Regions (Smooth)
- Gradual transitions (forest fades to grassland)
- Properties blend at boundaries
- **Use for:** Realistic worlds, smooth visuals

### Layered Regions (Overlapping)
- Multiple classification systems (biome + elevation + moisture)
- Cells can be "high mountain forest" (3 attributes)
- **Use for:** Rich terrain variety, complex interactions

---

## Configuration Per Game

### Godgame Configuration

```
Region Types:
  - TemperateForest (balanced)
  - Grasslands (farming)
  - Mountains (mining)
  - <OPTIONAL: Desert, Swamp>

Environment Conditions:
  - Temperature (-20 to +40°C)
  - Humidity (0-100%)
  - Moisture Grid (0-1 per cell)

Cycles:
  - Seasons (Spring/Summer/Fall/Winter) - 5min each
  - Day/Night (optional) - 2min each

Events:
  - Rain (adds moisture)
  - Drought (removes moisture)
  - Storm (rain + wind + visual drama)

Resources:
  - Wood (trees, regenerates)
  - Ore (nodes, finite or slow regen)
  - Berries (bushes, seasonal)

Grid Resolution:
  - Reuse spatial grid (already exists)
  - ~5-10m per cell
```

---

### Space4X Configuration

```
Region Types (Planet Classifications):
  - Terran (earth-like)
  - Arid (desert world)
  - Frozen (ice moon)
  - Toxic (poison atmosphere)
  - Molten (lava world)
  - Barren (dead rock)
  - Gas Giant (no surface)

Environment Conditions:
  - Temperature (-273 to +2000°C)
  - Atmospheric Pressure (0-100 atm)
  - Radiation (0-1000 rads)
  - Gravity (0-5G)

Cycles:
  - Orbital Period (affects temperature, energy)
  - Tectonic Activity (earthquakes, volcanism)

Events:
  - Solar Flares (radiation spike)
  - Meteor Strikes (devastation)
  - Volcanic Eruptions (new land, minerals)
  - Atmospheric Storms

Resources:
  - Minerals (deposits, finite)
  - Energy (solar, geothermal, gas)
  - Exotic Matter (rare finds)

Grid Resolution:
  - Planetary surface grid
  - ~100km per cell (planet scale)
```

---

## Open Questions (Framework Level)

### Scope
1. **Complexity tier?** Static, simple, or full simulation?
2. **Performance budget?** How many cells? Update frequency?

### Spatial
3. **Grid resolution?** Fine (1m) or coarse (10m+)?
4. **Region boundaries?** Discrete or gradient?
5. **Multiple grids?** (Moisture, fertility, temperature separate or unified?)

### Dynamics
6. **Update frequency?** Per tick, per second, slower?
7. **Propagation?** Do effects spread (fire, moisture) or stay local?
8. **Persistence?** Do changes last or revert to baseline?

### Cycles
9. **Seasonal length?** Real-time minutes or game-time hours?
10. **Day/night?** Include or skip?

### Events
11. **Event frequency?** Common or rare?
12. **Player control?** Can player trigger all events or just some (miracles)?

---

## Truth Source Implications

**If Environment Implemented (Any Game):**

**Required Components:**
```
RegionClassification    // Biome/planet type
EnvironmentState       // Current conditions (singleton or per-region)
EnvironmentGrid        // Spatial grid (optional, for fine detail)
EnvironmentEvent       // Weather/disasters (optional, for dynamics)
```

**Optional Components:**
```
ResourceGrowth         // Dynamic harvestables
CycleState            // Seasons, orbits, day/night
WeatherState          // Active weather
```

**Required Systems:**
```
EnvironmentUpdateSystem     // Update conditions over time
EventTriggerSystem         // Spawn weather/disasters
ResourceGrowthSystem       // Vegetation/mineral dynamics
VisualExpressionSystem     // Update visuals from state
```

---

## Reusability Checklist

**Framework is reusable when:**
- [ ] Region-agnostic (biomes OR planet types OR city zones)
- [ ] Condition-agnostic (moisture OR radiation OR pollution)
- [ ] Event-agnostic (rain OR solar flare OR smog alert)
- [ ] Resource-agnostic (wood OR minerals OR land value)
- [ ] Scale-agnostic (1m cells OR 100km cells)
- [ ] Configurable complexity (static → simple → full)

**Current Status:** ✅ All patterns are generalizable

---

## Next Steps

**Godgame:**
1. Decide scope: Static world OR simple weather OR full ecosystem?
2. If weather: Design rain/drought impact on resources
3. If full: Design biomes, moisture, vegetation growth

**Space4X:**
1. Define planet types (terran, arid, frozen, etc.)
2. Planet conditions (habitability, richness)
3. Events (solar flares, tectonics)
4. Reuse same grid/event framework

**Both games use same environmental simulation core!**

---

**For Designers:** Choose complexity tier first, then design specifics  
**For Product:** Environment is scope multiplier - each tier adds significant cost  
**For Tech:** Single framework supports fantasy worlds AND alien planets with config swap

---

**Last Updated:** 2025-10-31  
**Applicability:** Godgame ✓, Space4X ✓, Survival ✓, City Builder ✓, Any spatial simulation ✓




# Environment Systems (Biomes, Vegetation, Climate, Moisture)

**Status:** Draft - <WIP: All environmental systems empty>  
**Category:** System - World Simulation  
**Scope:** Global environmental simulation  
**Created:** 2025-10-31  
**Last Updated:** 2025-10-31

**⚠️ CURRENT STATE:**
- ❌ `VegetationGrowthSystem.cs` exists but **EMPTY**
- ❌ `ClimateSystem.cs` exists but **EMPTY**
- ❌ `MoistureGridSystem.cs` exists but **EMPTY**
- ❌ `WindSystem.cs` exists but **EMPTY**
- ❌ No biome system
- ❌ No terrain types
- ❌ Complete greenfield design

**⚠️ SCOPE DECISIONS NEEDED:**
1. **Is environment simulation in scope?** Or static terrain only?
2. **Dynamic vegetation?** Or fixed trees/plants?
3. **Weather system?** Rain, snow, storms?
4. **Biome variety?** Multiple climate zones or single biome?
5. **Player interaction?** Can god affect weather/growth?

---

## Purpose

**Primary Goal:** Living, reactive world that responds to player and villagers  
**Secondary Goals:**
- Biomes create visual variety and strategic diversity
- Vegetation provides resources (wood, food)
- Climate affects villager needs and behavior
- Moisture influences agriculture and growth
- Weather creates dramatic moments (storms, droughts)

**Inspiration:**
- **Populous:** Terrain deformation, weather miracles
- **Black & White 2:** Verdant miracle, forest growth
- **Banished:** Harsh winters, crop seasons
- **Civilization:** Biomes affect yields and strategies

---

## System Overview

### Components

1. **Biome Types** <UNDEFINED: What biomes exist?>
   - Role: Regional climate/terrain classification
   - Type: Spatial zones or per-cell attributes

2. **Vegetation** <UNDEFINED: Dynamic or static?>
   - Role: Trees, plants, crops
   - Type: Entities that grow/die/harvested

3. **Climate State** <UNDEFINED: Global or per-biome?>
   - Role: Temperature, humidity, season
   - Type: Singleton or spatial grid

4. **Moisture Grid** <UNDEFINED: How granular?>
   - Role: Ground wetness affecting growth
   - Type: Per-cell values (spatial grid)

5. **Weather Events** <UNDEFINED: Active or passive?>
   - Role: Rain, drought, storms
   - Type: Temporary modifiers or entities

### Connections

```
<WIP: Proposed environmental flow>

Biome
    ↓ Defines
Climate Base (temp, humidity)
    ↓ Affected by
Weather Events (rain, drought)
    ↓ Modifies
Moisture Grid (ground wetness)
    ↓ Influences
Vegetation Growth (trees, crops)
    ↓ Provides
Resources (wood, food)
    ↓ Harvested by
Villagers
```

### Feedback Loops

- **Positive:** Rain → moisture → vegetation grows → more wood → build more → support more villagers
- **Negative:** Deforestation → less vegetation → less moisture retention → drought → famine
- **Balance:** Sustainable harvesting maintains ecosystem

---

## Biome System

**<NEEDS DESIGN: What biomes exist in Godgame?>**

### Option A: Single Biome (MVP - Simplest)
- Entire map is one biome (temperate forest?)
- Uniform climate, vegetation
- **Pros:** Simple, focused
- **Cons:** No variety, less strategy

### Option B: Multiple Biomes (Full Sim)
- Map divided into biome zones
- Each biome has distinct properties

**Proposed Biomes (If Implemented):**

| Biome | Climate | Vegetation | Resources | Villager Impact |
|-------|---------|------------|-----------|-----------------|
| **Temperate Forest** | Mild, rainy | Dense trees, bushes | Wood (high), Berries | Easy living, moderate farming |
| **Grasslands** | Warm, seasonal | Grass, scattered trees | Wood (low), Grain | Excellent farming, exposed |
| **Mountains** | Cold, dry | Sparse pines | Ore (high), Stone | Harsh living, mining focused |
| **Desert** | Hot, arid | Cacti, scrub | <SPEC?> | Difficult, water critical |
| **Swamp** | Humid, wet | Mangroves, moss | Fish (?), special plants | Disease risk?, unique resources |

**<RECOMMENDATION: Start single biome (temperate), add variety later>**

---

### Option C: Gradient Biomes (Blended)
- No hard biome boundaries
- Gradual transitions (forest → grassland → desert)
- Properties blend based on position
- **Pros:** Realistic, smooth
- **Cons:** Complex

**<NEEDS DECISION: Discrete zones or gradients?>**

---

## Vegetation System

**<NEEDS DESIGN: How do plants work?>**

### Current State
- ✅ Resource nodes exist (`GodgameResourceNode` - wood, ore)
- ❌ No growth mechanics
- ❌ No dynamic spawning
- ❌ No seasons/lifecycle

### Option A: Static Vegetation (Simple)
- Trees/plants placed in editor
- Never grow, never die
- Harvested → depleted → respawn after delay
- **Pros:** Simple, predictable
- **Cons:** Not living, no ecosystem

### Option B: Dynamic Growth (Simulation)

**Vegetation Entity:**
```
VegetationId : IComponentData {
    ushort VegetationType;     // Oak, Pine, Wheat, etc.
    byte GrowthStage;          // Seed, Sapling, Adult, Mature
    float Age;                 // In ticks or seasons
    float Health;              // 0-100 (affected by moisture, climate)
}

VegetationGrowth : IComponentData {
    float GrowthRate;          // Speed of maturation
    float MoistureRequired;    // 0-1 (drought tolerance)
    float TemperatureMin/Max;  // Climate requirements
    float YieldAmount;         // Resources when harvested
}
```

**Growth Loop:**
```
Each tick:
  - Check moisture in cell
  - Check temperature (climate)
  - If conditions met: GrowthStage advances
  - If drought: Health decreases
  - If mature: Harvest-ready (resource node)
  - If dead: Despawn or leave stump
```

**Pros:** Living ecosystem, strategic resource management  
**Cons:** Complex, performance cost (many entities)

---

### Option C: Grid-Based Growth (Hybrid)
- No individual plant entities
- Per-cell vegetation density value
- Visual: Spawn/despawn plants based on density
- **Pros:** Performant, emergent patterns
- **Cons:** Less granular control

**<RECOMMENDATION: Start static (Option A), add growth in v2.0 if desired>**

---

## Climate System

**<NEEDS DESIGN: What is climate in this game?>**

### Option A: Global Climate (Singleton)

**Single world state:**
```
ClimateState : IComponentData (singleton) {
    float Temperature;         // -50°C to +50°C (or abstract 0-100)
    float Humidity;            // 0-100%
    byte Season;               // Spring, Summer, Fall, Winter
    uint TicksIntoSeason;      // Season progression
}
```

**Changes:**
- Seasonal cycles (automatic progression)
- Player miracles (rain increases humidity temporarily)
- Random events (heatwave, cold snap)

**Pros:** Simple, single source of truth  
**Cons:** Entire world same climate (unrealistic)

---

### Option B: Spatial Climate (Grid-Based)

**Per-cell climate:**
```
ClimateCell : struct {
    float Temperature;
    float Humidity;
    byte BiomeType;
}

ClimateGrid : IComponentData (singleton) {
    BlobAssetReference<ClimateCellBlob> Grid;
}
```

**Allows:**
- Different biomes have different climates
- Mountains cold, deserts hot, swamps humid
- Weather affects regions not whole map

**Pros:** Realistic, biome diversity  
**Cons:** Complex, performance consideration

---

### Option C: Biome-Level Climate

**Per-biome climate values:**
```
BiomeClimate : IComponentData {
    byte BiomeId;
    float BaseTemperature;
    float BaseHumidity;
    float SeasonalVariation;
}
```

**Middle ground:** Multiple climates, not per-cell granularity

**<RECOMMENDATION: Start Option A (global), add spatial if biomes added>**

---

## Moisture System

**<NEEDS DESIGN: How does ground moisture work?>**

### Moisture Grid (Spatial)

**Current:** `MoistureGridSystem.cs` file exists but **EMPTY**

**Proposed:**
```
MoistureGrid : IComponentData (singleton) {
    int GridWidth, GridHeight;
    float CellSize;              // Meters per cell
    BlobAssetReference<MoistureCellBlob> Cells;
}

MoistureCell : struct {
    float MoistureLevel;         // 0-1 (dry to saturated)
    float DrainageRate;          // How fast moisture evaporates
    float AbsorptionRate;        // How fast rain soaks in
}
```

**Moisture Sources:**
- Rain events → Add moisture to cells
- Irrigation (if implemented) → Add moisture near water
- <FOR REVIEW: Rivers, lakes?>

**Moisture Sinks:**
- Evaporation → Moisture decreases over time (faster when hot)
- Plant absorption → Vegetation consumes moisture to grow
- Drainage → Flows downhill? (terrain slope)

**Affects:**
- Vegetation health (low moisture = drought stress)
- Crop yields (farms need moisture)
- Visual: Grass color (brown when dry, green when wet)

---

### Moisture Mechanics

**Update Pattern:**
```
Each tick/second:
  For each moisture cell:
    // Add from rain
    IF weather == Rain:
      MoistureLevel += RainRate × deltaTime
    
    // Evaporation
    MoistureLevel -= EvaporationRate × Temperature × deltaTime
    
    // Plant consumption
    IF vegetation in cell:
      MoistureLevel -= PlantAbsorption × deltaTime
    
    // Clamp
    MoistureLevel = clamp(MoistureLevel, 0, 1)
```

**Design Question:** How fine-grained? (1m cells? 5m? 10m?)

---

## Weather System

**<NEEDS DESIGN: Dynamic weather or static?>**

### Option A: No Weather (Static)
- Clear skies always
- Moisture from magic only (rain miracle)
- **Pros:** Simple
- **Cons:** Less dynamic, miracles less impactful

### Option B: Simple Weather (State-Based)

**Weather State:**
```
WeatherState : IComponentData (singleton) {
    byte CurrentWeather;       // Clear, Rain, Storm, Drought
    uint DurationRemaining;    // Ticks until change
    float Intensity;           // 0-1
}

Weather types:
  - Clear (default)
  - Rain (adds moisture)
  - Storm (heavy rain + wind + lightning visual)
  - Drought (high evaporation, crop stress)
```

**Transitions:**
- Natural cycles (random or seasonal)
- Player miracles trigger weather (Rain miracle)
- <FOR REVIEW: Can player extend/end weather?>

**Pros:** Dynamic world, miracle integration  
**Cons:** Simplistic (global weather)

---

### Option C: Regional Weather (Advanced)

**Weather per region/biome:**
- Forest area has rain
- Mountain area has snow
- Desert always clear
- Weather fronts move across map

**Pros:** Realistic, strategic  
**Cons:** Complex simulation

**<RECOMMENDATION: Option B (simple weather) if environment in scope>**

---

## Vegetation Types

**<NEEDS DESIGN: What plants exist?>**

### Resource Vegetation (Functional)

**Trees (Wood Source):**
- Oak (temperate, medium growth, good wood)
- Pine (mountains, fast growth, average wood)
- <FOR REVIEW: Palm (tropical), Birch, Willow?>

**Plants (Food/Special):**
- Berry bushes (food source)
- Wheat/grain (farmable if agriculture system)
- <FOR REVIEW: Herbs (special resources), Flowers (aesthetic)?>

**Current Implementation:**
- ✅ Resource nodes exist (generic wood/ore)
- ❌ No type variety (just "wood node")
- ❌ No visual variety per type

---

### Visual Vegetation (Aesthetic)

**Ground Cover:**
- Grass (responds to moisture - green/brown)
- Flowers (good god villages?)
- Dirt (high traffic areas)
- <FOR REVIEW: Snow, sand based on biome?>

**Decorative:**
- Rocks, boulders
- Fallen logs
- Mushrooms?

**<NEEDS DECISION: Functional only or rich visual ecosystem?>**

---

## Biome Design (If Multi-Biome)

**<FOR REVIEW: Biome characteristics>**

### Temperate Forest (Starter Biome)
**Climate:**
- Temperature: 10-25°C (mild)
- Humidity: 40-70% (moderate)
- Seasons: All four (Spring → Summer → Fall → Winter)

**Vegetation:**
- Density: High (dense forests)
- Types: Oak, Birch, Berry bushes
- Growth: Medium speed

**Resources:**
- Wood: Abundant
- Ore: Moderate (hills)
- Food: Berries, hunting

**Villager Impact:**
- Moderate living (not too harsh)
- Good for starting village
- Balanced resource access

**Visual:**
- Green in spring/summer
- Orange/red in fall
- Brown/white in winter (if seasons)

---

### Grasslands (Open Plains)
**Climate:**
- Temperature: 15-30°C (warm)
- Humidity: 20-50% (drier)
- Seasons: Wet/dry or summer/winter

**Vegetation:**
- Density: Low (scattered trees)
- Types: Grass, grain, sparse trees
- Growth: Fast (grass), slow (trees)

**Resources:**
- Wood: Scarce
- Ore: Low
- Food: Excellent (farming)

**Villager Impact:**
- Exposed (no cover from enemies)
- Agriculture-focused
- Trade for wood/stone

**Visual:**
- Golden grass
- Wide open vistas
- Big sky

---

### Mountains (High Altitude)
**Climate:**
- Temperature: -10 to 15°C (cold)
- Humidity: 20-40% (dry)
- Seasons: Long winter, short summer

**Vegetation:**
- Density: Very low (alpine)
- Types: Pine, scrub, moss
- Growth: Very slow

**Resources:**
- Wood: Low
- Ore: Abundant (mining)
- Stone: Abundant

**Villager Impact:**
- Harsh living (cold, difficult terrain)
- Mining-focused
- Defensive advantages (high ground)

**Visual:**
- Rocky, barren
- Snow-capped peaks
- Gray stone dominates

---

### Desert <FOR REVIEW: Include or skip?>
**Climate:**
- Temperature: 25-45°C (hot)
- Humidity: 5-20% (arid)
- Seasons: Minimal (always hot)

**Vegetation:**
- Density: Very low
- Types: Cacti, scrub
- Growth: Slow, drought-resistant only

**Resources:**
- Wood: None
- Ore: <SPEC?>
- Water: Critical scarcity

**Villager Impact:**
- Very harsh (water management critical)
- Unique challenges
- High difficulty

**Visual:**
- Yellow sand
- Sparse vegetation
- Heat shimmer

**<NEEDS DECISION: Include desert or focus on temperate/grasslands for MVP?>**

---

## Climate System Design

**<NEEDS SPEC: What does climate actually DO?>**

### Global Climate State (Proposed)

```
ClimateState : IComponentData (singleton) {
    float Temperature;         // Current temp (-50 to +50°C)
    float Humidity;            // Current humidity (0-100%)
    byte Season;               // 0=Spring, 1=Summer, 2=Fall, 3=Winter
    uint TicksIntoSeason;      // 0 to SeasonLength
    uint SeasonLengthTicks;    // How long each season
}
```

**Effects of Climate:**

**Temperature:**
- < 0°C: Water freezes, crops die, villagers need heating (fires, buildings)
- 0-10°C: Cold, slow growth
- 10-25°C: Optimal (temperate)
- 25-35°C: Hot, high evaporation
- \> 35°C: Heat stress, villagers need water/shade

**Humidity:**
- < 20%: Drought conditions, high evaporation
- 20-40%: Dry
- 40-70%: Comfortable
- \> 70%: High moisture, fast vegetation growth

**Seasons (If Implemented):**
- **Spring:** Planting season, vegetation grows, renewal
- **Summer:** Peak growth, hot, abundant
- **Fall:** Harvest season, growth slows, leaves change
- **Winter:** Dormant, cold, villagers indoors more

**<NEEDS DECISION: Are seasons in scope or always summer?>**

---

## Moisture Grid Design

**<NEEDS SPEC: How granular is moisture tracking?>**

### Grid Resolution

**Option A: Coarse Grid (10m cells)**
- 100m × 100m map = 10×10 grid = 100 cells
- **Pros:** Fast, simple
- **Cons:** Low fidelity

**Option B: Fine Grid (1m cells)**
- 100m × 100m map = 100×100 grid = 10,000 cells
- **Pros:** High detail, local effects
- **Cons:** Memory/performance cost

**Option C: Adaptive/Match Spatial Grid**
- Use existing `SpatialGridConfig` (already exists for registries!)
- Reuse cell structure
- **Pros:** No duplicate grid, consistent
- **Cons:** Tied to spatial grid resolution

**<RECOMMENDATION: Option C - reuse spatial grid>**

---

### Moisture Mechanics

```
Per moisture cell:

Sources (Increase moisture):
  + Rain weather (global or regional)
  + Rain miracle (player-cast, area)
  + Rivers/lakes (constant source - if implemented)
  + Irrigation (if agriculture system)

Sinks (Decrease moisture):
  - Evaporation (temperature-based)
  - Plant absorption (vegetation growth)
  - Drainage (if terrain slopes)
  - Villager consumption (wells?)

Update:
  MoistureDelta = Sources - Sinks
  MoistureLevel = clamp(Current + Delta × dt, 0, 1)
```

**Visual Expression:**
- Dry (< 0.2): Brown grass, cracked earth
- Normal (0.2-0.6): Green grass
- Wet (0.6-0.8): Dark green, lush
- Saturated (> 0.8): Puddles, muddy

---

## Vegetation Growth Mechanics

**<IF dynamic vegetation implemented>**

### Growth Stages

```
VegetationGrowth : IComponentData {
    byte GrowthStage;          // 0=Seed, 1=Sprout, 2=Young, 3=Adult, 4=Mature
    float GrowthProgress;      // 0-1 within current stage
    float GrowthRate;          // Base speed (species-dependent)
}
```

**Growth Requirements:**
```
Growth Speed = BaseRate × 
  MoistureFactor × 
  TemperatureFactor × 
  SunlightFactor (?)
  
MoistureFactor:
  IF moisture < 0.2: 0.1 (drought stress)
  IF moisture 0.2-0.6: 1.0 (optimal)
  IF moisture > 0.6: 0.8 (waterlogged)
  
TemperatureFactor:
  IF temp in optimal range: 1.0
  IF temp outside range: 0.0-0.5 (stress)
```

**Maturity & Harvest:**
```
Seed (0): Not visible, underground
Sprout (1): Small, can't harvest
Young (2): Growing, low yield
Adult (3): Full size, good yield
Mature (4): Old, max yield, may seed new plants
```

**Harvesting:**
- Villager harvests → Removes tree, adds wood to inventory
- <FOR REVIEW: Leave stump? Regrow? Plant new seed?>

---

## Weather Events

**<NEEDS DESIGN: Weather mechanics>**

### Weather Types

**Clear (Default):**
- No effects
- Standard evaporation
- Moderate growth

**Rain (Common):**
- Adds moisture to grid (+0.3 per rain duration)
- Reduces temperature slightly (-5°C)
- Visual: Rain particles, wet ground
- Audio: Rain sounds
- Duration: 30-120 seconds

**Storm (Dramatic):**
- Heavy moisture (+0.6)
- Wind effects (visual sway)
- Lightning flashes (visual only or damage?)
- <FOR REVIEW: Can damage buildings/crops?>
- Duration: 60-180 seconds

**Drought (Challenging):**
- High evaporation rate (×3)
- No rain for extended period
- Crops fail, vegetation stressed
- <FOR REVIEW: Triggers famine events?>
- Duration: 5-15 minutes real-time

**Snow (If Winter):**
- Covers ground (white visual)
- Freezes moisture
- Slows vegetation growth to 0
- <FOR REVIEW: Villagers need heating?>
- Duration: Entire winter season

---

### Weather Triggers

**Natural:**
- Seasonal patterns (rain common in spring, rare in summer)
- Random events (10% chance per minute?)
- Climate state (high humidity → more rain)

**Player-Influenced:**
- Rain miracle → Forces rain weather for duration
- Fire miracle → Local heat, evaporation spike
- <FOR REVIEW: Can player summon drought? Clear skies?>

**<NEEDS DECISION: Fully natural, fully player-controlled, or hybrid?>**

---

## Wind System

**<NEEDS DESIGN: Does wind matter?>**

**Current:** `WindSystem.cs` file exists but **EMPTY**

### Option A: Visual Only
- Wind affects vegetation sway (animation)
- Particle effects (leaves, dust)
- No gameplay impact
- **Pros:** Atmospheric, cheap
- **Cons:** Decorative only

### Option B: Gameplay Impact

**Wind State:**
```
WindState : IComponentData (singleton) {
    float2 Direction;          // Vector (0-360°)
    float Strength;            // 0-1 (calm to gale)
    byte Type;                 // Breeze, Wind, Storm, Gale
}
```

**Effects:**
- Fire spread (wind direction affects fire miracle propagation)
- Projectile arc (slingshot affected by wind)
- Sound (howling when strong)
- <FOR REVIEW: Sailing if water/boats?>

**<NEEDS DECISION: Worth the complexity?>**

**<RECOMMENDATION: Skip wind for MVP (visual only if added)>**

---

## Scale & Scope

### Minimal Environment (MVP - Recommended)
- ❌ No biomes (single temperate terrain)
- ❌ No seasons (always summer)
- ❌ No dynamic vegetation (static trees)
- ❌ No weather (clear skies)
- ❌ No moisture grid (plants just exist)
- ✅ Static resource nodes only

**Pros:** Focus on core gameplay (god hand, villagers, resources)  
**Cons:** World feels static

---

### Simple Environment (v2.0)
- Single biome (temperate)
- Simple weather (rain/clear cycles)
- Basic moisture (affects growth)
- Static vegetation but moisture-dependent respawn

**Adds:**
- Rain miracle has visible effect
- Drought creates resource scarcity
- Strategic resource management

---

### Full Environment (v3.0+)
- Multiple biomes (3-5 types)
- Dynamic vegetation (growth, death, lifecycle)
- Full moisture simulation
- Seasonal cycles
- Weather variety (rain, storm, drought, snow)

**Adds:**
- Rich ecosystem simulation
- Biome-specific strategies
- Long-term planning (seasons)

---

## Design Questions

### Scope (Critical)
1. **Is environment simulation in MVP?** Or defer to v2.0+?
2. **If yes, how deep?** Static, simple, or full sim?
3. **Performance budget:** How many vegetation entities? Moisture cells?

### Biomes
4. Single biome or multiple?
5. Which biomes? (Temperate only? Add grasslands/mountains?)
6. Discrete zones or gradual transitions?

### Vegetation
7. Static or dynamic growth?
8. Harvestable regrowth or one-time?
9. Visual variety (species) or generic trees?

### Climate
10. Global or spatial?
11. Seasons or always same?
12. Temperature/humidity tracking or abstract?

### Moisture
13. Grid-based or simplified?
14. Grid resolution if implemented?
15. Affects gameplay or just visuals?

### Weather
16. Dynamic weather or static clear skies?
17. Can player control weather (beyond rain miracle)?
18. Regional or global weather?

---

## Example Scenarios (If Fully Implemented)

### Scenario: The Drought Crisis
> Summer drought begins. Moisture grid shows red (dry) across map.
> 
> Vegetation health drops. Trees stop growing, berries wilt.
> 
> Villagers struggle to gather food. Morale drops (hungry, thirsty).
> 
> You cast Rain miracle. Moisture increases. Grass turns green overnight.
> 
> Villagers grateful. Prayer surges. Crisis averted.
> 
> **Design Value:** Player intervention matters, miracles solve problems

---

### Scenario: Seasonal Cycles
> Spring: Vegetation grows rapidly. Villagers plant crops. Hopeful season.
> 
> Summer: Peak growth. Harvest berries. Hot, abundant.
> 
> Fall: Growth slows. Harvest crops. Prepare for winter (stockpile food/wood).
> 
> Winter: Dormant. Cold. Villagers consume stockpiles. Indoor time.
> 
> Spring returns: Cycle repeats.
> 
> **Design Value:** Long-term planning, rhythm, survival challenge

---

### Scenario: Biome Expansion
> Village starts in temperate forest. Abundant wood, moderate ore.
> 
> Population grows. Expand to grasslands (south).
> 
> Grasslands: Less wood, but excellent farming. Build farms, import wood.
> 
> Discover mountains (north): Abundant ore! Send miners. But harsh climate.
> 
> Each biome requires different villager allocation, different buildings.
> 
> **Design Value:** Strategic diversity, exploration, specialization

---

## Truth Source Status

**Currently Exist:**
- ❌ None - all environment systems are empty stubs

**Would Need (If Environment Implemented):**

**Minimal (Static World):**
```
// Just biome/terrain type per location
TerrainType : IComponentData (per area or grid cell)
```

**Simple (Weather + Moisture):**
```
ClimateState : IComponentData (singleton)
WeatherState : IComponentData (singleton)
MoistureGrid : IComponentData (singleton with blob grid)
```

**Full (Everything):**
```
BiomeRegion : IComponentData
ClimateState : IComponentData
WeatherState : IComponentData
MoistureGrid : IComponentData
VegetationGrowth : IComponentData (per plant)
SeasonState : IComponentData
WindState : IComponentData
```

---

## Generalization (For Space4X and Other Games)

**See:** `Docs/Concepts/Meta/Generalized_Environment_Framework.md` (to be created)

**Universal Patterns:**
- Biomes → Planet types (terran, arid, frozen, toxic)
- Climate → Planet atmosphere/conditions
- Moisture → Resource availability zones
- Vegetation → Harvestable features
- Weather → Planet events (storms, solar flares)

**Same simulation logic, different themes!**

---

## Related Documentation

- Truth Sources: `Docs/TruthSources_Inventory.md#climate-weather` (currently ❌ Not Implemented)
- Rain Miracle: `Docs/Concepts/Miracles/` (should interact with weather/moisture)
- Resource Nodes: Truth sources ✅ exist (GodgameResourceNode)
- Generalized Framework: `Docs/Concepts/Meta/Generalized_Environment_Framework.md` (next)

---

**For Designers:** CRITICAL - Decide environment scope before other systems (affects resource balance, miracle design, biome art)  
**For Product:** Environment sim is MAJOR scope item. Recommend: Skip MVP (static world), add v2.0 (simple weather), v3.0+ (full sim)  
**For Art:** If multi-biome, multiplies asset requirements significantly (3 biomes = 3× vegetation assets)

---

## Scope Recommendation

**MVP (Godgame v1.0):**
- ❌ Skip environment simulation entirely
- ✅ Static terrain (one biome: temperate)
- ✅ Static resource nodes (wood/ore)
- ✅ Always clear weather
- **Rationale:** Focus on core god game loop (hand, villagers, miracles)

**v2.0 (Polish Update):**
- ✅ Add simple weather (rain/clear cycles)
- ✅ Add basic moisture (affects resource respawn)
- ✅ Rain miracle integrates with weather
- **Rationale:** Adds dynamism without complexity

**v3.0+ (Expansion):**
- ✅ Multiple biomes (temperate, grasslands, mountains)
- ✅ Seasons
- ✅ Dynamic vegetation growth
- ✅ Full ecosystem simulation
- **Rationale:** Rich world for established game

---

**Last Updated:** 2025-10-31  
**Design Phase:** Exploring possibilities, no commitments yet  
**Next:** Create generalized framework for cross-project reuse




# Unified Editor & Sandbox System (Merged Concept)

**Status**: Concept
**Created**: 2025-11-27
**Framework**: PureDOTS (Game-Agnostic)
**Priority**: P1 - Core extensibility and development tool

---

## Vision

A **unified editor** that serves three audiences:
1. **Players** - Create custom maps, scenarios, game modes (Warcraft 3-style UGC)
2. **Advanced Modders** - Tweak gameplay rules, formulas, AI behavior
3. **Developers** - Modify engine settings, performance tune, debug systems

**Key Principle**: "Everything is data, everything is tweakable, everyone uses the same tool"

---

## The Spectrum of Control

### Level 1: Content Creation (Players)
**What**: Custom maps, triggers, entity stats
**Audience**: Casual players, content creators
**Safety**: Sandboxed, validated, curated

```
Examples:
- Design a tower defense map
- Create custom unit types
- Set up wave spawning triggers
- Paint terrain, place entities
```

### Level 2: Gameplay Modification (Advanced Modders)
**What**: Gameplay formulas, AI behavior, economic rules
**Audience**: Power users, modding community, aspiring designers
**Safety**: Validation with warnings, can break balance

```
Examples:
- Modify damage formula (linear vs quadratic scaling)
- Change resource gather rates
- Adjust AI aggression thresholds
- Customize buff/debuff calculations
```

### Level 3: Engine Configuration (Developers)
**What**: Physics settings, spatial partitioning, job system tuning
**Audience**: Framework developers, game teams, QA
**Safety**: No validation, can crash game, experts only

```
Examples:
- Adjust physics solver iterations
- Change spatial hash cell size
- Modify job batch sizes
- Toggle Burst compilation
```

---

## Unified UI Architecture

### Tabbed Interface

```
┌─────────────────────────────────────────────────────────────┐
│ PureDOTS Master Editor                          [Help] [•••] │
├─────────────────────────────────────────────────────────────┤
│ [Content] [Gameplay] [Engine] [Debug] [Performance]         │
├─────────────────────────────────────────────────────────────┤
│                                                              │
│   (Current tab content)                                      │
│                                                              │
│                                                              │
└─────────────────────────────────────────────────────────────┘
```

### Content Tab (Player Level)

```
┌─────────────────────────────────────────────────────────────┐
│ Content Editor                                   👤 Player   │
├─────────────────────────────────────────────────────────────┤
│ [Map] [Entities] [Triggers] [Terrain] [Scenarios]          │
├─────────────────────────────────────────────────────────────┤
│ ┌─────────────┐  ┌──────────────────────────────────────┐ │
│ │ Palettes    │  │ Map Viewport                         │ │
│ │             │  │                                      │ │
│ │ Units:      │  │  [3D/2D View]                       │ │
│ │  - Archer   │  │                                      │ │
│ │  - Mage     │  │  Drag & drop entities               │ │
│ │  - Tank     │  │  Paint terrain                       │ │
│ │             │  │  Define regions                      │ │
│ │ Terrain:    │  │                                      │ │
│ │  - Grass    │  │                                      │ │
│ │  - Water    │  │                                      │ │
│ │  - Mountain │  │                                      │ │
│ └─────────────┘  └──────────────────────────────────────┘ │
│                                                              │
│ ┌──────────────────────────────────────────────────────────┐│
│ │ Trigger Editor                                          ││
│ │                                                          ││
│ │ Trigger: "Spawn Wave 1"                                 ││
│ │   Event: [TimeElapsed ▼] 30 seconds                    ││
│ │   Condition: [Variable ▼] Wave < 5                     ││
│ │   Actions:                                               ││
│ │     - Create 10 units "Zombie" at "SpawnPoint"         ││
│ │     - Increment variable "Wave"                         ││
│ │                                                          ││
│ │ [+ Add Trigger] [Edit] [Delete]                        ││
│ └──────────────────────────────────────────────────────────┘│
└─────────────────────────────────────────────────────────────┘
```

### Gameplay Tab (Advanced Modder)

```
┌─────────────────────────────────────────────────────────────┐
│ Gameplay Rules Editor                         🔧 Advanced   │
├─────────────────────────────────────────────────────────────┤
│ [Combat] [Economy] [AI] [Progression] [Balancing]          │
├─────────────────────────────────────────────────────────────┤
│                                                              │
│ ▼ Combat Formulas                                           │
│   Damage Formula:                                            │
│     Current: Damage = Attack × (1 - Defense/(Defense+100)) │
│     ┌────────────────────────────────────────────────────┐ │
│     │ Formula Editor:                                    │ │
│     │                                                    │ │
│     │ Damage = [Attack ▼] × [Operator ▼]               │ │
│     │          (1 - [Defense ▼] / ([Defense ▼] + [100]))│ │
│     │                                                    │ │
│     │ Preview:                                           │ │
│     │   Attack=100, Defense=50 → Damage=66.7           │ │
│     │   Attack=100, Defense=100 → Damage=50            │ │
│     │   Attack=100, Defense=200 → Damage=33.3          │ │
│     │                                                    │ │
│     │ [Graph View] [Test Values] [Save Formula]        │ │
│     └────────────────────────────────────────────────────┘ │
│                                                              │
│   Defense Exponent: [1.0    ] (0.5-2.0)                    │
│     0.5 = Square root (diminishing returns)                 │
│     1.0 = Linear (current)                                  │
│     2.0 = Quadratic (very effective)                        │
│                                                              │
│   Critical Hit:                                              │
│     Chance:     [0.15  ] (0-1) = 15%                       │
│     Multiplier: [2.0   ] (1.5-5.0) = 200% damage          │
│                                                              │
│ ▼ Economy Settings                                           │
│   Resource Gather Rates:                                     │
│     Wood:  [1.0 per tick] [×10 Fast Test] [÷10 Slow Test] │
│     Stone: [0.5 per tick]                                   │
│     Gold:  [0.1 per tick]                                   │
│                                                              │
│   Resource Caps:                                             │
│     Wood:  [1000] [ ] Unlimited                            │
│     Stone: [500]  [ ] Unlimited                            │
│     Gold:  [100]  [ ] Unlimited                            │
│                                                              │
│ ▼ AI Behavior                                                │
│   Aggression:      [0.7    ] (0-1) Balanced                │
│   Flee Threshold:  [0.3    ] (0-1) Flee at 30% HP         │
│   Build Priority:  [Defense ▼] (Defense/Economy/Rush)      │
│                                                              │
│ [Export Mod Rules] [Import from Template] [Reset Defaults] │
└─────────────────────────────────────────────────────────────┘
```

### Engine Tab (Developer)

```
┌─────────────────────────────────────────────────────────────┐
│ Engine Configuration                         ⚠️  Developer   │
├─────────────────────────────────────────────────────────────┤
│ [Spatial] [Physics] [Jobs] [Memory] [Rendering] [AI]       │
├─────────────────────────────────────────────────────────────┤
│ ⚠️  WARNING: Changes can break game or cause crashes        │
│                                                              │
│ ▼ Spatial Partitioning                                       │
│   Grid Cell Size:      [====|====] 50 m (1-1000)           │
│   Max Per Cell:        [===|=====] 500 (10-10000)          │
│   Query Radius:        [==|======] 150 m (1-5000)          │
│   Rebuild Frequency:   [=|=======] 10 ticks (1-60)         │
│   [Apply] [Reset to Defaults]                              │
│                                                              │
│   Impact Preview:                                            │
│     Memory Usage:   12.5 MB → 6.8 MB (-45%)                │
│     Query Time:     0.5ms → 0.4ms (-20%)                   │
│     Precision:      Reduced (coarser queries)               │
│                                                              │
│ ▼ Physics Engine                                             │
│   Gravity Y:           [===|=====] -9.81 m/s² (-100 to +100)│
│   Fixed Delta Time:    [====|====] 0.016 s (0.001-0.1)     │
│   Max Velocity:        [==|======] 500 m/s (1-10000)       │
│   Solver Iterations:   [=|=======] 8 (1-50)                │
│   [Apply] [Reset]                                           │
│                                                              │
│   Impact Preview:                                            │
│     Frame Time:     +0ms (no change)                        │
│     Stability:      Same                                     │
│                                                              │
│ ▼ Job System                                                 │
│   Batch Size:          [====|====] 1000 (1-10000)          │
│   Max Threads:         [====|====] 8 (1-128)               │
│   [Apply] [Reset]                                           │
│                                                              │
│ ▼ Stress Tests                                               │
│   [Run Extreme Density Test]                                │
│   [Run Physics Chaos Test]                                  │
│   [Run Parallelism Scaling Test]                            │
│                                                              │
│ [Save Profile] [Load Profile] [Export Config]              │
└─────────────────────────────────────────────────────────────┘
```

### Debug Tab

```
┌─────────────────────────────────────────────────────────────┐
│ Debug & Inspector                                 🐛 Debug   │
├─────────────────────────────────────────────────────────────┤
│ [Runtime] [Entities] [Systems] [Triggers] [Profiler]       │
├─────────────────────────────────────────────────────────────┤
│                                                              │
│ ▼ Live Entity Inspector                                      │
│   Selected Entity: [Archer #42]                             │
│                                                              │
│   Components:                                                │
│     HealthComponent:                                         │
│       MaxHealth:     [100  ] (Live edit)                   │
│       CurrentHealth: [75   ] (Read-only)                   │
│                                                              │
│     CombatStats:                                             │
│       AttackDamage:  [25   ] (Live edit)                   │
│       Armor:         [10   ] (Live edit)                   │
│       AttackSpeed:   [1.0  ] (Live edit)                   │
│                                                              │
│     MovementModel:                                           │
│       MaxSpeed:      [5.0  ] (Live edit)                   │
│       Acceleration:  [2.0  ] (Live edit)                   │
│                                                              │
│   [Apply Changes] [Reset to Default] [Kill Entity]         │
│                                                              │
│ ▼ Trigger Debugger                                           │
│   Active Triggers: [3]                                       │
│     ✓ "Spawn Wave 1" - Executed at tick 1800               │
│     ⏸ "Spawn Wave 2" - Waiting for AllEnemiesDefeated      │
│     ⏹ "Victory" - Disabled                                  │
│                                                              │
│   [Step Through] [Pause Triggers] [Resume]                 │
│                                                              │
│ ▼ System Performance                                         │
│   MovementSystem:       0.8ms  [Graph]                      │
│   CombatSystem:         1.2ms  [Graph]                      │
│   TriggerRuntimeSystem: 0.3ms  [Graph]                      │
│                                                              │
│   [Detailed Profiler] [Export Metrics]                     │
└─────────────────────────────────────────────────────────────┘
```

### Performance Tab

```
┌─────────────────────────────────────────────────────────────┐
│ Performance Monitoring                          📊 Analytics │
├─────────────────────────────────────────────────────────────┤
│ [Overview] [Metrics] [Bottlenecks] [Comparisons]           │
├─────────────────────────────────────────────────────────────┤
│                                                              │
│ ▼ Current Session                                            │
│   Frame Time:       16.2ms (60 FPS) ✓                      │
│   Entity Count:     2,451                                    │
│   Active Triggers:  3                                        │
│   Memory Usage:     185 MB                                   │
│                                                              │
│   Frame Time Breakdown:                                      │
│     ████████░░ Simulation    8.5ms (52%)                    │
│     ██░░░░░░░░ Rendering     2.1ms (13%)                    │
│     ███░░░░░░░ Physics       3.2ms (20%)                    │
│     █░░░░░░░░░ Triggers      0.8ms (5%)                     │
│     █░░░░░░░░░ Other         1.6ms (10%)                    │
│                                                              │
│ ▼ Metrics Over Time (Last 60 seconds)                       │
│   [Graph: Frame time over time]                             │
│   [Graph: Entity count over time]                           │
│   [Graph: Memory usage over time]                           │
│                                                              │
│ ▼ Bottleneck Analysis                                        │
│   Top 5 Expensive Systems:                                   │
│     1. PathfindingSystem      2.8ms (Hot!)                  │
│     2. CombatResolutionSystem 2.1ms                          │
│     3. PhysicsStepSystem      1.9ms                          │
│     4. SpatialHashUpdate      1.2ms                          │
│     5. RenderingCulling       0.9ms                          │
│                                                              │
│   Recommendations:                                            │
│     ⚠️  PathfindingSystem: Reduce MaxSearchNodes (10000→5000)│
│     💡 Consider updating pathfinding less frequently         │
│                                                              │
│ [Export Report] [Compare to Baseline] [Start Recording]    │
└─────────────────────────────────────────────────────────────┘
```

---

## Unified Data Model

### Everything is a ConfigPackage

Whether you're tweaking entity stats or physics settings, it's all data:

```csharp
public struct ConfigPackage
{
    /// <summary>
    /// Which level of modification is this?
    /// </summary>
    public ConfigLevel Level;

    /// <summary>
    /// Content modifications (maps, entities, triggers)
    /// </summary>
    public BlobAssetReference<ContentConfig> ContentConfig;

    /// <summary>
    /// Gameplay modifications (formulas, rules, AI)
    /// </summary>
    public BlobAssetReference<GameplayConfig> GameplayConfig;

    /// <summary>
    /// Engine modifications (physics, jobs, spatial)
    /// </summary>
    public BlobAssetReference<EngineConfig> EngineConfig;
}

public enum ConfigLevel : byte
{
    Content = 0,    // Player mods (sandboxed, validated)
    Gameplay = 1,   // Advanced mods (warnings, can break balance)
    Engine = 2,     // Developer config (no validation, can crash)
}
```

### ContentConfig (Player Level)

```csharp
public struct ContentConfig
{
    /// <summary>
    /// Custom entities (from ModdingAndEditorFramework)
    /// </summary>
    public BlobAssetReference<ModEntityCatalog> CustomEntities;

    /// <summary>
    /// Trigger definitions (from ModdingAndEditorFramework)
    /// </summary>
    public BlobAssetReference<ModTriggerGraph> Triggers;

    /// <summary>
    /// Terrain/map data
    /// </summary>
    public BlobAssetReference<ModTerrainData> TerrainData;
}
```

### GameplayConfig (Advanced Modder)

```csharp
public struct GameplayConfig
{
    /// <summary>
    /// Combat formula modifications
    /// </summary>
    public BlobAssetReference<CombatFormulaConfig> CombatFormulas;

    /// <summary>
    /// Economy rate modifications
    /// </summary>
    public BlobAssetReference<EconomyConfig> EconomyRates;

    /// <summary>
    /// AI behavior modifications
    /// </summary>
    public BlobAssetReference<AIBehaviorConfig> AIBehavior;

    /// <summary>
    /// Progression curve modifications
    /// </summary>
    public BlobAssetReference<ProgressionConfig> ProgressionCurves;
}

public struct CombatFormulaConfig
{
    /// <summary>
    /// Damage formula type
    /// </summary>
    public DamageFormulaType FormulaType;

    /// <summary>
    /// Defense exponent (0.5-2.0)
    /// </summary>
    public float DefenseExponent;

    /// <summary>
    /// Critical hit chance (0-1)
    /// </summary>
    public float CritChance;

    /// <summary>
    /// Critical hit multiplier (1.5-5.0)
    /// </summary>
    public float CritMultiplier;

    /// <summary>
    /// Custom formula coefficients
    /// </summary>
    public BlobArray<FormulaCoefficient> CustomCoefficients;
}

public enum DamageFormulaType : byte
{
    Linear = 0,        // Damage = Attack × (1 - Defense/(Defense+100))
    Exponential = 1,   // Damage = Attack × exp(-Defense/200)
    Logarithmic = 2,   // Damage = Attack × log(1 + Defense)
    Custom = 255,      // User-defined coefficients
}
```

### EngineConfig (Developer)

```csharp
public struct EngineConfig
{
    /// <summary>
    /// Spatial partitioning settings (from FoundationalSettingsSandbox)
    /// </summary>
    public SpatialConfig Spatial;

    /// <summary>
    /// Physics engine settings
    /// </summary>
    public PhysicsConfig Physics;

    /// <summary>
    /// Job system settings
    /// </summary>
    public JobSystemConfig JobSystem;

    /// <summary>
    /// Memory management settings
    /// </summary>
    public MemoryConfig Memory;

    /// <summary>
    /// Rendering settings
    /// </summary>
    public RenderingConfig Rendering;
}

public struct SpatialConfig
{
    public float CellSize;
    public int MaxPerCell;
    public float QueryRadius;
    public int RebuildFrequency;
}

public struct PhysicsConfig
{
    public float3 Gravity;
    public float FixedDeltaTime;
    public float MaxVelocity;
    public int SolverIterations;
}
```

---

## Permission System

### User Roles

```csharp
public enum EditorPermission : byte
{
    Player = 0,      // Content creation only
    Modder = 1,      // Content + Gameplay rules
    Developer = 2,   // Content + Gameplay + Engine
    Admin = 255,     // Everything + dangerous operations
}
```

### Permission Checks

```csharp
public class EditorPermissionSystem
{
    public bool CanModify(ConfigLevel level, EditorPermission userPermission)
    {
        return userPermission >= (EditorPermission)level;
    }

    public void ValidateConfig(ConfigPackage config, EditorPermission userPermission)
    {
        // Content always allowed (sandboxed)
        if (config.Level == ConfigLevel.Content)
        {
            ValidateContentConfig(config.ContentConfig);
            return;
        }

        // Gameplay requires Modder permission
        if (config.Level == ConfigLevel.Gameplay)
        {
            if (userPermission < EditorPermission.Modder)
            {
                throw new PermissionException("Gameplay modification requires Modder permission");
            }
            ValidateGameplayConfig(config.GameplayConfig);
            return;
        }

        // Engine requires Developer permission
        if (config.Level == ConfigLevel.Engine)
        {
            if (userPermission < EditorPermission.Developer)
            {
                throw new PermissionException("Engine modification requires Developer permission");
            }
            // No validation (developer mode)
            return;
        }
    }
}
```

---

## Workflows

### Player Workflow: Create Tower Defense Map

1. Open **Content** tab
2. Paint terrain (path, build zones)
3. Place starting towers
4. Create triggers:
   - "Spawn Wave 1" - TimeElapsed 10s → Create 10 zombies
   - "Victory" - AllEnemiesDefeated + Wave==10 → Victory
5. Test in editor (play mode)
6. Share to Steam Workshop

**No gameplay or engine access needed** - everything is sandboxed

---

### Advanced Modder Workflow: Create "Glass Cannon" Mod

1. Open **Content** tab
2. Create custom unit "GlassCannon"
3. Open **Gameplay** tab
4. Modify combat formula:
   - CritChance: 0.5 (50% crit chance)
   - CritMultiplier: 5.0 (500% damage)
5. Modify unit stats via formula:
   - Attack: 200% of normal
   - Defense: 25% of normal (glass)
6. Test with AI (high risk, high reward gameplay)
7. Share mod with balance notes

**Uses gameplay customization** - changes formulas, not engine

---

### Developer Workflow: Performance Tuning

1. Open **Performance** tab
2. Identify bottleneck: PathfindingSystem (2.8ms)
3. Open **Engine** tab → AI section
4. Reduce MaxSearchNodes: 10000 → 5000
5. Open **Debug** tab
6. Run stress test (1000 units pathfinding)
7. Check **Performance** tab: PathfindingSystem now 1.4ms ✓
8. Save engine profile "Performance Mode"
9. Export config to JSON

**Uses engine tuning** - modifies core systems

---

## Integration Points

### With ModdingAndEditorFramework

**Content tab IS the modding editor**:
- Uses same `ModPackage`, `TriggerGraph`, `EntityCatalog`
- UI is just a friendly wrapper around data structures

### With FoundationalSettingsSandbox

**Engine tab IS the foundational sandbox**:
- Uses same `SpatialConfig`, `PhysicsConfig`, `JobSystemConfig`
- UI is just sliders/inputs for those settings

### With Scenario Runner

**Debug tab integrates scenario runner**:
- Can load scenarios
- Step through triggers
- Inspect entity state
- Export metrics

---

## Advantages of Unified System

### For Players
✅ **Gradual learning curve** - Start with simple map making, graduate to modding
✅ **Consistency** - Same UI paradigm across all tabs
✅ **Discoverability** - "Oh, there's a Gameplay tab? What's in there?"

### For Modders
✅ **Power** - Can customize everything from entities to formulas
✅ **Testing** - Debug tab lets you iterate quickly
✅ **Performance** - Profile tab shows impact of mods

### For Developers
✅ **Dogfooding** - Use the same tools as players
✅ **Efficiency** - One tool instead of scattered editor scripts
✅ **Debugging** - Live entity inspection, trigger stepping

### For Framework
✅ **Consistency** - All config is data (JSON + blobs)
✅ **Determinism** - Changes are data modifications, not code
✅ **Extensibility** - Easy to add new tabs/features

---

## Security & Safety

### Content Level (Player)
- ✅ Full validation
- ✅ Sandboxing (entity limits, trigger complexity)
- ✅ Auto-moderation (filter offensive content)
- ✅ Steam Workshop integration (community reports)

### Gameplay Level (Modder)
- ⚠️ Validation with warnings
- ⚠️ Can break game balance (that's the point)
- ⚠️ Can't crash (formulas clamped to safe ranges)
- ✅ Can be disabled by server (multiplayer)

### Engine Level (Developer)
- ❌ No validation (expert mode)
- ❌ Can crash game
- ❌ Can corrupt saves
- ⚠️ Only accessible with `-developer` flag or dev build

---

## Implementation Phases

### Phase 1: Merge Data Models (Week 1-2)
- [ ] Unify `ModPackage` + `FoundationalConfig` → `ConfigPackage`
- [ ] Define permission system
- [ ] Serialize to JSON + blobs

### Phase 2: Content Tab (Week 3-5)
- [ ] Map editor UI
- [ ] Entity palette
- [ ] Trigger editor
- [ ] Test: Create tower defense map

### Phase 3: Gameplay Tab (Week 6-7)
- [ ] Formula editor UI
- [ ] AI behavior tweaks
- [ ] Economy sliders
- [ ] Test: Create "glass cannon" mod

### Phase 4: Engine Tab (Week 8)
- [ ] Settings sliders (from FoundationalSettingsSandbox)
- [ ] Stress tests
- [ ] Profile save/load
- [ ] Test: Performance tuning

### Phase 5: Debug Tab (Week 9)
- [ ] Live entity inspector
- [ ] Trigger debugger
- [ ] System profiler
- [ ] Test: Debug custom scenario

### Phase 6: Performance Tab (Week 10)
- [ ] Metrics graphs
- [ ] Bottleneck analysis
- [ ] Recommendations
- [ ] Test: Identify bottlenecks

### Phase 7: Integration & Polish (Week 11-12)
- [ ] Permission enforcement
- [ ] Steam Workshop integration
- [ ] Tutorial/onboarding
- [ ] Test: Full workflow (player → modder → developer)

---

## Example: From Player to Developer

### Session 1 (Player)
Alex creates a simple tower defense map using **Content** tab.
- Places 10 towers, 3 spawn points
- Creates 5 wave triggers
- Shares on Steam Workshop

### Session 2 (Curious Player)
Alex discovers **Gameplay** tab.
- "What's this damage formula thing?"
- Clicks "Graph View", sees damage curve
- Adjusts crit chance from 15% → 25%
- "Whoa, crits are way more common now!"

### Session 3 (Advanced Modder)
Alex creates a "High Risk" mod.
- Modifies damage formula: Exponential scaling
- Increases crit multiplier to 3.0
- Reduces base health by 50%
- Creates "hardcore mode" gameplay variant
- Shares as separate mod

### Session 4 (Power User)
Alex's mod is popular but laggy.
- Opens **Performance** tab
- Sees: PathfindingSystem is bottleneck
- Opens **Engine** tab (requires `-developer` flag)
- Reduces MaxSearchNodes
- Re-tests, performance improves
- Shares performance config with mod

**Result**: Alex went from casual map maker to advanced modder to performance tuner, all in the same tool!

---

## Comparison with Other Engines

| Feature | Unity Editor | Unreal Editor | Warcraft 3 Editor | **PureDOTS Unified Editor** |
|---------|-------------|---------------|-------------------|----------------------------|
| **Player Access** | No | No | Yes | **Yes** |
| **Runtime Editing** | No | Limited | No | **Yes** |
| **ECS Native** | No | No | N/A | **Yes** |
| **Gradual Progression** | No | No | No | **Yes** (Player→Modder→Dev) |
| **Determinism** | No | No | Mostly | **Perfect** |
| **Modding Safety** | Manual | Manual | Limited | **Automatic** (sandboxing) |
| **Performance Tuning** | Separate tools | Separate tools | No | **Integrated** |

---

## Summary

**Unified Editor merges**:
- **Modding Editor** (Warcraft 3-style UGC)
- **Foundational Sandbox** (runtime engine tweaking)
- **Debug Tools** (inspector, profiler, trigger debugger)

**Three levels**:
1. **Content** (Players) - Maps, entities, triggers
2. **Gameplay** (Modders) - Formulas, AI, rules
3. **Engine** (Developers) - Physics, jobs, spatial

**Benefits**:
- ✅ Consistent UI and paradigm
- ✅ Gradual learning curve
- ✅ Dogfooding (devs use same tools as players)
- ✅ Everything is data (deterministic, serializable)

**Result**: A single tool that serves everyone from casual map makers to expert performance tuners! 🛠️

---

**See Also**:
- [ModdingAndEditorFramework.md](ModdingAndEditorFramework.md) - Warcraft 3-style editor (Content level)
- [FoundationalSettingsSandbox.md](FoundationalSettingsSandbox.md) - Engine tweaking (Engine level)
- [Godgame CustomGameModding](../../../Assets/Projects/Godgame/Docs/Systems/CustomGameModding.md)
- [Space4X CustomGameModding](../../../Assets/Projects/Space4X/Docs/Systems/CustomGameModding.md)

**Status**: Concept - Ready for Prototyping
**Next Steps**:
1. Prototype unified `ConfigPackage` data model
2. Build basic Content tab (map editor)
3. Add Gameplay tab (formula editor)
4. Test full workflow (player → modder → developer progression)

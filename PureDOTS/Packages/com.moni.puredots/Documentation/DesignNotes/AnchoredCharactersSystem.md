# Anchored Characters System

**Status**: Concept
**Created**: 2025-11-26
**Applies To**: Space4X, Godgame (potentially LastLightVR)
**Priority**: P1 - Core player attachment mechanic

---

## Concept

**Anchored Characters** are entities that players care about - favorite captains, beloved villagers, veteran defenders - who are given special treatment by the engine to ensure they remain fully simulated and rendered regardless of distance, LOD settings, or normal culling rules.

### Core Fantasy

> "I want to know that Captain Aria is actually commanding that distant fleet, not just an abstract registry entry. When I zoom out to see the battle, I want to see HER ship, HER actions, even if she's across the galaxy."

### Problem Being Solved

**Standard ECS/DOTS optimization**:
- Entities far from camera → culled from rendering
- Entities outside simulation radius → despawned or simplified
- Distant entities → low LOD or abstract simulation

**Player attachment problem**:
- Player spends 20 hours with Captain Aria
- Develops emotional connection
- Zooms out to strategic view
- Aria's ship becomes a dot or disappears entirely
- **Immersion broken** - "Is she even real? Or just a stat in a database?"

**Anchored characters solve this by**:
- Always rendering at full quality (or reduced but still visible)
- Always simulating at full detail (AI, stats, actions)
- Always updating (even when off-screen)
- Making the world feel persistent and real

---

## Use Cases by Project

### Space4X: Anchored Captains/Aces

**Who gets anchored**:
- Player's favorite ship captains
- Legendary ace pilots
- Notable NPCs (rival admirals, allies, mentors)
- Player's "flagship" carrier

**What it means**:
- Captain Aria's carrier is always visible in fleet battles
- Her ship renders at medium-high LOD even across the star system
- Her AI decisions happen in real-time (not abstracted)
- Her crew morale/alignment simulates continuously
- If she's in danger, player can see it from strategic view

**Example**:
```
Strategic Map View (normally shows icons/dots):
  Standard Fleet: [•] [•] [•] (abstract dots)
  Anchored Captain Aria: [Detailed Carrier Model with lights/effects]

Player can:
  - See Aria's ship maneuvering
  - Watch her launch fighters
  - Notice she's taking damage
  - Feel urgency: "I need to help her!"
```

### Godgame: Anchored Villagers/Heroes

**Who gets anchored**:
- Player's favorite villagers
- Village leaders (chief, master smith, high priest)
- Heroes who survived major events
- NPCs with significant "Tales of the Fallen" potential

**What it means**:
- Master Smith Borin is always visible working the forge
- His hammer swings animate even when player looks at distant village
- His mood/health/hunger simulates continuously
- If a raid happens, he reacts in real-time
- Player can check on him anytime and see actual activity

**Example**:
```
Zoomed Out Village View (normally shows abstract):
  Standard Villagers: [Abstract crowd/icons]
  Anchored Hero Borin: [Full character model, actual animation]

Player sees:
  - Borin crafting at the forge (real animation)
  - His apprentice learning beside him (full simulation)
  - Smoke from his forge (actual VFX)
  - If demons attack, Borin grabs his hammer and fights (real AI)
```

### LastLightVR: Anchored Defenders

**Who gets anchored**:
- Veteran defenders who survived multiple Last Stands
- Named heroes with "Tales of the Fallen"
- Player's right-hand NPCs
- Defenders with strong emotional connection

**What it means**:
- Veteran Guardian Kael always renders in full VR detail
- His combat animations don't simplify at distance
- His AI continuously evaluates threats
- Player can glance across the battlefield and see Kael actually fighting
- Creates attachment: "I can't let Kael die - look at him hold that line!"

---

## Technical Architecture

### Component Structure

```csharp
/// <summary>
/// Marks this entity as "anchored" - exempt from normal culling/LOD/despawn rules.
/// </summary>
public struct AnchoredCharacter : IComponentData
{
    /// <summary>
    /// Priority level (higher = more important if we need to limit anchored count)
    /// 0 = normal anchored
    /// 1-5 = increasingly important
    /// 10 = player flagship/avatar
    /// </summary>
    public byte Priority;

    /// <summary>
    /// Who anchored this? (for multiplayer - which player cares about this entity?)
    /// Entity.Null = all players care (major NPCs)
    /// </summary>
    public Entity AnchoredBy;

    /// <summary>
    /// Reason/tag for why anchored (debug/telemetry)
    /// 0 = player favorited
    /// 1 = story-critical NPC
    /// 2 = legendary/veteran status
    /// 3 = flagship/leader
    /// </summary>
    public byte AnchorReason;

    /// <summary>
    /// When was this character anchored? (for "cooling off" if player un-favorites)
    /// </summary>
    public uint AnchoredAtTick;
}

/// <summary>
/// Optional: reduced detail level for anchored characters at extreme distance.
/// Still rendered, but maybe lower LOD than full quality.
/// </summary>
public struct AnchoredRenderingOverride : IComponentData
{
    /// <summary>
    /// Minimum LOD level this character will use (0 = full detail always)
    /// </summary>
    public byte MinLODLevel; // 0 = full, 1 = medium, 2 = low (but never culled)

    /// <summary>
    /// Should this character cast shadows even at distance?
    /// </summary>
    public bool AlwaysCastShadows;

    /// <summary>
    /// Should VFX/particles be active even when far?
    /// </summary>
    public bool AlwaysRenderVFX;
}

/// <summary>
/// Optional: buffer tracking all characters this player has anchored.
/// </summary>
[InternalBufferCapacity(8)]
public struct PlayerAnchoredCharacterBuffer : IBufferElementData
{
    public Entity AnchoredEntity;
    public uint AnchoredAtTick;
    public byte Priority;
}
```

### System Integration

**Rendering Pipeline**:
```csharp
// In your culling system
public partial class CullingSystem : SystemBase
{
    protected override void OnUpdate()
    {
        // Normal entities: cull by frustum, distance, etc.
        Entities
            .WithNone<AnchoredCharacter>()
            .ForEach((Entity e, in LocalTransform transform) =>
            {
                // Standard culling logic
                if (!frustum.Contains(transform.Position))
                {
                    Cull(e);
                }
            }).Run();

        // Anchored entities: never cull, but optionally reduce LOD
        Entities
            .WithAll<AnchoredCharacter>()
            .ForEach((Entity e,
                      in LocalTransform transform,
                      in AnchoredRenderingOverride renderOverride) =>
            {
                // ALWAYS render (never cull)
                // But adjust LOD based on distance if configured
                float distance = math.distance(transform.Position, cameraPos);
                byte lodLevel = CalculateLODForAnchoredCharacter(distance, renderOverride.MinLODLevel);

                SetLOD(e, lodLevel);
                SetRenderingEnabled(e, true); // Always true
            }).Run();
    }
}
```

**Simulation Pipeline**:
```csharp
// In your AI update system
public partial class AIUpdateSystem : SystemBase
{
    protected override void OnUpdate()
    {
        // Normal entities: only update if in simulation radius
        Entities
            .WithNone<AnchoredCharacter>()
            .ForEach((ref AIState ai, in LocalTransform transform) =>
            {
                if (IsWithinSimulationRadius(transform.Position))
                {
                    UpdateAI(ref ai);
                }
            }).Schedule();

        // Anchored entities: ALWAYS update AI
        Entities
            .WithAll<AnchoredCharacter>()
            .ForEach((ref AIState ai) =>
            {
                UpdateAI(ref ai);
            }).Schedule();
    }
}
```

**Despawn Prevention**:
```csharp
// In your entity lifecycle system
public partial class EntityDespawnSystem : SystemBase
{
    protected override void OnUpdate()
    {
        // Despawn entities far from simulation center
        Entities
            .WithNone<AnchoredCharacter>() // Don't despawn anchored!
            .ForEach((Entity e, in LocalTransform transform) =>
            {
                if (ShouldDespawn(transform.Position))
                {
                    ecb.DestroyEntity(e);
                }
            }).Schedule();

        // Anchored entities are NEVER despawned (unless explicitly killed)
    }
}
```

---

## Player Interaction / Authoring

### How Players Anchor Characters

**Option 1: Explicit "Favorite" Button**:
```
Space4X:
  - Right-click captain portrait → "Anchor this captain"
  - Star icon appears on UI
  - Captain is now always rendered/simulated

Godgame:
  - Select villager → "Mark as Important"
  - Heart icon appears above their head
  - Villager is now always visible
```

**Option 2: Automatic Based on Player Attention**:
```
If player:
  - Selects an entity 5+ times in a session
  - Spends 30+ seconds watching them
  - Names them (custom naming)
  - Promotes them (e.g., villager → guard captain)
Then: Auto-anchor
```

**Option 3: Story-Driven**:
```
- Tutorial captain is always anchored
- Village chief is always anchored
- NPCs with active quests are anchored
- Characters in cutscenes are anchored during cutscene
```

### Limits & Budget

**Performance consideration**: Can't anchor infinite entities

**Proposed limits**:
```csharp
public struct AnchoredCharacterBudget : IComponentData
{
    public byte MaxAnchoredPerPlayer; // e.g., 10
    public byte CurrentAnchoredCount;

    // When player tries to anchor 11th character:
    // - Prompt: "You can only anchor 10 characters. Remove one?"
    // - Or: automatically un-anchor least-recently-viewed
}
```

**Smart budgeting**:
- Priority 10 (flagship): Always anchored, doesn't count toward budget
- Priority 5+ (story NPCs): Always anchored, doesn't count toward budget
- Priority 0-4 (player favorites): Counts toward budget

---

## Performance Impact & Mitigations

### Concerns

**Rendering**:
- Anchored characters bypass frustum culling
- Could render 10+ entities even when off-screen
- VFX/particles always active = GPU cost

**Simulation**:
- Anchored characters always tick AI
- Could have 10+ full AI updates even at strategic zoom
- Pathfinding, combat, inventory always active = CPU cost

**Memory**:
- Anchored characters never despawn
- Could accumulate memory if not careful

### Mitigations

**1. Reduced LOD for Anchored (but still visible)**:
```
Distance < 50m:  LOD 0 (full detail)
Distance 50-200m: LOD 1 (medium) - Anchored characters use this
Distance 200m+:   LOD 2 (low) - Anchored characters use this
Normal entities:  Culled entirely at 200m+
```
Anchored characters still render, but use cheaper models at distance.

**2. Budget Enforcement**:
- Hard cap: 10 anchored per player (configurable)
- Auto-un-anchor least-viewed if exceeded
- UI warning when approaching limit

**3. Partial Simulation at Extreme Distance**:
```csharp
// Even anchored characters can simplify AI at extreme distance
if (distance > 500f)
{
    // Full AI, but update at 1/4 frequency
    UpdateAISlowly(ref ai);
}
else
{
    // Normal full AI update every frame
    UpdateAI(ref ai);
}
```

**4. Smart VFX Culling**:
```csharp
// Anchored character renders, but VFX only if in frustum or close
if (frustum.Contains(position) || distance < 100f)
{
    EnableVFX(entity);
}
else
{
    DisableVFX(entity); // Still render model, but no particles
}
```

**5. Testing & Telemetry**:
```csharp
// Track performance cost of anchored characters
public struct AnchoredCharacterTelemetry : IComponentData
{
    public float TotalRenderCostMs; // How much time rendering anchored
    public float TotalSimCostMs;    // How much time simulating anchored
    public int TotalAnchoredCount;  // How many exist

    // If cost too high, warn player or auto-reduce quality
}
```

---

## Integration with Existing Systems

### Rendering (Entities Graphics / Hybrid Renderer)

**Challenge**: Entities Graphics auto-culls based on camera frustum

**Solution**: Custom render filter
```csharp
// Add to RenderFilterSettings
var filter = new RenderFilterSettings
{
    Layer = 0,
    RenderingLayerMask = RenderingLayerMask.Default,
    MotionMode = MotionVectorGenerationMode.Camera,
    ShadowCastingMode = ShadowCastingMode.On,
    ReceiveShadows = true,
    StaticShadowCaster = false,

    // NEW: Anchored characters ignore frustum culling
    IgnoreFrustumCulling = HasComponent<AnchoredCharacter>(entity)
};
```

Alternatively: Move anchored characters to a special rendering layer that never culls.

### Spatial Partitioning

**Challenge**: Spatial grids often only query nearby entities

**Solution**: Anchored characters exist in a separate "always-query" list
```csharp
// Normal spatial query
var nearbyEntities = SpatialGrid.Query(position, radius);

// Anchored characters are ALWAYS included
nearbyEntities.AddRange(AnchoredCharacterRegistry.GetAll());

// Now systems that search for targets/allies always find anchored characters
```

### Registry System (PureDOTS)

**Good news**: Registry already tracks entities persistently!

**Integration**:
- Anchored characters are flagged in registry
- Registry ensures they stay loaded/simulated
- When player favorites a captain, mark in CarrierRegistry:
  ```csharp
  registry.SetFlag(captainEntity, RegistryFlags.PlayerAnchored);
  ```

---

## UX Design

### Visual Indicators

**In-World**:
```
Anchored Character:
  - Subtle glow/outline (configurable color per player in multiplayer)
  - Icon above head (star, heart, crown, etc.)
  - Name tag always visible (not just on mouseover)
```

**In UI**:
```
Character List:
  [★] Captain Aria (Anchored)
  [ ] Captain Brek
  [ ] Captain Cel

★ = Anchored (golden star)
Clicking star = Toggle anchor
```

**Strategic View**:
```
Normal Fleet Icon: [•]
Anchored Fleet Icon: [★] (larger, more detailed, named)
```

### Tooltips / Onboarding

**Tutorial**:
```
"You've spent a lot of time with Captain Aria.
 Would you like to ANCHOR her?

 Anchored characters:
 ✓ Always visible, even across the galaxy
 ✓ Fully simulated at all times
 ✓ Never despawn or simplify

 You can anchor up to 10 characters."
```

**Settings**:
```
Anchored Characters Settings:
  [x] Enable anchored characters
  [ ] Auto-anchor frequently-selected characters
  [ ] Auto-anchor story NPCs
  Max anchored: [10] (slider 5-20)
  Anchored render quality: [Medium] (Low/Medium/High)
```

---

## Extension Request for PureDOTS

This should be a **PureDOTS extension** because:
- ✅ Game-agnostic (Space4X, Godgame, LastLightVR all benefit)
- ✅ Reusable pattern (any game with character attachment)
- ✅ Framework-level concern (rendering, simulation, culling)

### Proposed Extension Request

**File**: `PureDOTS/Docs/ExtensionRequests/2025-11-26-anchored-characters-system.md`

**Summary**:
> Add `AnchoredCharacter` component and supporting systems to exempt player-favorite entities from culling, despawning, and LOD simplification. Enables games to keep beloved characters fully rendered and simulated regardless of distance.

**Components**:
- `AnchoredCharacter` - Core tag + metadata
- `AnchoredRenderingOverride` - Rendering quality settings
- `PlayerAnchoredCharacterBuffer` - Track player's anchored entities

**Systems**:
- Integration with culling (never cull anchored)
- Integration with LOD (reduce but never eliminate)
- Integration with despawn (never despawn anchored)
- Integration with spatial queries (always include anchored)

**Budget management**:
- Max anchored per player (configurable)
- Telemetry for performance impact

---

## Examples of Emergent Gameplay

### Space4X Example

**Scenario**: Player anchors Captain Aria (their favorite)

**Session 1**:
- Aria commands a mining expedition across the galaxy
- Player zooms to strategic view to manage colonies
- Normally, Aria's ship would be an abstract dot
- BUT: Aria is anchored → player sees her actual carrier model
- Aria encounters pirates
- Player notices from strategic view: "Aria's in trouble!"
- Player zooms in, sends reinforcements
- **Emotional payoff**: "I saved her because I could SEE her"

**Session 10**:
- Aria is now a legendary admiral with 20 battles survived
- Player always anchors her
- In final climactic battle, Aria leads the fleet
- Player sees her ship fighting across the entire battlefield
- Aria's carrier is destroyed
- Player sees the explosion, even from strategic view
- **Emotional impact**: "I watched her die. She was always THERE. Now she's gone."

### Godgame Example

**Scenario**: Player anchors Borin, the master smith

**Outcome**:
- Borin is always visible at the forge
- Player checks on him regularly
- Develops parasocial relationship: "How's Borin doing?"
- Raid happens, demons attack smithy
- Player sees Borin grab his hammer and fight
- Borin survives, becomes legend
- Player feels: "Borin is MY smith. I protected him."

**Contrast with non-anchored**:
- Generic Smith #47
- Player doesn't know who's at the forge
- Raid happens, smith dies
- Player reaction: "Oh, a smith died. I'll make another."
- NO emotional connection

---

## Open Questions

### 1. Multiplayer Conflicts

**Problem**: In 4-player co-op, each player can anchor 10 characters. That's 40 anchored entities!

**Solutions**:
- **Option A**: Shared budget (10 anchored total, team decides)
- **Option B**: Per-player rendering (you only see YOUR anchored in full detail)
- **Option C**: Priority system (if budget exceeded, lowest priority dropped)

### 2. Anchor "Cooldown"

**Problem**: Player rapidly anchors/un-anchors to bypass budget

**Solution**: Cooldown timer?
```
Un-anchor character → 5 minute cooldown before can re-anchor
Prevents anchor-spam
```

### 3. Automatic Un-Anchoring

**When to auto-un-anchor?**
- Character dies (obviously)
- Player hasn't viewed character in 30+ minutes?
- Character becomes irrelevant (retired, left service, etc.)?

**Notification**:
```
"Captain Aria has retired.
 She is no longer anchored.
 Would you like to anchor someone else?"
```

### 4. Scaling to 100+ Anchored in Endgame

**Scenario**: Late-game player wants to anchor their entire legendary fleet (50 captains)

**Problem**: Performance can't handle it

**Solutions**:
- Hard cap (can't anchor more than 20 ever)
- Soft cap (warning at 10, allowed up to 20 with performance hit)
- Tiered anchoring:
  - Tier 1 (10 slots): Full rendering, full simulation
  - Tier 2 (20 slots): Medium rendering, full simulation
  - Tier 3 (30 slots): Low rendering, simplified simulation

---

## Implementation Phases

### Phase 1: Core Components & Rendering (Week 1-2)

- [ ] Create `AnchoredCharacter` component
- [ ] Create `AnchoredRenderingOverride` component
- [ ] Modify culling system to never cull anchored
- [ ] Test: Anchored character visible across entire map

### Phase 2: Simulation Integration (Week 3)

- [ ] Modify AI systems to always update anchored
- [ ] Modify despawn system to never despawn anchored
- [ ] Add to spatial queries (always include anchored)
- [ ] Test: Anchored character AI runs even off-screen

### Phase 3: Player Interaction (Week 4)

- [ ] UI button to anchor/un-anchor
- [ ] Visual indicators (star icon, glow, etc.)
- [ ] Budget enforcement (max 10)
- [ ] Test: Player can anchor favorite character

### Phase 4: Polish & Optimization (Week 5-6)

- [ ] LOD reduction at distance (still visible, but cheaper)
- [ ] VFX culling (model renders, but particles off when far)
- [ ] Telemetry (track performance cost)
- [ ] Settings (enable/disable, max count, render quality)
- [ ] Tutorial/onboarding

### Phase 5: Game-Specific Integration

**Space4X**:
- [ ] Anchor captains from captain roster UI
- [ ] Anchor on promotion (conscript → captain)
- [ ] Story captains auto-anchored

**Godgame**:
- [ ] Anchor villagers from villager panel
- [ ] Anchor village leaders (chief, master smith, etc.)
- [ ] Heroes who survive miracles auto-anchored

**LastLightVR**:
- [ ] Anchor defenders from loadout screen
- [ ] Veterans auto-anchored after 3+ last stands
- [ ] Named defenders auto-anchored

---

## Success Metrics

### Technical Success
- ✅ Anchored characters render 100% of the time (no culling)
- ✅ Anchored characters simulate 100% of the time (no despawn)
- ✅ Performance impact < 5ms per 10 anchored characters
- ✅ LOD system reduces cost at distance

### Player Experience Success
- ✅ Players anchor their favorite characters (>50% use feature)
- ✅ Players report feeling attached to anchored characters
- ✅ Player quotes like: "I love that I can always see Captain Aria"
- ✅ Emotional moments: "I watched my favorite die. It hurt."

### Design Success
- ✅ Anchored characters create emergent stories
- ✅ Players feel world is persistent and real
- ✅ Budget system prevents performance abuse
- ✅ Feature enhances (not detracts from) core gameplay

---

## Related Systems

- **Registry System** - Anchored characters should be flagged in registry
- **Telemetry** - Track which characters get anchored (data-driven design)
- **Save/Load** - Anchored status must persist across saves
- **Multiplayer** - Each player's anchored list syncs to server

---

## See Also

- [PureDOTS Registry System](../Runtime/Registry/RegistryUtilities.cs)
- [Entities Graphics Documentation](https://docs.unity3d.com/Packages/com.unity.entities.graphics@latest)
- [LOD Group Documentation](https://docs.unity3d.com/Manual/LevelOfDetail.html)

---

**Created**: 2025-11-26
**Status**: Concept - Ready for Extension Request
**Next Steps**:
1. Review with team
2. File PureDOTS extension request
3. Prototype in Space4X first (easiest to test)
4. Expand to Godgame once validated

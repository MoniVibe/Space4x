# Probabilistic Simulation for Non-Anchored Entities

**Status**: Concept Analysis
**Created**: 2025-11-26
**Related**: [AnchoredCharactersSystem.md](AnchoredCharactersSystem.md)
**Purpose**: Evaluate performance vs realism trade-offs for distant entity simulation

---

## Concept

**Core Idea**: Entities outside the "anchor influence" (non-anchored, distant from player) perform their tasks **probabilistically** rather than deterministically to save performance.

**Example**:
```
Anchored Character (Always Deterministic):
  - Smith Borin swings hammer every 2 seconds
  - Creates 1 sword per 10 swings (100% consistent)
  - Fully simulated AI, pathfinding, animation

Non-Anchored Character (Probabilistic):
  - Smith #47 has 20% chance to "attempt craft" each tick
  - If attempt succeeds, has 80% chance to produce sword
  - AI simplified or skipped
  - Animation abstract or frozen
  - Result: ~same average output, 90% less CPU
```

---

## Pros ✅

### 1. **Massive Performance Savings**

**CPU Reduction**:
```
Deterministic (100 non-anchored smiths):
  - 100 AI updates per frame
  - 100 pathfinding queries
  - 100 animation states
  - 100 inventory checks
  ≈ 10ms per frame

Probabilistic (100 non-anchored smiths):
  - 20 "roll dice" checks per frame (20% chance)
  - 16 actual craft attempts (80% success)
  - No AI, pathfinding, or animation
  ≈ 1ms per frame
```

**90% CPU savings** for distant entities!

**Scalability**:
- Can simulate 1000+ non-anchored entities for cost of 100 deterministic
- Enables larger game worlds
- More NPCs without performance hit

### 2. **Player Doesn't Notice (When Done Right)**

**Perception**: Players rarely watch distant, non-anchored entities long enough to notice probabilistic behavior.

**Example (Godgame)**:
```
Player zoomed out, watching village overview:
  - 50 villagers working farms (not anchored)
  - Probabilistic: Each has 10% chance to "harvest" per tick
  - Result: ~5 harvests per tick (on average)
  - Looks identical to deterministic 50 harvests over 10 ticks
  - Player sees: "farmers are working" ✓
```

**Invisible optimization**: If output rates match, player experience is identical.

### 3. **Statistical Equivalence Over Time**

**Law of Large Numbers**: Over time, probabilistic simulation converges to expected value.

**Example (Space4X)**:
```
Deterministic Mining:
  - 10 miners each mine 5 ore/min
  - Total: 50 ore/min (exact)

Probabilistic Mining:
  - 10 miners each have 20% chance to mine 25 ore
  - Expected value: 10 * 0.20 * 25 = 50 ore/min
  - Over 10 minutes: ~500 ore (±10% variance)
  - Close enough for most gameplay
```

**Trade-off**: Short-term variance for long-term equivalence.

### 4. **Natural Abstraction of Distance**

**Philosophical fit**: "Things far away are less certain."

**Realism argument**:
- In real life, you don't know exact state of distant things
- Probabilistic = "I don't know if the smith finished that sword yet, but probably"
- Fits mental model of distant events

**Anchored characters exception**:
- Characters you care about → deterministic (certain)
- Characters you don't → probabilistic (uncertain)
- Reinforces anchor importance!

### 5. **Easier Multiplayer Synchronization**

**Challenge**: Deterministic simulation requires perfect sync of every entity.

**Probabilistic advantage**:
```
Deterministic:
  - Client A simulates 1000 villagers exactly
  - Client B simulates 1000 villagers exactly
  - Must match perfectly or desync
  - High bandwidth (sync all 1000 states)

Probabilistic:
  - Clients share random seed
  - Each client rolls same dice (deterministic RNG)
  - Only sync anchor-influenced entities exactly
  - Low bandwidth (sync 10-20 anchored, rest is RNG)
```

**Caveat**: Only works if RNG is deterministic and seeded identically.

### 6. **Simplifies "Off-Screen" Events**

**Use case**: Player can't see distant village, but it should still "exist."

**Probabilistic enables abstract simulation**:
```
Village out of view:
  - No rendering (culled)
  - No AI (probabilistic tasks)
  - No pathfinding (frozen positions)
  - But still produces resources (probabilistic rolls)
  - Feels alive without full simulation cost
```

**Alternative without probabilistic**: Must choose between:
- Full simulation (expensive)
- No simulation (village frozen, breaks immersion)
- Probabilistic (cheap + immersive)

---

## Cons ❌

### 1. **Breaks Determinism (Critical for Some Games)**

**Problem**: Probabilistic simulation is inherently **non-deterministic** unless RNG is carefully controlled.

**Impact on Replay Systems**:
```
Deterministic (Rewind/Replay safe):
  - Tick 100: Smith creates sword
  - Rewind to Tick 99, replay to 100
  - Result: Smith still creates sword (exact same)

Probabilistic (Rewind/Replay unsafe):
  - Tick 100: 20% roll succeeds → Smith creates sword
  - Rewind to Tick 99, replay to 100
  - Different RNG state → 20% roll fails → No sword!
  - Desync!
```

**PureDOTS uses rewind extensively** - this could be a dealbreaker.

**Mitigation**: Use deterministic RNG seeded per entity + tick.

### 2. **Short-Term Variance Can Break Gameplay**

**Problem**: Probabilistic outcomes vary in short term, which can feel unfair or break balance.

**Example (Space4X mining)**:
```
Player sends fleet to mine asteroid:
  - Expected: 500 ore over 10 minutes
  - Deterministic: Always 500 ore
  - Probabilistic: Could be 300-700 ore (variance)

If player needs exactly 500 ore for upgrade:
  - Deterministic: Reliable, player can plan
  - Probabilistic: "RNG screwed me" feeling
```

**Player frustration**: "Why did my mine produce 30% less this time?"

**Mitigation**:
- Use narrower variance (±5% instead of ±30%)
- Apply smoothing (running average)
- Only use for non-critical gameplay

### 3. **Anchor Influence Creates "Simulation Bubbles"**

**Problem**: Reality changes based on what's anchored/visible.

**Example (Godgame)**:
```
Scenario A: Player watches Smith Borin (anchored)
  - Borin deterministically crafts 10 swords
  - Result: 10 swords in 10 minutes

Scenario B: Player looks away (Borin not anchored)
  - Borin probabilistically crafts ~10 swords
  - Actual result: 7 swords (bad RNG)

Player notices: "Wait, Borin made fewer swords when I wasn't watching?"
```

**Immersion break**: "The world only exists when I look at it."

**Mitigation**:
- Don't apply probabilistic to anchored characters
- Use tight variance (±10% max)
- Smooth results over longer time windows

### 4. **Exploitable by Players (Metagaming)**

**Problem**: If players know simulation is probabilistic, they can game it.

**Example (Space4X)**:
```
Player discovers:
  - Anchored miners are deterministic (reliable)
  - Non-anchored miners are probabilistic (variable)

Strategy: Anchor all miners → always get max output
```

**Breaks anchor budget**: Players anchor for mechanical advantage, not emotional attachment.

**Mitigation**:
- Don't expose the mechanic (invisible optimization)
- Make variance small enough it doesn't matter
- Limit anchor budget strictly

### 5. **Complex to Implement Correctly**

**Challenge**: Probabilistic simulation that "feels right" is hard.

**Issues**:
```
Too random:
  - Output swings wildly
  - Feels unfair
  - Players notice and complain

Too deterministic:
  - No performance savings
  - Defeats the purpose

Just right:
  - Narrow variance (±5-10%)
  - Deterministic RNG (rewind-safe)
  - Smooth over time
  - Invisible to player
```

**Development cost**: More complex than full simulation or no simulation.

### 6. **Debugging Nightmares**

**Problem**: Probabilistic bugs are **hard to reproduce**.

**Scenario**:
```
Bug report: "Smith sometimes doesn't craft sword"

Deterministic world:
  - Reproduce: Run to tick 1000
  - Smith always fails at tick 1000
  - Fix: Obvious bug in craft logic

Probabilistic world:
  - Reproduce: Run to tick 1000
  - Smith succeeds (RNG different)
  - Run again: Smith fails
  - Root cause: ???
  - Fix: Add deterministic RNG logging, replay, debug
```

**Testing difficulty**: Probabilistic outcomes require statistical testing (1000+ runs).

### 7. **Rewind/Replay Compatibility Issues**

**PureDOTS Rewind System**: Allows players to rewind time and replay.

**Deterministic requirement**: Replay must produce **identical** results.

**Probabilistic conflict**:
```
Tick 1000: Player rewinds

Deterministic world:
  - Replay from tick 900 to 1000
  - All events happen identically
  - Perfect rewind

Probabilistic world (naive):
  - Replay from tick 900 to 1000
  - RNG state different
  - Events diverge
  - Rewind broken!
```

**Critical issue**: If PureDOTS games need rewind, probabilistic simulation must be **deterministically seeded**.

**Solution**: Use per-entity, per-tick RNG seeds:
```csharp
uint seed = Hash(entityID, tickNumber);
Random rng = new Random(seed);
bool success = rng.NextFloat() < 0.20f;
```

This makes probabilistic **deterministic** (same seed → same result).

---

## Technical Architecture

### Approach 1: Deterministic Probabilistic Simulation

**Goal**: Probabilistic outcomes that are **rewind-safe**.

```csharp
public partial class ProbabilisticCraftingSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var tick = SystemAPI.GetSingleton<TimeState>().Tick;

        Entities
            .WithNone<AnchoredCharacter>() // Only non-anchored
            .ForEach((Entity e, ref CraftingState state) =>
            {
                // Deterministic RNG seeded by entity ID + tick
                uint seed = Hash(e.Index, tick);
                var rng = new Unity.Mathematics.Random(seed);

                // Probabilistic task attempt
                if (rng.NextFloat() < 0.20f) // 20% chance
                {
                    // Attempt craft
                    if (rng.NextFloat() < 0.80f) // 80% success
                    {
                        state.ItemsCrafted++;
                    }
                }
            }).Schedule();
    }

    private uint Hash(int entityIndex, uint tick)
    {
        return math.hash(new uint2((uint)entityIndex, tick));
    }
}
```

**Pros**:
- ✅ Rewind-safe (same entity + tick → same result)
- ✅ Deterministic (replay works)
- ✅ Still probabilistic (performance savings)

**Cons**:
- ❌ More complex than simple probabilistic
- ❌ Still need careful testing

### Approach 2: Smoothed Probabilistic Output

**Goal**: Reduce short-term variance, prevent "bad RNG" feeling.

```csharp
public struct ProbabilisticCraftingState : IComponentData
{
    public float AccumulatedProgress; // Running fractional progress
    public int ItemsCrafted;
}

public partial class SmoothedProbabilisticCraftingSystem : SystemBase
{
    protected override void OnUpdate()
    {
        Entities
            .WithNone<AnchoredCharacter>()
            .ForEach((ref ProbabilisticCraftingState state) =>
            {
                // Accumulate fractional progress (probabilistic rate)
                state.AccumulatedProgress += 0.16f; // Expected: 0.2 * 0.8 = 0.16 items/tick

                // When accumulated >= 1, craft an item
                if (state.AccumulatedProgress >= 1.0f)
                {
                    state.ItemsCrafted++;
                    state.AccumulatedProgress -= 1.0f;
                }
            }).Schedule();
    }
}
```

**Result**: Over time, produces **exact** expected value, zero variance.

**Pros**:
- ✅ Deterministic (rewind-safe)
- ✅ Zero long-term variance (predictable)
- ✅ Still saves performance (no AI, pathfinding, animation)

**Cons**:
- ❌ Not truly probabilistic (smoothed to deterministic)
- ❌ Less randomness (might feel mechanical)

**Best of both worlds**: Combine approaches 1 and 2 for small randomness + smooth output.

### Approach 3: Hybrid Simulation Tiers

**Goal**: Different simulation levels based on distance/importance.

```
Tier 1: Anchored Characters (Full Simulation)
  - Deterministic AI
  - Full pathfinding
  - Full animation
  - 100% accuracy
  - Cost: 10ms per entity

Tier 2: Near Player (Reduced Simulation)
  - Simplified AI (fewer decisions)
  - Simplified pathfinding (node-based)
  - Full animation (visible)
  - 100% accuracy
  - Cost: 5ms per entity

Tier 3: Mid-Distance (Probabilistic + Smoothed)
  - No AI (probabilistic outcomes)
  - No pathfinding (frozen positions)
  - No animation (static or looping)
  - 95% accuracy (±5% variance)
  - Cost: 0.5ms per entity

Tier 4: Far Distance (Abstract/Batch)
  - No individual simulation
  - Batch outcomes (e.g., "village produces 50 ore/min")
  - 90% accuracy (±10% variance)
  - Cost: 0.01ms per entity
```

**Automatically tier based on distance**:
```csharp
float distance = math.distance(entityPosition, playerPosition);

if (HasComponent<AnchoredCharacter>(e))
{
    SimulateFull(e); // Tier 1
}
else if (distance < 50f)
{
    SimulateReduced(e); // Tier 2
}
else if (distance < 200f)
{
    SimulateProbabilistic(e); // Tier 3
}
else
{
    SimulateAbstract(e); // Tier 4
}
```

**Pros**:
- ✅ Gradual degradation (no hard cutoffs)
- ✅ Scales to thousands of entities
- ✅ Player perception smooth

**Cons**:
- ❌ Complex to implement
- ❌ Hard to balance tiers
- ❌ Potential for exploits (player hovers at tier boundary)

---

## Game-Specific Considerations

### Space4X: Mining & Economy

**Use Case**: 100 mining fleets across galaxy, player can't watch all.

**Probabilistic fit**: ✅ Good
- Mining output is continuous (variance smooths out)
- Player rarely checks exact ore counts per minute
- Economy is statistical (average matters, not exact)

**Recommendation**:
```
Anchored fleets: Deterministic (player's favorites)
Near fleets (<100 units): Reduced simulation
Far fleets (>100 units): Probabilistic + smoothed
  - Expected: 50 ore/min
  - Actual: 48-52 ore/min (±4% variance)
  - Player notices: Minimal
```

### Godgame: Villager AI

**Use Case**: 200 villagers in village, player zoomed out.

**Probabilistic fit**: ⚠️ Mixed
- Some tasks okay (farming, crafting)
- Some tasks critical (combat, miracles)

**Recommendation**:
```
Anchored villagers: Deterministic (favorites, leaders)
Combat villagers: Always deterministic (fairness)
Worker villagers (distant): Probabilistic
  - Farming: 10% chance to harvest per tick
  - Crafting: 20% chance to craft per tick
  - Expected output maintained
```

**Critical**: Never make combat probabilistic (feels unfair).

### LastLightVR: Defender AI

**Use Case**: 50 defenders spread across battlefield, player fighting on one front.

**Probabilistic fit**: ❌ Poor
- VR players can glance around battlefield
- Probabilistic behavior visible (defender freezes, then jumps)
- Immersion breaks easily in VR

**Recommendation**:
```
All defenders in player's view: Full simulation
Defenders on other fronts: Reduced simulation (not probabilistic)
  - Simplified AI (fewer decisions)
  - Animations continue (visible in peripheral)
  - Never truly probabilistic (VR needs consistency)
```

**VR exception**: Player's field of view is wider and more sensitive to inconsistencies.

---

## Multiplayer Implications

### Challenge: Deterministic Sync

**Multiplayer requirement**: All clients must agree on world state.

**Probabilistic conflict**:
```
Client A: Smith rolls 0.19 → crafts sword
Client B: Smith rolls 0.21 → no sword
Desync!
```

**Solution**: Shared deterministic RNG seed
```csharp
// Server broadcasts seed at start of match
uint globalSeed = 12345;

// Each client uses same seed
uint entitySeed = Hash(globalSeed, entityID, tick);
Random rng = new Random(entitySeed);

// Both clients get same result
bool crafted = rng.NextFloat() < 0.20f;
```

**Result**: Probabilistic but synchronized.

### Bandwidth Savings

**Deterministic world**:
```
Server → Clients: "All 1000 villagers' states"
Bandwidth: 1000 * 64 bytes = 64KB per tick
```

**Probabilistic world**:
```
Server → Clients: "RNG seed: 12345"
Bandwidth: 4 bytes per tick

Clients compute probabilistic outcomes locally using seed
Result: 99.9% bandwidth reduction
```

**Massive savings** for large-scale multiplayer.

---

## Recommendations

### When to Use Probabilistic Simulation ✅

**Good fit**:
1. **Continuous processes** (mining, farming, crafting)
   - Output matters over time, not per-tick
   - Variance smooths out
2. **Distant entities** (player can't see details)
   - Won't notice probabilistic behavior
   - Immersion intact
3. **Non-critical gameplay** (background tasks)
   - Not combat, not player-facing
   - If it fails occasionally, no big deal
4. **Large entity counts** (100+ entities)
   - Law of large numbers applies
   - Statistical equivalence emerges
5. **Multiplayer games** (bandwidth-limited)
   - Shared RNG seed reduces sync cost
   - Deterministic probabilistic simulation

### When to Avoid Probabilistic Simulation ❌

**Bad fit**:
1. **Combat/conflict** (fairness critical)
   - Players expect deterministic outcomes
   - "RNG killed me" feels bad
2. **Player-facing actions** (direct interaction)
   - If player initiates, expect consistency
   - Probabilistic = frustration
3. **VR games** (high immersion bar)
   - Players notice inconsistencies easily
   - Full simulation feels better
4. **Short sessions** (< 10 minutes)
   - Variance doesn't smooth out
   - Bad RNG ruins session
5. **Rewind-heavy games** (strict determinism)
   - Unless using deterministic RNG seeds
   - Complex to implement correctly

### Hybrid Recommendation for PureDOTS

**Tiered simulation approach**:

```
Layer 1: Anchored Characters (10-20 entities)
  - Full deterministic simulation
  - 100% accuracy
  - High cost, but limited count

Layer 2: Near Player (50-100 entities)
  - Reduced simulation (simplified AI)
  - 100% accuracy
  - Medium cost

Layer 3: Mid-Distance (200-500 entities)
  - Probabilistic + smoothed
  - 95-98% accuracy
  - Low cost

Layer 4: Far/Abstract (1000+ entities)
  - Batch statistical simulation
  - 90% accuracy
  - Minimal cost
```

**Automatic tiering**: Entity transitions between layers based on:
- Distance to player/camera
- Anchor status
- Player attention (if entity recently selected)

**Result**: Scales to large worlds, maintains quality where it matters.

---

## Implementation Checklist

If implementing probabilistic simulation:

### Phase 1: Foundation
- [ ] Design deterministic RNG seeding (entity ID + tick)
- [ ] Create `ProbabilisticSimulationTier` component
- [ ] Add telemetry (track tier distribution, performance)

### Phase 2: Core Systems
- [ ] Modify AI systems to check tier before updating
- [ ] Implement probabilistic outcome calculation
- [ ] Add smoothing (accumulate fractional progress)

### Phase 3: Integration
- [ ] Integrate with anchor system (anchored = tier 1)
- [ ] Distance-based tier assignment
- [ ] Transition logic (smoothly change tiers)

### Phase 4: Testing
- [ ] Test rewind/replay (ensure determinism)
- [ ] Test multiplayer sync (ensure clients agree)
- [ ] Statistical testing (variance within bounds)
- [ ] Playtest (does it feel right?)

### Phase 5: Polish
- [ ] Settings (let players adjust tier thresholds)
- [ ] Debug visualization (show entity tiers)
- [ ] Performance profiling (measure savings)

---

## Open Questions

1. **Variance tolerance**: What's acceptable variance for players?
   - ±5%? ±10%? ±20%?
   - Varies by game and context

2. **Transition smoothness**: How do entities transition between tiers?
   - Instant (pop in/out of detail)?
   - Gradual (fade between states)?

3. **Combat exception**: Should combat ever be probabilistic?
   - No: Always deterministic (fairness)
   - Yes: Background battles can be probabilistic

4. **Player awareness**: Should players know about tiering?
   - Hidden: Invisible optimization
   - Visible: Show "simulating at reduced detail" indicator

5. **Anchor budget interaction**: Does probabilistic reduce anchor pressure?
   - Yes: Players don't need to anchor for performance
   - No: Players still anchor for emotional reasons

---

## Conclusion

**Probabilistic simulation for non-anchored entities is a powerful optimization**, but comes with trade-offs:

**Best case**: 90% CPU savings, invisible to player, scales to large worlds
**Worst case**: Breaks determinism, feels unfair, creates exploits

**Recommendation for PureDOTS**:
1. **Use hybrid tiered approach** (not pure probabilistic)
2. **Anchor system defines tier 1** (full simulation)
3. **Distance-based tiers 2-4** (reduced → probabilistic → abstract)
4. **Deterministic RNG seeding** (rewind-safe)
5. **Smooth variance** (±5% max for player-facing outputs)
6. **Combat always deterministic** (fairness > performance)

**Implementation priority**: P2 (after anchored characters proven)

---

## See Also

- [AnchoredCharactersSystem.md](AnchoredCharactersSystem.md) - Defines tier 1 simulation
- [PureDOTS Rewind System](../Runtime/Systems/RewindCoordinatorSystem.cs)
- [Deterministic RNG Patterns](https://docs.unity3d.com/Packages/com.unity.mathematics@1.3/api/Unity.Mathematics.Random.html)

---

**Created**: 2025-11-26
**Status**: Concept Analysis - Ready for Discussion
**Next Steps**: Team review → prototype in Space4X → measure player perception

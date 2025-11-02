# Time Controls Input

**Status:** Approved - <WIP: Not implemented>  
**Category:** Mechanic - Time System Input  
**Complexity:** Simple  
**Created:** 2025-10-31  
**Legacy Source:** `Docs/Concepts/legacy/Input_TimeControls.md`

**⚠️ CURRENT STATE:**
- ✅ `TimeState` component exists (PureDOTS)
- ✅ `RewindState` component exists (PureDOTS)
- ❌ Time control input actions NOT in `InputReaderSystem`
- ❌ No pause toggle binding
- ❌ No rewind hold binding
- ❌ No step back binding
- ❌ No speed multiplier bindings

---

## Overview

**Purpose:** Player input bindings for controlling time/rewind system  
**Player Impact:** Pause, rewind, speed up/down, step back through time  
**System Role:** Input layer for PureDOTS time management

---

## How It Works

### Inputs
- Unity Input System actions (from `.inputactions` asset)
- Keyboard bindings (primary), controller (secondary)

### Process
1. `InputReaderSystem` reads Unity Input System
2. Writes time commands to `TimeState` / `RewindState` (or command buffer?)
3. PureDOTS time systems react to state changes

### Outputs
- `TimeState.TimeScale` modified (pause, speed changes)
- `RewindState` commands triggered
- <UNDEFINED: Direct component modification or command pattern?>

---

## Rules

1. **UI Precedence:** Time controls blocked when UI has focus
   - Condition: Pointer over UI element
   - Effect: Don't process time inputs

2. **Speed Multipliers:** Discrete values (not continuous)
   - Condition: SpeedUp/SpeedDown pressed
   - Effect: Step through: 0, 0.25, 0.5, 1, 2, 4, 8

3. **Rewind Rate Ramp:** Hold duration → rewind speed
   - Condition: RewindHold action held
   - Effect: -1× at 0s → -8× at 1.5s (curve)

### Edge Cases
- Speed up while paused → Resume at next speed tier (not 0)
- Rewind while paused → <CLARIFICATION: Allow or block?>
- Multiple speed keys pressed → Last wins

### Priority Order
1. UI check (block if over UI)
2. Pause (highest priority time action)
3. Rewind hold
4. Step back
5. Speed multipliers

---

## Parameters

| Action | Default Binding | Interaction | Alt Binding |
|--------|-----------------|-------------|-------------|
| Pause | Spacebar | Press | P |
| RewindHold | R (hold) | Hold | <UNDEFINED> |
| StepBack | Left Arrow | Press | , (comma) |
| SpeedUp | ] or + | Press | Right Arrow |
| SpeedDown | [ or - | Press | Left Arrow |

**Current Implementation:** ❌ None in `InputReaderSystem`

---

## Example

**Given:** Game running at 1× speed, tick=1000  
**When:** Player presses ']' (SpeedUp)  
**Then:**
1. `InputReaderSystem` detects SpeedUp action
2. Current TimeScale = 1.0, next tier = 2.0
3. Writes: `TimeState.TimeScale = 2.0`
4. Game runs at 2× speed
5. Visual feedback: "2.0×" shown in Time HUD

**Given:** Playing at 2× speed  
**When:** Player holds 'R' for 1.5 seconds  
**Then:**
1. RewindHold started → time begins rewinding
2. Hold duration 0s: -1× rewind speed
3. Hold duration 0.75s: -4× rewind speed (lerp)
4. Hold duration 1.5s: -8× rewind speed (max)
5. Release 'R': Stop rewinding, return to 2× forward

---

## Player Feedback

- **Visual:** Time HUD shows current speed ("2.0×", "PAUSED", "-4× REWIND")
- **Audio:** <NEEDS SPEC: Tick sounds speed up? Rewind "whoosh"?>
- **UI:** Speed indicator, rewind progress bar

**Current:** ❌ No time HUD implemented

---

## Balance

- **Early:** Players learn pause/resume (tutorial)
- **Mid:** Speed control for pacing (fast gather, slow combat)
- **Late:** Rewind mastery for difficult moments

### Exploits
- Speed 8× forever → Intentional (player choice, no exploit)
- Rewind spam → <CLARIFICATION: Cooldown? Cost? Memory limit?>

---

## Interaction Matrix

| Other Mechanic | Relationship | Notes |
|----------------|--------------|-------|
| PureDOTS TimeState | Output | Writes TimeScale ✅ |
| PureDOTS RewindState | Output | Triggers rewind commands |
| UI Input | Conflict | UI blocks time controls |
| Gameplay Input | Independent | Time runs independently |

---

## Technical

- **Max entities:** N/A (singleton state)
- **Update freq:** Per frame (input checking)
- **Data needs:** `TimeState`, `RewindState`, input action references

---

## Tests

- [ ] Pause sets TimeScale = 0
- [ ] Unpause resumes previous speed
- [ ] SpeedUp cycles through multipliers (1 → 2 → 4 → 8)
- [ ] SpeedDown cycles reverse (8 → 4 → 2 → 1 → 0.5 → 0.25)
- [ ] RewindHold ramps rate based on hold duration
- [ ] StepBack decrements tick by 1
- [ ] UI precedence: time controls ignored when over UI

---

## Open Questions

1. **Rewind activation:** Direct TimeScale = -1 or command to rewind system?
2. **Step back:** Decrement tick directly or send command?
3. **Speed persistence:** Remember last speed across pause/unpause?

---

## Version History

- **v0.1 - 2025-10-31:** Ported from legacy Input_TimeControls.md

---

## Related Mechanics

- Time State: Truth sources ✅ `TimeState`, `RewindState` exist (PureDOTS)
- Time HUD: `Docs/Concepts/UI_UX/Time_HUD.md` (to be created)
- Input System: `Docs/Concepts/UI_UX/Input_System.md` (to be created)

---

## Truth Source Mapping

**Existing Components (✅ Implemented - PureDOTS):**

```csharp
// PureDOTS TimeState (VERIFIED EXISTS)
public struct TimeState : IComponentData {
    public uint Tick;         // ✅ Current simulation tick
    public float TimeScale;   // ✅ Speed multiplier (0 = paused)
    public double ElapsedTime;// ✅ Total time
}

// PureDOTS RewindState (VERIFIED EXISTS)
public struct RewindState : IComponentData {
    public byte CanRewind;    // ✅ Capability flag
    public uint OldestTick;   // ✅ Rewind bounds
    public uint LatestTick;   // ✅
    public uint CurrentBranch;// ✅ Timeline branch
}
```

**Existing Input System (🟡 Partial):**

```csharp
// Assets/Scripts/Godgame/Interaction/Input/InputReaderSystem.cs
// ✅ Reads camera movement (WASD, mouse)
// ✅ Reads mouse clicks (LMB, RMB)
// ❌ Does NOT read time controls yet
```

**Missing Implementation:**

```csharp
// Extend InputReaderSystem.cs to add:

// Time control action references (from .inputactions)
private InputAction _pauseAction;
private InputAction _rewindHoldAction;
private InputAction _stepBackAction;
private InputAction _speedUpAction;
private InputAction _speedDownAction;

public void OnUpdate(ref SystemState state) {
    // Existing input reading...
    
    // ❌ ADD: Time control reading
    if (_pauseAction.WasPressedThisFrame()) {
        var timeState = SystemAPI.GetSingletonRW<TimeState>();
        timeState.ValueRW.TimeScale = timeState.ValueRO.TimeScale == 0 ? 1.0f : 0f;
    }
    
    if (_speedUpAction.WasPressedThisFrame()) {
        var timeState = SystemAPI.GetSingletonRW<TimeState>();
        float[] tiers = { 0, 0.25f, 0.5f, 1f, 2f, 4f, 8f };
        int currentIndex = FindClosestTier(timeState.ValueRO.TimeScale, tiers);
        int nextIndex = math.min(currentIndex + 1, tiers.Length - 1);
        timeState.ValueRW.TimeScale = tiers[nextIndex];
    }
    
    // <SIMILAR for SpeedDown, StepBack, RewindHold>
}
```

## Design Intent (How It Should Feel)

**Player Perspective:**
- **Pause (Spacebar):** Instant freeze - think, plan, breathe
- **Rewind (Hold R):** Hold to "scrub" backwards through time, rate increases with hold
- **Step Back (←):** Precise undo - one tick back at a time
- **Speed Up/Down (]/[):** Control game pace - skip boring, slow intense moments

**Feel Goals:**
- Time controls feel **powerful** (god controls time itself)
- Rewind feels **smooth** (not jarring)
- Speed changes feel **immediate** (no lag)
- Controls **never conflict** with gameplay (UI precedence)

---

## Input Bindings (Proposed)

| Action | Primary Key | Alt Key | Feel | Why This Binding? |
|--------|-------------|---------|------|-------------------|
| Pause | Spacebar | P | Instant stop | Universal pause key |
| Rewind Hold | R (hold) | - | Scrub backwards | R = Rewind, natural |
| Step Back | ← (Left Arrow) | , (Comma) | Precise control | Arrow = direction, comma = step |
| Speed Up | ] (Right Bracket) | → (Right Arrow) | Accelerate | Right = faster, bracket = increment |
| Speed Down | [ (Left Bracket) | ← (Left Arrow) | Decelerate | Left = slower, bracket = decrement |

**Rationale:** Spacebar thumb-accessible, R easy to hold, arrows intuitive direction, brackets for fine control

---

## Behavior Design

### Pause Toggle
**Feel:** Instantaneous, satisfying click  
**Behavior:** Press → freeze. Press again → resume at previous speed  
**Feedback:** Screen tint (slight gray?), "PAUSED" overlay, audio mutes/continues?

### Rewind Hold  
**Feel:** Smooth scrubbing, variable speed  
**Curve:** 
```
Hold 0.0s  → -1× (slow rewind)
Hold 0.5s  → -2× 
Hold 1.0s  → -4×
Hold 1.5s+ → -8× (max rewind)
```
**Feedback:** Rewind sound (tape rewinding?), time HUD shows "-4×", visual "scrub" effect

### Speed Multipliers
**Feel:** Discrete clicks through gears  
**Values:** 0.25× → 0.5× → 1× → 2× → 4× → 8×  
**Wrapping:** 8× + SpeedUp = stays at 8×, 0.25× + SpeedDown = stays at 0.25×  
**Feedback:** Speed badge updates ("2.0×"), subtle time flow VFX?

### Step Back
**Feel:** Precise, deliberate  
**Behavior:** Each press = -1 tick  
**Limit:** <NEEDS SPEC: Can step back forever or memory limited?>  
**Feedback:** Tick counter decrements, world "pops" to previous state

---

## UI Precedence Rule

**Critical:** Time controls BLOCKED when:
- Cursor over UI element
- Modal dialog open
- Text input focused
- <FOR REVIEW: Cutscene playing?>

**Why:** Prevent accidental pauses during UI interaction (typing in search field shouldn't pause game)

---

## Design Questions

1. **Pause during combat:** Allow or force resolution first?
2. **Rewind cost:** Free or prayer cost or limited uses?
3. **Speed memory:** Resume at last speed or always 1×?
4. **Max speed limit:** Should 8× be cap or allow 16×, 32×?
5. **Step back limit:** Memory budget (last 100 ticks?) or infinite?
6. **Audio during speed change:** Pitch shift or mute?
7. **Rewind visual:** Reverse animation or "ghost" overlay?

---

## Truth Source Notes (For Later Implementation)

**Existing:** ✅ `TimeState.TimeScale` (PureDOTS) - Just needs input wiring  
**Existing:** ✅ `RewindState` (PureDOTS) - Rewind capability exists  
**Needed:** Input action bindings in Unity Input System asset  
**Needed:** Time HUD to show feedback

**Current:** Input system reads camera/hand controls, NOT time controls yet

---

**For Designers:** Focus on FEEL - rewind curve, speed tiers, pause feedback  
**For Implementers:** (Later) Check `InputReaderSystem.cs` pattern when ready to code


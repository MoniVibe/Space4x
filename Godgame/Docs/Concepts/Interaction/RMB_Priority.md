# RMB Priority Routing

**Status:** Approved  
**Category:** Mechanic - Input System  
**Complexity:** Medium  
**Created:** 2025-10-31  
**Last Updated:** 2025-10-31

---

## Overview

**Purpose:** Single deterministic source for right-click behavior resolution  
**Player Impact:** Prevents conflicts when multiple systems want RMB (siphon vs drag vs throw)  
**System Role:** Central dispatcher for all right-click interactions

---

## How It Works

### Inputs
- Unity Input System: `UI/RightClick` action (Started, Performed, Canceled)
- Frame context: Raycast hits, UI state, hand cargo state
- Registered handlers: Each system registers with priority value

### Process
1. Build `RmbContext` each frame (what's under cursor, hand state, UI state)
2. On RMB input phase (Started/Performed/Canceled):
   - Sort handlers by priority (descending)
   - Find first handler where `CanHandle(context) == true`
   - Dispatch to that handler's `OnRmb(context, phase)`
3. Only one handler executes per frame

### Outputs
- Selected handler receives input
- All other handlers ignored for this frame
- Context stored for next frame (hysteresis)

---

## Rules

1. **Priority Order:** Higher number = higher priority (100 > 50)
   - Condition: Multiple handlers claim same context
   - Effect: Highest priority wins

2. **Hysteresis:** Keep current handler until `CanHandle == false` for 3 frames
   - Condition: Tie between handlers
   - Effect: Prevents flicker when cursor on boundary

3. **UI Override:** UI always wins over world interactions
   - Condition: Pointer over UI element
   - Effect: All world handlers blocked

### Edge Cases
- No valid handlers → Fallback to context menu
- Handler crashes → Log error, try next handler
- Multiple Started in same frame → Use first only

### Priority Order
1. UI (100)
2. ModalTool (90)
3. StorehouseDump (80)
4. PileSiphon (70)
5. Drag (60)
6. GroundDrip (50)
7. SlingshotAim (40)
8. Fallback (0)

---

## Parameters

| Parameter | Default | Range | Impact |
|-----------|---------|-------|--------|
| HysteresisFrames | 3 | 1-10 | Handler switch stability |
| CooldownSeconds | 0.1 | 0-0.5 | Delay between handler changes |

---

## Example

**Given:** Player holds wood (500 units), cursor over pile (same type)  
**When:** RMB Started  
**Then:** 
1. Context: `{ HitPile: true, HandHasCargo: true, HandType: Wood, PileType: Wood }`
2. Check handlers: UI (false), ModalTool (false), Dump (false), **PileSiphon (true)** ✓
3. Dispatch to PileSiphon.OnRmb(Started)
4. Store PileSiphon as active handler
5. Next frames: PileSiphon receives Performed until Canceled

---

## Player Feedback

- **Visual:** Cursor changes based on active handler (hand icon, arrow, crosshair)
- **Audio:** Subtle click on handler switch
- **UI:** Tooltip shows current action ("Siphon", "Dump", "Throw")

---

## Balance

- **Early:** Players learn one interaction at a time (tutorial gates)
- **Mid:** Multiple handlers available, priority feels intuitive
- **Late:** Mastery players exploit priority (e.g., dump overrides siphon)

### Exploits
- Rapid handler switching (cursor dance) → Hysteresis prevents
- Force lower-priority handler → No exploit; design intent

---

## Interaction Matrix

| Other Mechanic | Relationship | Notes |
|----------------|--------------|-------|
| Hand State Machine | Dependency | Hand state affects handler CanHandle |
| Slingshot | Consumer | Registers at priority 40 |
| Pile Siphon | Consumer | Registers at priority 70 |
| Storehouse Dump | Consumer | Registers at priority 80 |

---

## Technical

- **Max entities:** N/A (singleton router)
- **Update freq:** Every frame (context builder), on input events (dispatch)
- **Data needs:** `RmbContext` struct, handler list, current active handler

---

## Tests

- [ ] UI blocks all world handlers
- [ ] Storehouse dump preempts pile siphon when both valid
- [ ] Hysteresis prevents rapid switching on boundary
- [ ] Handler disabled mid-action → gracefully cancel
- [ ] 30 FPS vs 120 FPS produces same behavior

---

## Open Questions

1. Should priority be data-driven (ScriptableObject) or hardcoded?
2. Should we allow handlers to "veto" lower priorities explicitly?

---

## Version History

- **v0.1 - 2025-10-31:** Initial draft from legacy RMBtruthsource.md

---

## Related Mechanics

- Hand State Machine: `Docs/Concepts/Interaction/Hand_State_Machine.md`
- Pile Siphon: `Docs/Concepts/Resources/Pile_Siphon.md` (to be created)
- Storehouse Dump: `Docs/Concepts/Buildings/Storehouse_Intake.md` (to be created)

---

**Implementation:** See `Docs/Legacy_TruthSources_Salvage.md#rmb-routing-system`


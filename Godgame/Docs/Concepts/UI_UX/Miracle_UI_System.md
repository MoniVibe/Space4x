# Miracle UI & Dispensation System

**Status:** Approved - <DESIGN DECISIONS CAPTURED>  
**Category:** UI/UX - Miracle Interaction  
**Scope:** Global  
**Created:** 2025-11-02  
**Last Updated:** 2025-11-02

---

## Purpose

**Primary Goal:** Provide intuitive, contextual access to miracle casting without UI clutter  
**Secondary Goals:**
- Quick access to favorite miracles via worship sites
- Full miracle catalog always available via bottom bar
- Radial menu for fast selection
- Black & White 2 familiarity

---

## Design Decisions ✅

### Activation Method
- ✅ **Button/menu based** (Black & White 2 style)
- ✅ **Side-to-side shake** to cancel
- ✅ **Radial menus** as added dispensation method

### UI Layout
- ✅ **Favorite miracles:** Atop worship sites (temples, shrines, cathedrals)
- ✅ **All miracles:** Bottom bar (persistent UI)
- ✅ **Radial menu:** Quick selection wheel (activation method TBD)

---

## UI Components

### 1. Bottom Bar (Persistent UI)

**Location:** Bottom of screen, horizontal panel  
**Contents:**
- All available miracles (scrollable if needed)
- Category tabs: Weather, Healing, Destruction, Summoning, etc.
- Mana cost display per miracle
- Cooldown indicators (if applicable)
- Intensity slider (when miracle selected)
- Mode toggles (e.g., Rain: drizzle/storm/blizzard)

**Interaction:**
1. Player clicks miracle button
2. Parameters appear (intensity, mode)
3. Adjust settings
4. Click target location on map
5. Miracle casts

**Visual Style:**
- Minimal, translucent (near-HUD-less philosophy)
- Icon-based with tooltips
- Highlight available vs locked miracles
- Pulse/glow when selected

---

### 2. Worship Site Shortcuts (Contextual)

**Location:** Floating above temples, shrines, cathedrals (3D world space)  
**Contents:**
- Favorite miracle slots (number TBD)
- Quick-cast buttons
- Visual: Miracle icons orbiting the worship site
- Appear on hover or click worship site

**Interaction:**
1. Player hovers/clicks temple
2. Favorite miracle icons appear above building
3. Click miracle icon
4. Intensity/mode quick-select or defaults to last-used
5. Click target to cast

**Benefits:**
- Contextual (only visible near worship sites)
- Fast casting (fewer clicks)
- Thematic (miracles come from holy places)
- Reduces bottom bar clutter

**Customization:**
- <DESIGN QUESTION: Can player assign favorites? Drag-and-drop from bottom bar?>
- <DESIGN QUESTION: Auto-assigned based on most-used miracles?>
- <DESIGN QUESTION: Different favorites per worship site type? (Temple = weather, Cathedral = epic?)>

---

### 3. Radial Menu (Fast Selection)

**Activation:** <DESIGN QUESTION: Middle mouse button? Hold key + mouse move? Right-click without target?>

**Layout:**
- Circular menu centered on cursor
- 8-12 sectors (miracle categories or individual miracles)
- Mouse movement selects sector
- Release to confirm

**Contents Options:**
- **Option A:** Miracle categories (hover to expand)
  - Weather → Rain, Tornado, etc.
  - Healing → Heal, Shield, etc.
- **Option B:** Direct miracle selection (12 most-used)
- **Option C:** Context-sensitive (shows relevant miracles for situation)

**Visual:**
- Smooth rotation
- Sector highlight on hover
- Icon + name per sector
- Mana cost displayed
- Grayed-out if insufficient mana

**Benefits:**
- No UI to bottom bar (stays on cursor)
- Fast for experienced players
- Genre-appropriate (RTS radial menus)

<parameter name="contents"># Miracle UI & Dispensation System

**Status:** Approved - <DESIGN DECISIONS CAPTURED>  
**Category:** UI/UX - Miracle Interaction  
**Scope:** Global  
**Created:** 2025-11-02  
**Last Updated:** 2025-11-02

---

## Purpose

**Primary Goal:** Provide intuitive, contextual access to miracle casting without UI clutter  
**Secondary Goals:**
- Quick access to favorite miracles via worship sites
- Full miracle catalog always available via bottom bar
- Radial menu for fast selection
- Black & White 2 familiarity

---

## Design Decisions ✅

### Activation Method
- ✅ **Button/menu based** (Black & White 2 style)
- ✅ **Side-to-side shake** to cancel
- ✅ **Radial menus** as added dispensation method

### UI Layout
- ✅ **Favorite miracles:** Atop worship sites (temples, shrines, cathedrals)
- ✅ **All miracles:** Bottom bar (persistent UI)
- ✅ **Radial menu:** Quick selection wheel (activation method TBD)

---

## UI Components

### 1. Bottom Bar (Persistent UI)

**Location:** Bottom of screen, horizontal panel  
**Contents:**
- All available miracles (scrollable if needed)
- Category tabs: Weather, Healing, Destruction, Summoning, etc.
- Mana cost display per miracle
- Cooldown indicators (if applicable)
- Intensity slider (when miracle selected)
- Mode toggles (e.g., Rain: drizzle/storm/blizzard)

**Interaction:**
1. Player clicks miracle button
2. Parameters appear (intensity, mode)
3. Adjust settings
4. Click target location on map
5. Miracle casts

**Visual Style:**
- Minimal, translucent (near-HUD-less philosophy)
- Icon-based with tooltips
- Highlight available vs locked miracles
- Pulse/glow when selected

---

### 2. Worship Site Shortcuts (Contextual)

**Location:** Floating above temples, shrines, cathedrals (3D world space)  
**Contents:**
- Favorite miracle slots (number TBD)
- Quick-cast buttons
- Visual: Miracle icons orbiting the worship site
- Appear on hover or click worship site

**Interaction:**
1. Player hovers/clicks temple
2. Favorite miracle icons appear above building
3. Click miracle icon
4. Intensity/mode quick-select or defaults to last-used
5. Click target to cast

**Benefits:**
- Contextual (only visible near worship sites)
- Fast casting (fewer clicks)
- Thematic (miracles come from holy places)
- Reduces bottom bar clutter

**Customization:**
- <DESIGN QUESTION: Can player assign favorites? Drag-and-drop from bottom bar?>
- <DESIGN QUESTION: Auto-assigned based on most-used miracles?>
- <DESIGN QUESTION: Different favorites per worship site type? (Temple = weather, Cathedral = epic?)>

---

### 3. Radial Menu (Fast Selection)

**Activation:** <DESIGN QUESTION: Middle mouse button? Hold key + mouse move? Right-click without target?>

**Layout:**
- Circular menu centered on cursor
- 8-12 sectors (miracle categories or individual miracles)
- Mouse movement selects sector
- Release to confirm

**Contents Options:**
- **Option A:** Miracle categories (hover to expand)
  - Weather → Rain, Tornado, etc.
  - Healing → Heal, Shield, etc.
- **Option B:** Direct miracle selection (12 most-used)
- **Option C:** Context-sensitive (shows relevant miracles for situation)

**Visual:**
- Smooth rotation
- Sector highlight on hover
- Icon + name per sector
- Mana cost displayed
- Grayed-out if insufficient mana

**Benefits:**
- No trip to bottom bar (stays on cursor)
- Fast for experienced players
- Genre-appropriate (RTS radial menus)

---

## Cancel Gesture: Side-to-Side Shake

**Trigger:** Rapid horizontal mouse movement  
**Detection:** 3+ direction changes within 0.5 seconds  
**Effect:** Cancel active miracle selection, return to normal cursor

**Example:**
```
Frame 0: Mouse at X=500
Frame 5: Mouse at X=600  (→ right)
Frame 10: Mouse at X=450 (← left, change 1)
Frame 15: Mouse at X=620 (→ right, change 2)
Frame 20: Mouse at X=480 (← left, change 3)
→ Shake detected! Cancel miracle.
```

**Visual Feedback:**
- Miracle selection UI shakes/vibrates
- Red X flash
- Cancel sound effect (whoosh)

**Use Cases:**
- Change mind mid-selection
- Quick escape from wrong miracle
- Panic cancel (enemy incoming)

---

## Interaction Flow Comparison

### Bottom Bar Flow (Precise)
```
1. Click miracle button (bottom bar)
2. Adjust intensity slider
3. Select mode (drizzle/storm/blizzard)
4. Move cursor to target
5. Click to cast
   OR shake to cancel
```
**Best for:** Precise control, unfamiliar miracles, parameter tweaking

---

### Worship Site Flow (Fast Favorites)
```
1. Click temple
2. Click favorite miracle icon
3. Click target (uses default/last parameters)
   OR shake to cancel
```
**Best for:** Frequently-used miracles, quick response, spatial awareness

---

### Radial Menu Flow (Expert)
```
1. Activate radial (middle mouse?)
2. Move mouse to sector
3. Release to confirm miracle
4. Click target
   OR shake to cancel
```
**Best for:** Muscle memory, speed, minimal UI distraction

---

## Worship Site Integration

### Building Types

**Shrine (Small):**
- <DESIGN QUESTION: 1 favorite slot? 2?>
- Low-tier miracles only?
- Small mana pool contribution

**Temple (Medium):**
- <DESIGN QUESTION: 3 favorite slots? 4?>
- Mid-tier miracles available
- Medium mana pool contribution

**Cathedral (Large):**
- <DESIGN QUESTION: 5 favorite slots? 6?>
- All miracles available (including epics)
- Large mana pool contribution

**Altar (Minimal):**
- <DESIGN QUESTION: Include or skip?>
- 1 favorite slot?
- Minimal mana contribution

---

## UI State Management

**Active States:**
1. **Idle:** No miracle selected
2. **Selected:** Miracle chosen, awaiting target
3. **Configuring:** Adjusting parameters (intensity, mode)
4. **Targeting:** Cursor shows AoE preview
5. **Casting:** Animation playing
6. **Cooldown:** (If applicable) Timer shown

**Transitions:**
```
Idle → Selected [click miracle button/icon/radial]
Selected → Configuring [adjust parameters]
Configuring → Targeting [parameters confirmed]
Targeting → Casting [click target]
Casting → Idle [animation complete]

Any state → Idle [shake cancel detected]
```

---

## PureDOTS Integration

### Components Needed

```csharp
// UI state (Presentation layer, not simulation)
MiracleUIState : IComponentData (singleton) {
    MiracleType selectedMiracle;
    float intensity;            // 0-1 slider value
    byte mode;                  // Mode enum per miracle type
    byte uiState;               // Idle, Selected, Configuring, etc.
}

// Worship site favorites
WorshipSiteMiracleSlots : IBufferElementData {
    MiracleType miracleType;
    byte slotIndex;             // 0-N
}

// Radial menu state
RadialMenuState : IComponentData (singleton) {
    bool isOpen;
    float2 centerPosition;
    byte selectedSector;
    uint activationTick;
}

// Shake detection
ShakeDetector : IComponentData (singleton) {
    float2 lastMousePos;
    byte directionChangeCount;
    uint windowStartTick;
    bool shakeDetected;
}
```

### Systems Required

```csharp
// Presentation layer (PresentationSystemGroup)
MiracleUIRenderSystem - Renders bottom bar, worship site icons
RadialMenuSystem - Handles radial menu logic
ShakeDetectionSystem - Monitors mouse movement for cancel

// Input routing
MiracleInputHandler - Processes miracle selection input
  - Priority in HandInputRouterSystem
  - Creates MiracleSelectionState when activated
  
// Simulation layer (GameplaySystemGroup)
MiracleActivationSystem - Spawns miracle effect entities
  - Reads MiracleUIState from presentation
  - Validates mana cost
  - Creates miracle entity with parameters
```

### Input Priority Integration

**Existing RMB Handler Priority (from PureDOTS):**
```
100: UI (blocks game)
 90: ModalTool
 80: StorehouseDump
 70: PileSiphon
 60: Drag
 50: GroundDrip
 40: SlingshotAim
  0: Fallback
```

**Miracle UI additions:**
```
 95: RadialMenu (high priority when open)
 85: WorshipSiteShortcut (click on temple)
  5: BottomBarMiracle (fallback, low priority)
```

---

## Accessibility

**Visual:**
- High contrast miracle icons
- Colorblind-safe category colors
- Scalable UI (4K support)
- Icon + text labels

**Motor:**
- All three methods available (bottom bar, worship site, radial)
- Click-based (no gesture complexity beyond shake)
- Hotkey support (keyboard shortcuts for miracles)

**Audio:**
- Screen reader support for miracle names/costs
- Audio cues for selection/confirmation

---

## Performance Considerations

**UI Rendering:**
- Bottom bar: Persistent canvas, minimal updates
- Worship site icons: Render only when nearby (spatial culling)
- Radial menu: Show/hide on activation (no persistent cost)

**Update Frequency:**
- Mana display: Update on change only (event-driven)
- Availability checks: 1 Hz (sufficient for locked/unlocked state)
- Shake detection: Per-frame (lightweight, just tracking deltas)

**Entity Count:**
- No miracle UI entities in simulation world
- Pure presentation layer
- No impact on DOTS performance

---

## Open Questions

1. <DESIGN QUESTION: How many favorite slots per worship site?
   - Shrine: 1-2?
   - Temple: 3-4?
   - Cathedral: 5-6?
   - Scale with building tier?>

2. <DESIGN QUESTION: Can player customize favorites?
   - Drag-drop from bottom bar?
   - Auto-assigned by usage frequency?
   - Per worship site or global favorites?>

3. <DESIGN QUESTION: Radial menu activation?
   - Middle mouse button?
   - Hold key (Q?) + mouse move?
   - Right-click on empty ground?>

4. <DESIGN QUESTION: Radial menu contents?
   - 8 sectors (cardinal + diagonal)?
   - 12 sectors (clock positions)?
   - Categories or direct miracles?>

5. <DESIGN QUESTION: Worship site activation distance?
   - Click only (on building)?
   - Hover within radius?
   - Auto-show when camera near?>

6. <DESIGN QUESTION: Parameter quick-select from worship sites?
   - Use last-used parameters?
   - Show mini intensity slider?
   - Skip parameters entirely (use defaults)?>

7. <DESIGN QUESTION: Does shake cancel work during hand carry state too?
   - Universal cancel gesture?
   - Or miracle-selection only?>

8. <DESIGN QUESTION: Bottom bar collapse/expand?
   - Always visible?
   - Hide when not in use (hotkey to show)?
   - Auto-hide in certain camera modes?>

---

## PureDOTS Implementation Notes

### System Placement

```
PresentationSystemGroup
  ├─ MiracleUIRenderSystem
  │   └─ Render bottom bar, worship site icons
  │
  ├─ RadialMenuSystem
  │   └─ Handle radial menu state/rendering
  │
  └─ ShakeDetectionSystem
      └─ Track mouse deltas, detect cancel gesture

HandInputRouterSystem (already exists)
  └─ Add MiracleInputHandler
      └─ Route miracle selections to activation
```

### Data Flow

```
[Player clicks miracle UI]
    ↓
MiracleUIState updated (presentation layer)
    ↓
MiracleInputHandler reads state (input layer)
    ↓
Creates MiracleActivationCommand (simulation layer)
    ↓
MiracleActivationSystem spawns effect entity
    ↓
Deducts mana, applies effects
```

### Worship Site Detection

```csharp
// Existing PureDOTS pattern: Spatial queries
OnClick(float3 worldPos) {
    // Check if clicked building is worship site
    Entity clickedEntity = SpatialQuery.RaycastToEntity(worldPos);
    
    if (HasComponent<WorshipSiteTag>(clickedEntity)) {
        // Get favorite miracles
        var slots = GetBuffer<WorshipSiteMiracleSlots>(clickedEntity);
        
        // Show UI at building position
        ShowWorshipSiteUI(GetComponent<LocalTransform>(clickedEntity).Position, slots);
    }
}
```

### Shake Detection Pattern

```csharp
// Per-frame in PresentationSystemGroup
[BurstCompile]
partial struct ShakeDetectionJob : IJobEntity {
    public float2 MousePosition;
    public float DeltaTime;
    
    void Execute(ref ShakeDetector detector) {
        float2 delta = MousePosition - detector.lastMousePos;
        
        if (math.abs(delta.x) > threshold) {
            // Direction change detected
            if (math.sign(delta.x) != math.sign(detector.lastDelta.x)) {
                detector.directionChangeCount++;
            }
        }
        
        // Check window expiry
        if (currentTick - detector.windowStartTick > shakeWindowTicks) {
            // Reset window
            detector.directionChangeCount = 0;
            detector.windowStartTick = currentTick;
        }
        
        // Shake detected!
        if (detector.directionChangeCount >= 3) {
            detector.shakeDetected = true;
            // Trigger cancel event
        }
        
        detector.lastMousePos = MousePosition;
    }
}
```

---

## Visual Mockup (Text)

### Bottom Bar
```
┌─────────────────────────────────────────────────────────┐
│ [Rain] [Heal] [Fire] [Shield] [Meteor] [▶] Mana: 5420 │
└─────────────────────────────────────────────────────────┘
         ↑ Selected
    Intensity: [||||||||----] 80%
    Mode: ○ Drizzle ● Storm ○ Blizzard
```

### Worship Site
```
        🌧️       ✨
           [Temple]
        ⚡       🔥
        
Hover temple → 4 favorite miracle icons orbit building
Click icon → Quick-cast with defaults
```

### Radial Menu
```
         [Heal]
    [Shield] ╱│╲ [Joy]
         ╱  │  ╲
    [Rain]─  +  ─[Fire]
         ╲  │  ╱
  [Despair] ╲│╱ [Meteor]
        [Shield]
        
Move mouse → Highlight sector → Release → Select
```

---

## Tutorial Integration

**First Miracle Cast Experience:**
1. Tutorial highlights bottom bar
2. "Click Rain to refresh your villagers"
3. Player clicks Rain button
4. Intensity slider appears (tutorial sets to 50%)
5. "Click near your village"
6. Player clicks
7. Rain miracle casts
8. Tutorial: "Miracles cost mana. Earn mana through worship."

**Teaching Worship Sites:**
1. "You've built a temple!"
2. Camera focuses on temple
3. Favorite icons appear automatically
4. "Click the icons to cast miracles quickly"
5. Player tries it
6. "You can customize favorites later"

**Teaching Radial Menu:**
1. <DESIGN QUESTION: When introduced?>
2. "Hold [key] to open miracle wheel"
3. "Move mouse to select"
4. Practice casting

---

## Comparison to Black & White 2

**Similarities:**
- ✅ Button-based activation
- ✅ Parameter selection (intensity)
- ✅ Target selection on map
- ✅ Contextual shortcuts (worship sites)

**Differences:**
- ✅ Added radial menu (BW2 didn't have)
- ✅ Bottom bar (BW2 used side panel)
- ✅ Shake to cancel (BW2 used ESC key)
- ❌ No gesture drawing (BW2 had optional gestures)

**Improvements:**
- Multiple access methods (accessibility)
- Contextual worship site UI (spatial awareness)
- Radial menu for speed (modern UX)

---

## Related Concepts

- `Docs/Concepts/Miracles/Miracle_System_Vision.md` - Core miracle mechanics
- `Docs/Concepts/Core/Prayer_Power.md` - Mana economy
- `Docs/Concepts/Experiences/First_Miracle.md` - Tutorial moment
- `Docs/Concepts/Buildings/` - Worship site mechanics (TBD)

---

## PureDOTS Requirements

**Existing Systems:**
- ✅ `HandInputRouterSystem` - Can add miracle handler
- ✅ `PresentationSystemGroup` - UI rendering here
- ✅ Spatial queries - For worship site clicks

**New Systems Needed:**
- ❌ `MiracleUIRenderSystem` - Bottom bar + worship sites
- ❌ `RadialMenuSystem` - Radial menu logic
- ❌ `ShakeDetectionSystem` - Cancel gesture
- ❌ `MiracleInputHandler` - Route selections to activation

**No PureDOTS modifications required** - all game-specific code in Godgame assemblies

---

**For Implementers:** Start with bottom bar only (simplest), add worship sites in v2, radial menu in v3  
**For Designers:** Worship site slot counts affect player strategy (more slots = more convenience)  
**For Artists:** Need miracle icons for all types, worship site VFX for icon display

---

**Last Updated:** 2025-11-02


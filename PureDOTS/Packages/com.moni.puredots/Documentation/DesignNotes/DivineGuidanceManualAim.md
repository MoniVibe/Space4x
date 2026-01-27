# Divine Guidance: Manual Aim & Trajectory Crafting

**Status**: Concept Design
**Last Updated**: 2025-11-29
**Cross-Project**: Space4X (tech-gated) + Godgame (miracle-based)

---

## Overview

**Divine Guidance** is a player intervention system that allows direct control over entity aiming and projectile trajectories. Combined with rewind and time stop mechanics, players can craft precise combat outcomes through iterative refinement.

**Core Philosophy**: Transform passive observation into active participation—players become tacticians, snipers, or gods shaping the flow of battle.

---

## Cross-Project Design

| Aspect | Space4X | Godgame |
|--------|---------|---------|
| **Narrative** | Advanced targeting AI override | Divine intervention / miracles |
| **Activation** | Tech-gated unlock | Always available (innate god power) |
| **Scope** | Individual units or squads | Individual entities or bands |
| **Limitation** | Energy/cooldown resource | Miracle points / divine favor |
| **Visual** | Holographic targeting overlay | Ethereal guidance rays |
| **Precision** | Sniper-grade (pixel-perfect) | Influenced (guided but not absolute) |

---

## Core Mechanics

### 1. Manual Aim Mode

**Activation**:
- **Space4X**: Select entity → Press "Override Targeting" (requires tech unlock)
- **Godgame**: Select entity → Hold "Divine Hand" → Move cursor to aim

**Behavior**:
- **Time slows** to 10% speed (or pauses if player holds modifier key)
- **Camera focuses** on selected entity (smooth zoom)
- **Trajectory line** renders from entity weapon to cursor position
- **Impact prediction** shows where projectile will hit based on:
  - Current velocity
  - Target movement (predictive leading)
  - Environmental factors (gravity, wind in Godgame)
  - Obstacles (collision detection)

**Visual Feedback**:
```
┌─────────────────────────────────┐
│  [Entity: Frigate "Endeavor"]   │
│  ════════════════════════════    │  ← Trajectory line (bright)
│         ╱                        │
│        ╱  ← Predicted impact     │
│       ●   (crosshair)            │
│      ╱                           │
│  [Target: Enemy Cruiser]         │
│   Hit probability: 87%           │
│   Damage: 45-60 (weak point)     │
└─────────────────────────────────┘
```

**Trajectory Properties**:
- **Color-coded** by hit probability:
  - Green (>80%): High confidence hit
  - Yellow (50-80%): Moderate chance
  - Red (<50%): Low chance / miss
  - Blue (special): Weak point or critical hit zone
- **Dotted sections**: Uncertainty (due to target evasion, RNG)
- **Branching trajectories**: Show ricochet or multi-target paths

### 2. Projectile Influence (Godgame)

**Concept**: Players don't directly control trajectory, but **influence** it post-fire.

**Implementation**:
1. Entity fires projectile normally (AI-controlled)
2. While projectile in flight, player can activate "Divine Nudge"
3. Cursor position creates **attraction field** that bends projectile path
4. Projectile curves toward cursor (limited angle, e.g., ±30° max deflection)
5. Costs miracle points per second of influence

**Visual**:
```
Projectile: ────→ (original path)
            ↗
Player cursor ●  (attraction point)

Result: ────→╱ (curved path toward cursor)
```

**Balancing**:
- **Limited deflection**: Can't do 180° turns (physical constraints)
- **Diminishing returns**: Stronger deflection = exponentially higher miracle cost
- **Skill-based**: Requires timing and cursor precision
- **Counterplay**: Enemies can have "Divine Shield" that resists influence

### 3. Rewind Integration

**The Crafting Loop**:
1. **Observe** combat outcome (miss, partial hit, friendly fire)
2. **Rewind** to moment before firing
3. **Adjust** aim or influence parameters
4. **Replay** and observe new outcome
5. **Iterate** until satisfied

**Example Flow** (Space4X sniper scenario):
```
Attempt 1: Aimed at enemy engine
→ Miss (target evaded)
→ Rewind 5 seconds

Attempt 2: Led target by 10m
→ Hit, but armor absorbed damage
→ Rewind 5 seconds

Attempt 3: Aimed at exposed sensor array
→ Critical hit! Target blinded
→ Accept outcome
```

**Rewind UI**:
- **Timeline scrubber** shows previous aim attempts (ghosted trajectories)
- **Outcome preview**: Hover over timeline point to see result without committing
- **Save/Load aims**: Bookmark successful aim solutions for similar scenarios

### 4. Time Stop Precision

**Enhanced Mode**:
- **Full time stop**: Freeze simulation (requires high-tier tech in Space4X, high miracle cost in Godgame)
- **Rotate camera** freely while time frozen
- **Measure distances**: Click-and-drag ruler tool
- **Calculate trajectories**: Multi-bounce shots, trick shots
- **Resume time**: When ready, unpause and watch plan execute

**Use Cases**:
- **Sniper shots**: Thread needle between friendlies to hit distant target
- **Trick shots**: Bank projectile off asteroid to hit target behind cover
- **Multi-kills**: Align penetrating shot to hit multiple enemies
- **Defensive**: Intercept incoming projectile with own projectile (bullet-time style)

---

## Tech Progression (Space4X)

### Tier 1: Basic Targeting Override
**Unlock**: Early-game tech
**Features**:
- Manual aim (time slows to 50% speed)
- Basic trajectory line (straight path, no prediction)
- Cooldown: 30 seconds per use

### Tier 2: Predictive Targeting
**Unlock**: Mid-game tech
**Features**:
- Trajectory accounts for target movement
- Hit probability display
- Time slows to 25% speed
- Cooldown: 20 seconds

### Tier 3: Quantum Targeting Suite
**Unlock**: Late-game tech
**Features**:
- Full time stop available
- Multi-trajectory preview (try 3 aims simultaneously)
- Weak point highlighting
- Ricochet/penetration calculation
- Cooldown: 10 seconds

### Tier 4: Tactical Omniscience
**Unlock**: End-game tech
**Features**:
- No cooldown (unlimited use)
- Probability fields (see all possible outcomes)
- Auto-aim assist (AI suggests optimal shots)
- Shared targeting (squad-level coordination)

---

## Miracle System (Godgame)

### Basic Divine Nudge
**Cost**: 5 Miracle Points per second of influence
**Effect**: Bend projectile path up to ±15°
**Range**: 50m from cursor
**Cooldown**: None (cost-limited)

### Divine Volley
**Cost**: 20 Miracle Points (one-time)
**Effect**:
- Select up to 5 entities
- All fire simultaneously at cursor position
- Trajectories auto-coordinated to avoid friendly fire
**Duration**: Instant
**Cooldown**: 60 seconds

### Hand of Fate
**Cost**: 50 Miracle Points
**Effect**:
- Full time stop for 10 seconds
- Unlimited trajectory adjustments during stop
- Can redirect enemy projectiles too (turn their fire against them)
**Cooldown**: 120 seconds

### Blessing of the Marksman
**Cost**: 30 Miracle Points
**Effect**:
- Target entity gains +50% accuracy for 60 seconds
- Automatic weak-point targeting
- Critical hit chance +30%
**Duration**: 60 seconds (passive buff, no manual aim needed)
**Cooldown**: 90 seconds

---

## Technical Implementation

### Component Structure

```csharp
// Marks entity as available for manual aim
public struct ManualAimEnabled : IComponentData
{
    public bool IsActive;              // Currently being aimed by player
    public float AimPrecision;         // 0-1, tech/skill level
    public float CooldownRemaining;    // Seconds until next use (Space4X)
}

// Stores player-overridden aiming data
public struct PlayerAimOverride : IComponentData
{
    public float3 TargetPosition;      // World position cursor aims at
    public float3 PredictedImpact;     // Where projectile will actually hit
    public float HitProbability;       // 0-1
    public bool IsWeakPoint;           // Aimed at vulnerable spot
    public Entity IntendedTarget;      // Which entity player wants to hit
}

// Projectile influence (Godgame)
public struct DivineInfluence : IComponentData
{
    public float3 AttractionPoint;     // Cursor world position
    public float InfluenceStrength;    // 0-1, based on miracle point expenditure
    public float MaxDeflection;        // Max degrees projectile can bend (30° default)
    public float CurrentDeflection;    // Current bend angle
}

// Trajectory visualization data
public struct TrajectoryPreview : IComponentData
{
    public BlobAssetReference<TrajectoryPath> Path;  // Sequence of positions
    public float4 LineColor;           // Color-coded by hit probability
    public bool ShowRicochet;          // Display bounce paths
    public int SegmentCount;           // Granularity of curve
}

// Blob for trajectory path
public struct TrajectoryPath
{
    public BlobArray<float3> Points;   // Discretized path points
    public BlobArray<float> Probabilities;  // Hit chance at each segment
}
```

### System Workflow

#### 1. Aim Input System
```csharp
[BurstCompile]
public partial struct AimInputSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        // Detect player input (mouse position, hotkey press)
        if (Input.GetKey(KeyCode.LeftShift) && Input.GetMouseButtonDown(0))
        {
            // Raycast from cursor to world
            var cursorWorldPos = GetCursorWorldPosition();

            // Find selected entity with ManualAimEnabled
            foreach (var (aim, entity) in
                SystemAPI.Query<RefRW<ManualAimEnabled>>()
                    .WithAll<PlayerSelected>())
            {
                if (aim.ValueRO.CooldownRemaining > 0) continue;

                // Activate aim mode
                aim.ValueRW.IsActive = true;

                // Add PlayerAimOverride component
                state.EntityManager.AddComponentData(entity, new PlayerAimOverride {
                    TargetPosition = cursorWorldPos,
                    PredictedImpact = float3.zero,  // Calculated by prediction system
                    HitProbability = 0f,
                    IsWeakPoint = false,
                    IntendedTarget = Entity.Null
                });

                // Slow time (or pause if modifier held)
                var timeControl = SystemAPI.GetSingleton<TimeControlState>();
                timeControl.TimeScale = Input.GetKey(KeyCode.LeftControl) ? 0f : 0.1f;
            }
        }
    }
}
```

#### 2. Trajectory Prediction System
```csharp
[BurstCompile]
public partial struct TrajectoryPredictionSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        foreach (var (aimOverride, weaponData, transform, entity) in
            SystemAPI.Query<RefRW<PlayerAimOverride>, RefRO<WeaponData>, RefRO<LocalTransform>>()
                .WithAll<ManualAimEnabled>())
        {
            var startPos = transform.ValueRO.Position;
            var targetPos = aimOverride.ValueRO.TargetPosition;
            var velocity = weaponData.ValueRO.ProjectileSpeed;

            // Physics simulation: predict path
            var trajectory = SimulateProjectilePath(
                startPos,
                targetPos,
                velocity,
                weaponData.ValueRO.GravityScale,
                maxSteps: 100
            );

            // Check for collisions along path
            var hitResult = RaycastAlongPath(trajectory);

            // Calculate hit probability
            var hitProb = CalculateHitProbability(
                targetPos,
                hitResult.ImpactPoint,
                aimOverride.ValueRO.IntendedTarget
            );

            // Update override data
            aimOverride.ValueRW.PredictedImpact = hitResult.ImpactPoint;
            aimOverride.ValueRW.HitProbability = hitProb;
            aimOverride.ValueRW.IsWeakPoint = hitResult.IsWeakPoint;

            // Store trajectory for rendering
            state.EntityManager.SetComponentData(entity, new TrajectoryPreview {
                Path = CreateTrajectoryBlob(trajectory),
                LineColor = GetColorByProbability(hitProb),
                ShowRicochet = hitResult.CanRicochet,
                SegmentCount = trajectory.Length
            });
        }
    }

    private float CalculateHitProbability(float3 intended, float3 predicted, Entity target)
    {
        var distance = math.distance(intended, predicted);

        // Base probability from distance to cursor
        var baseProb = math.saturate(1.0f - distance / 10f);  // 0 prob at 10m+ miss

        // Bonus if actually hits intended target
        if (target != Entity.Null && HitsEntity(predicted, target))
        {
            baseProb = math.min(baseProb + 0.3f, 1.0f);
        }

        return baseProb;
    }
}
```

#### 3. Projectile Influence System (Godgame)
```csharp
[BurstCompile]
public partial struct ProjectileInfluenceSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var deltaTime = SystemAPI.Time.DeltaTime;

        foreach (var (influence, velocity, transform) in
            SystemAPI.Query<RefRW<DivineInfluence>, RefRW<Velocity>, RefRO<LocalTransform>>()
                .WithAll<Projectile>())
        {
            if (influence.ValueRO.InfluenceStrength <= 0) continue;

            var currentPos = transform.ValueRO.Position;
            var attractionPoint = influence.ValueRO.AttractionPoint;

            // Calculate desired direction (toward cursor)
            var toAttraction = math.normalize(attractionPoint - currentPos);

            // Current direction
            var currentDir = math.normalize(velocity.ValueRO.Linear);

            // Blend directions based on influence strength
            var blendFactor = influence.ValueRO.InfluenceStrength * deltaTime * 2f;  // Adjust for deflection speed
            var newDir = math.normalize(math.lerp(currentDir, toAttraction, blendFactor));

            // Check deflection limit
            var deflectionAngle = math.degrees(math.acos(math.dot(currentDir, newDir)));
            influence.ValueRW.CurrentDeflection += deflectionAngle;

            if (influence.ValueRW.CurrentDeflection > influence.ValueRO.MaxDeflection)
            {
                // Clamp to max deflection
                newDir = RotateTowardLimit(currentDir, toAttraction, influence.ValueRO.MaxDeflection);
            }

            // Apply new velocity direction (preserve speed)
            var speed = math.length(velocity.ValueRO.Linear);
            velocity.ValueRW.Linear = newDir * speed;

            // Consume miracle points (handled by miracle system)
            var miracleCost = influence.ValueRO.InfluenceStrength * deltaTime * 5f;  // 5 MP/sec
            ConsumeMiraclePoints(state, miracleCost);
        }
    }
}
```

#### 4. Trajectory Rendering System (Presentation)
```csharp
// Non-Burst: Uses Unity rendering API
public partial class TrajectoryRenderSystem : SystemBase
{
    private LineRenderer lineRenderer;

    protected override void OnCreate()
    {
        lineRenderer = new GameObject("TrajectoryLine").AddComponent<LineRenderer>();
        lineRenderer.material = Resources.Load<Material>("TrajectoryLineMaterial");
        lineRenderer.startWidth = 0.1f;
        lineRenderer.endWidth = 0.05f;
    }

    protected override void OnUpdate()
    {
        foreach (var (trajectory, aim) in
            SystemAPI.Query<RefRO<TrajectoryPreview>, RefRO<PlayerAimOverride>>()
                .WithAll<ManualAimEnabled>())
        {
            if (!aim.ValueRO.IsActive) continue;

            // Convert blob to LineRenderer positions
            ref var path = ref trajectory.ValueRO.Path.Value;
            lineRenderer.positionCount = path.Points.Length;

            for (int i = 0; i < path.Points.Length; i++)
            {
                lineRenderer.SetPosition(i, path.Points[i]);

                // Color gradient based on probability
                var prob = path.Probabilities[i];
                lineRenderer.startColor = GetColorByProbability(prob);
            }

            // Render impact marker
            var impactPos = aim.ValueRO.PredictedImpact;
            RenderImpactCrosshair(impactPos, aim.ValueRO.HitProbability);

            // Render UI tooltip
            RenderAimTooltip(aim.ValueRO);
        }
    }

    private void RenderAimTooltip(PlayerAimOverride aim)
    {
        var tooltipText = $"Hit: {aim.HitProbability:P0}";
        if (aim.IsWeakPoint) tooltipText += " [WEAK POINT]";

        // Use Unity UI or TextMeshPro to render near cursor
        // ...
    }
}
```

---

## Rewind Integration

### Recording Aim Attempts

```csharp
public struct AimAttempt : IBufferElementData
{
    public uint Tick;                  // When aim was attempted
    public float3 TargetPosition;      // Where player aimed
    public float3 ActualImpact;        // Where it actually hit
    public float HitProbability;       // Predicted prob
    public bool WasSuccessful;         // Did it achieve desired outcome?
}

// System records attempts
[BurstCompile]
public partial struct RecordAimAttemptsSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        var currentTick = SystemAPI.GetSingleton<TimeSpine>().CurrentTick;

        foreach (var (aim, attempts) in
            SystemAPI.Query<RefRO<PlayerAimOverride>>()
                .WithAll<ManualAimEnabled>()
                .WithChangeFilter<PlayerAimOverride>())
        {
            // Aim changed, record new attempt
            attempts.Add(new AimAttempt {
                Tick = currentTick,
                TargetPosition = aim.ValueRO.TargetPosition,
                ActualImpact = aim.ValueRO.PredictedImpact,
                HitProbability = aim.ValueRO.HitProbability,
                WasSuccessful = false  // Updated post-fire
            });
        }
    }
}
```

### Timeline Scrubber UI

```
Rewind Timeline:
├─ [Attempt 1: 45% hit] ───────── Miss
├─ [Attempt 2: 72% hit] ───────── Graze (10 dmg)
├─ [Attempt 3: 89% hit] ───────── Critical! (60 dmg) ← Current
└─ [Attempt 4: ...] (preview)

[Rewind 5s] [Rewind 10s] [Accept Outcome]
```

**Features**:
- Click timeline point to preview outcome (ghosted trajectory)
- Hover to see tooltip with damage/result
- Green checkmark = successful hit
- Red X = miss/fail
- Yellow ! = partial success

---

## Balancing Considerations

### Space4X

**Advantages**:
- Sniper gameplay: Skilled players can punch above weight class
- Tactical depth: Disable specific subsystems (engines, weapons, sensors)
- Defensive play: Intercept incoming missiles

**Limitations**:
- Cooldown prevents spam (30s default, scaling with tech)
- Energy cost (drains ship power)
- Only affects one shot at a time (can't aim entire volley)
- AI can counter with evasive maneuvers (reduces hit probability)

**Multiplayer**:
- In PvP, manual aim is allowed but doesn't pause time for opponent
- Creates asymmetry: one player aims in slow-mo, opponent reacts in real-time
- Requires skill vs. opponent's unpredictability

### Godgame

**Advantages**:
- Divine intervention fantasy: Feel like a god shaping outcomes
- Can "save" poorly-positioned units
- Enables trick shots (bounce arrows off walls, curve over obstacles)

**Limitations**:
- Miracle point cost (limited resource)
- Can't fully override physics (max 30° deflection)
- Enemies with "Divine Shield" resist influence (high-tier units, blessed factions)
- Influence weakens with distance from cursor (inverse square law)

**Multiplayer** (God vs. God):
- Both gods can influence projectiles
- Creates "tug of war" (one god nudges arrow toward target, other god deflects it away)
- Higher miracle expenditure wins the contest

---

## UX Design

### Visual Language

**Space4X**:
- **Holographic blue** trajectory lines (sci-fi aesthetic)
- **Angular UI** overlays (military HUD style)
- **Numerical readouts**: Hit %, damage range, time to impact
- **Weak point highlights**: Red outlines on enemy subsystems

**Godgame**:
- **Golden/ethereal** trajectory lines (divine light)
- **Organic curves**: Smooth, flowing (not rigid like sci-fi)
- **Symbolic feedback**: Icons instead of numbers (sun = high chance, clouds = uncertain)
- **Divine hand cursor**: Changes to glowing hand icon when aiming

### Audio Feedback

**Space4X**:
- Activation: Sharp electronic beep
- Aiming: Subtle targeting tone (pitch changes with hit probability)
- Fire: Crisp, satisfying weapon discharge
- Hit: Metallic impact + damage feedback

**Godgame**:
- Activation: Chime or celestial hum
- Aiming: Warm, resonant tone (volume increases near targets)
- Fire: Whoosh + divine echo
- Hit: Thud + blessing sound (if influenced)

---

## Accessibility

### Difficulty Modes

**Assisted Aim** (Easy):
- Auto-suggests optimal aim positions
- Trajectory "snaps" to nearby targets
- Increased hit probability (+20%)
- Longer time slow duration

**Standard Aim** (Normal):
- As designed
- No assistance

**Pure Skill** (Hard):
- No trajectory preview (aim blind)
- No hit probability display
- Shorter time slow duration
- Higher cooldowns

### Input Options

- **Mouse**: Precision cursor control
- **Controller**: Smooth analog stick aiming with aim assist
- **Touch**: Tap to aim, swipe to adjust trajectory
- **Accessibility**: Hold-to-aim (no precise clicking required)

---

## Future Extensions

### Multi-Shot Coordination
- Aim 3-5 units simultaneously
- Stagger fire timings for sequential hits
- Create "firing solution" templates (save and reuse)

### Projectile Interception
- Aim your projectile to intercept enemy projectile mid-flight
- Bullet-time style "missile defense"

### Environmental Interactions
- Bank shots off reflective surfaces
- Use gravity wells to curve trajectories
- Exploit wind/atmospheric effects (Godgame)

### Co-op Aiming
- Multiplayer: Multiple players aim different units
- Synchronized volley fire
- Shared rewind timeline (vote on which attempt to accept)

---

## Pattern Bible Entry

### "The Divine Sniper"

**Scope**: Individual + Player Intervention

**Preconditions**:
- Manual aim enabled (tech unlock or miracle power)
- Entity with ranged weapon
- Player attention focused on entity
- Critical moment (boss fight, siege, duel)

**Gameplay Effects**:
- Time slows to 10% (or pauses)
- Trajectory visualization active
- Player aims precisely
- Rewind available for iteration
- On success: Satisfying "planned shot" dopamine hit
- On repeated success: Player feels like tactical genius

**Narrative Hook**: "The god-guided arrow that never misses, the sniper shot calculated across infinite timelines."

**Priority**: Core (this is a major feature pillar)

**Related Systems**: Manual Aim, Rewind, Time Control, Projectiles, Miracles

---

## Implementation Roadmap

### Phase 1: Prototype (2-3 weeks)
- [ ] Basic trajectory rendering (straight line)
- [ ] Time slow integration
- [ ] Mouse input → aim override
- [ ] Simple hit detection

### Phase 2: Prediction (2 weeks)
- [ ] Projectile physics simulation
- [ ] Target movement prediction
- [ ] Hit probability calculation
- [ ] Color-coded trajectory

### Phase 3: Rewind Integration (1 week)
- [ ] Record aim attempts in history buffer
- [ ] Timeline UI showing previous attempts
- [ ] Outcome preview on hover

### Phase 4: Godgame Influence (2 weeks)
- [ ] Post-fire trajectory bending
- [ ] Miracle point consumption
- [ ] Deflection limits
- [ ] Divine hand cursor + VFX

### Phase 5: Polish (2 weeks)
- [ ] Weak point highlighting
- [ ] Multi-trajectory preview
- [ ] Audio feedback
- [ ] Accessibility options
- [ ] Tutorial integration

**Total**: ~10 weeks for full implementation

---

## Success Metrics

**Player Engagement**:
- % of combat encounters using manual aim
- Average rewind attempts per aim (2-3 = good iteration loop)
- Player-reported satisfaction ("I felt like a badass" survey)

**Balance**:
- Win rate delta with/without manual aim (target: +15-20%)
- Time spent in aim mode vs. total combat time (target: <30%)
- Miracle point expenditure on aim vs. other miracles (target: 25-40%)

**Technical**:
- Trajectory prediction performance (<1ms per frame)
- Rewind storage overhead (<100KB per minute of combat)
- Zero UI lag when entering aim mode

---

## Conclusion

**Divine Guidance / Manual Aim** transforms PureDOTS from pure simulation into a hybrid simulation-action game where player skill directly impacts outcomes. Combined with rewind, it becomes a puzzle-solving tool: "How can I shape this battle to my desired outcome?"

**Space4X**: Tactical sniper gameplay, disable subsystems, feel like elite commander
**Godgame**: Divine intervention, miracle-powered influence, feel like active god

Both implementations leverage the same core systems (trajectory prediction, rewind, time control) with game-specific flavor and balancing. This is a **major feature pillar** that justifies standalone marketing ("Craft your own fate, one shot at a time").

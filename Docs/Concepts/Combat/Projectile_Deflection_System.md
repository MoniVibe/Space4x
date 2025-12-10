# Projectile Deflection & Point Defense System (Space4X)

## Overview
Projectiles in space combat are affected by gravitational fields (planets, stars, black holes), energy shields, magnetic storms, and explosive countermeasures. Mass and velocity determine deflection resistance. Projectiles can be shredded by point defense systems (CIWS, flak), producing damaging fragments. Cluster munitions spawn hundreds of sub-projectiles in wide arcs. Ships deploy defensive explosives and energy barriers to intercept incoming fire.

**Integration**: Body ECS (60 Hz) for projectile physics, Mind ECS (1 Hz) for fire control system calculations.

---

## Projectile Deflection Mechanics

### Gravitational Forces

**1. Planetary Gravity Wells**
```
Railgun round fired near Earth (low orbit, 400km altitude):
- Railgun velocity: 8,000 m/s
- Round mass: 5 kg
- Earth's gravity: 8.7 m/s² at 400km altitude
- Flight time to target (200km away): 25 seconds

Gravitational deflection:
- Downward acceleration: 8.7 m/s² × 25s = 217.5 m/s downward velocity gained
- Vertical drop: 0.5 × 8.7 × 25² = 2,718 meters (2.7 km drop!)

Result: Round misses target (aimed at enemy ship, hits empty space 2.7km below)

Fire Control Solution:
- Advanced targeting computers compensate automatically (+2.7km elevation adjustment)
- Low-tech ships: Manual compensation (gunner skill check, INT 70+)
```

**2. Black Hole Deflection** (Extreme Gravity):
```
Missile passes 100km from micro black hole (1 solar mass):
- Schwarzschild radius: 3 km
- Gravitational pull at 100km: Extreme (light bending)
- Missile velocity: 5,000 m/s
- Missile mass: 200 kg

Gravitational slingshot:
- Missile trajectory bent 45° toward black hole
- Missile accelerates (gains 2,000 m/s from gravity assist)
- New velocity: 7,000 m/s
- New direction: 45° off original course

Tactical use:
- Skilled pilots intentionally use black hole for "impossible shots"
- Missiles curve around obstacles, unpredictable trajectories
- Enemy point defense confused (tracks wrong trajectory)
```

**3. Binary Star System** (Complex Gravity):
```
Combat in binary star system:
- Two stars orbiting each other, L1 Lagrange point between them
- Projectiles crossing L1 point unpredictably deflected (chaotic gravity)

Railgun shot through L1 point:
- Random deflection: ±15° (changes each shot, unstable gravity)
- Hit chance: Reduced by 60% (targeting computers struggle)

Tactical doctrine:
- Avoid shooting through L1 points (too unpredictable)
- OR: Saturate area with hundreds of rounds (volume fire compensates)
```

### Energy Shields and Magnetic Fields

**1. Energy Shield Deflection**
```
Battleship protected by energy shield:
- Shield radius: 200m sphere
- Shield strength: 10,000 GJ (gigajoules stored)
- Deflection mechanism: Electromagnetic repulsion

Incoming railgun round:
- Velocity: 6,000 m/s
- Mass: 8 kg
- Kinetic energy: 0.5 × 8 × 6000² = 144 MJ

Shield deflection:
- Round enters shield boundary
- Electromagnetic force applied: 50,000 N (lateral push)
- Deflection angle: 35° (sufficient to miss ship)
- Shield energy drained: 200 MJ (0.002% of total)

Result: Round deflected harmlessly, ship undamaged

Shield overload scenario:
- 100 railgun rounds hit simultaneously (14.4 GJ total kinetic energy)
- Shield depletes: 10 GJ → 0 GJ (shields down)
- Last 30 rounds penetrate (shields failed), hit hull
```

**2. Magnetic Storm Deflection**
```
Combat near pulsar (strong magnetic field):
- Magnetic field strength: 10⁸ Tesla (extreme)
- Affects metallic projectiles, ionized particles

Railgun round (ferromagnetic penetrator):
- Deflected 80° by magnetic field (extremely powerful)
- Tumbles uncontrollably (loses stabilization)
- Hits random target 5km off-course

Countermeasure:
- Use non-ferromagnetic rounds (tungsten, depleted uranium)
- Reduces deflection to 10° (manageable)
```

**3. Ion Storm** (Charged Particle Cloud):
```
Ion storm in region:
- Ionized hydrogen cloud, 1000 km³
- Deflects lasers (beam refraction)
- Ionizes projectiles (charge buildup)

Laser beam through ion storm:
- Beam refracts 25° (unpredictable scatter)
- Beam energy absorbed 40% (reduced damage)
- Effective range: Reduced from 100,000 km to 40,000 km

Railgun round:
- Round becomes charged (ionization)
- Magnetic deflection from own charge (spiral trajectory)
- Inaccuracy: ±12° (targeting computers compensate partially)
```

### Mass and Velocity Deflection Resistance

**Light Projectiles** (High Deflection):
```
Missile:
- Mass: 200 kg
- Velocity: 4,000 m/s
- Deflection from 5,000N gravity: 8° angle change (noticeable)

Laser Beam (photons):
- Mass: ~0 (photons)
- Velocity: 299,792,458 m/s (light speed)
- Deflection from 5,000N gravity: 0.001° (gravitational lensing, minimal)

Flechette (anti-personnel):
- Mass: 0.05 kg (50g tungsten dart)
- Velocity: 1,500 m/s
- Deflection from 5,000N gravity: 25° angle change (highly susceptible)
```

**Heavy Projectiles** (Low Deflection):
```
Railgun Round (Battleship-class):
- Mass: 50 kg (massive penetrator)
- Velocity: 10,000 m/s
- Deflection from 5,000N gravity: 0.5° angle change (barely affected)

Kinetic Impactor (Orbital Bombardment):
- Mass: 5,000 kg (tungsten rod)
- Velocity: 15,000 m/s (dropped from orbit)
- Deflection from 5,000N gravity: 0.02° angle change (essentially immune)

Nuclear Warhead Missile:
- Mass: 2,000 kg
- Velocity: 6,000 m/s
- Deflection from 5,000N gravity: 1.5° angle change (minor)
```

**High Velocity Projectiles** (Reduced Deflection):
```
Hypervelocity Railgun (Experimental):
- Mass: 2 kg (small round, ultra-high velocity)
- Velocity: 30,000 m/s (0.01% light speed)
- Deflection from 5,000N gravity: 0.1° angle change (speed compensates for low mass)

Laser Pulse (Concentrated):
- Velocity: 299,792 km/s
- Deflection: Negligible except near black holes (gravitational lensing)
```

---

## Energy Shield Deflection

### Active Shield Systems

**1. Electromagnetic Shield** (Charged Particle Deflection):
```
Cruiser equipped with EM shield:
- Shield type: Electromagnetic repulsion
- Effective range: 150m from hull
- Power draw: 500 MW continuous

Incoming plasma bolt (4,000 m/s, ionized particles):
- Bolt enters shield perimeter
- EM force pushes bolt 40° off-course
- Bolt misses ship

Limitation:
- Only works on charged projectiles (plasma, ions, charged kinetics)
- Neutral projectiles (unpowered missiles) pass through unaffected
- Laser beams unaffected (photons uncharged)
```

**2. Kinetic Barrier** (Force Field):
```
Capital ship kinetic barrier:
- Shield type: Graviton field (artificial gravity)
- Radius: 300m dome
- Strength: 8,000 GJ capacity

Incoming railgun volley (20 rounds, 6,000 m/s each):
- Each round: 144 MJ kinetic energy
- Total volley: 2.88 GJ
- Barrier deflects all rounds (35-50° deflection)
- Barrier depleted: 8,000 GJ → 5,000 GJ (38% capacity remaining)

Overload:
- Next volley (30 rounds): 4.32 GJ
- Barrier fails (insufficient energy)
- Rounds 1-20: Deflected (shields collapse after 20th hit)
- Rounds 21-30: Penetrate, hit hull
```

**3. Ablative Shield** (Vaporization Defense):
```
Ablative shield (sacrificial armor layer):
- Material: Ice/water vapor (cheap, effective)
- Thickness: 2m layer, 500m² coverage
- Total mass: 1,000,000 kg (1,000 tons)

Incoming laser beam (1 GJ pulse):
- Beam hits ablative layer
- Vaporizes 100 kg ice (latent heat of vaporization)
- Steam expands, disperses beam energy
- Ship hull undamaged

Depletion:
- 10 laser hits: 1,000 kg ablative consumed (0.1% of shield)
- Shield lasts 10,000 hits before depletion
- Resupply: Harvest ice from asteroids/comets (cheap refill)
```

### Psionic Barriers (Advanced Civilizations)

**Telekinetic Shield** (Psion-powered):
```
Psion aboard ship generates kinetic barrier:
- Psion power: PSI 95 (extremely rare talent)
- Barrier radius: 50m
- Duration: 5 minutes (concentration, exhaustion afterward)

Barrier effects:
- Deflects 1-5 projectiles per second (psion's reaction limit)
- Deflection angle: 60-90° (very effective)
- Cannot block sustained fire (overwhelmed if >5 projectiles/second)

Cost:
- Extreme psi drain (psion incapacitated for 6 hours after)
- Emergency use only (last-ditch defense)
```

---

## Projectile Shredding & Point Defense

### CIWS (Close-In Weapon Systems)

**1. Rotary Autocannon** (Kinetic CIWS):
```
Frigate CIWS specs:
- Weapon: 30mm rotary autocannon
- Rate of fire: 6,000 rounds/minute (100 rds/sec)
- Effective range: 3 km
- Ammo: 20,000 rounds (200 seconds sustained fire)

Engagement scenario:
- Incoming missile (5,000 m/s, 15 km away)
- CIWS detects at 15 km, engages at 5 km
- Time to impact: 1 second (5 km / 5,000 m/s)
- Rounds fired: 100 rounds (1 second × 100 rds/sec)

Interception:
- 8 rounds hit missile (8% hit rate at 5 km)
- Missile shredded (8 × 30mm explosive rounds)
- Missile detonates prematurely (500m from ship)
- Shrapnel hits ship (minor damage, better than direct hit)
```

**2. Laser CIWS** (Directed Energy):
```
Destroyer laser CIWS:
- Weapon: 10 MW continuous wave laser
- Beam focus: 0.1m diameter at 10 km
- Engagement time: 0.5 seconds per target
- Power supply: 1 GJ capacitor (100 shots before recharge)

Engagement scenario:
- Incoming missile volley (30 missiles, 4,000 m/s, 40 km away)
- CIWS engages at 30 km
- Time available: 7.5 seconds (30 km / 4,000 m/s)
- Targets engaged: 15 missiles (7.5s / 0.5s per target)

Results:
- 12 missiles destroyed (80% kill rate, laser precision)
- 3 missiles damaged (warheads disabled, coasting debris)
- 15 missiles penetrate (CIWS saturated, insufficient time)

Ship damage:
- 15 missiles hit (nuclear warheads)
- Ship destroyed (CIWS overwhelmed by volume)

Tactical lesson: Saturation attacks defeat CIWS (volume > precision)
```

**3. Flak Cannons** (Fragmentation Cloud):
```
Battleship flak battery:
- Weapon: 8× 120mm flak cannons
- Shell: Proximity-fused fragmentation rounds
- Detonation range: 1 km proximity to target
- Fragment count: 500 fragments per shell, 280° upward arc

Engagement scenario:
- Incoming fighter squadron (12 fighters, 2,000 m/s)
- Flak battery fires barrage (8 shells) at predicted intercept point
- Shells detonate in fighter formation

Fragmentation effects:
- 4,000 fragments (8 shells × 500 fragments)
- Fragments spread across 5 km³ volume
- Average 50 fragments per fighter path

Results:
- 6 fighters destroyed (direct fragment hits)
- 4 fighters damaged (engines hit, limping)
- 2 fighters evade (skilled pilots, detected flak early)

Effectiveness: 83% attrition (10/12 fighters lost or crippled)
```

### Mid-Flight Projectile Shredding

**1. Explosion Interception**
```
Cruiser fires anti-missile missile at incoming railgun volley:
- Volley: 50 railgun rounds, 7,000 m/s
- Anti-missile: Nuclear-pumped fragmentation warhead
- Detonation: 2 km from cruiser, in volley path

Shredding results:
- 20 rounds: Direct exposure, vaporized (nuclear flash)
- 15 rounds: Partial exposure, shredded into fragments (fragmentation cloud)
- 10 rounds: Deflected (blast wave push, 20-40° deflection)
- 5 rounds: Missed (outside blast radius)

Fragments from shredded rounds:
- 15 rounds × 8 fragments each = 120 fragments
- Each fragment: 15 damage (vs 80 damage intact round)
- Fragments scatter, 40 hit ship (total 600 damage)

Comparison:
- Without interception: 50 rounds × 80 damage = 4,000 damage (catastrophic)
- With interception: 5 rounds + 40 fragments = 400 + 600 = 1,000 damage (survivable)

Defensive value: 75% damage reduction
```

**2. Energy Net** (Experimental Defense):
```
Capital ship deploys energy net:
- Net: Web of high-energy plasma beams (10,000 MW)
- Coverage: 1 km² grid
- Duration: 5 seconds (massive power draw)

Incoming missile swarm enters net:
- 100 missiles (2,000 m/s, 100 kg each)
- Net: Plasma beams vaporize/shred light projectiles

Results:
- 70 missiles: Vaporized (direct plasma exposure)
- 20 missiles: Shredded (partial exposure, fragmented)
- 10 missiles: Penetrate (heavy missiles, armored nose cones)

Effectiveness: 90% attrition (10/100 missiles survive)

Cost: 50 GJ energy (one use per battle, capacitor recharge 30 minutes)
```

---

## Cluster Munitions & Fragmentation

### Flechette Warheads (Anti-Ship)

**Flechette Missile**:
```
Missile specs:
- Warhead: 10,000 tungsten flechettes (50g each, 500 kg total)
- Detonation: 5 km from target (proximity fuse)
- Spread: 280° forward arc (cone toward target)

Detonation:
- Flechettes launch at 3,000 m/s (explosive charge)
- Spread across 2 km² area by time they reach target
- Average flechette density: 5 flechettes per m²

Target: Destroyer (1,000 m² cross-section):
- 5,000 flechettes hit ship (1,000 m² × 5 flechettes/m²)
- Each flechette: 8 damage (high velocity, penetrates light armor)
- Total damage: 40,000 damage

Result:
- Destroyer hull breached in 300 locations
- Atmosphere venting, fires, crew casualties
- Ship crippled (not destroyed, but combat-ineffective)

Countermeasure:
- Point defense shoots down missile before detonation (standard doctrine)
- If missile detonates early (10 km range), flechette spread too wide (20% hit rate)
```

### Cluster Missiles (Anti-Fighter)

**Cluster Missile**:
```
Missile specs:
- Payload: 50 sub-munitions (each 2 kg, guided micromissiles)
- Detonation: 10 km from fighter squadron (wide area coverage)
- Sub-munition speed: 4,000 m/s

Engagement:
- Fighter squadron (8 fighters, 3 km² formation)
- Cluster missile releases 50 sub-munitions
- Each sub-munition autonomously targets nearest fighter

Results:
- Each fighter targeted by 6-7 sub-munitions (50 / 8)
- Fighter point defense destroys 3-4 sub-munitions each
- 2-3 sub-munitions hit each fighter

Fighter casualties:
- 6 fighters destroyed (2-3 hits fatal for light fighters)
- 2 fighters survive (lucky, better defense)

Effectiveness: 75% kill rate (6/8 fighters)
```

### Shrapnel Torpedoes (Capital Ship Weapon)

**Shrapnel Torpedo**:
```
Torpedo specs:
- Warhead: 5,000 kg steel casing + 500 kg high explosive
- Detonation: Impact on target or proximity (100m)
- Fragment count: 80,000 fragments (averaging 60g each)

Impact detonation on battleship:
- Torpedo hits battleship armor (doesn't penetrate)
- Detonates on surface, fragments spray in 280° arc forward
- Fragments ricochet off armor at angles

Fragment damage:
- 60,000 fragments hit ship (75% of fragments)
- Armor deflects 90% fragments (heavy armor)
- 6,000 fragments penetrate weak points (sensor arrays, antenna, exposed systems)
- Each fragment: 5 damage (small, but numerous)
- Total damage: 30,000 damage (not fatal, but degraded systems)

Result:
- Battleship hull intact (armor held)
- Sensors destroyed (blind)
- Communications destroyed (isolated)
- Point defense destroyed (defenseless)
- Battleship mission-killed (combat-ineffective)

Tactical use: Disable capital ships before main assault
```

---

## Defensive Explosives & Countermeasures

### Active Defense Systems

**1. Missile Defense Missiles**
```
Battleship carries 200 interceptor missiles:
- Interceptor specs: 8,000 m/s, 50 kg, fragmentation warhead
- Engagement range: 50 km (intercept before enemy missiles arrive)

Combat scenario:
- Enemy launches 150 missiles at battleship
- Battleship launches 100 interceptors (2:3 ratio)

Interception results:
- 80 interceptors hit (80% success rate)
- Each interceptor destroys 1-2 enemy missiles (fragmentation cloud)
- Total enemy missiles destroyed: 120 (80% attrition)
- 30 enemy missiles penetrate (battleship CIWS engages)

CIWS results:
- CIWS destroys 20 missiles
- 10 missiles hit battleship (nuclear warheads)

Result: Battleship heavily damaged but survives (layered defense)
```

**2. Chaff Clouds** (Decoy/Deflection):
```
Cruiser deploys chaff cloud:
- Chaff: 10 tons aluminum strips, released in cloud
- Cloud size: 1 km³
- Duration: 5 minutes (dispersion)

Effects:
- Laser beams: Scattered by chaff (50% energy loss)
- Radar-guided missiles: Confused (lock on chaff instead of ship)
- Kinetic rounds: Deflected/tumbled by chaff impacts (5-10° deflection)

Incoming missile attack:
- 30 missiles (radar-guided homing)
- 20 missiles lock chaff cloud, explode prematurely (67% success)
- 10 missiles track ship (IR backup guidance)

Cruiser survives: Chaff saved ship from 67% of attack
```

**3. Explosive Reactive Armor** (ERA):
```
Tank (ground unit) with ERA tiles:
- Tile: 5 kg explosive sandwiched in armor plates
- Coverage: 500 ERA tiles across hull
- Mechanism: Incoming round triggers tile explosion, counteracts penetration

Incoming anti-tank missile:
- Missile: HEAT warhead (shaped charge, jet penetration)
- Missile hits ERA tile
- ERA detonates, blast wave disrupts shaped charge jet
- Jet loses 80% penetration capability

Result:
- Jet penetrates only 50mm (vs 250mm without ERA)
- Tank armor (300mm): Holds, tank survives
- ERA tile consumed (one-time use, that section now vulnerable)

Limitation: Only protects each section once (no regeneration)
```

**4. Ship Point Defense Rockets**
```
Destroyer carries 50 defensive rockets:
- Rocket: 100 kg, fragmentation warhead, 5 km range
- Purpose: Create shrapnel cloud in path of incoming fire

Combat scenario:
- Incoming railgun volley (80 rounds, tightly grouped)
- Destroyer fires 10 defensive rockets at predicted intercept point
- Rockets detonate, create shrapnel cloud (1 km³)

Interception:
- 50 railgun rounds pass through cloud (62% of volley)
- 30 rounds shredded/deflected by shrapnel (38% attrition)
- 50 rounds hit destroyer (heavy damage, but not destroyed)

Comparison:
- Without defense: 80 rounds hit (ship destroyed)
- With defense: 50 rounds hit (ship crippled but alive)

Defensive value: 37.5% damage reduction
```

---

## Fire Control & Prediction Systems

### AI Targeting Compensation

**Basic Targeting Computer** (Early Tech):
```
Frigat with basic computer:
- Compensation: Gravity, target movement
- Prediction: 1 second ahead (linear extrapolation)
- Accuracy: ±50m at 10 km range

Engagement:
- Target: Enemy cruiser, 10 km away, accelerating
- Computer predicts cruiser position 1 second ahead
- Cruiser changes acceleration (evasive maneuver)
- Shot misses by 200m (prediction failed)

Hit rate: 30% (basic computer struggles with evasion)
```

**Advanced Fire Control** (AI-assisted):
```
Battleship with AI targeting:
- Compensation: Gravity, magnetic fields, ion storms
- Prediction: 5 seconds ahead (pattern analysis)
- Accuracy: ±5m at 100 km range

Engagement:
- Target: Enemy battleship, 80 km away, evading
- AI analyzes evasion pattern (detects rhythm)
- Predicts maneuver 3 seconds ahead
- Shot hits (AI predicted correctly)

Hit rate: 85% (AI dominates targeting)
```

### Manual Gunner Skill

**Human Gunner** (Skill-based):
```
Gunner stats: INT 80, PER 90
- Can compensate for gravity (INT check)
- Can predict enemy movement (PER check)
- Cannot compensate for complex fields (ion storms, black holes)

Engagement in standard conditions:
- Hit rate: 60% (skilled human, good conditions)

Engagement in ion storm:
- Hit rate: 20% (human struggles, AI required)

Tactical doctrine: Human gunners for standard engagements, AI for complex scenarios
```

---

## ECS Integration

### Body ECS (60 Hz) - Projectile Physics

**Systems**:
- `ProjectileDeflectionSystem`: Apply gravitational, magnetic, energy shield forces
- `ProjectileShreddingSystem`: CIWS hits, explosion interceptions, energy net destruction
- `FragmentSpawnSystem`: Flechette releases, shrapnel torpedo detonation, railgun shredding
- `BlastWaveSystem`: Defensive explosions, missile intercepts, flak bursts
- `EnergyShieldSystem`: Kinetic barriers, EM shields, ablative vaporization

**Components**:
```csharp
public struct ProjectileComponent : IComponentData
{
    public float3 Position;
    public float3 Velocity;            // m/s
    public float Mass;                 // kg
    public float StructuralIntegrity;  // 0-1 (HP of projectile)
    public ProjectileType Type;        // Railgun, Missile, Laser, Flechette, Fragment
    public bool IsGuided;              // Missile vs ballistic
}

public struct GravityFieldComponent : IComponentData
{
    public float3 GravitySource;       // Planet, star, black hole position
    public float GravitationalConstant; // G × mass (m³/s²)
    public float EventHorizon;         // Black hole only (m, inside = destroyed)
}

public struct EnergyShieldComponent : IComponentData
{
    public Entity ProtectedShip;
    public float3 ShieldCenter;
    public float ShieldRadius;         // m
    public float ShieldStrength;       // GJ (energy capacity)
    public float DeflectionForce;      // N (force applied to projectiles)
    public ShieldType Type;            // Kinetic, EM, Ablative
}

public struct CIWSComponent : IComponentData
{
    public Entity ShipEntity;
    public float3 MountPosition;
    public float EngagementRange;      // m (3-50 km typical)
    public int RoundsPerSecond;        // Rate of fire
    public int RemainingAmmo;
    public CIWSType Type;              // Autocannon, Laser, Flak
}

public struct ClusterWarheadComponent : IComponentData
{
    public int SubmunitionCount;       // 50-10,000 submunitions
    public float SubmunitionMass;      // kg
    public float ReleaseDistance;      // m (distance from target to release)
    public float SpreadAngle;          // 280° typical
    public bool IsGuided;              // Smart submunitions vs dumb flechettes
}

public enum ProjectileType : byte
{
    RailgunRound,
    Missile,
    LaserBeam,
    Flechette,
    Fragment,
    Torpedo,
    KineticImpactor
}

public enum ShieldType : byte
{
    Kinetic,         // Force field, deflects all projectiles
    Electromagnetic, // Deflects charged particles only
    Ablative,        // Vaporizes to absorb energy
    Psionic          // Telekinetic barrier (rare)
}

public enum CIWSType : byte
{
    Autocannon,  // Kinetic, short range, high volume
    Laser,       // Energy, long range, precision
    Flak,        // Fragmentation cloud, area denial
    Missile      // Interceptor missiles, long range
}
```

### Mind ECS (1 Hz) - Fire Control

**Systems**:
- `FireControlSystem`: AI targeting calculations, prediction, compensation
- `GunnerySkillSystem`: Human gunner skill checks, accuracy modifiers
- `TacticalDecisionSystem`: Choose defensive countermeasures (chaff, interceptors, ERA)

**Components**:
```csharp
public struct FireControlComponent : IComponentData
{
    public Entity WeaponSystem;
    public bool IsAIAssisted;          // AI vs manual gunner
    public int AIProcessingPower;      // 0-100 (AI quality)
    public int GunnerSkill;            // 0-100 (human gunner INT+PER)
    public float PredictionTime;       // Seconds ahead (1-5s typical)
    public float Accuracy;             // ±meters at reference range
}

public struct TargetPredictionComponent : IComponentData
{
    public Entity TargetEntity;
    public float3 PredictedPosition;   // Where target will be when shot arrives
    public float3 PredictedVelocity;
    public float PredictionConfidence; // 0-1 (how certain AI is)
    public bool IsEvading;             // Target performing evasive maneuvers
}
```

---

## Example Scenarios

### Scenario 1: Gravity Slingshot Attack
```
Frigate engages enemy destroyer near Jupiter:
- Frigate fires missile toward Jupiter's gravity well
- Missile trajectory: 45° off target, seemingly missing

Gravitational slingshot:
- Missile accelerates toward Jupiter (gravity assist)
- Gains 5,000 m/s velocity (15,000 m/s total)
- Curves around Jupiter, trajectory bends 90°
- Now on intercept course with destroyer

Destroyer's response:
- Targeting computer predicts linear trajectory (missile will miss)
- Missile curves unexpectedly (computer didn't account for Jupiter)
- Point defense surprised, engages late
- Missile hits destroyer (unexpected angle, overwhelmed defense)

Result: Skilled frigate pilot uses gravity for "impossible shot"
```

### Scenario 2: CIWS Saturation Attack
```
Battleship faces missile swarm:
- Incoming: 200 missiles (saturation attack)
- Battleship defenses:
  - 4× laser CIWS (15 targets each, 60 total)
  - 8× autocannon CIWS (10 targets each, 80 total)
  - 100 interceptor missiles (100 targets)

Defensive sequence:
- Interceptors engage at 50 km: Destroy 80 missiles (80% success)
- 120 missiles survive, continue approach
- Laser CIWS engage at 30 km: Destroy 60 missiles (all 4 CIWS saturated)
- 60 missiles survive, continue approach
- Autocannon CIWS engage at 5 km: Destroy 40 missiles (rapid fire, close range)
- 20 missiles penetrate defenses

Impact:
- 20 nuclear missiles hit battleship
- Battleship destroyed (catastrophic damage)

Lesson: Volume defeats point defense (200 missiles vs 240 intercepts = 20 leakers)
```

### Scenario 3: Flechette Warhead vs Fighter Wing
```
Cruiser engages fighter wing (20 fighters):
- Launches flechette missile at wing
- Missile detonates 8 km from fighters
- 50,000 flechettes spray forward (280° cone)

Flechette cloud:
- Spreads to 4 km² by time it reaches fighters
- Fighter wing formation: 3 km²
- Average flechette density: 12 flechettes per m²

Fighter cross-sections:
- Each fighter: 50 m² (small, agile)
- Each fighter hit by 600 flechettes (50 m² × 12)

Damage:
- Each flechette: 4 damage (light, but numerous)
- Each fighter: 600 × 4 = 2,400 damage
- Fighter durability: 1,200 HP

Result:
- All 20 fighters destroyed instantly (flechette cloud lethal)
- Cruiser eliminates entire wing with single missile

Countermeasure:
- Fighters detect launch, scatter formation (5 km² spread)
- Flechette density reduced to 5/m²
- Each fighter hit by 250 flechettes = 1,000 damage
- 8 fighters destroyed, 12 survive (dispersed formation saved lives)
```

### Scenario 4: Energy Shield Overload
```
Destroyer with kinetic shield (5,000 GJ capacity):
- Shield deflects all projectiles <500 MJ per round
- Shield recharge: 100 GJ/second (slow)

Railgun duel:
- Enemy battleship fires 10-round volley
- Each round: 200 MJ kinetic energy
- Total volley: 2,000 MJ = 2 GJ

Shield deflection:
- All 10 rounds deflected (well within capacity)
- Shield: 5,000 GJ → 4,998 GJ (0.04% drain)

Sustained fire:
- Battleship fires 10 volleys (100 rounds over 20 seconds)
- Total energy: 20 GJ
- Shield: 5,000 GJ → 3,000 GJ (40% capacity, recharge can't keep up)

Critical barrage:
- Battleship fires massed volley (50 rounds simultaneously)
- Total energy: 10 GJ
- Shield: 3,000 GJ → 0 GJ (shields depleted on 30th round)
- Last 20 rounds penetrate, hit destroyer hull

Result: Destroyer crippled (shield overloaded by sustained + massed fire)
```

---

## Key Design Principles

1. **Mass and Velocity Dominate**: Heavy/fast projectiles resist deflection, light/slow projectiles easily deflected
2. **Gravity Wells Are Hazards**: Planetary orbits, black holes, binary stars create unpredictable deflections
3. **Energy Shields Depletable**: Shields block damage until energy exhausted (finite capacity)
4. **CIWS Saturatable**: Point defense has limits (engage 10-100 targets, then overwhelmed)
5. **Fragmentation Multiplies Threat**: Flechettes/fragments create thousands of damaging sub-projectiles
6. **Layered Defense Critical**: Interceptors → CIWS → shields → armor (multiple layers prevent single-point failure)
7. **AI Targeting Superior**: AI compensates for complex fields (ion storms, magnetic deflection) better than humans
8. **Volume Defeats Precision**: Saturation attacks (200 missiles) overwhelm point defense (100 intercepts)
9. **Defensive Explosives Viable**: Anti-missile missiles, chaff clouds, flak bursts reduce incoming fire 40-70%
10. **Gravity Slingshots Tactical**: Skilled pilots use planetary gravity for "impossible shots" (curve around obstacles)

---

**Integration with Other Systems**:
- **Infiltration Detection**: Defensive turrets auto-engage detected infiltrator ships
- **Crisis Alert States**: High alert = all CIWS active, interceptors ready (anticipate attack)
- **Blueprint System**: Improved flechette designs (tighter spread, higher velocity)
- **Permanent Augmentation**: Cybernetic targeting implants (human gunners gain AI-level accuracy)

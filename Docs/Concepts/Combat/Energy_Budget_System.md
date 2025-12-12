# Energy Budget and Overcharge System - Space4X

## Overview

Space combat platforms (mechs, fighters, corvettes, destroyers, battleships) operate on fusion or antimatter reactors with finite power output. Modules can be **overcharged** beyond baseline specifications for enhanced performance, or **underpowered** to conserve energy. Power can be **dynamically redistributed** between systems through a **focus mode** interface, enabling tactical adaptation in real-time.

**Core Systems:**
- **Reactor Core**: Generates megawatts (MW) of power
- **Power Capacitors**: Store surplus energy for burst overcharge
- **Dynamic Allocation**: Redirect power from non-critical to critical systems
- **Thermal Management**: Overcharged systems generate exponentially more heat
- **Emergency Modes**: All-in configurations for desperate situations

---

## Power Generation

### Reactor Types

**1. Fusion Micro-Reactor (Light Mechs, Fighters)**
- Output: 800-1,500 MW
- Fuel: Deuterium-Tritium pellets
- Efficiency: 82%
- Idle draw: 150 MW (containment fields)
- Hot restart time: 0.5 seconds
- Cold restart time: 30 seconds

**2. Standard Fusion Reactor (Medium Mechs, Corvettes)**
- Output: 2,000-4,000 MW
- Fuel: Deuterium-Helium-3
- Efficiency: 88%
- Idle draw: 300 MW
- Hot restart: 1 second
- Cold restart: 60 seconds

**3. Heavy Fusion Reactor (Heavy Mechs, Destroyers)**
- Output: 4,500-10,000 MW
- Fuel: D-He3 + Boron-11
- Efficiency: 92%
- Idle draw: 600 MW
- Hot restart: 2 seconds
- Cold restart: 90 seconds

**4. Capital Antimatter Reactor (Titans, Battleships)**
- Output: 12,000-50,000 MW
- Fuel: Antimatter (contained)
- Efficiency: 98%
- Idle draw: 2,000 MW (magnetic containment)
- Hot restart: 5 seconds
- Cold restart: 180 seconds
- Risk: Containment failure = 5 kiloton explosion

---

### Reactor Overcharge

Reactors themselves can be overcharged:

```
Normal Operation (100%): Rated output, 10,000 hour lifespan
Boosted (120%): +20% output, 30% more heat, 7,000 hour lifespan
Overcharged (150%): +50% output, +125% heat, 3,000 hour lifespan
Emergency (200%): +100% output, +300% heat, 500 hour lifespan, 5% meltdown risk per hour
```

**Example:**
Standard Fusion Reactor (2,000 MW):
- Normal: 2,000 MW, 10,000 hours to next overhaul
- Boosted (120%): 2,400 MW, 7,000 hours
- Overcharged (150%): 3,000 MW, 3,000 hours
- Emergency (200%): 4,000 MW, 500 hours, 5% meltdown risk/hour

**Tactical Use**: Emergency overcharge during critical battles, accept reduced lifespan

---

## Power Consumption by System

### Baseline Draw (Strike Mech, 2,000 MW reactor)

**Life Support**: 80 MW (constant)
**Avionics**: 120 MW (sensors, comms, nav)
**Mobility (Idle)**: 150 MW (servos, gyros)
**Mobility (Combat)**: 400 MW (high-speed maneuvering)
**Weapons (Standby)**: 50 MW per weapon
**Weapons (Firing)**: 300-1,200 MW per weapon (burst)
**Shields**: 600 MW (active, taking hits)
**Targeting Computer**: 100 MW

**Idle Configuration:**
- Life support: 80 MW
- Avionics: 120 MW
- Mobility idle: 150 MW
- 2× Weapons standby: 100 MW
- **Total**: 450 MW (22% of reactor capacity)

**Combat Configuration (All Systems Active):**
- Life support: 80 MW
- Avionics: 120 MW
- Mobility combat: 400 MW
- 2× Railguns firing: 2,400 MW (burst)
- Shields active: 600 MW
- Targeting: 100 MW
- **Total**: 3,700 MW (185% of reactor capacity, **unsustainable**)

**Solution**: Use power focus modes or burst firing tactics

---

## Module Overcharge Performance

### Weapons Overcharge

**Railgun (baseline 800 MW, 1,400 damage)**

```
Power%   MW Draw   Damage   Heat (MW)   Capacitor Drain   Barrel Wear
100%     800       1,400    400         None              1× (baseline)
125%     1,000     1,680    625         None              1.56×
150%     1,200     1,960    900         Recommended       2.25×
175%     1,400     2,170    1,225       Required          3.06×
200%     1,600     2,520    1,600       Required          4×
225%     1,800     2,660    2,025       Required          5.06× (barrel failure risk: 8%/shot)
```

**Damage Scaling**: `Damage = Base × (0.6 + (Power% × 0.007))`

**Example:**
- 150% power: 1,400 × (0.6 + (150 × 0.007)) = 1,400 × 1.65 = **2,310 damage**
- Actual (with diminishing returns): **1,960 damage** (40% increase for 50% more power)

---

### Shield Overcharge

**Energy Shield (baseline 600 MW, 3,500 HP barrier)**

```
Power%   MW Draw   Shield HP   Recharge Delay   Overload Risk
100%     600       3,500       2.0 sec          0%
125%     750       4,200       1.7 sec          0%
150%     900       4,900       1.4 sec          2%/min
175%     1,050     5,425       1.1 sec          6%/min
200%     1,200     5,950       0.8 sec          15%/min
```

**Shield HP Scaling**: `HP = Base × (0.7 + (Power% × 0.006))`

**Overload Risk**: Shield emitters fail, must reboot (30 sec downtime)

---

### Sensor Overcharge

**Sensor Suite (baseline 100 MW, 800m range)**

```
Power%   MW Draw   Range (m)   ECM Resistance   Stealth Detection
100%     100       800         Base             TL 6 and below
125%     125       1,040       +15%             TL 7
150%     150       1,280       +35%             TL 8
175%     175       1,456       +60%             TL 9
200%     200       1,632       +90%             TL 10 (all stealth)
```

**Range Scaling**: `Range = Base × (0.5 + (Power% × 0.0065))`

---

### Mobility Overcharge

**Leg Servos (baseline 300 MW, 18 m/s speed)**

```
Power%   MW Draw   Speed (m/s)   Jump Distance   Actuator Wear
100%     300       18            80m             1×
125%     375       21.6          104m            1.56×
150%     450       25.2          128m            2.25×
175%     525       28.08         145.6m          3.06× (joint failure risk)
200%     600       30.96         163.2m          4× (high failure risk)
```

**Speed Scaling**: `Speed = Base × (0.6 + (Power% × 0.008))`

---

## Power Focus Modes

### 1. Balanced (Default)

All systems at 100% power.

**Strike Mech Example:**
- Weapons: 100% (1,400 damage per railgun)
- Shields: 100% (3,500 HP)
- Sensors: 100% (800m range)
- Mobility: 100% (18 m/s)
- **Idle draw**: 450 MW
- **Combat draw**: 3,700 MW (burst)

---

### 2. Attack Focus (Alpha Strike)

Weapons overcharged, defense minimized.

**Configuration:**
- Weapons: 175% (2,170 damage, **+55%**)
- Shields: 50% (1,750 HP, -50%)
- Sensors: 75% (680m range, -15%)
- Mobility: 100% (18 m/s)

**Power Draw:**
- Weapons: 1,400 MW each × 2 = 2,800 MW
- Shields: 300 MW
- Sensors: 75 MW
- Mobility: 300 MW (idle)
- Life support + avionics: 200 MW
- **Total combat**: 3,675 MW (183% of reactor, **unsustainable**)

**Solution**: Burst fire (3 second volley, 10 second cooldown), use capacitor banks

**Tactic**: Ambush predator, overwhelming first strike, disengage before retaliation

---

### 3. Defense Focus (Fortress Mode)

Shields and armor maximized, offense reduced.

**Configuration:**
- Weapons: 75% (1,050 damage, -25%)
- Shields: 175% (5,425 HP, **+55%**)
- Sensors: 100% (800m)
- Mobility: 50% (9 m/s, -50%, **anchored stance**)

**Power Draw:**
- Weapons: 600 MW × 2 = 1,200 MW
- Shields: 1,050 MW
- Sensors: 100 MW
- Mobility: 150 MW (idle)
- Life support + avionics: 200 MW
- **Total combat**: 2,700 MW (135% of reactor)

**Deficit**: 2,700 - 2,000 = -700 MW

**Capacitor support**: Draw from capacitors for shield surge (20 second endurance with 4,000 MW·s capacitor)

**Tactic**: Hold chokepoint, tank incoming fire, outlast attacker

---

### 4. Mobility Focus (Hit-and-Run)

Maximum speed and evasion, minimal armor.

**Configuration:**
- Weapons: 100% (1,400 damage)
- Shields: 0% (**disabled**)
- Sensors: 125% (1,040m, **early warning**)
- Mobility: 175% (28 m/s, **+56%**)

**Power Draw:**
- Weapons: 800 MW × 2 = 1,600 MW
- Shields: 0 MW
- Sensors: 125 MW
- Mobility: 525 MW
- Life support + avionics: 200 MW
- **Total combat**: 2,450 MW (122% of reactor)

**Deficit**: -450 MW (sustainable for 30 seconds from reserves)

**Tactic**: Kite enemies, strike from long range, evade retribution

---

### 5. Stealth Focus (Ghost Ops)

Maximum sensor power and stealth, minimal combat capability.

**Configuration:**
- Weapons: 50% (700 damage, **minimal signatures**)
- Shields: 0% (disabled)
- Sensors: 200% (1,632m range, **detect all stealth**)
- Mobility: 75% (13.5 m/s, **silent running**)
- Active Camouflage: 200% (98% stealth, **near invisible**)

**Power Draw:**
- Weapons: 400 MW × 2 = 800 MW
- Sensors: 200 MW
- Camouflage: 1,200 MW
- Mobility: 225 MW
- Life support + avionics: 200 MW
- **Total**: 2,625 MW (131% of reactor)

**Tactic**: Reconnaissance, infiltration, assassination (single perfect shot, then flee)

---

### 6. Emergency Power (Last Stand)

All non-essential systems offline, single critical system at maximum.

**Configuration (Weapon Focus):**
- Primary Weapon: 250% (3,150 damage, **+125%**, **extreme overcharge**)
- All other systems: **Disabled**

**Power Draw:**
- Primary weapon: 2,000 MW
- Life support: 80 MW (minimal)
- **Total**: 2,080 MW

**Capacitor Burst**: Add 2,000 MW from capacitor for 10 seconds
- Effective weapon power: 4,000 MW = **300% overcharge**
- Damage: 1,400 × (0.6 + (300 × 0.007)) = **4,340 damage** (but capped at 250% = 3,500 damage)
- **Heat**: 1,600 MW × (300/100)² = **14,400 MW** (instant overheat, likely barrel meltdown)

**Use Case**: Finishing blow on critical target, mech unlikely to survive

---

## Power Capacitor Banks

### Capacitor Specifications

**Small Capacitor (1 slot)**
- Storage: 2,000 MW·s (2,000 MW for 1 sec, 1,000 MW for 2 sec, etc.)
- Charge rate: 300 MW (charges in 6.67 seconds if excess power available)
- Discharge rate: 2,000 MW (instant burst)
- Weight: 400 kg
- Cost: 150,000 credits

**Medium Capacitor (2 slots)**
- Storage: 5,000 MW·s
- Charge rate: 500 MW
- Discharge rate: 3,500 MW
- Weight: 900 kg
- Cost: 400,000 credits

**Large Capacitor (3 slots)**
- Storage: 12,000 MW·s
- Charge rate: 800 MW
- Discharge rate: 6,000 MW
- Weight: 2,200 kg
- Cost: 1,200,000 credits

---

### Capacitor Usage Strategies

**1. Burst Damage (Capacitor-Assisted Alpha Strike)**

**Strike Mech** with Medium Capacitor (5,000 MW·s):
- Reactor: 2,000 MW
- Normal weapon draw: 800 MW × 2 = 1,600 MW
- Overcharged (175%): 1,400 MW × 2 = 2,800 MW
- **Deficit**: -800 MW (normally unsustainable)

**With Capacitor:**
- Reactor supplies: 2,000 MW
- Capacitor supplies: 800 MW
- **Combined**: 2,800 MW (sustainable for 6.25 seconds)
- After 6.25 sec, capacitor depleted, weapons drop to 143% power (sustainable on reactor alone)

**Total burst window**: 6.25 seconds at 175% power (2,170 damage × 6.25 = **13,563 total damage**)

---

**2. Shield Surge (Emergency Defense)**

**Assault Mech** under heavy fire:
- Shield HP: 3,000 (100% power, 600 MW draw)
- Incoming DPS: 4,000
- **Shield depletes in**: 3,000 / 4,000 = 0.75 seconds

**Shield Overcharge (200% + Capacitor):**
- Shield HP: 5,950 (200% power)
- Power draw: 1,200 MW
- Capacitor adds: 1,200 MW (reactor can sustain shields at 200%)
- **Shield lasts**: 5,950 / 4,000 = **1.49 seconds** (2× survival time)
- After capacitor depletes, shields drop to sustainable level (150% = 4,900 HP)

**Result**: Capacitor provides 10 seconds of 200% shields, buys time to destroy attacker or retreat

---

**3. Mobility Boost (Tactical Reposition)**

**Scout Mech** needs to cross killzone (600m, under fire):
- Normal speed: 25 m/s
- Time exposed: 600 / 25 = **24 seconds**
- Incoming fire: 800 DPS
- Total damage: 800 × 24 = **19,200 damage** (likely fatal)

**Mobility Overcharge (200% + Capacitor):**
- Speed: 25 × 1.8 = **45 m/s**
- Power draw: 600 MW (vs 300 MW normal)
- Capacitor provides: 300 MW deficit
- Time exposed: 600 / 45 = **13.3 seconds**
- Total damage: 800 × 13.3 = **10,640 damage** (44% damage reduction)

**Result**: Capacitor enables life-saving sprint across danger zone

---

## Thermal Management

### Heat Generation

All systems generate waste heat, overcharged systems generate exponentially more:

```
Heat Generated = Base Heat × (Power%²  / 100²)
```

**Example (Railgun, 400 MW base heat):**
- 100%: 400 MW
- 150%: 400 × 2.25 = **900 MW**
- 200%: 400 × 4.0 = **1,600 MW**

---

### Heat Capacity

**Light Mech**: 3,000 MW·s (3,000 MW for 1 sec before overheat)
**Medium Mech**: 8,000 MW·s
**Heavy Mech**: 20,000 MW·s
**Titan Mech**: 60,000 MW·s

---

### Heat Dissipation

**Passive Radiators**:
- Light: 1,200 MW (vacuum), 2,000 MW (atmosphere)
- Medium: 2,500 MW (vacuum), 4,200 MW (atmosphere)
- Heavy: 5,500 MW (vacuum), 9,200 MW (atmosphere)
- Titan: 18,000 MW (vacuum), 30,000 MW (atmosphere)

**Active Cooling (module, 1 slot)**:
- Heat dissipation: +1,500-3,000 MW
- Power draw: 200-400 MW
- Weight: 600-1,200 kg

**Emergency Coolant Purge**:
- Instant heat reduction: -80% (e.g., 8,000 → 1,600 MW·s)
- Coolant depleted, requires refill
- Refill time: 5 minutes (out of combat)
- Uses per mission: 2-3 charges

---

### Overheat Penalties

```
Heat%   Movement   Accuracy   Weapon Cooldown   Shield Efficiency   Risk
50%     100%       100%       100%              100%                Safe
60%     95%        95%        +10%              95%                 Warning
75%     85%        85%        +25%              85%                 Caution
90%     70%        70%        +50%              70%                 Critical
100%    0%         0%         Offline           Offline             Emergency Shutdown (30 sec)
110%    0%         0%         Offline           Offline             Component Damage (10%/sec)
120%    0%         0%         Offline           Offline             Core Breach (explosion imminent)
```

---

### Heat Management Scenario

**Heavy Assault Mech** in prolonged firefight:

**Configuration:**
- 2× Plasma Cannons: 150% power, 1,400 MW heat each = **2,800 MW/sec**
- Shields: 125% power, 750 MW heat = **750 MW/sec**
- Mobility: 100% power, 300 MW heat = **300 MW/sec**
- **Total heat generation**: 3,850 MW/sec

**Heat Dissipation:**
- Passive (atmosphere): 9,200 MW/sec
- Active cooling: 2,400 MW/sec
- **Total dissipation**: 11,600 MW/sec

**Net heat balance**: 3,850 - 11,600 = **-7,750 MW/sec** (cooling down)

**Result**: Can sustain overcharged weapons indefinitely in atmosphere

---

**Same mech in vacuum (space combat):**
- Passive (vacuum): 5,500 MW/sec
- Active cooling: 2,400 MW/sec
- **Total dissipation**: 7,900 MW/sec

**Net heat balance**: 3,850 - 7,900 = **-4,050 MW/sec** (still cooling, but marginal)

**Risk**: If shields take heavy fire (heat spikes to 5,000 MW/sec), net becomes positive (+1,100 MW/sec)
- Heat capacity: 20,000 MW·s
- **Time to overheat**: 20,000 / 1,100 = **18.2 seconds**

**Solution**: Burst fire (5 sec attack, 3 sec cooldown), or emergency coolant purge

---

## Automated Power Management

### AI Power Modes

**1. Conservative (Efficiency Priority)**
- All systems: 100% or below
- Capacitors charge passively
- Heat: <50% at all times
- **Use**: Extended operations, patrol, escort

**2. Aggressive (Offense Priority)**
- Weapons: 150%
- Shields: 75%
- Sensors: 100%
- Mobility: 125%
- **Use**: Ambush, alpha strike, overwhelming enemies

**3. Defensive (Survival Priority)**
- Weapons: 75%
- Shields: 175%
- Sensors: 125%
- Mobility: 75%
- **Use**: Retreat, holding position, tanking

**4. Adaptive (AI Decision-Making)**
AI analyzes threat level, resource state, and mission objective to dynamically adjust:

**Threat Assessment:**
```
Low Threat (1-2 enemies, weak):
- Weapons: 100%
- Shields: 100%
- Minimize power draw

High Threat (5+ enemies, strong):
- Weapons: 150% (eliminate threats quickly)
- Shields: 125% (survival critical)
- Capacitors: Discharge for burst damage

Critical Threat (overwhelming odds):
- Shields: 200% (survival priority)
- Weapons: 50% (conserve power)
- Mobility: 150% (retreat)
```

---

### Conditional Triggers

Players can set automated power redistribution triggers:

**Examples:**

**1. "If shields < 25%, redirect all power to shields"**
- Trigger: Shield HP drops below 25%
- Action: Shields → 200%, Weapons → 0%, Sensors → 25%
- Duration: Until shields reach 75%

**2. "If enemy within 100m, overcharge weapons to 175%"**
- Trigger: Enemy detected within 100m
- Action: Weapons → 175%, Shields → 75%
- Duration: Until enemy >150m away or destroyed

**3. "If heat > 85%, reduce all systems to 75%"**
- Trigger: Heat exceeds 85%
- Action: All systems → 75%
- Duration: Until heat <60%

**4. "If capacitor full, discharge into weapons for alpha strike"**
- Trigger: Capacitor reaches 100% charge
- Action: Weapons → 250% (with capacitor boost)
- Duration: 8 seconds or capacitor depleted

---

## Reactor Failures

### Overcharge Risks

Reactors running above 150% face containment risks:

```
Reactor Power%   Meltdown Risk   Consequence
100%             0%              Safe operation
120%             0.01%/hour      Containment strain
150%             0.1%/hour       Electromagnetic instability
175%             1%/hour         Plasma leaks, radiation exposure
200%             5%/hour         Critical containment stress
225%             15%/hour        Imminent breach
250%             40%/hour        Catastrophic failure likely
```

**Meltdown Consequence (Antimatter Reactor):**
- Explosion: 5 kiloton yield
- Mech destroyed instantly
- All units within 500m: 8,000-15,000 damage
- All units within 1km: 2,000-5,000 damage
- Radiation zone: 2km radius, 50 rads/sec for 10 minutes

**Tactical Use**: Kamikaze attack (sacrifice mech to destroy enemy formation)

---

### Emergency Shutdown

If reactor overheats or reaches critical stress:

**Automatic Shutdown Sequence:**
1. All weapons offline (instant)
2. Shields offline (instant)
3. Mobility reduced to 25% (emergency locomotion only)
4. Sensors reduced to 50%
5. Reactor emergency venting (30 second cooldown)
6. Reactor restart (30-90 seconds depending on reactor type)

**Total Downtime**: 60-120 seconds (vulnerable to enemy fire)

**Player Override**: Can force restart early (50% chance of permanent reactor damage)

---

## Combat Scenario: Power Management Under Fire

**Setup:**
- **Player**: Assault Mech "Warhammer" (Heavy Fusion Reactor, 4,500 MW)
- **Enemy**: 3× Strike Mechs (Medium Fusion, 2,000 MW each)

**Warhammer Loadout:**
- 2× Particle Beam Cannons (1,800 MW each active, 3,500 damage each)
- 2× Energy Shields (600 MW each, 3,000 HP each)
- Active Cooling (+2,400 MW dissipation)
- Large Capacitor (12,000 MW·s)

**Initial State:**
- Reactor: 4,500 MW
- Capacitor: 100% (12,000 MW·s)
- Heat: 15% (3,000 / 20,000)

---

**Engagement Timeline:**

**T=0 sec: Ambush**
Player detects 3 enemies at 800m, switches to **Attack Focus**:
- Weapons: 175% (2,170 damage × 2 = **4,340 damage/volley**, 1,400 MW × 2 = 2,800 MW draw)
- Shields: 75% (2,250 HP each, 450 MW × 2 = 900 MW)
- Sensors: 100% (100 MW)
- Total: 3,800 MW
- **Deficit**: 3,800 - 4,500 = +700 MW (sustainable)

**T=2 sec: First Volley**
Both cannons fire:
- Damage: 4,340
- Heat: 1,225 MW × 2 = 2,450 MW generated
- Heat dissipation: 5,500 + 2,400 = 7,900 MW
- Net heat: -5,450 MW/sec (cooling down despite firing)
- Enemy Strike Mech #1: Takes 4,340 damage, shields depleted (3,500), hull damaged (840 damage)

**T=4 sec: Enemy Returns Fire**
3× Strike Mechs fire railguns (1,400 damage each):
- Total incoming: 4,200 damage
- Warhammer shields: 2,250 × 2 = 4,500 HP
- Shields absorb all damage, reduced to 300 HP remaining
- Player activates **Capacitor Discharge into Shields**

**T=5 sec: Shield Surge**
Capacitor discharges 1,200 MW into shields for 10 seconds:
- Shields: 75% → 200% (instantly)
- Shield HP: Regenerates to 5,950 × 2 = **11,900 HP**
- Reactor + capacitor: 4,500 + 1,200 = 5,700 MW available

**T=6 sec: Second Volley**
- Damage: 4,340
- Enemy Strike Mech #1: Destroyed
- Remaining enemies: 2

**T=8 sec: Concentrated Fire**
2× remaining enemies focus fire on Warhammer:
- Incoming: 2,800 damage
- Shields: 11,900 - 2,800 = **9,100 HP remaining**

**T=10 sec: Third Volley**
- Damage: 4,340
- Enemy Strike Mech #2: Destroyed
- Remaining enemies: 1

**T=12 sec: Final Enemy Retreats**
Last Strike Mech attempts retreat at 28 m/s (mobility overcharge):
- Player switches to **Mobility Focus**
- Mobility: 175% (28 m/s, matches enemy speed)
- Weapons: 100% (1,800 damage, reduced to conserve power)
- Shields: 50% (1,500 HP, enough to tank pot shots)

**T=14 sec: Pursuit**
Warhammer closes distance, final volley:
- Damage: 1,800 × 2 = 3,600
- Enemy Strike Mech #3: Destroyed

---

**Post-Combat State:**
- Hull: 100% (shields absorbed all damage)
- Shields: 1,500 HP (recharging)
- Heat: 35% (7,000 / 20,000)
- Capacitor: 0% (depleted, charging at 800 MW = 15 seconds to full)
- Power: Sustainable
- **Result**: Flawless victory, 3 enemies destroyed in 14 seconds

**Key Tactical Decisions:**
1. Attack Focus for alpha strike (eliminated one enemy immediately)
2. Capacitor discharge into shields (prevented hull damage)
3. Mobility Focus for pursuit (prevented enemy escape)

---

## Pilot Skills and Power Management

### Skill: Power Distribution Mastery

**Level 1**: Unlock focus presets (Attack, Defense, Mobility, Stealth)

**Level 3**: Reduce power redistribution delay from 1 sec → 0.3 sec

**Level 5**: +10% capacitor charge rate

**Level 8**: Unlock automated conditional triggers (3 slots)

**Level 10**: +20% heat dissipation efficiency

**Level 12**: Reduce overcharge burnout risk by 40%

**Level 15**: +30% capacitor storage capacity

**Level 18**: Unlock "Adaptive AI" power mode

**Level 20**: Can sustain 200% overcharge indefinitely (heat/burnout immunity for one module)

---

## Conclusion

Dynamic energy budget management transforms space combat from static engagements into tactical puzzles. Pilots must balance burst damage against sustained firepower, offensive capability versus defensive resilience, and short-term performance gains against long-term system degradation. Mastery of power redistribution, capacitor timing, and thermal management separates elite pilots from casualties.

**Strategic Principles:**
1. **Burst > Sustain**: Win fights in seconds, not minutes
2. **Capacitors Decide Battles**: Time surges for critical moments
3. **Heat is Your Limit**: Overheat = death, manage aggressively
4. **Power Focus = Specialization**: Jack-of-all-trades loses to specialists
5. **Emergency Modes = Last Resort**: High risk, high reward, accept the consequences

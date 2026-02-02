# Space4X MVP Waffle (Implementation Lockdown)

Purpose: capture concrete MVP decisions so implementation stays deterministic and unambiguous. Fill in the TBDs with your intent; we will wire these into configs + systems.

Status: DRAFT
Owner: (you)
Last updated: (TBD)

---

## 0) Scope + Non-Goals (MVP only)
- Scope includes: movement, combat loop, resource extraction loop, module catalogs (engine/shield/sensor/armor/weapon/bridge/cockpit/ammo), crew/seat interface, docking rules, orbits abstraction, focus hooks.
- Non-goals (for now): hero visuals, detailed weapon flavors, morphing terrain mining, advanced tech unlocks, full UI.

TBD:
- MVP scenarios that must pass:
  - (list scenario ids)
- Anything explicitly deferred to creative passes:
  - (list)

---

## 1) Movement / Flight Model
### 1.1 Core motion contract
- Translation model (choose one):
  - [ ] Accel = thrust/mass, velocity integration (inertial)
  - [ ] Direct velocity set (arcade)
  - [ ] Hybrid (define blend)
- Rotational model:
  - Turn rate derived from (engine? hull? pilot?)
  - Inertia dampening (Y/N, and when)
- Damping policy:
  - Linear damping source (systems/pilot/auto?)
  - Angular damping source (systems/pilot/auto?)

TBD:
- Formula for max accel / turn rate:
- Inertial dampening rules:
- What ?overpush? does (pilot strain / engine wear / heat?):

### 1.2 Cruise vs Combat
- Trigger conditions (distance to threat, stance, order type):
- Effects of switching modes (speed cap, accel, turn, sensor mode):
- Transition timing (instant / ramp / cooldown):

TBD:
- Define ?combat mode? rules:
- Define ?cruise mode? rules:

### 1.3 Approach Bias / Broadside Gating
- Define ?contact range? (distance or time to target):
- Broadside only within contact range? (Y/N)
- Unknown approach behavior (default heading, range bias):

TBD:
- Contact range value:
- Approach bias when target unknown:

### 1.4 Collision & Impulse
- Collision damage model (impulse -> hull? only above threshold?):
- Explosion impulse effect on motion (scale & decay):

TBD:
- Collision threshold & damage:
- Explosion impulse rules:

---

## 2) Combat Loop
### 2.1 Weapon Arcs / Facing
- Per?mount arc vs hull arc (which takes precedence?):
- Mixed arcs on same ship (how to bias facing?):

TBD:
- Arc resolution rule:
- Default arc values (if none specified):

### 2.2 Gunnery / Tracking / Aim
- Gunnery skill formula (stats + proficiency):
- Tracking penalty inputs (angular velocity, distance):
- Aim smoothing / reaction latency (pilot skill influence):

TBD:
- Gunnery formula:
- Tracking penalty formula:
- Aim latency baseline + skill reduction:

### 2.3 Attack Runs (Strike Craft & Ships)
- Run start distance:
- Break?off distance:
- Re?attack interval:
- Behavior on target loss:

TBD:
- Concrete values for run params:
- Strike craft vs capital differences:

---

## 3) Resource Extraction Loop
### 3.1 Mining State Machine
- States (e.g., Find ? Approach ? Mine ? Return ? Offload):
- Who assigns orders (captain/logistics officer):

TBD:
- Exact state transitions & triggers:
- Order ownership rules:

### 3.2 Efficiency & Risk
- Efficiency factors (pilot skill, module quality, tech level):
- Risk/accident model (overheat/instability meter?):

TBD:
- Efficiency formula:
- Risk formula + thresholds:

### 3.3 Resource Accounting
- Depletion rules:
- Reservation & contention handling:
- Raw ? refined (defer or include minimal?):

TBD:
- Depletion per tick:
- Reservation policy:

---

## 4) Module Catalogs (MVP)
### 4.1 Module Contract
Every module provides:
- Mass
- Power draw
- Heat output
- Efficiency scalar
- Tech level

TBD:
- Required fields per module class:
- Minimal defaults by class:

### 4.2 Ammo as Cargo
- Ammo in ordnance containers (resource)
- Weapons consume via efficiency factor

TBD:
- Ammo container schema:
- Consumption pipeline:

---

## 5) Crew / Seats / Aggregate AI
### 5.1 Seat Roles
- Captain, XO, Shipmaster, Navigation, Weapons, Sensors, Logistics, Chief Engineer?

TBD:
- Which roles required for MVP:
- Default fallback if role absent:

### 5.2 Aggregated AI Interface
- Flow: Captain Intent ? Officer Directives ? Subsystem Control
- Cohesion effects (range choice, facing, accuracy, rate of fire)

TBD:
- Data handoff contract (intent/directive types):
- Cohesion multipliers:

---

## 6) Docking / Despawn
- Despawn vs latch rules:
- Docking mandatory until teleport tech

TBD:
- Size thresholds:
- Tech gating rules:

---

## 7) Orbits & Scale
- Local vs system vs galaxy movement
- Orbits simulated or visual only?

TBD:
- Abstraction boundary for AI vs visuals:
- Orbit model (simple circle, Kepler, spline):

---

## 8) Focus System (Cognitive)
- Focus boosts (accuracy, reaction, work speed, etc.)
- Per?seat aggregation rules

TBD:
- Focus effects list:
- Stack / cap rules:

---

## 9) Entity Profiles & Proficiency
- Proficiency = time * wisdom * aptitude
- Stats: physique / finesse / will, plus command/tactics/logistics/etc.

TBD:
- Exact formulas for proficiency gain:
- Which stats drive which skills (gunnery, dogfight, boarding):

---

## 10) Quality / Purity
- Quality affects module/ship output
- Purity may be distinct (optional)

TBD:
- Default quality pipeline:
- Purity vs quality split (Y/N):

---

## 11) Strike Craft / Small Craft
- Multi?seat vs single pilot vs remote
- Speed modes (patrol vs sprint)

TBD:
- Transition rules between modes:
- Remote control penalties:

---

## 12) Decisions to Lock
(Write the final decisions here so we can code directly.)
- Decision 1:
- Decision 2:
- Decision 3:

---

## 13) Config Targets (to be wired into code)
List the concrete config names/values that should exist once decisions are locked.
- Example: Space4XMovementInertiaConfig.ContactRange = ???
- Example: Space4XWeaponTuningConfig.TrackingPenaltyScale = ???

TBD

---

## 14) Open Questions
- Q1:
- Q2:
- Q3:

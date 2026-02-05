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

Modding note:
- All defaults below should live in config components (singleton IComponentData + authoring assets) and be overridable by scenario JSON param_overrides.
- Treat numbers here as **baseline tunables** (safe + boring).

---

## 1) Movement / Flight Model
### 1.1 Core motion contract
- Translation model (choose one):
  - [ ] Accel = thrust/mass, velocity integration (inertial)
  - [ ] Direct velocity set (arcade)
  - [X] Hybrid (define blend) - Allow for flexibility during creative passes, but make it performance aware.
- Rotational model:
  - Turn rate derived from (engine? hull? pilot?)
  - Inertia dampening (Y/N, and when)
- Damping policy:
  - Linear damping source (systems/pilot/auto?)
  - Angular damping source (systems/pilot/auto?)
Allow for all
TBD:
- Formula for max accel / turn rate:
- Inertial dampening rules:
- What ?overpush? does (pilot strain / engine wear / heat?):
Defaults (tunable):
- Max accel = (EnginePerformanceOutput.ThrustAuthority / max(1, VesselMass)) * Mobility/Quality scalars.
- Max turn = (EnginePerformanceOutput.TurnAuthority / max(1, VesselMass)) * Mobility/Quality scalars.
- Damping = auto‑stabilize toward 0 velocity/rotation when no intent, scaled by pilot skill.
- Overpush = temporary +speed/+turn with rising risk meter; risk feeds to heat/instability later.

### 1.2 Cruise vs Combat
- Trigger conditions (distance to threat, stance, order type): generic defaults, offset by entity profiles
- Effects of switching modes (speed cap, accel, turn, sensor mode): we will iterate modes on creative passes
- Transition timing (instant / ramp / cooldown): depends on detection time (sensors officer and his instruments), and captain/faction's policies regarding the contact, etc.

TBD:
- Define ?combat mode? rules:
- Define ?cruise mode? rules:
Defaults (tunable):
- Cruise: maxSpeed=1.0x, accel=1.0x, turn=0.9x, sensors=wide.
- Combat: maxSpeed=0.9x, accel=1.1x, turn=1.1x, sensors=focused.
- Transition: 0.5–1.5s ramp based on sensors/captain profile.

### 1.3 Approach Bias / Broadside Gating
- Define ?contact range? (distance or time to target): generic default
- Broadside only within contact range? (Y/N) allow flexibility
- Unknown approach behavior (default heading, range bias): depends on captain's call, and that usually depends on his ship's capabilities and arcs (shields directional? weapons spinal or broadside? etc.)

TBD:
- Contact range value:
- Approach bias when target unknown:
Defaults (tunable):
- Contact range = max(200, 0.7 * maxWeaponRange) for the vessel.
- Unknown approach: align to travel vector + “best arc” (if spinal, nose‑in; if broadside, gradual yaw only at contact).

### 1.4 Collision & Impulse
- Collision damage model (impulse -> hull? only above threshold?): impulse-> hull, later depends on armor/shields, etc.
- Explosion impulse effect on motion (scale & decay): should be formulaic, i think, but it should also be efficient, and not blow up our sim

TBD:
- Collision threshold & damage:
- Explosion impulse rules:
Defaults (tunable):
- Collision damage = max(0, impulse - threshold) * hullFactor; threshold = 1.25 * (mass * 5).
- Explosion impulse = radial force with 1/r falloff, clamped to maxImpulse; short decay (0.2–0.5s).

---

## 2) Combat Loop
### 2.1 Weapon Arcs / Facing
- Per?mount arc vs hull arc (which takes precedence?): captain/steersman decision, captain can always override (unless mutinous, we will get there eventually)
- Mixed arcs on same ship (how to bias facing?): range and available firepower in that range bracket; captains may decide to attempt a point blank spinal weapon discharge (to everyone's detriment, sometimes), or they can attempt to main thrust away to gain range, they may decide to ram the target - up to their profiles.

TBD:
- Arc resolution rule:
- Default arc values (if none specified): 90 degrees
Defaults (tunable):
- Arc resolution: prioritize mounts with highest expected DPS in current range band.

### 2.2 Gunnery / Tracking / Aim
- Gunnery skill formula (stats + proficiency): generic defaults for now
- Tracking penalty inputs (angular velocity, distance): generic defaults
- Aim smoothing / reaction latency (pilot skill influence): generic defaults

TBD:
- Gunnery formula:
- Tracking penalty formula:
- Aim latency baseline + skill reduction:
Defaults (tunable):
- Gunnery = 0.45*Tactics + 0.35*Finesse + 0.20*Command (normalized 0–1).
- Tracking penalty = clamp(1 - omega * lerp(basePenalty*1.4, basePenalty*0.6, gunnery), 0, 1).
- Aim latency = 0.35s baseline → 0.08s at max skill.

### 2.3 Attack Runs (Strike Craft & Ships)
- Run start distance: depends on vessels' speed and maneuverability, as well as pilot's decisions; some pilots will prefer "hugging" the enemy hulls and avoid arcs of fire, some will decide to discharge their weapons from afar to avoid threat zones, depends on profiles and doctrines
- Break?off distance: per above
- Re?attack interval: per above
- Behavior on target loss: defer to squad leader, or sortie operator, decides itself if none available.

TBD:
- Concrete values for run params:
- Strike craft vs capital differences:
Defaults (tunable):
- Start distance = 1.1 * preferred weapon max range.
- Break‑off = 0.6 * max range (or when hull < threshold).
- Re‑attack interval = 3–6s (skill reduces).
- Strike craft: tighter break‑off + higher turn bias; capitals: broader turn bias + slower reacquire.

---

## 3) Resource Extraction Loop
### 3.1 Mining State Machine
- States (e.g., Find ? Approach ? Mine ? Return ? Offload):
- Who assigns orders (captain/logistics officer): Captain has (or decides) orders to extract either specific or general resources->steersman and sensors, logistics receive orders and attempt to search for high value resource nodes in a given area (either captain's or fleet's AO)->logistic officers receive info from sensors and map out extraction protocols for that area, radius depends on profiles and doctrine->steersman and main logistics and sensors officers plot best area to "park" capital ship or gatherer ship for optimal throughput, depending on captain's constraints (hostile territory? explored? combat strength adequate? stealth ops? etc.)

TBD:
- Exact state transitions & triggers:
- Order ownership rules:
Defaults (tunable):
- State machine: Search → Approach → Mine → Return → Offload → Idle.
- Ownership: Captain intent → Logistics assigns targets → Steersman executes.

### 3.2 Efficiency & Risk
- Efficiency factors (pilot skill, module quality, tech level): generic defaults
- Risk/accident model (overheat/instability meter?): collision risks, overheat, instability, equipment failures

TBD:
- Efficiency formula:
- Risk formula + thresholds:
Defaults (tunable):
- Efficiency = baseRate * moduleEff * (0.5 + 0.5*pilotSkill) * techQuality.
- Risk meter grows with overpush + hazards; threshold triggers slowdowns or failures.

### 3.3 Resource Accounting
- Depletion rules: generic defaults
- Reservation & contention handling: ticketing system? we can have advisor research it
- Raw ? refined (defer or include minimal?): raw>refined, minimal

TBD:
- Depletion per tick:
- Reservation policy:
Defaults (tunable):
- Depletion = baseRate * efficiency (clamped by remaining units).
- Reservation = simple ticketing; lease expires after N ticks without progress.

---

## 4) Module Catalogs (MVP)
### 4.1 Module Contract
Every module provides:
- Mass
- Power draw
- Heat output
- Efficiency scalar
- Tech level
sounds good
TBD:
- Required fields per module class:
- Minimal defaults by class:
Defaults (tunable):
- Engine: thrust, turn, response, efficiency, boost, vectoring.
- Weapons: damage, cooldown, arc, accuracy, ammo usage.
- Shields: capacity, regen, recharge delay, resist profile.
- Sensors: range, precision, update cadence.
- Armor: thickness, resistance profile.
- Bridge/Cockpit: seats, command bonus, cohesion bonus.
- Ammo: capacity, type, throughput.

### 4.2 Ammo as Cargo
- Ammo in ordnance containers (resource) yes
- Weapons consume via efficiency factor - maybe

TBD:
- Ammo container schema:
- Consumption pipeline:
Defaults (tunable):
- Container: ammoTypeId, capacity, current, transferRate.
- Weapons pull from container each shot; efficiency modifies consumption.

---

## 5) Crew / Seats / Aggregate AI
### 5.1 Seat Roles
- Captain, XO, Shipmaster, Chief Navigation, Chief Weapons, Chief Sensors, Chief Logistics, Chief Engineer - each has their underling officers to take care of things in their responsibility slice on the ship.

TBD:
- Which roles required for MVP:
- Default fallback if role absent:
Defaults (tunable):
- Required MVP: Captain, Navigation, Weapons, Logistics.
- Fallback: missing roles defer to Captain/Pilot with penalties.

### 5.2 Aggregated AI Interface
- Flow: Available info > Captain Intent (some allow deliberation with chief officers, especially on critical decision matters, more in creative passes) > Chief officers orders > Officers Directives > Subsystem Control via officers and crew
- Cohesion effects (range choice, facing, accuracy, rate of fire) creative pass, generic defaults

TBD:
- Data handoff contract (intent/directive types):
- Cohesion multipliers:
Defaults (tunable):
- Intent types: Move, Engage, Withdraw, Mine, Escort.
- Directives: Facing, Range, Fire Discipline, Sensor Focus.
- Cohesion: +5–20% accuracy/turn/ROF, capped.

---

## 6) Docking / Despawn
- Despawn vs latch rules: that's for presentation pass, default to despawn
- Docking mandatory until teleport tech ok

TBD:
- Size thresholds:
- Tech gating rules:
Defaults (tunable):
- Despawn by default if docking target mass > 5x ship mass; otherwise latch.
- No teleport until tech flag (future).

---

## 7) Orbits & Scale
- Local vs system vs galaxy movement - so the vision goes, galaxy doesn't have to move currently
- Orbits simulated or visual only? simulated

TBD:
- Abstraction boundary for AI vs visuals: 
- Orbit model (simple circle, Kepler, spline):
Defaults (tunable):
- AI uses simplified orbital positions; visuals interpolate smooth Kepler-ish curves.
- Orbit model: circular for MVP, switchable to Kepler.

---

## 8) Focus System (Cognitive)
- Focus boosts (accuracy, reaction, work speed, etc.) - everything pretty much, we will worry about this on creative passes
- Per?seat aggregation rules - rephrase?

TBD:
- Focus effects list:
- Stack / cap rules:
Defaults (tunable):
- Effects: accuracy, reaction time, work speed, sensor clarity.
- Stack: additive up to +0.25, diminishing after +0.15.

---

## 9) Entity Profiles & Proficiency
- Proficiency = time * wisdom * aptitude - more like an experience level but more on creative pass
- Stats: physique / finesse / will - these are archetypes, they dictate amount of experience gained in each field -> command/tactics/logistics/etc.

TBD:
- Exact formulas for proficiency gain:
- Which stats drive which skills (gunnery, dogfight, boarding):
Defaults (tunable):
- Proficiency gain = dt * wisdom * aptitude * taskWeight.
- Gunnery ↔ tactics+finesse; dogfight ↔ finesse+will; boarding ↔ physique+will.

---

## 10) Quality / Purity
- Quality affects module/ship output - pretty much everything, with added special attributes and affixes on higher quality levels, with detrimental ones on lower quality
- Purity may be distinct (optional)

TBD:
- Default quality pipeline:
- Purity vs quality split (Y/N):
Defaults (tunable):
- Quality = average(materialQuality * processQuality) → output multiplier.
- Purity disabled for MVP; keep hook for later.

---

## 11) Strike Craft / Small Craft
- Multi?seat vs single pilot vs remote - all is possible
- Speed modes (patrol vs sprint) - idle, patrol, escort, attack, sptrint, maybe more

TBD:
- Transition rules between modes:
- Remote control penalties:
Defaults (tunable):
- Mode transitions by threat proximity + order type.
- Remote control: -15% reaction, -10% accuracy, -10% cohesion.

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

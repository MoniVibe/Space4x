# Desktop Agent Slice: Engines + Fuels + Production Chain v0 (Space4x)

Goal: define fuel types, engine families, and a coherent early-mid-late production chain that supports logistics gameplay.

Scope: docs only (no code, no scenario edits, no `Packages/*lock*`).

Deliverable: this one doc.

## Definitions

Energy density buckets (relative):
- `E0` = external (propellant carries little/no energy; depends on ship/station power)
- `E1` = low
- `E2` = medium
- `E3` = high
- `E4` = extreme

Thrust class (relative): `T0` low, `T1` medium, `T2` high, `T3` extreme.

Efficiency class (relative): `I0` low, `I1` medium, `I2` high, `I3` extreme.

## Fuel Table

Each fuel has at least one distinct logistical constraint (cryogenic, pressurized, toxic/corrosive, radiation/handling, or external-power coupling).

| fuel_id | Source (primary) | Storage constraints | Energy density | Hazards | Engine families that use it |
| --- | --- | --- | --- | --- | --- |
| `fuel.solid_composite` | industrial | ambient; bulky; limited throttle (cast grain) | `E2` | fire/explosion; debris | `engine_family.solid_booster` |
| `fuel.kerolox` | industrial + ice/atmo (O2) | LOX cryogenic; RP-1 ambient | `E3` | explosion; oxidizer handling | `engine_family.chem_main`, `engine_family.chem_lander` |
| `fuel.methalox` | ice + atmo (CO2/CH4) + industrial catalysts | LOX/LCH4 cryogenic | `E3` | explosion; boiloff | `engine_family.chem_main`, `engine_family.chem_lander` |
| `fuel.hydrolox` | ice + gas giant (H2) | deep cryogenic (LH2 boiloff dominates) | `E3` | explosion; extreme boiloff | `engine_family.chem_high_isp`, `engine_family.ntr` (as propellant) |
| `fuel.hypergolic` | industrial (N2O4 + MMH/UDMH) | ambient storable; toxic/corrosive; contamination risk | `E2` | highly toxic; corrosive; fire | `engine_family.chem_storable`, `engine_family.rcs` |
| `fuel.hydrogen_propellant` | ice + gas giant | deep cryogenic; very low density (big tanks) | `E0` | boiloff; embrittlement | `engine_family.ntr`, `engine_family.fusion_torch` (reaction mass) |
| `fuel.noble_gas_propellant` | atmo (noble gas distillation) | high-pressure tanks; low mass flow limits | `E0` | high-pressure; supply constrained | `engine_family.hall`, `engine_family.ion`, `engine_family.mpd` |
| `fuel.fusion_d_he3` | ice (D via heavy water) + gas giant (He3) | cryogenic; high handling/security; rare | `E4` | radiation; strategic material | `engine_family.fusion_torch` |

## Engine Family Table

| engine_family_id | Thrust class | Efficiency class | Power requirements | Typical hull role | Why you'd choose it |
| --- | --- | --- | --- | --- | --- |
| `engine_family.solid_booster` | `T2` | `I0` | none | early lifter, missiles, emergency kick stage | cheap, storable, instant thrust; logistics tradeoff is poor control and bulky propellant |
| `engine_family.chem_main` | `T2` | `I0-I1` | low (pumps/avionics) | general purpose ships, interceptors, freighters | simple, high thrust; fuels are common but drive cryo/oxidizer logistics |
| `engine_family.chem_high_isp` | `T1` | `I1` | low-medium (cryo + pumps) | long burns, transfer stages | better delta-v than kerolox/methalox; forces deep-cryogenic infrastructure |
| `engine_family.chem_storable` | `T1` | `I0-I1` | low | patrol craft, long-idle landers, depot-tender | ambient storage removes boiloff, but introduces toxicity/corrosion and supply-chain overhead |
| `engine_family.chem_lander` | `T1-T2` | `I0-I1` | low-medium | landers, VTOL-ish bodies, surface shuttles | throttle and responsiveness; pays a premium in local fuel logistics (oxidizer or toxic storable) |
| `engine_family.rcs` | `T0` | `I0` | low | attitude, docking, fine formation | precision and reliability; fuels tend to be storable but hazardous |
| `engine_family.hall` | `T0` | `I2` | high (steady electric) | tugs, long-haul logistics, station-keeping | great propellant efficiency; forces power generation + radiator mass and low-thrust planning |
| `engine_family.ion` | `T0` | `I3` | very high (electric) | ultra-efficient probes, courier drones | extreme efficiency with tiny propellant; punishingly low thrust and power hungry |
| `engine_family.mpd` | `T0-T1` | `I2` | extreme (electric + thermal) | late-game war logistics, heavy tugs | bridges the gap toward torch drives; requires serious power, cooling, and expensive propellant flow |
| `engine_family.ntr` | `T1-T2` | `I1-I2` | medium (reactor + pumps) | deep-space cruisers, rapid-response logistics | high thrust with good delta-v; fuel is "mostly propellant logistics" plus reactor-grade infrastructure |
| `engine_family.fusion_torch` | `T2-T3` | `I2-I3` | extreme (fusion plant) | late-game capitals, strategic mobility | compresses travel time and logistics footprint; gated by rare fuels and heat rejection |

## Production Chain Map (Minimal, Closed-Loop)

This map is deliberately small: the point is to support logistics play with clear bottlenecks and distinct constraints, not to model every chemical.

### Graph (read left to right)

```text
RAW EXTRACTION
  ice (H2O, CO2) -----> electrolysis -----> LOX + LH2 -----> hydrolox / hydrogen_propellant
         |                                  |
         |                                  +--> heavy water (D2O) --> deuterium (D)
         |
  atmo scoop (CO2, N2, noble gases) --> gas separation --> CO2 + N2 + noble gases --> noble_gas_propellant
         |                                                     |
         |                                                     +--> nitrates / nitric acid --> N2O4
         |
  ore mining (Fe/Ni/etc) --> smelting --> metals --> alloys --> parts --> engines/tanks/cryoplants

MID REFINING (industrial loops)
  CO2 + H2 --(Sabatier)--> CH4 + H2O (recycle) --> methalox
  CO/CO2 + H2 --(FT)--> RP-1 + H2O (recycle) --> kerolox
  N2 + H2 --> ammonia --> hydrazines --> hypergolic

LATE GATES
  gas giant skim --> He3 -------------------------------> fusion_d_he3 --> fusion_torch
```

### Recipes (numbered, minimal)

1. Ice mining -> `water` (and optionally `dry_ice` / `co2`)
2. Ore mining -> `ore`
3. Atmo scoop -> `co2` + `n2` + `noble_gases` (argon/krypton/xenon bucket)
4. Smelt `ore` -> `metals`
5. Metallurgy `metals` -> `alloys`
6. Fabrication `alloys` -> `parts` (pumps, tanks, valves, seals, radiators)
7. Electrolysis `water` + `power` -> `lox` + `lh2`
8. Cryoplant `lox`/`lh2` + `power` + `parts` -> `fuel.hydrolox` (and also enables `fuel.hydrogen_propellant`)
9. Sabatier `co2` + `lh2` + `power` + `parts` -> `lch4` + `water` (water feeds back to electrolysis)
10. Blend `lch4` + `lox` -> `fuel.methalox`
11. FT synth `co2` (or `co`) + `lh2` + `power` + `parts` -> `rp1` + `water`
12. Blend `rp1` + `lox` -> `fuel.kerolox`
13. Hypergolic line `n2` + `lh2` + `power` + `parts` -> `fuel.hypergolic` (toxic storable)
14. Gas separation `noble_gases` + `power` + `parts` -> `fuel.noble_gas_propellant`
15. Late: skim gas giant -> `he3` (rare) ; enrich heavy water -> `deuterium` ; combine -> `fuel.fusion_d_he3`

Outputs supported by the loop:
- Fuels: `solid_composite`, `kerolox`, `methalox`, `hydrolox`, `hypergolic`, `hydrogen_propellant`, `noble_gas_propellant`, `fusion_d_he3`
- Ammo (minimal): `ammo.solid_motor` (uses `solid_composite`), `ammo.missile_bus` (uses `hypergolic` or `solid_composite`), `ammo.kinetic_slug` (uses `alloys`)
- Parts: tanks (cryo/pressure), pumps, valves, radiators, reactor shielding (late)

## Invariants

- Every fuel has at least one distinct logistical constraint that changes route planning (boiloff, pressure, toxicity, rarity, or external-power coupling).
- "Late fuels" are gated by high power + specialized infrastructure:
  - cryogenic plants and insulation for LH2/LOX
  - enrichment/separation for noble gases and deuterium
  - gas giant skim + security chain for He3
- Electric engines decouple propellant from energy: they consume `E0` propellant but require persistent power and radiators.
- The chain is closed-loop at the mid tier: Sabatier/FT produce water as a byproduct, feeding electrolysis (power becomes the dominant limiter).
- A colony can reach T2 propulsion without importing magical ingredients: ice + atmo + ore + power is sufficient to field `engine_family.ntr` (propellant + infrastructure).

## Acceptance Checks

- Can outline a colony plan that reaches T2 propulsion (choose one: `engine_family.ntr` or `engine_family.hall`) with explicit inputs and facilities, using only resources in this doc plus `power`.
- For every fuel row, can point to:
  - where it comes from (ice/atmo/gas giant/industrial)
  - what storage constraint makes it interesting
  - at least one engine family that uses it
- For every engine family row, can point to:
  - at least one fuel/propellant it consumes (directly or as reaction mass)
  - a reason to choose it that is not just "bigger numbers"
- The production map stays readable: at most 15 recipes, no extra intermediates unless they unlock a new logistics constraint or a new tier.


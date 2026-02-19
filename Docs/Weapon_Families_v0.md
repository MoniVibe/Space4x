# Weapon Families v0 (Space4X)

Goal: define a clean taxonomy and balance targets for Space4X weapon families with stable IDs and clear invariants.

Scope: docs only. This document defines IDs and design constraints but does not wire anything into runtime.

## Terms

Range bands (v0, tunable later):

- `PD`: 0 to 2 km (terminal defense window, high angular rates)
- `Brawler`: 1 to 8 km (fleet knife-fight, sustained DPS matters)
- `Artillery`: 6 to 40 km (standoff volleys, prediction matters)
- `Strike`: 15 to 120 km (alpha delivery with travel time, ammo logistics)

Tracking (v0 glossary):

- `High`: can reliably engage fighters/missiles at PD ranges
- `Medium`: can engage corvettes/frigates in brawler ranges, struggles vs dedicated evasion builds
- `Low`: assumes large/slow targets or external disable (web/slow/lock) support

## Weapon Families (Stable IDs)

These IDs should remain stable across tiers and variants. Tiers/marks are expressed as data on the family, not by changing the family ID.

| weapon_family_id | role | damage model | range band | tracking | counters | power/heat/ammo hooks | good at | bad at | upgrade knobs (tier scaling) | manufacturing inputs (placeholder) |
|---|---|---|---|---|---|---|---|---|---|---|
| `wf_pd_laser` | PD | hitscan beam, low impulse per tick, continuous | PD | High | Counters: missiles, fighters, drones | Power: steady high draw. Heat: steady medium. Ammo: none. | stripping incoming salvos before impact; finishing low-HP small craft | chewing through armor; contributing meaningful DPS at 10+ km | tracking cone, beam dps, capacitor efficiency (power per dps), heat per dps, max simultaneous locks | lens array, emitter crystals, thermal bus, capacitor bank |
| `wf_pd_flak` | PD | prox-burst shrapnel, AoE cone, falloff | PD (edge into brawler) | High | Counters: clustered missiles, fighter balls | Power: medium. Heat: low. Ammo: yes (shells). | area denial; punishing swarms; defending slow logistics ships | single tough targets; long-range duels; ammo starvation | burst radius, fragments per burst, rate of fire, ammo per kill, fuse quality (false positives) | flak shells, proximity fuses, fragmentation liners, autoloader |
| `wf_brawl_autocannon` | brawler | kinetic slugs, high ROF, recoil/dispersion | Brawler | Medium | Counters: light hulls, exposed modules | Power: low. Heat: medium. Ammo: yes (magazines). | efficient sustained DPS; pressure through partial cover; finishing disabled ships | extreme range; heavy armor without support; heat-limited continuous fire | ROF, dispersion, armor damage multiplier, magazine size, reload time, recoil compensation | rotary assembly, barrel liners, feed mechanism, tungsten slugs |
| `wf_brawl_plasma` | brawler | slow plasma bolts, high impulse, splash/heat transfer | Brawler | Low to Medium | Counters: armored targets, slow capitals | Power: high burst. Heat: high. Ammo: none (reactor-fed). | cracking armor; forcing heat management failures; high value per hit | evasive frigates; long-range kiting; targets behind heavy PD screens (if bolts are interceptable) | bolt speed, alpha per shot, splash radius, heat transfer, cooldown time, power spike size | magnetic bottle, injector assembly, ceramic insulators, pulse capacitors |
| `wf_art_railgun` | artillery | hypervelocity penetrators, low spread, high alpha | Artillery | Low | Counters: large ships, stations, predictable or slowed targets | Power: very high per shot. Heat: medium. Ammo: yes (rods). | opening volleys; deleting key modules; punishing slow turns | fighters/missiles; close-range knife fights; targets with high lateral acceleration | muzzle velocity, charge time, alpha, penetrator type, heat recovery, ammo efficiency | coil stack, capacitor bank, tungsten rod stock, structural bracing |
| `wf_strike_torpedo` | strike | guided torpedoes, very high alpha, travel time | Strike | Medium (guidance-limited) | Counters: slow capitals, stations, fixed defenses | Power: low. Heat: low. Ammo: yes (torps). | forcing reactions; punching above ship weight; long-range threat projection | PD-heavy fleets; ECM/decoys; fast skirmishers; ammo exhaustion | seeker strength, counter-countermeasures, warhead class, turn rate, magazine size, reload/forge time | warheads, seekers, fuel cells, guidance computers, decoy packs |

## Balance Targets (Cross-Family)

These are the intended tradeoffs. Exact numbers are not locked in v0, but the relationships should hold.

1. PD families (`wf_pd_laser`, `wf_pd_flak`) are the best in the PD band versus missiles/fighters, but must not be a primary ship-killer versus armored hulls at brawler/artillery ranges.
2. Brawler families must be the best sustained DPS per mount in the brawler band, but they accept risk by needing proximity (and are punished by artillery/strike if they cannot close).
3. Artillery must trade tracking and time-to-hit for reach and per-hit value. It should feel oppressive versus large predictable targets, and unreliable versus evasive targets without support.
4. Strike must trade reload/ammo logistics and counterplay (PD, ECM, decoys) for standoff alpha and target selection.
5. No single family is strictly dominant across all bands. Every family must have at least one common counterplay path that is not "bring the same thing but more."

## Family Invariants (Must Hold)

1. PD must always be best versus missiles and fighters in the terminal window.
2. Artillery must always be worse at tracking than brawler weapons, and worse than PD by a wide margin.
3. Strike weapons must always be meaningfully counterable by defensive investment (PD density, ECM/decoys, formation choices).
4. Brawlers must always outperform artillery on small, fast targets inside the brawler band.
5. Families must remain identity-stable across tiers: tier increases improve the same knobs, not change the role.

## Acceptance Checks

1. Pick exactly these 6 families and you can cover PD, brawler, artillery, and strike roles with explicit tradeoffs (no "must-have" family).
2. For each role, there is at least one clear counter-investment path:

PD: overwhelmed by saturation, baited by decoys, or out-ranged.

Brawler: punished by standoff (artillery/strike) and kiting, or by heat/ammo attrition.

Artillery: punished by evasion, jamming, and close-in pressure.

Strike: punished by layered PD and ECM/decoys, and by ammo logistics.
3. Two fleets of equal budget can build different identities (PD-heavy escort, brawler rush, artillery standoff, strike doctrine) without any identity being strictly superior across common scenarios.

## Open Questions (Keep Short)

1. What is the canonical "distance scale" for combat right now (km bands above vs whatever the current sim uses)?
2. What armor model is intended for v0 balance math (flat DR, percent DR, layered armor, module-level armor)?
3. Are plasma bolts interceptable by PD (projectile) or treated as energy (beam-like) for counterplay?
4. What is the intended strength of ECM/decoys relative to PD in early tiers (and is it a ship module or a fleet doctrine stat)?
5. How should heat and power constraints surface at the player level (hard disable, soft DPS falloff, or risk events)?

## Intent Card (For PR Description)

- Intent: add a docs-only v0 taxonomy for Space4X weapon families with stable IDs, invariants, and acceptance checks.
- Scope: `space4x/Docs/Weapon_Families_v0.md` only.
- Out of scope: runtime code, Assets, tuning data, schemas, or scenario updates.
- Risk: low (docs-only), but establishes ID names that future runtime should respect.
- Follow-ups: wire these IDs into catalogs/blobs after the combat model (armor, PD intercept rules, ECM) is locked.

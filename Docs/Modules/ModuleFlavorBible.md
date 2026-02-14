# Space4X Module Flavor Bible (v0)

## Scope
This pack adds deterministic, content-first module variety for Borderlands-style space loot using the module BOM catalog.

## Families And Marks
All families support `Mk1` through `Mk3` with bounded stat scaling per mark.

| Family | Scenario ID Prefix | Slot Set |
| --- | --- | --- |
| Railgun | `module.weapon.railgun` | Barrel/Emitter, Capacitor, Cooling, Mount, Firmware |
| MissilePod | `module.weapon.missile_pod` | Barrel/Emitter, Capacitor, Cooling, Mount, Firmware |
| FlakArray | `module.weapon.flak_array` | Barrel/Emitter, Capacitor, Cooling, Mount, Firmware |
| PlasmaProjector | `module.weapon.plasma_projector` | Barrel/Emitter, Capacitor, Cooling, Mount, Firmware |
| ShieldGenerator | `module.defense.shield_generator` | Coil, Regulator, HeatSink, Core, Firmware |
| ScannerSuite | `module.utility.scanner_suite` | Coil, Regulator, HeatSink, Core, Firmware |

Part pool policy: every required slot family has 4 catalog parts (one per manufacturer), which keeps each slot in the required 3-8 range.

## Manufacturers (Feel Map)
| Manufacturer | Identity |
| --- | --- |
| Helios | High DPS, high heat, long range, lower reliability. |
| Bastion | Lower DPS ceiling, low heat, shorter range, high reliability. |
| Vanta | Stealth-burst posture, high alpha, slower cycle pacing. |
| Aurora | Control/utility focus, better tracking and lock control. |

## Deterministic Naming Rule
Name format is always:

`<Manufacturer> Mk<Mark> <Model> <Prefix> <Suffix>`

Prefix and suffix come from deterministic affix pools in `space4x_module_bom_catalog_v0.json`:
- `lowPrefixes`, `midPrefixes`, `highPrefixes`
- `suffixes`

Given the same seed + catalog content, module names and digests are stable.

## Example Rolls (seeded preview style)
1. Helios Mk3 Railgun Prime Lance
2. Bastion Mk2 ShieldGenerator Tuned Ward
3. Vanta Mk1 MissilePod Field Crown
4. Aurora Mk3 ScannerSuite Apex Halo
5. Helios Mk2 PlasmaProjector Calibrated Spindle
6. Bastion Mk1 FlakArray Frontier Bastion
7. Vanta Mk3 Railgun Mythic Matrix
8. Aurora Mk2 MissilePod Command Array
9. Helios Mk1 ScannerSuite Line Crown
10. Bastion Mk3 ShieldGenerator Ascendant Ward

## Roll Preview Export
Run in batchmode with:

`-executeMethod Space4X.EditorTools.Space4XModuleBomDebugCommands.Roll200ModulesPreview --seed 43101 --count 200 --out Temp/Reports/space4x_module_roll_preview_200.md`

Output columns:
- `rollId`
- `name`
- `family`
- `mark`
- `manufacturer`
- `key stats`
- `digest`

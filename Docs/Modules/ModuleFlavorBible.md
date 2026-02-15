# Module Flavor Bible (Pack v0)

## Families, Slots, Marks
| Family | ID | Slots | Marks |
| --- | --- | --- | --- |
| Railgun | `module.weapon.railgun` | barrel_emitter, capacitor, cooling, mount, firmware | Mk1, Mk2, Mk3 |
| MissilePod | `module.weapon.missile_pod` | barrel_emitter, capacitor, cooling, mount, firmware | Mk1, Mk2, Mk3 |
| FlakArray | `module.weapon.flak_array` | barrel_emitter, capacitor, cooling, mount, firmware | Mk1, Mk2, Mk3 |
| PlasmaProjector | `module.weapon.plasma_projector` | barrel_emitter, capacitor, cooling, mount, firmware | Mk1, Mk2, Mk3 |
| ShieldGenerator | `module.defense.shield_generator` | coil, regulator, heat_sink, core, firmware | Mk1, Mk2, Mk3 |
| ScannerSuite | `module.utility.scanner_suite` | coil, regulator, heat_sink, core, firmware | Mk1, Mk2, Mk3 |

## Manufacturer Feel Sheets
| Manufacturer | Feel | Key Tradeoffs |
| --- | --- | --- |
| Helios | High DPS, high heat, long range, lower reliability. | dpsx1.15, alphax1.09, rangex1.1, heatx1.13, reliabilityx0.89, trackingx0.95, utilityx0.94 |
| Bastion | Lower DPS, cooler operation, shorter range, high reliability. | dpsx0.9, alphax0.94, rangex0.9, heatx0.84, reliabilityx1.17, trackingx1.02, utilityx1.04 |
| Vanta | Burst/alpha specialist with cooldown pressure and stealth lean. | dpsx1.03, alphax1.18, cycle_timex1.08, heatx1.02, reliabilityx0.93, stealth_revealx1.11, utilityx1.02 |
| Aurora | Tracking/control utility profile with wider beam handling. | dpsx0.96, alphax0.95, rangex1.01, heatx0.95, reliabilityx1.04, trackingx1.16, utilityx1.19 |

## Affix Glossary
- Low prefixes: Field, Frontier, Rough, Line, Jury-Rigged, Workhorse
- Mid prefixes: Tuned, Calibrated, Command, Veteran, Refit, Balanced
- High prefixes: Prime, Apex, Ascendant, Mythic, Masterwork, Signature
- Suffixes: Array, Lance, Spindle, Ward, Halo, Matrix, Crown, Bastion

## Deterministic Naming Rule
Name = ``Manufacturer + Mk + Model + Prefix + Suffix``

## 20 Example Names Per Manufacturer
### Helios
| # | Name | Stat Feel |
| --- | --- | --- |
| 1 | Helios Mk1 Railgun Field Lance | hot, long reach burst |
| 2 | Helios Mk2 MissilePod Calibrated Matrix | hot, long reach burst |
| 3 | Helios Mk3 FlakArray Ascendant Lance | hot, long reach burst |
| 4 | Helios Mk1 PlasmaProjector Line Spindle | hot, long reach burst |
| 5 | Helios Mk2 ShieldGenerator Refit Crown | hot, long reach burst |
| 6 | Helios Mk3 ScannerSuite Signature Spindle | hot, long reach burst |
| 7 | Helios Mk1 Railgun Field Ward | hot, long reach burst |
| 8 | Helios Mk2 MissilePod Calibrated Bastion | hot, long reach burst |
| 9 | Helios Mk3 FlakArray Ascendant Ward | hot, long reach burst |
| 10 | Helios Mk1 PlasmaProjector Line Halo | hot, long reach burst |
| 11 | Helios Mk2 ShieldGenerator Refit Array | hot, long reach burst |
| 12 | Helios Mk3 ScannerSuite Signature Halo | hot, long reach burst |
| 13 | Helios Mk1 Railgun Field Matrix | hot, long reach burst |
| 14 | Helios Mk2 MissilePod Calibrated Lance | hot, long reach burst |
| 15 | Helios Mk3 FlakArray Ascendant Matrix | hot, long reach burst |
| 16 | Helios Mk1 PlasmaProjector Line Crown | hot, long reach burst |
| 17 | Helios Mk2 ShieldGenerator Refit Spindle | hot, long reach burst |
| 18 | Helios Mk3 ScannerSuite Signature Crown | hot, long reach burst |
| 19 | Helios Mk1 Railgun Field Bastion | hot, long reach burst |
| 20 | Helios Mk2 MissilePod Calibrated Ward | hot, long reach burst |

### Bastion
| # | Name | Stat Feel |
| --- | --- | --- |
| 1 | Bastion Mk1 Railgun Field Lance | cool and reliable hold |
| 2 | Bastion Mk2 MissilePod Calibrated Matrix | cool and reliable hold |
| 3 | Bastion Mk3 FlakArray Ascendant Lance | cool and reliable hold |
| 4 | Bastion Mk1 PlasmaProjector Line Spindle | cool and reliable hold |
| 5 | Bastion Mk2 ShieldGenerator Refit Crown | cool and reliable hold |
| 6 | Bastion Mk3 ScannerSuite Signature Spindle | cool and reliable hold |
| 7 | Bastion Mk1 Railgun Field Ward | cool and reliable hold |
| 8 | Bastion Mk2 MissilePod Calibrated Bastion | cool and reliable hold |
| 9 | Bastion Mk3 FlakArray Ascendant Ward | cool and reliable hold |
| 10 | Bastion Mk1 PlasmaProjector Line Halo | cool and reliable hold |
| 11 | Bastion Mk2 ShieldGenerator Refit Array | cool and reliable hold |
| 12 | Bastion Mk3 ScannerSuite Signature Halo | cool and reliable hold |
| 13 | Bastion Mk1 Railgun Field Matrix | cool and reliable hold |
| 14 | Bastion Mk2 MissilePod Calibrated Lance | cool and reliable hold |
| 15 | Bastion Mk3 FlakArray Ascendant Matrix | cool and reliable hold |
| 16 | Bastion Mk1 PlasmaProjector Line Crown | cool and reliable hold |
| 17 | Bastion Mk2 ShieldGenerator Refit Spindle | cool and reliable hold |
| 18 | Bastion Mk3 ScannerSuite Signature Crown | cool and reliable hold |
| 19 | Bastion Mk1 Railgun Field Bastion | cool and reliable hold |
| 20 | Bastion Mk2 MissilePod Calibrated Ward | cool and reliable hold |

### Vanta
| # | Name | Stat Feel |
| --- | --- | --- |
| 1 | Vanta Mk1 Railgun Field Lance | alpha spike, cooldown tax |
| 2 | Vanta Mk2 MissilePod Calibrated Matrix | alpha spike, cooldown tax |
| 3 | Vanta Mk3 FlakArray Ascendant Lance | alpha spike, cooldown tax |
| 4 | Vanta Mk1 PlasmaProjector Line Spindle | alpha spike, cooldown tax |
| 5 | Vanta Mk2 ShieldGenerator Refit Crown | alpha spike, cooldown tax |
| 6 | Vanta Mk3 ScannerSuite Signature Spindle | alpha spike, cooldown tax |
| 7 | Vanta Mk1 Railgun Field Ward | alpha spike, cooldown tax |
| 8 | Vanta Mk2 MissilePod Calibrated Bastion | alpha spike, cooldown tax |
| 9 | Vanta Mk3 FlakArray Ascendant Ward | alpha spike, cooldown tax |
| 10 | Vanta Mk1 PlasmaProjector Line Halo | alpha spike, cooldown tax |
| 11 | Vanta Mk2 ShieldGenerator Refit Array | alpha spike, cooldown tax |
| 12 | Vanta Mk3 ScannerSuite Signature Halo | alpha spike, cooldown tax |
| 13 | Vanta Mk1 Railgun Field Matrix | alpha spike, cooldown tax |
| 14 | Vanta Mk2 MissilePod Calibrated Lance | alpha spike, cooldown tax |
| 15 | Vanta Mk3 FlakArray Ascendant Matrix | alpha spike, cooldown tax |
| 16 | Vanta Mk1 PlasmaProjector Line Crown | alpha spike, cooldown tax |
| 17 | Vanta Mk2 ShieldGenerator Refit Spindle | alpha spike, cooldown tax |
| 18 | Vanta Mk3 ScannerSuite Signature Crown | alpha spike, cooldown tax |
| 19 | Vanta Mk1 Railgun Field Bastion | alpha spike, cooldown tax |
| 20 | Vanta Mk2 MissilePod Calibrated Ward | alpha spike, cooldown tax |

### Aurora
| # | Name | Stat Feel |
| --- | --- | --- |
| 1 | Aurora Mk1 Railgun Field Lance | tracking/control utility |
| 2 | Aurora Mk2 MissilePod Calibrated Matrix | tracking/control utility |
| 3 | Aurora Mk3 FlakArray Ascendant Lance | tracking/control utility |
| 4 | Aurora Mk1 PlasmaProjector Line Spindle | tracking/control utility |
| 5 | Aurora Mk2 ShieldGenerator Refit Crown | tracking/control utility |
| 6 | Aurora Mk3 ScannerSuite Signature Spindle | tracking/control utility |
| 7 | Aurora Mk1 Railgun Field Ward | tracking/control utility |
| 8 | Aurora Mk2 MissilePod Calibrated Bastion | tracking/control utility |
| 9 | Aurora Mk3 FlakArray Ascendant Ward | tracking/control utility |
| 10 | Aurora Mk1 PlasmaProjector Line Halo | tracking/control utility |
| 11 | Aurora Mk2 ShieldGenerator Refit Array | tracking/control utility |
| 12 | Aurora Mk3 ScannerSuite Signature Halo | tracking/control utility |
| 13 | Aurora Mk1 Railgun Field Matrix | tracking/control utility |
| 14 | Aurora Mk2 MissilePod Calibrated Lance | tracking/control utility |
| 15 | Aurora Mk3 FlakArray Ascendant Matrix | tracking/control utility |
| 16 | Aurora Mk1 PlasmaProjector Line Crown | tracking/control utility |
| 17 | Aurora Mk2 ShieldGenerator Refit Spindle | tracking/control utility |
| 18 | Aurora Mk3 ScannerSuite Signature Crown | tracking/control utility |
| 19 | Aurora Mk1 Railgun Field Bastion | tracking/control utility |
| 20 | Aurora Mk2 MissilePod Calibrated Ward | tracking/control utility |

## 30 Rolled Example Modules (Name + 3 Key Stats)
| # | Name | Family | Stats |
| --- | --- | --- | --- |
| 1 | Helios Mk1 Railgun Workhorse Spindle | `module.weapon.railgun` | dps=50.6; range=72.6; heat=31.64 |
| 2 | Bastion Mk2 MissilePod Tuned Crown | `module.weapon.missile_pod` | dps=42.3; alpha=76.14; cycle_time=1.1 |
| 3 | Vanta Mk3 FlakArray Apex Spindle | `module.weapon.flak_array` | dps=51.5; tracking=85; heat=27.54 |
| 4 | Aurora Mk1 PlasmaProjector Rough Ward | `module.weapon.plasma_projector` | dps=44.16; range=49.49; heat=32.3 |
| 5 | Helios Mk2 ShieldGenerator Veteran Bastion | `module.defense.shield_generator` | shield_hp=430; regen=18; reliability=0.86 |
| 6 | Bastion Mk3 ScannerSuite Masterwork Ward | `module.utility.scanner_suite` | sensor_range=120; tracking=51; utility=63.44 |
| 7 | Vanta Mk1 Railgun Workhorse Halo | `module.weapon.railgun` | dps=45.32; range=66; heat=28.56 |
| 8 | Aurora Mk2 MissilePod Tuned Array | `module.weapon.missile_pod` | dps=45.12; alpha=76.95; cycle_time=1.1 |
| 9 | Helios Mk3 FlakArray Apex Halo | `module.weapon.flak_array` | dps=57.5; tracking=80.75; heat=30.51 |
| 10 | Bastion Mk1 PlasmaProjector Rough Matrix | `module.weapon.plasma_projector` | dps=41.4; range=44.1; heat=28.56 |
| 11 | Vanta Mk2 ShieldGenerator Veteran Lance | `module.defense.shield_generator` | shield_hp=430; regen=18; reliability=0.9 |
| 12 | Aurora Mk3 ScannerSuite Masterwork Matrix | `module.utility.scanner_suite` | sensor_range=120; tracking=58; utility=72.59 |
| 13 | Helios Mk1 Railgun Workhorse Crown | `module.weapon.railgun` | dps=50.6; range=72.6; heat=31.64 |
| 14 | Bastion Mk2 MissilePod Tuned Spindle | `module.weapon.missile_pod` | dps=42.3; alpha=76.14; cycle_time=1.1 |
| 15 | Vanta Mk3 FlakArray Apex Crown | `module.weapon.flak_array` | dps=51.5; tracking=85; heat=27.54 |
| 16 | Aurora Mk1 PlasmaProjector Rough Bastion | `module.weapon.plasma_projector` | dps=44.16; range=49.49; heat=32.3 |
| 17 | Helios Mk2 ShieldGenerator Veteran Ward | `module.defense.shield_generator` | shield_hp=430; regen=18; reliability=0.86 |
| 18 | Bastion Mk3 ScannerSuite Masterwork Bastion | `module.utility.scanner_suite` | sensor_range=120; tracking=51; utility=63.44 |
| 19 | Vanta Mk1 Railgun Workhorse Array | `module.weapon.railgun` | dps=45.32; range=66; heat=28.56 |
| 20 | Aurora Mk2 MissilePod Tuned Halo | `module.weapon.missile_pod` | dps=45.12; alpha=76.95; cycle_time=1.1 |
| 21 | Helios Mk3 FlakArray Apex Array | `module.weapon.flak_array` | dps=57.5; tracking=80.75; heat=30.51 |
| 22 | Bastion Mk1 PlasmaProjector Rough Lance | `module.weapon.plasma_projector` | dps=41.4; range=44.1; heat=28.56 |
| 23 | Vanta Mk2 ShieldGenerator Veteran Matrix | `module.defense.shield_generator` | shield_hp=430; regen=18; reliability=0.9 |
| 24 | Aurora Mk3 ScannerSuite Masterwork Lance | `module.utility.scanner_suite` | sensor_range=120; tracking=58; utility=72.59 |
| 25 | Helios Mk1 Railgun Workhorse Spindle | `module.weapon.railgun` | dps=50.6; range=72.6; heat=31.64 |
| 26 | Bastion Mk2 MissilePod Tuned Crown | `module.weapon.missile_pod` | dps=42.3; alpha=76.14; cycle_time=1.1 |
| 27 | Vanta Mk3 FlakArray Apex Spindle | `module.weapon.flak_array` | dps=51.5; tracking=85; heat=27.54 |
| 28 | Aurora Mk1 PlasmaProjector Rough Ward | `module.weapon.plasma_projector` | dps=44.16; range=49.49; heat=32.3 |
| 29 | Helios Mk2 ShieldGenerator Veteran Bastion | `module.defense.shield_generator` | shield_hp=430; regen=18; reliability=0.86 |
| 30 | Bastion Mk3 ScannerSuite Masterwork Ward | `module.utility.scanner_suite` | sensor_range=120; tracking=51; utility=63.44 |


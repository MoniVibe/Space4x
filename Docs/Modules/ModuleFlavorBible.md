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




## Pack03 Expansion
- Added `modules_bom_catalog_pack03.json` with 16 new signature parts across weapon/support slot families for Railgun, MissilePod, FlakArray, PlasmaProjector, ShieldGenerator, and ScannerSuite loadouts.
- Added 13 affixes: 3 low, 3 mid, 3 high prefixes and 4 suffixes.
- Pack03 is additive-only and merges after pack02 by filename sort.

### Pack03 Affixes
- Low: Scavenged, Dockside, Recovered
- Mid: Fleetline, Vector-Tuned, Siege-Fit
- High: Sovereign, Relic-Grade, Overclocked
- Suffix: of Ember, of Bulwark, of Phantom, of Guidance

### Deterministic Seed Set
- Helios: 51001-51010
- Bastion: 52001-52010
- Vanta: 53001-53010
- Aurora: 54001-54010

### 40 Pack03 Example Names (10 per manufacturer)
#### Helios
| # | Seed | Name | Key Stats |
| --- | --- | --- | --- |
| 1 | 51001 | Helios Mk2 Railgun Vector-Tuned of Bulwark | dps=59.8; heat=36.16; reliability=0.801 |
| 2 | 51002 | Helios Mk3 MissilePod Overclocked of Guidance | dps=54.05; heat=29.38; reliability=0.81 |
| 3 | 51003 | Helios Mk1 FlakArray Scavenged of Bulwark | dps=48.3; heat=27.12; reliability=0.819 |
| 4 | 51004 | Helios Mk2 PlasmaProjector Vector-Tuned of Guidance | dps=63.25; heat=44.07; reliability=0.783 |
| 5 | 51005 | Helios Mk3 ShieldGenerator Overclocked of Bulwark | utility=56.4; reliability=0.863; heat=15.82 |
| 6 | 51006 | Helios Mk1 ScannerSuite Scavenged of Guidance | utility=67.68; reliability=0.845; heat=11.3 |
| 7 | 51007 | Helios Mk2 Railgun Vector-Tuned of Bulwark | dps=59.8; heat=36.16; reliability=0.801 |
| 8 | 51008 | Helios Mk3 MissilePod Overclocked of Guidance | dps=54.05; heat=29.38; reliability=0.81 |
| 9 | 51009 | Helios Mk1 FlakArray Scavenged of Bulwark | dps=48.3; heat=27.12; reliability=0.819 |
| 10 | 51010 | Helios Mk2 PlasmaProjector Vector-Tuned of Guidance | dps=63.25; heat=44.07; reliability=0.783 |

#### Bastion
| # | Seed | Name | Key Stats |
| --- | --- | --- | --- |
| 1 | 52001 | Bastion Mk3 Railgun Overclocked of Bulwark | dps=46.8; heat=26.88; reliability=1.053 |
| 2 | 52002 | Bastion Mk1 MissilePod Scavenged of Guidance | dps=42.3; heat=21.84; reliability=1.065 |
| 3 | 52003 | Bastion Mk2 FlakArray Vector-Tuned of Bulwark | dps=37.8; heat=20.16; reliability=1.076 |
| 4 | 52004 | Bastion Mk3 PlasmaProjector Overclocked of Guidance | dps=49.5; heat=32.76; reliability=1.03 |
| 5 | 52005 | Bastion Mk1 ShieldGenerator Scavenged of Bulwark | utility=62.4; reliability=1.135; heat=11.76 |
| 6 | 52006 | Bastion Mk2 ScannerSuite Vector-Tuned of Guidance | utility=74.88; reliability=1.112; heat=8.4 |
| 7 | 52007 | Bastion Mk3 Railgun Overclocked of Bulwark | dps=46.8; heat=26.88; reliability=1.053 |
| 8 | 52008 | Bastion Mk1 MissilePod Scavenged of Guidance | dps=42.3; heat=21.84; reliability=1.065 |
| 9 | 52009 | Bastion Mk2 FlakArray Vector-Tuned of Bulwark | dps=37.8; heat=20.16; reliability=1.076 |
| 10 | 52010 | Bastion Mk3 PlasmaProjector Overclocked of Guidance | dps=49.5; heat=32.76; reliability=1.03 |

#### Vanta
| # | Seed | Name | Key Stats |
| --- | --- | --- | --- |
| 1 | 53001 | Vanta Mk1 Railgun Scavenged of Bulwark | dps=53.56; heat=32.64; reliability=0.837 |
| 2 | 53002 | Vanta Mk2 MissilePod Vector-Tuned of Guidance | dps=48.41; heat=26.52; reliability=0.846 |
| 3 | 53003 | Vanta Mk3 FlakArray Overclocked of Bulwark | dps=43.26; heat=24.48; reliability=0.856 |
| 4 | 53004 | Vanta Mk1 PlasmaProjector Scavenged of Guidance | dps=56.65; heat=39.78; reliability=0.818 |
| 5 | 53005 | Vanta Mk2 ShieldGenerator Vector-Tuned of Bulwark | utility=61.2; reliability=0.902; heat=14.28 |
| 6 | 53006 | Vanta Mk3 ScannerSuite Overclocked of Guidance | utility=73.44; reliability=0.884; heat=10.2 |
| 7 | 53007 | Vanta Mk1 Railgun Scavenged of Bulwark | dps=53.56; heat=32.64; reliability=0.837 |
| 8 | 53008 | Vanta Mk2 MissilePod Vector-Tuned of Guidance | dps=48.41; heat=26.52; reliability=0.846 |
| 9 | 53009 | Vanta Mk3 FlakArray Overclocked of Bulwark | dps=43.26; heat=24.48; reliability=0.856 |
| 10 | 53010 | Vanta Mk1 PlasmaProjector Scavenged of Guidance | dps=56.65; heat=39.78; reliability=0.818 |

#### Aurora
| # | Seed | Name | Key Stats |
| --- | --- | --- | --- |
| 1 | 54001 | Aurora Mk2 Railgun Vector-Tuned of Bulwark | dps=49.92; heat=30.4; reliability=0.936 |
| 2 | 54002 | Aurora Mk3 MissilePod Overclocked of Guidance | dps=45.12; heat=24.7; reliability=0.946 |
| 3 | 54003 | Aurora Mk1 FlakArray Scavenged of Bulwark | dps=40.32; heat=22.8; reliability=0.957 |
| 4 | 54004 | Aurora Mk2 PlasmaProjector Vector-Tuned of Guidance | dps=52.8; heat=37.05; reliability=0.915 |
| 5 | 54005 | Aurora Mk3 ShieldGenerator Overclocked of Bulwark | utility=71.4; reliability=1.009; heat=13.3 |
| 6 | 54006 | Aurora Mk1 ScannerSuite Scavenged of Guidance | utility=85.68; reliability=0.988; heat=9.5 |
| 7 | 54007 | Aurora Mk2 Railgun Vector-Tuned of Bulwark | dps=49.92; heat=30.4; reliability=0.936 |
| 8 | 54008 | Aurora Mk3 MissilePod Overclocked of Guidance | dps=45.12; heat=24.7; reliability=0.946 |
| 9 | 54009 | Aurora Mk1 FlakArray Scavenged of Bulwark | dps=40.32; heat=22.8; reliability=0.957 |
| 10 | 54010 | Aurora Mk2 PlasmaProjector Vector-Tuned of Guidance | dps=52.8; heat=37.05; reliability=0.915 |



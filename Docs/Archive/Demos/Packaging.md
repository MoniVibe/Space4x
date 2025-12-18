# Demo Packaging

## Overview

Demo builds are packaged as self-contained zips with executable, scenarios, bindings, and reports. Each game has its own package.

## Package Structure

### Space4X Demo Package

```
Space4X_Demo_<date>.zip
├── Space4X_Demo.exe
├── Space4X_Demo_Data/
│   ├── Scenarios/
│   │   ├── combat_duel_weapons.json
│   │   ├── mining_loop.json
│   │   ├── compliance_demo.json
│   │   └── carrier_ops.json
│   ├── Bindings/
│   │   ├── Minimal.asset
│   │   └── Fancy.asset
│   └── Reports/
│       └── last_run.json
├── Readme.md
└── LICENSE (if applicable)
```

### Godgame Demo Package

```
Godgame_Demo_<date>.zip
├── Godgame_Demo.exe
├── Godgame_Demo_Data/
│   ├── Scenarios/
│   │   ├── villager_loop_small.json
│   │   ├── construction_ghost.json
│   │   └── time_rewind_smoke.json
│   ├── Bindings/
│   │   ├── Minimal.asset
│   │   └── Fancy.asset
│   └── Reports/
│       └── last_run.json
├── Readme.md
└── LICENSE (if applicable)
```

## Package Contents

### Executable

- Built with appropriate scripting symbols (`SPACE4X_SCENARIO` or `GODGAME_SCENARIO`)
- Includes all required assemblies
- Excludes other game's assemblies
- Platform-specific (Windows, Mac, Linux)

### Scenarios

**Location**: `Scenarios/` folder in package

**Included**:
- All known-good scenarios for the game
- Scenario JSON files
- Scenario metadata (if any)

**Space4X Scenarios**:
- `combat_duel_weapons.json`
- `mining_loop.json`
- `compliance_demo.json`
- `carrier_ops.json`

**Godgame Scenarios**:
- `villager_loop_small.json`
- `construction_ghost.json`
- `time_rewind_smoke.json`

### Bindings

**Location**: `Bindings/` folder in package

**Included**:
- `Minimal.asset` - Minimal presentation bindings
- `Fancy.asset` - Fancy presentation bindings

**Usage**: Live swap via hotkey `B` without rebuild

### Reports

**Location**: `Reports/` folder in package

**Included**:
- `last_run.json` - Last successful run metrics
- Optional: Preflight validation report

**Format**: JSON with metrics, determinism checks, budgets

### Readme.md

**Contents**:
- Demo overview
- Hotkeys reference
- Known limits
- Troubleshooting
- System requirements

**Example**:
```markdown
# Space4X Demo

## Hotkeys
- P: Pause/Play
- J: Toggle Jump/Flank planner
- B: Swap Minimal/Fancy bindings
- V: Toggle veteran proficiency
- R: Rewind sequence

## Known Limits
- Max entities: 10,000
- Snapshot ring: 1MB
- Fixed tick target: 16.67ms

## System Requirements
- Windows 10+
- DirectX 11
- 4GB RAM
```

## Package Naming

### Format

`<Game>_Demo_<YYYYMMDD>.zip`

**Examples**:
- `Space4X_Demo_20250115.zip`
- `Godgame_Demo_20250115.zip`

### Versioning

Optional version suffix:
- `Space4X_Demo_20250115_v1.0.zip`
- `Space4X_Demo_20250115_v1.1.zip`

## Launcher (Optional)

### Concept

Tiny launcher executable that:
1. Lists available scenarios
2. Allows scenario selection
3. Launches demo with selected scenario
4. Shows last run metrics

### Implementation

**Simple Launcher**:
- Console-based menu
- Scenario list from `Scenarios/` folder
- Launch demo with `--scenario=<selected>`

**GUI Launcher** (future):
- Unity-based launcher
- Visual scenario browser
- Metrics display
- Settings (bindings, veteran proficiency)

## CI Artifact Generation

### Automated Packaging

CI pipeline:
1. Build executable
2. Copy scenarios to package
3. Copy bindings to package
4. Generate Readme.md
5. Create zip archive
6. Upload to artifact storage

### Artifact Storage

**Location**: CI artifact storage (e.g., GitHub Actions artifacts, Jenkins)

**Retention**: 30 days (configurable)

**Naming**: Same as package name

## Package Validation

### Pre-Package Checks

Before packaging:
1. Verify executable exists
2. Verify scenarios exist
3. Verify bindings exist
4. Verify Readme.md exists
5. Run preflight validation (optional)

### Post-Package Checks

After packaging:
1. Verify zip structure
2. Verify file sizes (not empty)
3. Test extraction
4. Verify executable runs

## Distribution

### Internal Distribution

- Upload to internal file share
- Share link with team
- Include in release notes

### External Distribution

- Upload to public hosting (if applicable)
- Include download link in documentation
- Provide checksums for verification

## Package Size Optimization

### Minimize Size

- Exclude unnecessary assets
- Compress scenarios (if large)
- Use Unity's asset compression
- Exclude debug symbols (release build)

### Target Sizes

- Space4X Demo: < 500MB
- Godgame Demo: < 300MB

## Implementation Notes

- Packaging runs after successful build
- Uses Unity's build output directory
- Zip creation via standard tools (7zip, zip command)
- Readme.md generated from template
- Scenarios copied from `Assets/Scenarios/`
- Bindings copied from `Assets/Space4X/Bindings/`


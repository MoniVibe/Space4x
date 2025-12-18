# Demo Build Profiles

## Overview

Demo builds use Unity Editor build profiles (or CLI methods) to gate code compilation and exclude unnecessary assemblies. Each game has its own build target with specific scripting symbols.

## Build Targets

### Godgame Demo

**Scripting Symbols**: `GODGAME_SCENARIO`

**Assembly Exclusions**:
- Exclude Space4X assemblies (they already have `SPACE4X` defineConstraints)
- Optional: `PUREDOTS_AUTHORING` off unless authoring is embedded

**Build Profile Setup**:
1. Create new build profile: `Godgame Demo`
2. Set scripting symbols: `GODGAME_SCENARIO`
3. Configure assembly exclusions in Player Settings
4. Set output path: `Build/Godgame_Demo_<date>.exe`

### Space4X Demo

**Scripting Symbols**: `SPACE4X_SCENARIO`

**Assembly Exclusions**:
- Exclude Godgame-only editor scripts
- Ensure Space4X assemblies have `SPACE4X` defineConstraints

**Build Profile Setup**:
1. Create new build profile: `Space4X Demo`
2. Set scripting symbols: `SPACE4X_SCENARIO`
3. Configure assembly exclusions in Player Settings
4. Set output path: `Build/Space4X_Demo_<date>.exe`

### Shared Configuration

Both builds:
- Run Prefab Maker in Minimal set prebuild
- Ship both Minimal & Fancy bindings to allow live swap
- Include scenario JSONs in `Assets/Scenarios/<game>/`
- Include binding assets in `Assets/Space4X/Bindings/`

## CLI Build Methods

### ExecuteMethod Entry Point

Create `Demos.Build.Run` static method:

```csharp
public static class Demos
{
    public static class Build
    {
        public static void Run()
        {
            // Parse command line args:
            // --game=Godgame|Space4X
            // --scenario=<path>
            // --bindings=Minimal|Fancy
            // --output=<path>
        }
    }
}
```

### CLI Examples

**Godgame Demo**:
```bash
Unity -batchmode -projectPath . -executeMethod Demos.Build.Run --game=Godgame --scenario=construction_ghost.json --bindings=Minimal
```

**Space4X Demo**:
```bash
Unity -batchmode -projectPath . -executeMethod Demos.Build.Run --game=Space4X --scenario=combat_duel_weapons.json --bindings=Fancy
```

### Batchmode Build

For Windows desktop builds:
```bash
Unity -batchmode -projectPath . -quit -buildWindows64Player Build/Space4x.exe
```

Change output path per platform as needed.

## Scripting Symbol Configuration

### Editor Setup

1. Open **Edit > Project Settings > Player**
2. Select platform (e.g., Windows)
3. Expand **Other Settings**
4. Set **Scripting Define Symbols**:
   - For Space4X: `SPACE4X_SCENARIO`
   - For Godgame: `GODGAME_SCENARIO`
   - Optional tests: `SPACE4X_TESTS` or `GODGAME_TESTS`

### Assembly Definition Constraints

Space4X assemblies should have:
```xml
<defineConstraints>
    <constraint>SPACE4X</constraint>
</defineConstraints>
```

This prevents Space4X code from compiling in Godgame builds.

## Prebuild Steps

### Prefab Maker Integration

Before building:
1. Run Prefab Maker in Minimal mode
2. Validate prefab generation
3. Write idempotency JSON to verify consistency
4. Ensure both Minimal and Fancy bindings are available

### Preflight Validation

Run `Demos.Preflight.Run(game)` before build:
- Prefab Maker validation
- Determinism dry-runs (30/60/120Hz)
- Budget assertions (fixed_tick_ms, snapshot ring)
- Binding swap validation

See [Preflight.md](Preflight.md) for details.

## Build Output Structure

```
Build/
├── Space4X_Demo_<date>.exe
├── Space4X_Demo_<date>_Data/
│   ├── Scenarios/
│   ├── Bindings/
│   │   ├── Minimal.asset
│   │   └── Fancy.asset
│   └── Reports/
└── Godgame_Demo_<date>.exe
    └── ...
```

## CI Integration

For continuous integration:
1. Set scripting symbols via CLI: `-define:SPACE4X_SCENARIO`
2. Run preflight validation
3. Build executable
4. Package artifacts (see [Packaging.md](Packaging.md))
5. Upload to artifact storage


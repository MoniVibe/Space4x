# Space4x Project Setup

## Overview

Space4x is an independent Unity project that consumes the PureDOTS package for its DOTS framework.

## Structure

- `Assets/Scenes/` - All Space4x scenes
  - `Demo/` - Active demo scenes
  - `Hybrid/` - Archived hybrid showcase scenes (see ARCHIVED_README.md)
- `Assets/Scripts/Space4x/` - Space4x-specific code
- `Packages/manifest.json` - References PureDOTS package

## Package Reference

Space4x references PureDOTS via:
```json
"com.moni.puredots": "file:../../PureDOTS/Packages/com.moni.puredots"
```

## Development

1. Open `Space4x/` folder as Unity project
2. PureDOTS package is automatically available
3. Create scenes, prefabs, and scripts in `Assets/`
4. Use PureDOTS systems as needed

## Notes

- This project is independent of Godgame
- All assets (scenes, prefabs, configs) are local to this project
- PureDOTS package provides shared DOTS framework systems
- Demo scenes demonstrate Space4x mining/carrier mechanics



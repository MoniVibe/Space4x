# PureDOTS Project Briefing

Welcome to the PureDOTS migration effort. This repository is a fresh Unity project configured with Entities 1.4 and mirrors the package environment from the legacy `godgame` repo. The long-term goal is to deliver the full GodGame experience using a pure DOTS architecture, guided by existing TruthSource documentation.

## Current State

- Packages: Manifest matches the legacy project (Entities, Burst, Collections, URP, Coplay, MCP).
- TODO: `PureDOTS_TODO.md` outlines first setup tasks (assemblies, bootstrap, system migration, authoring).
- No gameplay systems have been ported yet—this repo starts clean.

## Mission for Primary Agent

1. Establish core DOTS infrastructure:
   - Create runtime/system/authoring asmdefs.
   - Implement a custom world bootstrap with fixed-step, simulation, and presentation groups.
   - Seed baseline singleton components (time state, history settings) to anchor determinism/rewind.

2. Port reusable DOTS components/systems from the legacy project:
   - Components: resources, villagers, time, history, input.
   - Baker/authoring scripts for SubScene workflows.
   - Systems already DOTS-native (time step, rewind core, resource gathering).

3. Document and validate:
   - Update `PureDOTS_TODO.md` as tasks complete.
   - Note assumptions/deviations in `Docs/Progress.md` (create if absent).
   - Keep alignment with TruthSources for design intent.

## Key Principles

- Pure DOTS: avoid `WorldServices`/service locators; use singleton components, buffers, and systems.
- Determinism & rewind remain central—use existing `TimeState`, `RewindState`, etc. as references.
- Presentation should be hybrid-friendly but minimal—simulation logic belongs in DOTS.
- Salvage carefully: port DOTS-ready code; reimplement hybrid logic for the new architecture.

## Suggested Starting Checklist

- [ ] Set up `PureDOTS.Runtime`, `PureDOTS.Systems`, `PureDOTS.Authoring` asmdefs.
- [ ] Add/refine `PureDotsWorldBootstrap` for the new project.
- [ ] Port `TimeState`, `RewindState`, `HistorySettings` components and associated systems.
- [ ] Create a simple DOTS-only test SubScene to validate the loop.

Coordinate with TruthSources and the legacy repo for reference data and design constraints. The focus is laying a clean foundation—future agents can expand gameplay domains once the base is solid.

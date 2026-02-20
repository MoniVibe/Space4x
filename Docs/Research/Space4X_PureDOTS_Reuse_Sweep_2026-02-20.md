# Space4X -> PureDOTS Reuse Sweep (2026-02-20)

## Scope

- Rescan Space4X and PureDOTS overlap areas.
- Identify reusable kernels vs Space4X-only policy/presentation.
- Execute one extraction immediately.

## Best-Practice Guardrails Applied

- Shared code stays game-agnostic and adapter-driven:
  - Unity assembly definition/package boundaries:
    - https://docs.unity3d.com/Manual/assembly-definition-files.html
    - https://docs.unity3d.com/Manual/cus-layout.html
- Deterministic simulation runs in fixed-step groups:
  - https://docs.unity.cn/Packages/com.unity.entities@1.0/manual/systems-update-order.html
- Keep game-specific policy behind an anti-corruption adapter layer:
  - https://learn.microsoft.com/en-us/azure/architecture/patterns/anti-corruption-layer
  - https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/identify-bounded-context
- Maneuver/steering logic kept as reusable steering kernel:
  - https://www.red3d.com/cwr/steer/gdc99/
  - https://gafferongames.com/post/fix_your_timestep/

## Extraction Completed In This Pass

- Added shared braking decision kernel to PureDOTS:
  - `puredots/Packages/com.moni.puredots/Runtime/Runtime/Movement/KinematicBrakingUtility.cs`
  - Includes:
    - `KinematicBrakingManeuver`
    - `KinematicBrakingDecisionInput`
    - `KinematicBrakingDecision`
    - `KinematicBrakingUtility.Evaluate(...)`
- Rewired Space4X vessel movement to call shared kernel:
  - `space4x/Assets/Scripts/Space4x/Systems/AI/VesselMovementSystem.cs`
  - Space4X keeps local policy and state fields, with thin enum adapters.

## Reuse Candidates (Next)

1. Module contract convergence (high impact, medium risk)
- Overlap:
  - `space4x/Assets/Scripts/Space4x/Registry/Space4XModuleComponents.cs`
  - `puredots/Packages/com.moni.puredots/Runtime/Runtime/Ships/CarrierModuleComponents.cs`
- Plan:
  - Unify slot/refit/health core contract in PureDOTS.
  - Keep Space4X inventory/fit UI structures in Space4X adapters.

2. Module host compatibility kernel (high impact, medium risk)
- Source:
  - `space4x/Assets/Scripts/Space4x/Registry/Space4XModuleHostCompatibility.cs`
- Plan:
  - Promote slot/module compatibility math and host-policy contract to PureDOTS.
  - Keep Space4X catalog lookups and specific ids in adapter.

3. Hull segment validation spine (high impact, higher risk)
- Source:
  - `space4x/Assets/Scripts/Space4x/Registry/ModuleDataSchemas.cs`
  - `space4x/Assets/Scripts/Space4x/Registry/ModuleCatalogUtility.cs`
- Plan:
  - Extract generic segment-role/count/family validation kernel.
  - Keep Space4X content catalogs and defaults in Space4X.

## Placement Rule Used

- PureDOTS:
  - deterministic simulation math kernels
  - no Space4X ids/types/theme terms
- Space4X:
  - camera/input/UI control modes
  - FleetCrawl content defaults and game-specific policy wiring

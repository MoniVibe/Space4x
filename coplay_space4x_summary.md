# Space4X Coplay Agent — Smoke Scene Validation

## Result
PASS

## Evidence
```
[Space4XSmokeWorldCounts] Phase=Final World='Game World' Catalog=True RenderSemanticKey=5 MaterialMeshInfo=5 Carrier=1 MiningVessel=2 Asteroid=2 ResourceSourceConfig=3 ResourceSourceState=3 ResolvedSectionEntity=1 ElapsedSeconds=0.4
[Space4XSmokePresentationCounts] Phase=Final World='Game World' RenderSemanticKey=5 RenderVariantKey=5 MeshPresenter=5 MaterialMeshInfo=5 RenderBounds=5 RenderFilterSettings=5 MaterialMeshInfoWithRenderFilterSettings=5 MaterialMeshInfoMissingRenderFilterSettings=0 LocalTransform=11 LocalToWorld=7 Carrier=1 MiningVessel=2 Asteroid=2 ResolvedSectionEntity=1 FallbackCarrier=0 FallbackMiningVessel=0 ...
[Space4XSmokePresentationCounts] Sample='Asteroid' Entity=Entity(1223:1) Pos=float3(18.75505f, 0f, 10.11223f) ... RenderSemanticKey=220 RenderVariantKey=4 ...
[Space4XSmokePresentationCounts] Sample='MiningVessel' Entity=Entity(1220:1) Pos=float3(7.83839f, 0f, -6.116253f) ... RenderSemanticKey=210 RenderVariantKey=3 ...
[Space4XSmokePresentationCounts] Sample='Carrier' Entity=Entity(1225:1) Pos=float3(0.6304963f, 0f, -4.557258f) ... RenderSemanticKey=200 RenderVariantKey=2 ...
```

## Scene Wiring Changes
None.

## New Errors
- [RegistryHealth] StorehouseRegistry degraded to Warning (flags: DirectoryMismatchWarning).
- [RegistryHealth] ResourceRegistry degraded to Warning (flags: DirectoryMismatchWarning).
- [RegistryHealth] VillagerRegistry degraded to Warning (flags: DirectoryMismatchWarning).

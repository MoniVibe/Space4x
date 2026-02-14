# Space4X Module Workbench

`Space4XModuleWorkbench` is an editor-only tool for rapid module flavor iteration using the deterministic BOM catalog/generator.

## Open the tool

- Unity menu: `Space4X/Modules/BOM V0/Module Workbench`

## Preview workflow

1. Set `Seed`, `Count`, `Quality`.
2. Optionally filter by `Family`, `Manufacturer`, and `Mark`.
3. Click `Generate`.
4. Inspect the generated list:
   - deterministic `rollId`
   - display name
   - part composition (`Parts`)
5. Select a row to inspect computed stats (`DPS`, `Heat`, `Range`, `Reliability`, `PowerDraw`) and raw stat totals.
6. Export with `Export CSV` or `Export JSON`.

## Comparison mode

1. Choose one `Family` and one `Mark`.
2. Choose `Manufacturer A` and `Manufacturer B`.
3. Set sample count (default 50) and quality target.
4. Click `Run Comparison`.
5. Review aggregate averages and deltas (A-B).
6. Export `CSV` or `MD` balance report.

## Export paths

- Default export root: `Temp/Reports/`
- Files are selected via save dialogs so you can route reports into artifact packs.

## Determinism notes

- Rolls use deterministic seeds through `Space4XModuleBomDeterministicGenerator`.
- Manufacturer-filtered previews/comparisons use deterministic seed stepping.
- Comparison output includes a digest to spot accidental drift between runs.

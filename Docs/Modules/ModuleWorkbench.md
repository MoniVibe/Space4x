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
   - name, family, manufacturer, mark
   - key stats (`DPS`, `Heat`, `Range`, `Reliability`)
5. Expand any row (`Parts`) to inspect slot/part composition and derived stats.
6. Select a row for full detail view and raw stat totals.
7. Click `Export Rolls CSV+MD`.

## Comparison mode

1. Choose one `Family` and one `Mark`.
2. Choose `Manufacturer A` and `Manufacturer B`.
3. Set sample count (default 50) and quality target.
4. Click `Run Comparison`.
5. Review deltas for each metric (`DPS`, `Heat`, `Range`, `Reliability`) as:
   - mean delta (A-B)
   - p50 delta (A-B)
   - p95 delta (A-B)
6. Export `CSV` or `MD` balance report.

## Export paths

- Roll preview exports (fixed paths):
  - `Temp/Reports/module_workbench_rolls.csv`
  - `Temp/Reports/module_workbench_rolls.md`
- Comparison exports:
  - `Temp/Reports/module_workbench_comparison.csv`
  - `Temp/Reports/module_workbench_comparison.md`

## Determinism notes

- Rolls use deterministic seeds through `Space4XModuleBomDeterministicGenerator`.
- Manufacturer-filtered previews/comparisons use deterministic seed stepping.
- Comparison output includes a digest to spot accidental drift between runs.

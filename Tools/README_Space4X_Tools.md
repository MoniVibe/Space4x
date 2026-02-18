# Space4X Offline Tools

These scripts are offline post-processing helpers. They do not require Unity and can be run on run artifacts directly.

## Telemetry Summarizer

Input: telemetry NDJSON, optional metrics JSON/CSV.  
Output: `summary.json` + `summary.md`.

```bash
python Tools/Telemetry/space4x_summarize_run.py \
  --telemetry path/to/telemetry.ndjson \
  --metrics_json path/to/metrics.json \
  --metrics_csv path/to/metrics.csv \
  --out_dir path/to/output
```

## Scenario Beats Generator

Input: Space4X scenario JSON.  
Output: beats JSON for shot-direction timelines.

```bash
python Tools/Scenarios/space4x_generate_beats.py \
  --scenario Assets/Scenarios/space4x_mining_combat.json \
  --out path/to/beats.json
```

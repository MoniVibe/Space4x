# Space4X Micro Catalog

- `Assets/Scenarios/space4x_questions_emitted_micro.json` → `space4x.q.meta.questions_emitted` → operator report emits `questions[]` with required ids and statuses → PASS when `questions[]` contains the required id with status.
- `Assets/Scenarios/space4x_blackcats_mapped_micro.json` → `space4x.q.meta.blackcats_mapped` → blackcat entries map to question ids with bounded evidence → PASS when no blackcats emitted, or when at least one mapped blackcat has evidence entities.

## Tier 1 - Production

- `Assets/Scenarios/space4x_production_progress_micro.json` → `space4x.q.production.chain_progress`, `space4x.q.production.stall_classified` → mining output progresses into carrier storage → PASS when chain progress > 0 and stall reason is None.
- `Assets/Scenarios/space4x_production_noinput_micro.json` → `space4x.q.production.stall_classified` → stall reason is classified as NoInput → PASS when stall reason matches NoInput (expected-fail style).
- `Assets/Scenarios/space4x_production_nostorage_micro.json` → `space4x.q.production.stall_classified` → stall reason is classified as NoStorage → PASS when stall reason matches NoStorage (expected-fail style).

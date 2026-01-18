# Space4X Micro Catalog

- `Assets/Scenarios/space4x_questions_emitted_micro.json` → `space4x.q.meta.questions_emitted` → operator report emits `questions[]` with required ids and statuses → PASS when `questions[]` contains the required id with status.
- `Assets/Scenarios/space4x_blackcats_mapped_micro.json` → `space4x.q.meta.blackcats_mapped` → blackcat entries map to question ids with bounded evidence → PASS when no blackcats emitted, or when at least one mapped blackcat has evidence entities.

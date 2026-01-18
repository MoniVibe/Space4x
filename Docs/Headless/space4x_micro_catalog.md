# Space4X Micro Catalog

- `Assets/Scenarios/space4x_questions_emitted_micro.json` → `space4x.q.meta.questions_emitted` → operator report emits `questions[]` with required ids and statuses → PASS when `questions[]` contains the required id with status.
- `Assets/Scenarios/space4x_blackcats_mapped_micro.json` → `space4x.q.meta.blackcats_mapped` → blackcat entries map to question ids with bounded evidence → PASS when no blackcats emitted, or when at least one mapped blackcat has evidence entities.

## Tier 1 - Orders

- `Assets/Scenarios/space4x_orders_micro.json` → `space4x.q.orders.command_consumed`, `space4x.q.orders.state_transition` → command queue processes a pending order → PASS when the order is consumed and transitions at least once.
- `Assets/Scenarios/space4x_orders_reject_micro.json` → `space4x.q.orders.reject_reason` → invalid target is rejected deterministically → PASS when reject reason matches `InvalidTarget`.

## Tier 1 - Ledger/Discovery

- `Assets/Scenarios/space4x_ledger_delta_micro.json` → `space4x.q.ledger.delta_expected` → event-driven ledger delta applied → PASS when actual delta matches expected.
- `Assets/Scenarios/space4x_discovery_deposit_micro.json` → `space4x.q.discovery.event_fired`, `space4x.q.ledger.delta_expected` → discovery event injects a ledger delta (scaffold) → PASS when event fired and delta matches.

## Suggested Run Order

- Tier 0: questions_emitted → sensors_micro → comms_micro → comms_blocked_micro → blackcats_mapped
- Tier 1: production_progress → production_noinput → production_nostorage → orders_micro → orders_reject → ledger_delta → discovery_deposit

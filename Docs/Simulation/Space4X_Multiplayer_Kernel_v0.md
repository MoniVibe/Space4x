# Space4X Multiplayer Kernel v0

## Intent

Define a multiplayer foundation that matches FleetCrawl's simulation depth without forcing a rewrite later.

Model choice:

- Terraria-style topology (hosted session or dedicated server)
- Unity Netcode for Entities style authority (server authoritative)
- Selective client prediction only where feel matters (local flagship control/fire), not for the whole sim

This keeps the simulation coherent while remaining practical for desktop+laptop growth testing.

## Why this model

1. FleetCrawl has heavy simulation slices (power, heat, signatures, AI, production). Full client authority or broad prediction is high-risk.
2. Server authority aligns with deterministic spine contracts and gives one source of truth.
3. Host-and-play enables fast iteration; dedicated server enables persistence and validator/nightly lanes.
4. Desktop+laptop setup maps cleanly to host/client validation from day one.

## Kernel Contract (v0)

### 1) Authority

- Server owns simulation truth:
  - state spine lifecycle/mode/scenario
  - combat and damage resolution
  - movement authority
  - economy/production progression
  - power/heat/signature effects
- Clients submit intents/commands only.
- No client-side permanent state mutation.

### 2) Command lane

- Versioned command envelope (player intent input):
  - move/steer/fire
  - target select/switch
  - RTS orders
  - god-mode interactions
- Commands are validated on server against authority + mode rules.
- Unknown/invalid commands are rejected with telemetry, never silently applied.

### 3) Snapshot lane

- Server replicates state snapshots (ghost state).
- Interest management required:
  - by distance
  - by ownership/faction relation
  - by sensor visibility/signature visibility
- Bandwidth budget per class (ships, projectiles, effects, debris, UI hints).

### 4) Prediction/reconciliation policy

- Predict:
  - local flagship steering/thrust
  - immediate local fire feedback (where needed)
- Do not predict:
  - economy/progression ticks
  - heat/signature propagation
  - non-owned AI behavior
- Server correction wins; client reconciles.

### 5) Version/protocol governance

- Protocol hash/version check on connect.
- Mismatched build/protocol rejected early with actionable reason.
- Registry IDs (M3 spine governance) are treated as net-critical.

### 6) Determinism observability

- Reuse spine digest for desync diagnostics:
  - periodic digest telemetry per peer/session
  - mismatch markers for quick triage
- Keep per-tick evidence bounded.

## Desktop + Laptop Growth Workflow

Primary loop:

1. Desktop runs host (`Host & Play`) or dedicated server lane.
2. Laptop joins as pure client.
3. Both lanes export telemetry summary for digest/latency/bandwidth checks.
4. Iterator changes stay branch-based; validator runs full matrix.

Minimum matrix per MP milestone:

- `D-H/L-C`: desktop host, laptop client
- `L-H/D-C`: laptop host, desktop client (sanity)
- `D-S + D-C + L-C`: dedicated server + two clients (as available)

## Milestones

### MP0: Foundation contract and transport wiring

Goal:

- establish server-authoritative command + snapshot pipeline skeleton
- add protocol/version guard
- expose connection/session telemetry

Acceptance:

- host and one remote client can connect/disconnect cleanly
- mismatch is rejected with explicit reason
- no simulation ownership ambiguity in logs

Current implementation status:

- Implemented kernel skeleton in ECS:
  - `Space4XMultiplayerKernelRootTag` singleton root
  - bounded connection request intake
  - protocol version/hash rejection paths with explicit reasons
  - bounded session event log
  - multiplayer telemetry metrics (`space4x.mp.*`)
- Verified with PlayMode tests:
  - `Space4X.Tests.PlayMode.Space4XMultiplayerKernelTests`
- Pending for full MP0 completion:
  - real transport/world bootstrap integration (Netcode client/server worlds)
  - host/client process-level handshake outside test harness

### MP1: Gameplay-critical authority path

Goal:

- move flagship movement, targeting, and weapon fire to authoritative command path

Acceptance:

- client command -> server apply -> replicated result roundtrip works
- no client-only damage/state commits
- same action seen consistently on both peers

### MP2: Selective prediction

Goal:

- add client prediction + reconciliation for flagship piloting/fire feel

Acceptance:

- perceived input latency reduced for local owner
- corrections stay bounded (no large rubber-banding spikes)
- non-owned entities remain server-driven

### MP3: Interest management and budgets

Goal:

- enforce relevancy/bandwidth budgets by entity class and sensor relation

Acceptance:

- controlled snapshot size under combat load
- client frame stability preserved at target entity counts
- hidden entities do not leak through replication

### MP4: Spine-integrated desync guardrails

Goal:

- integrate digest mismatch detection and diagnosis pipeline

Acceptance:

- deterministic mismatch events are surfaced with useful context
- triage flow points to command/snapshot/protocol root causes quickly

### MP5: Dedicated persistence handshake

Goal:

- align multiplayer checkpoint/load handoff with spine M5 save/load later

Acceptance:

- join/rejoin workflow survives checkpoint transitions
- schema/version migration path is explicit and test-covered

## Out of scope for v0

- matchmaking services and public lobby stack
- full lockstep simulation
- broad prediction for all systems
- anti-cheat hardening beyond authority enforcement/logging

## Sources

- Unity multiplayer overview (Unity 6.1): https://docs.unity.cn/6000.1/Documentation/Manual/multiplayer-overview.html
- Netcode for Entities prediction intro: https://docs.unity.cn/Packages/com.unity.netcode@1.5/manual/intro-to-prediction.html
- Netcode command stream: https://docs.unity.cn/Packages/com.unity.netcode@1.0/manual/command-stream.html
- Netcode client/server worlds: https://docs.unity.cn/Packages/com.unity.netcode@1.0/manual/client-server-worlds.html
- Netcode protocol checks: https://docs.unity.cn/Packages/com.unity.netcode@1.6/manual/network-protocol-checks.html
- Netcode optimization concepts: https://docs.unity.cn/Packages/com.unity.netcode@1.5/manual/optimizations.html
- Terraria multiplayer model reference: https://terraria.fandom.com/wiki/Multiplayer
- Terraria dedicated server setup reference: https://terraria.fandom.com/wiki/Guide:Setting_up_a_Terraria_server

# Mission Board (Current Implementation + Extension Points)

**Purpose**
The mission board generates offers (jobs/missions), assigns them to eligible ships, and rewards outcomes. It is the concrete “opportunity feed” for Galaxy Tamagotchi.

---

## 1) Current ECS Components

**Config + state**
- `Space4XMissionBoardConfig`
- `Space4XMissionBoardState`

**Offers**
- `Space4XMissionOffer` (offer entity with issuer, target, reward, risk, expiry)

**Assignments**
- `Space4XMissionAssignment` (attached to an agent; tracks phase, cargo state, due tick, etc.)

**Mission Types**
`Space4XMissionType`:
- `Scout`, `Mine`, `HaulDelivery`, `HaulProcure`, `Patrol`, `Intercept`, `BuildStation`

---

## 2) Current Flow (System Behavior)

**Generation**
- `Space4XAIMissionBoardSystem`
- Generates new offers every `GenerationIntervalTicks`
- Caps per faction via `MaxOffersPerFaction`
- Sources from:
  - Asteroids (mining)
  - POIs (scout)
  - Logistics demand (haul procure)
  - Systems/colonies/anomalies (varied)

**Assignment**
- Picks eligible agents: entities with `CaptainOrder` + `LocalTransform` and no `Space4XMissionAssignment`
- Writes `Space4XMissionAssignment`
- Updates `Space4XMissionOffer` status

**Execution + Completion**
- Assignment phases (`Space4XMissionPhase`, `Space4XMissionCargoState`)
- Rewards apply on completion:
  - Credits / influence to faction resources
  - Contact standing / LP updates via contact ledger

---

## 3) Persistence

Mission board config/state is persisted via:
- `Space4XSimServerPersistenceSystem`

---

## 4) Reward Model (Current)

**Credits**
`RewardCredits` + scaling for delivered cargo ratio.

**Standing / Loyalty**
`RewardStanding` and `RewardLp` update `Space4XContactStanding`.

---

## 5) Extension Points (Next Steps)

**Opportunity breadth**
- Add “Event” missions (treasure, anomaly, crisis response)
- Add “covert” or “diplomacy” missions with special risks

**Outcome transparency**
- Add `OpportunityOutcome` buffer (reason code + metrics)
- Log mission summaries to telemetry (for LLM agents)

**LLM interface hooks**
- Add read endpoint to return current offers
- Add accept/decline endpoints to allocate offers to fleets

---

## 6) SimServer Endpoints (Current)

- `GET /offers` -> current mission offers
- `GET /assignments` -> current assignments
- `POST /mission/accept` -> accept offer (auto-assign or specify assignee)
- `POST /mission/decline` -> decline offer

---

## 7) Suggested Next Schema (Minimal)

**Offer**
```
offerId, type, issuer, target, risk, reward, expiryTick, status
```

**Acceptance**
```
offerId, assigneeEntity, requestedTick
```

**Outcome**
```
offerId, status, reasonCode, rewardApplied, tickSpan
```

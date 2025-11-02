1) Core representation (PureDOTS)

**AlignmentTriplet**  
Every sentient entity (crew, captain, colony steward) owns a continuous alignment vector  
A = (L, G, I) ∈ [-1, +1]^3. Store it in an `AlignmentTriplet : IComponentData` with three `half` values.

- L (Lawfulness): +1 lawful, -1 chaotic. Drives obedience vs improvisation.  
- G (Altruism): +1 good, -1 evil. Signals prosocial vs exploitative intent.  
- I (Integrity): +1 pure, -1 corrupt. Tracks willingness to uphold ideals vs take shortcuts.

**EthicAxesBuffer**  
Use a sparse buffer (`DynamicBuffer<EthicAxisValue>`) where each element captures: `AxisId`, `Value`. Axis values live in [-2,+2] with ±1 = regular conviction, ±2 = fanatic. Enforce one entry per axis by design.

Initial axes for Space4x (expand later):
- WAR: +2 warlike ↔ -2 peaceful  
- MAT: +2 materialist ↔ -2 spiritual  
- AUTH: +2 authoritarian ↔ -2 egalitarian  
- XENO: +2 xenophobic ↔ -2 xenophilic  
- EXP: +2 expansionist ↔ -2 isolationist  

Axis Ids sit in a generated enum so systems can switch over them without boxing. Validation system ensures at-most two fanatic convictions or one fanatic + one regular, matching the narrative constraint that crews cannot be zealots on every front.

**Derived vectors**  
At runtime, transient systems may stitch the dense ethics vector `E` for SIMD math by streaming the buffer into a reusable float array indexed by `AxisId`. The combined trait vector is `[A | E_dense]`.

**Race, culture, outlooks**  
Individuals also carry `RaceId` and `CultureId` `IComponentData` structs (simple ushort ids resolved through authoring tables). Outlooks are tracked via `DynamicBuffer<OutlookEntry>` where each entry is `(OutlookId, Weight)` with Weight ∈ [-1,+1]. Only the three strongest absolute weights inform higher-level aggregates; retain the full buffer so narrative systems can surface niche traits.

2) Initiative & task scope

**InitiativeComponent**  
Stores base initiative `I_base` (species/template) and cached working initiative `I_runtime`. Apply morale and fatigue adjustments in a `FleetInitiativeSystem`.

Chaos bias:  
`I_runtime = clamp01(I_base + α * (-L) + fanaticMods + moraleBoost - fatiguePenalty)` with α ≈ 0.25. Chaotic entities (low L) act faster; lawful crews move deliberately.  

Fanatic nudges:  
- Fanatic WAR (≥ +2) : `+0.05` (eager to strike)  
- Fanatic AUTH (≥ +2) : `-0.05` (waits for orders)  
- Fanatic PEACE (≤ -2 on WAR) : `-0.05` (avoids escalation)

**Risk appetite**  
`risk = sigmoid(r0 + r1*(-L) + r2*(-I) + missionPressure)` — chaotic, corrupt, or under pressure become bolder.  
Actions publish a `ComplexityCost ∈ [0,1]`. A plan is eligible when `cost ≤ I_runtime * (1 + κ * risk)`. Captain orders can temporarily lift the cap for subordinates by writing a `TaskOverride` tag.

3) Behavior scoring & selection

Each candidate action archetype carries:
- `wAlign ∈ R^3` weighting the base alignment.  
- `wEthics ∈ R^m` keyed by axis index.  
- `bias` baseline urge.  
- `ComplexityCost`.

Score: `score = dot(wAlign, A) + dot(wEthics, E_dense) + bias + context`.  
Context covers current orders, nearby threats, fleet supply, loyalty debt, etc. The decision system runs per-entity in PureDOTS by batching actions with the same `DecisionProfile`.

Eligible actions enter a softmax with temperature τ (default 0.5) to keep emergent variance.  

**Space4x starter library** (tune relentlessly):

| Action                 | wAlign (L,G,I)        | Axis weights (WAR, MAT, AUTH, XENO, EXP) | Notes |
|------------------------|-----------------------|-------------------------------------------|-------|
| InterceptPirates       | (+0.4, +0.1, 0.0)     | (+0.6, 0.0, +0.2, 0.0, +0.3)              | Lawful/Warlike captains relish this |
| PillageColony          | (-0.5, -0.4, -0.3)    | (+0.7, +0.2, +0.1, +0.4, +0.1)            | Chaotic, evil, xenophobic crews trend up |
| EstablishOutpost       | (+0.2, +0.2, +0.1)    | (+0.4, 0.0, 0.0, -0.1, +0.8)              | Expansionist, altruistic crews |
| AidRefugees            | (+0.3, +0.7, +0.4)    | (-0.4, -0.1, -0.3, -0.2, -0.2)            | Peaceful, egalitarian fleets respond |
| BoardAndKidnap         | (-0.3, -0.5, -0.4)    | (+0.5, 0.0, +0.3, +0.6, 0.0)              | High risk appetite requirement |
| DeclareIndependence    | (-0.2, +0.1, -0.2)    | (-0.2, 0.0, -0.6, -0.1, +0.5)             | Chaotic egalitarians push uprisings |
| EnforceOrder           | (+0.5, -0.1, +0.1)    | (+0.2, 0.0, +0.8, +0.1, 0.0)              | Strict captains keep the peace |

4) Formation tightness & captain overrides

`FleetCohesion : IComponentData { half value; }` computed in `FleetCohesionSystem` using:

```
cohesion =
  clamp01( baseDiscipline
         + a * averageLawfulness
         + b * captainAuthority
         + c * trainingLevel
         + d * positiveAUTH_mean
         - e * (stress + attrition) );
```

Use coefficients (0.30, 0.35, 0.22, 0.20, 0.15, 0.25) as seeds. Cohesion modifies navigation solvers and weapon arcs.

Personal deviation chance:
```
chaos = (-L + 1) * 0.5;
pBreak = sigmoid(k0 + k1*chaos - k2*cohesion - k3*morale
                 + k4*fatigue + k5*confusion - 0.4*WAR_pos);
```

If `cohesion ≥ θ` (≈0.65) and crew lawfulness > φ (≈-0.2), impose `BehaviorLock` to keep them in line. Fanatic chaotic crews (L ≤ -0.75) ignore the lock and trigger mutiny events if suppressed too long.

5) Aggregation: crews → fleets → factions

Weighted aggregation uses influence scores:
- Rank factor: crew=1, officer=3, captain=6.  
- Experience factor: `1 + veteranLevel * 0.25`.  
- Fanatic factor on matching axis: 1.0 regular, 1.5 fanatic.

Group alignment = weighted mean of individuals.  
Group ethic value per axis = weighted mean with axis-specific fanatics.  
Low cohesion blends results toward simple average to reflect fractured command.  

When a fleet becomes a mobile colony, add civilian strata with their own `AlignmentTriplet` buffer. Colony governors (officers) veto transitions if their ethics oppose it (e.g., authoritarian warlords reject peaceful migration).

Aggregating cultures & outlooks:
- Races and cultures never average—retain the set of represented ids on the aggregate (`DynamicBuffer<RacePresence>`, `DynamicBuffer<CulturePresence>` with counts).  
- When building crew-level outlooks, sort individual outlook weights by absolute value, accumulate the top three distinct `OutlookId`s, and store them on the crew as `TopOutlookBuffer`. Downstream UX shows these as the crew's dominant philosophies.  
- For performance, maintain running sums (alignment, outlook numerators, influence weights) in a `CrewAggregationComponent`; recompute when membership changes.

6) Affiliations & conflict detection

Entities may belong to multiple organizations simultaneously (`DynamicBuffer<AffiliationTag>` with `(AffiliationType, EntityRef, LoyaltyStrength)`). Valid combinations exclude inherently opposed affiliations unless the entity carries a `SpyRole` tag.

Conflict evaluation pipeline:
- Each organization exposes a `DoctrineProfile` describing expected alignment ranges and mandatory outlooks.  
- On membership update, run `AffiliationComplianceSystem` to compare the entity's alignment/outlook to doctrine tolerances.  
- If deviation magnitude exceeds thresholds and entity lawfulness < 0, queue `MutinyIntent`. Lawful members with contracts (`ContractBinding` tag) delay rebellion until `ContractTerm` expires or cohesion collapses.  
- Crews evaluate aggregate deviation against their commanding officer's doctrine; if misalignment persists for N ticks, spawn `RebellionEvent`.

Desertion & mutiny heuristics:
- Desertion chance scales with `chaos` (low L) and negative `Integrity`.  
- Lawful crews (`L > 0.25`) honor contracts; they only desert when morale < 0.2 and cohesion < 0.3.  
- Chaotic warlike bands form autonomous raid groups when WAR axis ≥ +1.5 and cohesion with parent fleet < 0.4.  
- Peaceful egalitarians (`WAR ≤ -1`, `AUTH ≤ -1`) trigger independence movements instead of violent mutiny, creating breakaway colonies.

Spy/double-agent handling: mark entities with `SpyRole` plus an `InfiltrationTarget`. Compliance checks ignore conflicts on spy affiliations but track suspicion via a `SuspicionScore` computed from observed actions vs doctrine expectations. Once `SuspicionScore` crosses a captain-defined limit, initiate counterintelligence events.

7) Fanatic vs regular effects

- Magnitude matters directly in weighting and initiative modifiers.  
- Prevent simultaneous opposing poles on an axis (e.g., +2 WAR and -2 WAR). Different axes may conflict; that tension drives emergent drama.  
- Captains with `AUTH ≥ +1` and `L > 0.5` set a minimum cohesion floor of 0.5 during combat phases.  
- Peaceful fanatics apply a `NonLethalPreference` tag during target selection, redirecting damage scripts.

8) Pseudocode snippets (drop-in)

```
half clamp01(half x) => math.saturate(x);

half ComputeInitiative(half baseI, half law, half fanaticMods, half morale, half fatigue)
{
    half chaosBias = alpha * (-law);
    return math.saturate(baseI + chaosBias + fanaticMods + morale - fatigue);
}

half RiskAppetite(half law, half integrity, half pressure)
{
    return sigmoid(r0 + r1 * (-law) + r2 * (-integrity) + pressure);
}
```

Keep math inside Burst-friendly static methods. Queue actions as `CommandBuffer` events so capital ships, officers, and crews react deterministically each simulation tick.

9) Quick scenario

- Crew specialist: L=-0.7, G=-0.2, I=-0.3, ethics { WAR:+1, AUTH:-1 }. Initiative spikes, risk appetite high, likely to push for boarding unless captain enforces order.  
- Captain: L=+0.8, G=+0.2, I=+0.1, ethics { AUTH:+2, WAR:+1 }. Raises cohesion, suppresses chaotic breaks, favors `InterceptPirates` and `EnforceOrder`.  
- Result: Fleet stays tight; when chaos overrun happens (stress>0.6) the crew may trigger `DeclareIndependence`, creating narrative beats.

10) Tuning levers

- τ (softmax temperature) controls decisiveness.  
- α (chaos→initiative), κ (risk amplification), k-vector for formation breaks.  
- Cohesion coefficients determine how much officers matter vs training.  
- Action cost ladder shapes pacing: low cost → skirmish; high cost → invasions requiring high initiative + captain approval.

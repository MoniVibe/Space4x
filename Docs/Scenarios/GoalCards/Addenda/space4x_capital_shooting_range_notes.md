# Addendum: Capital Ship Firing Range - Nuance Notes
Date: 2026-02-09
Owner: shonh

## Doctrine and Experience Expression (Guidance)
- Warlike elites: aggressive cadence, low patience, prioritize target kill confirmation, accept heat spikes.
- Warlike rookies: overfire, lock churn under crossing targets, skip cooldown discipline.
- Spiritual elites: bias psionic/mana instruments (focus fire, aim stabilizers) and avoid wasteful volleys.
- Materialist elites: deploy drones early; treat drones as force multipliers; adjust drone roles by target profile.
- Corrupt materialists: prefer suicide drones or risky intercepts; accept higher losses for faster kills.
- Pure materialists: prefer repair/support drones; keep drone losses minimal; repair between waves.

## Cross-Domain Equivalence
- Master mage maps to apex operator/ship; firing range maps to skill-expression testbed (archers, gunners, pilots).
- PureDOTS owns the reusable mechanics (timing/accuracy/deflection); game-specific skins should not fork the model.

## Relations, Morale, Cohesion
- Friendly fire penalties reduce morale; cohesion drops on repeated incidents.
- Competitive entities lose morale when outperformed in time-to-kill or accuracy.
- Cohesion increases with clean, coordinated volleys; fleet cohesion can rise for grouped capital ships.
- Chaotic fanatic warlikes may escalate to internal conflict in extreme variants (non-base run).

## Experience Delta Expectations
- Reaction time: elite < veteran < experienced < rookie.
- Lock stability: elites maintain lock under high angular velocity; rookies churn locks.
- Target selection: elites minimize overkill; rookies retarget frequently.
- Heat discipline: elites manage heat budgets; rookies overhear.

## Schedule and Regimen Nuances
- Lawful/materialist crews start on time and respect cooldown windows; chaotic crews drift between blocks under stress.
- Warlike crews avoid breaking live_fire unless hard needs; may eat on shift if allowed.
- Needs overrides (hunger/fatigue/injury) can preempt live_fire; log interruptions inside the scoring window.

## Drone Nuance
- Repair drones should activate during cooldown windows for pure crews.
- Suicide drones should only trigger in high-threat target passes.
- Tactical seat decides drone posture based on profile tags.
 - Stealthed drones: intermittent lock drop; sensors must reacquire.
 - Armor vs shield drone sets: different effective time-to-kill and lock persistence.

## Edge Cases
- If drones or psionic instruments are stubbed, ensure telemetry still records intended selections.
- If target motion is too fast, adjust target profiles before interpreting crew skill deltas.
 - If friendly fire escalation is enabled, separate it into a variant so base metrics remain stable.

## Nuance Budget / Perf Notes (Optional)
- Budget target: TBD (use 100v100 stress test as coarse ceiling until costs are measured).
- Expensive features: drone micro, doctrine-specific targeting, psionic/mana instruments.
- Fallbacks: reduce drone behaviors to coarse roles; collapse doctrine differences to targeting bias only.
- Stress-test references: space4x_stress_capital_100_vs_100 (desktop run).

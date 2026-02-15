# Pitch Copy (v0)

In this demo, you command fleet-level combat while a deterministic simulation drives every ship state, engagement, and loss in real time. You start with a clean 20v20 fight to read intent and volley timing, then escalate to 100v100 to show scaling behavior, attrition pressure, and outcome clarity under live sim-truth rendering.

## Feature Bullets (v0-honest)
- Deterministic fixed-tick simulation backbone.
- Headless scenario runner with artifact outputs.
- Capital-vs-capital battle slices at multiple scales.
- Side-aware fleet movement and engagement setup.
- Continuous combat telemetry emission.
- Operator-facing answers and report artifacts.
- Sim-truth rendering path (no handcrafted fake battle playback).
- Fast scenario iteration via JSON-driven setups.
- Repeatable seeds for comparable reruns.
- Practical debug workflow around tick/time + outcome metrics.

## Non-Negotiables (Selling Points)
- **Determinism replay:** Same seed and build produce stable, comparable outcomes.
- **Headless proof:** Runs produce machine-checkable artifacts (`headless_answers.json`, `operator_report.json`).
- **Sim-truth rendering:** What is shown is derived from live simulation state, not authored cinematic stand-ins.

## What Is Not in v0
- This is not full campaign content or narrative progression.
- Presentation polish is intentionally secondary to simulation proof and repeatability.
- Balance is functional for demonstration, not final competitive tuning.

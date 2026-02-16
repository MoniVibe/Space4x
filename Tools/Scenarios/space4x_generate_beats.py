#!/usr/bin/env python3
"""Generate a simple Space4X beats JSON from a scenario file."""

from __future__ import annotations

import argparse
import json
from pathlib import Path
from typing import Any, Dict, Iterable, List, Optional, Tuple


def coerce_float(value: Any) -> Optional[float]:
    if isinstance(value, bool):
        return None
    if isinstance(value, (int, float)):
        return float(value)
    if isinstance(value, str):
        text = value.strip()
        if not text:
            return None
        try:
            return float(text)
        except ValueError:
            return None
    return None


def iter_nodes(root: Any) -> Iterable[Any]:
    stack = [root]
    while stack:
        node = stack.pop()
        yield node
        if isinstance(node, dict):
            for value in node.values():
                stack.append(value)
        elif isinstance(node, list):
            for value in node:
                stack.append(value)


def collect_action_records(root: Any) -> List[Dict[str, Any]]:
    actions: List[Dict[str, Any]] = []
    for node in iter_nodes(root):
        if not isinstance(node, dict):
            continue
        if "kind" in node and "time_s" in node:
            actions.append(node)
    return actions


def collect_key_values(root: Any, key_name: str) -> List[Any]:
    values: List[Any] = []
    for node in iter_nodes(root):
        if isinstance(node, dict) and key_name in node:
            values.append(node[key_name])
    return values


def normalize_time(value: float) -> float | int:
    rounded = round(value, 3)
    if abs(rounded - round(rounded)) < 1e-9:
        return int(round(rounded))
    return rounded


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Generate presentation beats from a Space4X scenario.")
    parser.add_argument("--scenario", required=True, help="Path to scenario JSON.")
    parser.add_argument("--out", required=True, help="Path to beats JSON output.")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    scenario_path = Path(args.scenario)
    out_path = Path(args.out)

    data = json.loads(scenario_path.read_text(encoding="utf-8"))
    scenario_id = data.get("scenarioId")
    if not isinstance(scenario_id, str) or not scenario_id.strip():
        scenario_id = scenario_path.stem

    beats: List[Tuple[float, str]] = [(0.0, "Spawn")]

    move_count = 0
    intercept_count = 0
    action_times: List[float] = []
    for action in collect_action_records(data):
        kind = str(action.get("kind", "")).strip()
        time_value = coerce_float(action.get("time_s"))
        if time_value is None:
            continue

        if kind == "MoveFleet":
            move_count += 1
            beats.append((time_value, f"Fleet move {move_count}"))
            action_times.append(time_value)
        elif kind == "TriggerIntercept":
            intercept_count += 1
            beats.append((time_value, f"Intercept wave {intercept_count}"))
            action_times.append(time_value)

    escort_release_times: List[float] = []
    for raw in collect_key_values(data, "escortRelease_s"):
        value = coerce_float(raw)
        if value is not None:
            escort_release_times.append(value)

    seen_escort = set()
    for time_value in sorted(escort_release_times):
        marker = round(time_value, 3)
        if marker in seen_escort:
            continue
        seen_escort.add(marker)
        beats.append((time_value, "Escort release"))
        action_times.append(time_value)

    duration = coerce_float(data.get("duration_s"))
    if duration is None:
        duration = max(action_times) if action_times else 120.0
    if duration < 0:
        duration = 0.0

    mid_time = duration * 0.5
    beats.append((mid_time, "Mid battle"))
    beats.append((duration, "Wrap"))

    unique: Dict[Tuple[float, str], None] = {}
    for time_value, label in beats:
        normalized = round(time_value, 3)
        unique[(normalized, label)] = None

    ordered = sorted(unique.keys(), key=lambda item: (item[0], item[1]))
    payload = {
        "scenarioId": scenario_id,
        "beats": [{"time_s": normalize_time(time_value), "label": label} for time_value, label in ordered],
    }

    out_path.parent.mkdir(parents=True, exist_ok=True)
    out_path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

#!/usr/bin/env python3
"""Summarize Space4X telemetry/metrics artifacts into JSON + Markdown."""

from __future__ import annotations

import argparse
import csv
import json
import math
import re
from pathlib import Path
from typing import Any, Dict, Iterable, List, Optional, Tuple


NON_ALNUM_RE = re.compile(r"[^a-z0-9]+")


def norm_tokens(*parts: Any) -> set[str]:
    tokens: set[str] = set()
    for part in parts:
        if part is None:
            continue
        text = str(part).lower()
        for token in NON_ALNUM_RE.split(text):
            if token:
                tokens.add(token)
    return tokens


def coerce_float(value: Any) -> Optional[float]:
    if isinstance(value, bool):
        return None
    if isinstance(value, (int, float)):
        number = float(value)
        if math.isfinite(number):
            return number
        return None
    if isinstance(value, str):
        text = value.strip()
        if not text:
            return None
        try:
            number = float(text)
            if math.isfinite(number):
                return number
        except ValueError:
            return None
    return None


def coerce_int(value: Any) -> Optional[int]:
    number = coerce_float(value)
    if number is None:
        return None
    return int(round(number))


def iter_scalars(root: Any) -> Iterable[Tuple[str, str, Any]]:
    stack: List[Tuple[str, Any]] = [("", root)]
    while stack:
        path, node = stack.pop()
        if isinstance(node, dict):
            for key, value in node.items():
                child = f"{path}.{key}" if path else str(key)
                stack.append((child, value))
            continue
        if isinstance(node, list):
            for index, value in enumerate(node):
                child = f"{path}[{index}]"
                stack.append((child, value))
            continue

        leaf = path.rsplit(".", 1)[-1]
        leaf = leaf.split("[", 1)[0]
        yield path, leaf, node


def extract_event_hint(record: Any) -> Optional[str]:
    if not isinstance(record, dict):
        return None

    for key in (
        "event",
        "eventType",
        "event_type",
        "type",
        "kind",
        "name",
        "metric",
        "metric_name",
    ):
        value = record.get(key)
        if isinstance(value, str) and value.strip():
            return value.strip()
    return None


def has_prefix(tokens: set[str], prefixes: Iterable[str]) -> bool:
    for token in tokens:
        for prefix in prefixes:
            if token.startswith(prefix):
                return True
    return False


class SummaryBuilder:
    def __init__(self) -> None:
        self.scenario_id: Optional[str] = None

        self.tick_candidates: List[int] = []
        self.duration_candidates: List[float] = []
        self.exit_code: Optional[int] = None
        self.exit_reason: Optional[str] = None

        self.carriers_spawned_events = 0
        self.spawns_total_events = 0
        self.intercept_events = 0
        self.kill_events = 0
        self.damage_events = 0
        self.carrier_pickup_events = 0

        self.carriers_spawned_metric: Optional[int] = None
        self.spawns_total_metric: Optional[int] = None
        self.intercept_metric: Optional[int] = None
        self.kills_metric: Optional[int] = None
        self.damage_metric: Optional[int] = None
        self.carrier_pickups_metric: Optional[int] = None

        self.mining_yield_sum = 0.0
        self.mining_yield_total_metric: Optional[float] = None

    def _set_metric_max(self, attr: str, value: Optional[int]) -> None:
        if value is None:
            return
        current = getattr(self, attr)
        if current is None or value > current:
            setattr(self, attr, value)

    def _set_float_metric_max(self, attr: str, value: Optional[float]) -> None:
        if value is None:
            return
        current = getattr(self, attr)
        if current is None or value > current:
            setattr(self, attr, value)

    def _apply_named_metric(self, metric_name: str, metric_value: Any) -> None:
        value_f = coerce_float(metric_value)
        if value_f is None:
            return

        tokens = norm_tokens(metric_name)
        value_i = int(round(value_f))

        if "scenarioid" in tokens or ("scenario" in tokens and "id" in tokens):
            if isinstance(metric_value, str) and metric_value.strip():
                self.scenario_id = metric_value.strip()
            return

        if ("spawn" in tokens or has_prefix(tokens, ("spawn",))) and ("carrier" in tokens):
            self._set_metric_max("carriers_spawned_metric", value_i)
        if ("spawn" in tokens or has_prefix(tokens, ("spawn",))) and (
            "total" in tokens or "count" in tokens
        ):
            self._set_metric_max("spawns_total_metric", value_i)
        if "intercept" in tokens and ("attempt" in tokens or "count" in tokens or "total" in tokens):
            self._set_metric_max("intercept_metric", value_i)
        if (has_prefix(tokens, ("kill", "destroy"))) and (
            "count" in tokens or "total" in tokens or "kills" in tokens
        ):
            self._set_metric_max("kills_metric", value_i)
        if "damage" in tokens and ("event" in tokens or "count" in tokens or "total" in tokens):
            self._set_metric_max("damage_metric", value_i)
        if "carrier" in tokens and has_prefix(tokens, ("pickup", "collect", "haul")):
            self._set_metric_max("carrier_pickups_metric", value_i)
        if "mining" in tokens and ("yield" in tokens or "ore" in tokens or "mined" in tokens):
            if "total" in tokens:
                self._set_float_metric_max("mining_yield_total_metric", value_f)
            else:
                self.mining_yield_sum += value_f
        if "tick" in tokens or "ticks" in tokens:
            if "rate" not in tokens and "duration" not in tokens:
                self.tick_candidates.append(value_i)
        if (
            "duration" in tokens
            and ("s" in tokens or "sec" in tokens or "seconds" in tokens or "elapsed" in tokens)
        ) or ("elapsed" in tokens and ("s" in tokens or "seconds" in tokens)):
            self.duration_candidates.append(value_f)
        if "exit" in tokens and "code" in tokens:
            self.exit_code = value_i

    def consume_record(self, record: Any) -> None:
        event_hint = extract_event_hint(record)
        record_tokens = norm_tokens(event_hint or "")

        metric_name: Optional[str] = None
        metric_value: Any = None
        if isinstance(record, dict):
            for key in ("metric", "metric_name", "name", "id", "key"):
                value = record.get(key)
                if isinstance(value, str) and value.strip():
                    metric_name = value.strip()
                    break
            for key in ("value", "metricValue", "metric_value", "count", "total"):
                if key in record:
                    metric_value = record.get(key)
                    break
        if metric_name is not None and metric_value is not None:
            self._apply_named_metric(metric_name, metric_value)

        for path, key, value in iter_scalars(record):
            key_tokens = norm_tokens(path, key)
            record_tokens.update(key_tokens)

            if isinstance(value, str):
                lower = value.strip().lower()
                if not lower:
                    continue

                if self.scenario_id is None and (
                    key_tokens.intersection({"scenarioid", "scenario", "scenario_id"}) or (
                        "scenario" in key_tokens and "id" in key_tokens
                    )
                ):
                    self.scenario_id = value.strip()

                if "exit" in key_tokens and "reason" in key_tokens:
                    self.exit_reason = value.strip()
                continue

            value_float = coerce_float(value)
            if value_float is None:
                continue
            value_int = int(round(value_float))

            if ("tick" in key_tokens or "ticks" in key_tokens) and "rate" not in key_tokens:
                if "duration" not in key_tokens:
                    self.tick_candidates.append(value_int)

            if ("duration" in key_tokens and ("s" in key_tokens or "sec" in key_tokens or "seconds" in key_tokens)) or (
                "elapsed" in key_tokens and ("s" in key_tokens or "seconds" in key_tokens)
            ):
                self.duration_candidates.append(value_float)

            if "exit" in key_tokens and "code" in key_tokens:
                self.exit_code = value_int

            if ("spawn" in key_tokens or has_prefix(key_tokens, ("spawn",))) and "carrier" in key_tokens and (
                "count" in key_tokens or "total" in key_tokens or "num" in key_tokens
            ):
                self._set_metric_max("carriers_spawned_metric", value_int)
            if ("spawn" in key_tokens or has_prefix(key_tokens, ("spawn",))) and (
                "count" in key_tokens or "total" in key_tokens or "num" in key_tokens
            ):
                self._set_metric_max("spawns_total_metric", value_int)
            if "intercept" in key_tokens and (
                "attempt" in key_tokens or "count" in key_tokens or "total" in key_tokens
            ):
                self._set_metric_max("intercept_metric", value_int)
            if has_prefix(key_tokens, ("kill", "destroy")) and (
                "count" in key_tokens or "total" in key_tokens
            ):
                self._set_metric_max("kills_metric", value_int)
            if "damage" in key_tokens and (
                "event" in key_tokens or "count" in key_tokens or "total" in key_tokens
            ):
                self._set_metric_max("damage_metric", value_int)
            if "carrier" in key_tokens and has_prefix(key_tokens, ("pickup", "collect", "haul")):
                self._set_metric_max("carrier_pickups_metric", value_int)

            if "mining" in key_tokens and (
                "yield" in key_tokens or "mined" in key_tokens or "ore" in key_tokens or "resource" in key_tokens
            ):
                if "total" in key_tokens:
                    self._set_float_metric_max("mining_yield_total_metric", value_float)
                else:
                    self.mining_yield_sum += value_float

        if has_prefix(record_tokens, ("spawn",)) and not has_prefix(record_tokens, ("despawn",)):
            self.spawns_total_events += 1
            if "carrier" in record_tokens:
                self.carriers_spawned_events += 1

        if "intercept" in record_tokens and (
            has_prefix(record_tokens, ("attempt", "trigger", "engage", "launch", "wave"))
            or event_hint is not None
        ):
            self.intercept_events += 1

        if has_prefix(record_tokens, ("kill", "destroy")):
            self.kill_events += 1

        if "damage" in record_tokens:
            self.damage_events += 1

        if "carrier" in record_tokens and has_prefix(record_tokens, ("pickup", "collect", "haul")):
            self.carrier_pickup_events += 1

    def _choose_count(self, metric_value: Optional[int], event_value: int) -> Optional[int]:
        if metric_value is not None:
            return max(metric_value, event_value)
        if event_value > 0:
            return event_value
        return None

    def build_summary(self, fallback_scenario: Optional[str]) -> Dict[str, Any]:
        scenario_id = self.scenario_id or fallback_scenario
        ticks = max(self.tick_candidates) if self.tick_candidates else None
        duration_s = max(self.duration_candidates) if self.duration_candidates else None

        mining_total = self.mining_yield_total_metric
        if self.mining_yield_sum > 0:
            mining_total = max(mining_total or 0.0, self.mining_yield_sum)

        return {
            "scenarioId": scenario_id,
            "runtime": {
                "ticks": ticks,
                "duration_s": duration_s,
                "exit_code": self.exit_code,
                "exit_reason": self.exit_reason,
            },
            "counts": {
                "carriers_spawned": self._choose_count(
                    self.carriers_spawned_metric, self.carriers_spawned_events
                ),
                "spawns_total": self._choose_count(self.spawns_total_metric, self.spawns_total_events),
            },
            "combat": {
                "intercept_attempts": self._choose_count(self.intercept_metric, self.intercept_events),
                "kills": self._choose_count(self.kills_metric, self.kill_events),
                "damage_events": self._choose_count(self.damage_metric, self.damage_events),
            },
            "economy": {
                "mining_yield_total": mining_total,
                "carrier_pickups": self._choose_count(
                    self.carrier_pickups_metric, self.carrier_pickup_events
                ),
            },
        }


def parse_ndjson(path: Path, builder: SummaryBuilder) -> None:
    with path.open("r", encoding="utf-8") as handle:
        for line in handle:
            text = line.strip()
            if not text:
                continue
            try:
                record = json.loads(text)
            except json.JSONDecodeError:
                continue
            builder.consume_record(record)


def parse_metrics_json(path: Path, builder: SummaryBuilder) -> None:
    try:
        payload = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError):
        return

    if isinstance(payload, list):
        for row in payload:
            builder.consume_record(row)
        return

    if isinstance(payload, dict):
        builder.consume_record(payload)


def parse_metrics_csv(path: Path, builder: SummaryBuilder) -> None:
    try:
        with path.open("r", encoding="utf-8", newline="") as handle:
            reader = csv.DictReader(handle)
            for row in reader:
                builder.consume_record(row)
    except OSError:
        return


def value_text(value: Any) -> str:
    if value is None:
        return "n/a"
    if isinstance(value, float):
        return f"{value:.3f}".rstrip("0").rstrip(".")
    return str(value)


def build_markdown(summary: Dict[str, Any]) -> str:
    runtime = summary["runtime"]
    counts = summary["counts"]
    combat = summary["combat"]
    economy = summary["economy"]

    lines = [
        "# Space4X Run Summary",
        "",
        f"- Scenario: {value_text(summary.get('scenarioId'))}",
        "",
        "## Runtime",
        f"- Ticks: {value_text(runtime.get('ticks'))}",
        f"- Duration (s): {value_text(runtime.get('duration_s'))}",
        f"- Exit code: {value_text(runtime.get('exit_code'))}",
        f"- Exit reason: {value_text(runtime.get('exit_reason'))}",
        "",
        "## Counts",
        f"- Carriers spawned: {value_text(counts.get('carriers_spawned'))}",
        f"- Spawns total: {value_text(counts.get('spawns_total'))}",
        "",
        "## Combat",
        f"- Intercept attempts: {value_text(combat.get('intercept_attempts'))}",
        f"- Kills: {value_text(combat.get('kills'))}",
        f"- Damage events: {value_text(combat.get('damage_events'))}",
        "",
        "## Economy",
        f"- Mining yield total: {value_text(economy.get('mining_yield_total'))}",
        f"- Carrier pickups: {value_text(economy.get('carrier_pickups'))}",
        "",
    ]
    return "\n".join(lines)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Summarize a Space4X telemetry run.")
    parser.add_argument("--telemetry", required=True, help="Path to telemetry NDJSON.")
    parser.add_argument("--metrics_json", help="Optional path to metrics JSON.")
    parser.add_argument("--metrics_csv", help="Optional path to metrics CSV.")
    parser.add_argument("--out_dir", required=True, help="Directory for summary outputs.")
    return parser.parse_args()


def detect_fallback_scenario(telemetry_path: Path) -> Optional[str]:
    stem = telemetry_path.stem
    if not stem:
        return None
    if stem.lower() in {"telemetry", "metrics", "run"}:
        return None
    return stem


def main() -> int:
    args = parse_args()
    telemetry_path = Path(args.telemetry)
    out_dir = Path(args.out_dir)

    builder = SummaryBuilder()
    parse_ndjson(telemetry_path, builder)

    if args.metrics_json:
        parse_metrics_json(Path(args.metrics_json), builder)
    if args.metrics_csv:
        parse_metrics_csv(Path(args.metrics_csv), builder)

    summary = builder.build_summary(detect_fallback_scenario(telemetry_path))

    out_dir.mkdir(parents=True, exist_ok=True)
    summary_json = out_dir / "summary.json"
    summary_md = out_dir / "summary.md"
    summary_json.write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8")
    summary_md.write_text(build_markdown(summary), encoding="utf-8")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

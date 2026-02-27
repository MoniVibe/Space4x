# Space4X Signature Observability Model (MVP)

Last updated: 2026-02-26

## Why this model

We want detection to react to runtime behavior, not only static authoring values.  
This MVP maps ship state into multi-spectrum signatures consumed by `PerceptionUpdateSystem`:

- `EMSignature`: passive power + active emissions (sensors/weapons/reactor load).
- `ExoticSignature`: thermal/heat proxy.
- `GraviticSignature`: mass + movement/wake proxy.
- `ParanormalSignature`: reserved psi lane (kept near baseline unless enabled by design).

## Runtime implementation

- `Space4XSignatureTelemetryBootstrapSystem`
  - Ensures one `Space4XSignatureTelemetryConfig` singleton.
  - Captures baseline `SensorSignature` per entity into `Space4XSignatureTelemetry`.
- `Space4XSignatureTelemetryUpdateSystem`
  - Runs in `PerceptionSystemGroup`, `UpdateBefore(PerceptionUpdateSystem)`.
  - Updates on cadence (`UpdateCadenceTicks`) instead of every tick.
  - Pulls inputs from `PowerLedger`, `ShipPowerConsumer` + `PowerConsumer`, `FleetcrawlHeatOutputState`, `VesselPhysicalProperties`, velocity components.
  - Writes derived signatures back to `SensorSignature` for channel detection.

## Best-practice notes used

- Keep channels behavior-driven (active vs passive emissions), not only hull class constants.
- Use cadence/throttling for expensive observability math.
- Avoid structural changes in hot update loops.
- Keep detection broad-phase/queries data-oriented to avoid accidental N^2 scans.

## References

- Radar equation fundamentals (NPS): https://man.fas.org/dod-101/navy/docs/fun/part06.htm
- Active vs passive sensing baseline (NASA): https://earthdata.nasa.gov/learn/backgrounders/remote-sensing
- Practical game radar/sensor model (Nebulous): https://nebfltcom.fandom.com/wiki/Sensors
- Passive EM/thermal detection framing (Aurora C#): https://aurora2.pentarch.org/index.php?topic=8495.0
- Space combat sensor abstraction tradeoffs (Children of a Dead Earth devlog): https://childrenofadeadearth.wordpress.com/2016/04/25/radar-systems/
- Unity ECS `ComponentLookup<T>` update guidance: https://docs.unity3d.com/Packages/com.unity.entities@1.0/manual/components-lookups.html

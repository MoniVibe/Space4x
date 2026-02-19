# Space4X Camera/Control Debug Checklist

Use this checklist when camera follow, mode transitions, or player steering feel wrong.

Headless first-pass (before manual visual debugging):
- `pwsh -NoProfile -File .\scripts\presentation_mode1_headless.ps1`
- If this returns `fail`, fix contract issues before feel/polish review.

## 1) Repro Baseline

- Enter play mode in the target gameplay scene.
- Start run from main menu flow.
- Repro sequence:
  - `1` (CursorOrient) for 5s movement.
  - `2` (CruiseLook) for 5s movement.
  - `3` (RTS) then back to `1`, repeat 3 times.

## 2) Runtime HUD Signals

- Toggle render preflight HUD with `F3` (`Space4XRenderPreflightHud`).
- Toggle camera follow HUD with `F6` (`Space4XCameraFollowDebugHud`).
- Toggle camera probe logging with `F7` (`Space4XCameraFollowProbe`) when collecting machine-readable camera evidence.
- Required healthy signals:
  - `Controlled` entity is not `Entity.Null`.
  - `Target` entity is not `Entity.Null`.
  - `Aligned=yes` while movement input is active.

If `Aligned=no`, camera handoff is broken even if simulation still runs.

## 3) Input Validation

- Open Input Debugger and verify keyboard/mouse events while switching modes.
- Confirm mode hotkeys (`1/2/3`) are firing and not blocked by action-map swaps.

## 4) ECS State Validation

- Open Entities Hierarchy:
  - Confirm exactly one entity carries `PlayerFlagshipTag`.
- Open Entities Systems:
  - Check that follow/controller systems are updating as expected during mode switches.

## 5) Presentation Loop Validation

- In Profiler Timeline, inspect frame where follow fails:
  - Confirm follow MonoBehaviour `LateUpdate` still runs.
  - Confirm no unintended camera controller remains enabled in the same frame.
- Use Frame Debugger only for render visibility issues (not movement ownership bugs).

## 6) Common Root Causes in This Project

- Stale or duplicated `PlayerFlagshipTag`.
- Camera target and controlled flagship diverged.
- RTS rig/applier stayed enabled after leaving mode `3`.
- Using stale transform source (`LocalToWorld`) for authoritative follow pose instead of `LocalTransform`.

## References

- Input Debugger: https://docs.unity.cn/Packages/com.unity.inputsystem@1.13/manual/Debugging.html
- Entities Hierarchy: https://docs.unity.cn/Packages/com.unity.entities@1.0/manual/editor-hierarchy-window.html
- Entities Systems: https://docs.unity.cn/Packages/com.unity.entities@1.0/manual/editor-systems-window.html
- Script execution order: https://docs.unity3d.com/Manual/managing-update-order.html
- Profiler CPU Timeline: https://docs.unity3d.com/Manual/ProfilerCPU.html
- Frame Debugger: https://docs.unity3d.com/Manual/FrameDebugger-debug.html

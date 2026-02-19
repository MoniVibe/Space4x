# Space4X Headless Workflow

## Build
- Menu: `Space4X/Build/Headless/Linux Server`
- CLI:  
  ```
  /Applications/Unity/Hub/Editor/6000.0.64f1/Unity \
    -batchmode -quit \
    -projectPath <repo>/Space4x \
    -executeMethod Space4X.Headless.Editor.Space4XHeadlessBuilder.BuildLinuxHeadless \
    -logFile headless_build.log
  ```
- Output: `Builds/Space4X_headless/Linux`
- Logs: `Space4X_HeadlessBuildReport.log`, `Space4X_HeadlessBuildFailure.log`, `Space4X_HeadlessEditor.log`

## Run
- Scenarios copied to `Space4X_Headless_Data/Scenarios/space4x`
- Example:
  ```
  PUREDOTS_TELEMETRY_LEVEL=summary \
  PUREDOTS_TELEMETRY_MAX_BYTES=524288000 \
  Builds/Space4X_headless/Linux/Space4X_Headless.x86_64 \
    --scenario Builds/Space4X_headless/Linux/Space4X_Headless_Data/Scenarios/space4x/space4x_demo_mining.json \
    --report reports/space4x_demo_mining.json
  ```
- Exit codes: `0` success, nonzero failure
- Telemetry: set `PUREDOTS_TELEMETRY_LEVEL=full` (or increase `PUREDOTS_TELEMETRY_MAX_BYTES`) only when debugging

## Nightly Ops Bus + Rebuild Handshake
- `TRI_STATE_DIR` (recommended): `/home/oni/Tri/.tri/state` (WSL) and `\\wsl$\Ubuntu\home\oni\Tri\.tri\state` (Windows).
- Ops layout: `ops/heartbeats`, `ops/requests`, `ops/claims`, `ops/results`, `ops/locks` (see `https://github.com/MoniVibe/PureDOTS/blob/main/Docs/Headless/OPS_BUS_PROTOCOL.md`).
- Lock rule: WSL runners must not execute players while `ops/locks/build.lock` exists and the lease is valid.
- Rebuild flow: request -> claim -> lock -> rebuild/publish -> result -> unlock.
- Reports/logs: written under `TRI_STATE_DIR/runs/YYYY-MM-DD/`.

## Headless/Test Conventions
- Player args: `-batchmode -nographics -logFile <path>`
- Scenario args: `--scenario <scenario.json> --report <report.json>`
- Test Runner CLI (licensed build lane only): `-runTests -testPlatform editmode|playmode -testResults <xml>`
- Presentation Mode 1 contract check (recommended): `pwsh -NoProfile -File .\scripts\presentation_mode1_headless.ps1` (run on isolated lane; no open Unity editor instances)
- Exit codes: `0` success, nonzero failure
- Tests compile only when `UNITY_INCLUDE_TESTS` is enabled.
- Resource/config assets required for headless builds must be validated in the PS build step.

## Notes
- `Assets/Space4x/Config/PureDotsResourceTypes.asset` must contain required resource IDs (updated via `Space4XConfigBootstrapper.EnsureAssets`)
- Tests are wrapped in `UNITY_INCLUDE_TESTS`; ensure the define is enabled before running Unity Test Runner
- Headless scene (`Assets/Scenes/HeadlessBootstrap.unity`) contains no `SubScene` objects; ECS data is baked directly

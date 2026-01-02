# Godgame Headless Workflow

## Build
- Menu: `Godgame/Build/Headless/Linux Server`
- CLI:
  ```
  /Applications/Unity/Hub/Editor/6000.0.64f1/Unity \
    -batchmode -quit \
    -projectPath <repo>/Godgame \
    -executeMethod Godgame.Headless.Editor.GodgameHeadlessBuilder.BuildLinuxHeadless \
    -logFile headless_build.log
  ```
- Output: `Builds/Godgame_headless/Linux`
- Logs: `Godgame_HeadlessBuildReport.log`, `Godgame_HeadlessBuildFailure.log`, `Godgame_HeadlessEditor.log`

## Run
- Scenarios copied to `Godgame_Headless_Data/Scenarios/godgame` (plus PureDOTS samples)
- Example:
  ```
  PUREDOTS_TELEMETRY_LEVEL=summary \
  PUREDOTS_TELEMETRY_MAX_BYTES=524288000 \
  Builds/Godgame_headless/Linux/Godgame_Headless.x86_64 \
    --scenario Builds/Godgame_headless/Linux/Godgame_Headless_Data/Scenarios/godgame/scenario_god_demo_01.json \
    --report reports/godgame_demo.json
  ```
- Exit codes: `0` success, `1` failure
- Telemetry: set `PUREDOTS_TELEMETRY_LEVEL=full` (or increase `PUREDOTS_TELEMETRY_MAX_BYTES`) only when debugging

## Nightly Ops Bus + Rebuild Handshake
- `TRI_STATE_DIR` (recommended): `/home/oni/Tri/.tri/state` (WSL) and `\\wsl$\Ubuntu\home\oni\Tri\.tri\state` (Windows).
- Ops layout: `ops/heartbeats`, `ops/requests`, `ops/claims`, `ops/results`, `ops/locks` (see `../../../puredots/Docs/Headless/OPS_BUS_PROTOCOL.md`).
- Lock rule: WSL runners must not execute players while `ops/locks/build.lock` exists and the lease is valid.
- Rebuild flow: request -> claim -> lock -> rebuild/publish -> result -> unlock.
- Reports/logs: written under `TRI_STATE_DIR/runs/YYYY-MM-DD/`.

## Headless/Test Conventions
- Player args: `-batchmode -nographics -logFile <path>`
- Scenario args: `--scenario <scenario.json> --report <report.json>`
- Test Runner CLI: `-runTests -testPlatform editmode|playmode -testResults <xml>`
- Exit codes: `0` success, nonzero failure
- Tests compile only when `UNITY_INCLUDE_TESTS` is enabled.
- Resource/config assets required for headless builds must be validated in the PS build step.

## Notes
- `Assets/Godgame/Config/PureDotsResourceTypes.asset` is populated via `GodgameConfigBootstrapper` and must contain the resource IDs the AI expects
- Demo/testing systems remain behind `UNITY_INCLUDE_TESTS` so player builds stay clean
- Headless scene excludes presentation SubScenes; ECS data is baked for runtime

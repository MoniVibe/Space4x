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

## Notes
- `Assets/Godgame/Config/PureDotsResourceTypes.asset` is populated via `GodgameConfigBootstrapper` and must contain the resource IDs the AI expects
- Demo/testing systems remain behind `UNITY_INCLUDE_TESTS` so player builds stay clean
- Headless scene excludes presentation SubScenes; ECS data is baked for runtime

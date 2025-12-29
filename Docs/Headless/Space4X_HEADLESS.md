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

## Notes
- `Assets/Space4x/Config/PureDotsResourceTypes.asset` must contain required resource IDs (updated via `Space4XConfigBootstrapper.EnsureAssets`)
- Tests are wrapped in `UNITY_INCLUDE_TESTS`; ensure the define is enabled before running Unity Test Runner
- Headless scene (`Assets/Scenes/HeadlessBootstrap.unity`) contains no `SubScene` objects; ECS data is baked directly

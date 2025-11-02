# Testing & Continuous Integration

## Editor Shortcuts

- Use the **PureDOTS** menu (`PureDOTS/Run PlayMode Tests` or `PureDOTS/Run EditMode Tests`) to trigger Unity Test Runner executions without opening the Test Runner window.

## Command Line / CI

1. Ensure the project path and Unity editor path are known to your pipeline.
2. Invoke the helper script `CI/run_playmode_tests.sh` (make it executable first):

   ```bash
   chmod +x CI/run_playmode_tests.sh
   UNITY_PATH="/path/to/Unity" CI/run_playmode_tests.sh
   ```

   The script defaults to the current repository root as the project path and writes results under `CI/TestResults/`.
3. Adapt the command for editmode tests by replacing `-testPlatform playmode` with `editmode`. A separate script can be copied/modified as needed.

## Recommended Artifacts

- `CI/TestResults/playmode-results.xml` – XML output consumable by most CI dashboards.
- `CI/TestResults/playmode.log` – Editor log captured while running tests.

Headless playmode tests now cover time singleton bootstrapping (`TimeStateTests`) and basic resource/villager loops (`ResourceLoopTests`). Expand this suite whenever new deterministic systems are added.

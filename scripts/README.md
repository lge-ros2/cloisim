# EditMode Test Runner

Use [run-editmode-tests.sh](run-editmode-tests.sh) to execute deterministic Unity EditMode unit tests for this repository.

## What It Does

The runner resolves the project root automatically, locates the Unity editor, executes Unity in batchmode automation, writes the XML result file and editor log, and prints a short summary after the run completes.

## How To Run

Run from the repository root:

```bash
./scripts/run-editmode-tests.sh
```

Run from inside the `scripts` directory:

```bash
./run-editmode-tests.sh
```

## Optional Test Filter

Pass a namespace, fixture, or test name as the first argument to narrow the run:

```bash
./scripts/run-editmode-tests.sh "CLOiSim.Tests.EditMode"
./scripts/run-editmode-tests.sh "CLOiSim.Tests.EditMode.SDF2UnityTests"
./scripts/run-editmode-tests.sh "CLOiSim.Tests.EditMode.CameraDataTests"
./scripts/run-editmode-tests.sh "CLOiSim.Tests.EditMode.CustomNoiseModelTests"
```

## Test File Layout

The EditMode tests are currently split by concern under `Assets/Editor/Tests/`.

- `EditModeUnitTests.cs`: general deterministic utility tests such as `SDF2Unity`, `ColorEncoding`, `Battery`, and `PID`
- `SensorEditModeUnitTests.cs`: sensor-related pure logic tests such as `RandomNumberGenerator`, `CameraData`, `LaserFilter`, `GaussianNoiseModel`, and `CustomNoiseModel`

The runner filter is based on namespace, fixture, or test name, not file path. Splitting sensor tests into a separate file improves maintainability, but you still target the fixture name when you want a narrow run.

## UNITY_EDITOR Override

The script reads the Unity version from `ProjectSettings/ProjectVersion.txt` and first looks for the editor under the default Unity Hub install path. If you want to use a different Unity executable, set `UNITY_EDITOR` explicitly:

```bash
export UNITY_EDITOR="/path/to/Unity"
./scripts/run-editmode-tests.sh
```

If `UNITY_EDITOR` is not set and the default Hub path is unavailable, the script falls back to `unity-editor` or `unity` from `PATH`.

## Output Files

- XML results: `Logs/TestResults/editmode-results.xml`
- Unity editor log: `Logs/EditModeTests.log`

## Summary Line

After the run, the script prints a summary line like this:

```text
Summary: result=Passed, total=18, passed=18, failed=0, skipped=0, inconclusive=0
```

- `result`: overall run result
- `total`: total executed tests
- `passed`: passing tests
- `failed`: failing tests
- `skipped`: skipped tests
- `inconclusive`: inconclusive tests

## Exit Status

- `0`: all tests passed
- `2`: one or more tests failed
- `3`: test run error, including setup or compilation problems

## Caveats

- The runner uses Unity batchmode automation and does not use `-nographics`.
- For headless CI later, run Unity under a virtual display such as Xvfb.
- Unity test execution requires exclusive project access. Close other Unity editor or background import processes before running tests.
- The current scope is pure EditMode unit tests only. PlayMode, GPU sensor, transport, plugin integration, and scene lifecycle coverage are outside this first phase.

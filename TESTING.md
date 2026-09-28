# Testing

Run these commands from the repository root.

## Core tests

```sh
dotnet test MikaUI.Core.Test/MikaUI.Core.Test.csproj --configuration Release
```

Before publishing, run `MikaUI.Core/Build/verify-package.sh`. It runs the Core tests,
rebuilds the DLL, and fails if the committed Unity package contains a different DLL.
Commit the updated DLL whenever Core source changes.

## Unity tests

On macOS, pass the test mode to the command-line runner:

```sh
./scripts/run-unity-tests.sh PlayMode
./scripts/run-unity-tests.sh EditMode
```

The script uses the Editor version in `MikaUI.Unity/ProjectSettings/ProjectVersion.txt`.
Set `UNITY_EDITOR_PATH` to override its location or `UNITY_TEST_TIMEOUT_SECONDS` to
change the default 30-minute limit. Each run writes its NUnit XML report and Unity log
under `artifacts/unity-tests/`, then prints the test counts and paths.

| Exit code | Meaning |
| --- | --- |
| 0 | All tests passed |
| 2 | Invalid configuration or unable to inspect running processes |
| 3 | Unity Licensing Client did not respond |
| 4 | The test run exceeded the time limit |
| 5 | Missing, invalid, or empty test results |
| 6 | One or more tests failed |
| 7 | Tests passed, but Unity Editor exited with an error |
| 8 | A Unity Editor is already running |

The script stops before launching a test if any Unity Editor is open; it never closes
that Editor. It reports a licensing-service timeout without restarting or terminating
the service.

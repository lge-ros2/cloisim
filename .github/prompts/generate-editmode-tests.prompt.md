---
name: Generate Edit Mode Tests
description: Generate CLOiSim Unity EditMode NUnit tests for a selected code slice, symbol, or behavior. Use when: adding focused test coverage for Unity runtime code without drifting into PlayMode or broad integration tests.
argument-hint: Describe the behavior, symbol, or edge cases to cover
agent: agent
model: GPT-5 (copilot)
---
Generate Unity EditMode NUnit tests for the provided code slice or requested behavior.

Inputs:
- Treat the selected code, referenced symbol, or named file as the required primary input.
- Use the user's argument to prioritize behaviors, edge cases, or regressions.
- If the target is too broad to test locally, ask the user to narrow it before generating code.

Repo constraints:
- Match existing EditMode patterns in `Assets/Editor/Tests/EditModeUnitTests.cs`, `Assets/Editor/Tests/PluginEditModeUnitTests.cs`, and `Assets/Editor/Tests/SensorEditModeUnitTests.cs`.
- Prefer the smallest testable slice. Do not invent broad scene or PlayMode coverage when a local EditMode test is possible.
- Clean up `GameObject`, `Material`, `Mesh`, and other Unity objects with `DestroyImmediate` in `finally` blocks when needed.
- For `CLOiSimPlugin` request-handler reflection tests, bind the exact `(in string, in cloisim.msgs.Any, ref DeviceMessage)` signature with `MakeByRefType()`.
- For `GPS` or `Sonar` EditMode tests, avoid unsafe `Reset()` paths by using test-only subclasses that no-op runtime hooks when necessary.
- Assume local validation should use `./scripts/run-editmode-tests.sh` with the narrowest feasible filter.

Output rules:
- Emit compilable C# test code first.
- Follow existing naming and assertion patterns from the repo.
- Include only the minimum fixture setup needed to exercise the behavior.
- After the code, list brief assumptions or missing dependencies only if they block a complete test.

Output shape:
1. Short title for the test intent.
2. C# EditMode test code.
3. `Assumptions:` only if required.
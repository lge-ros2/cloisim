---
name: Review Staged Unity Changes
description: Review staged CLOiSim Unity C# changes for bugs, regressions, lifecycle risks, and missing tests
argument-hint: Optional focus area or suspected risk to inspect
agent: agent
model: GPT-5 (copilot)
---
Review only the staged changes in this workspace.

Inputs:
- Treat the current Git staged diff as the required primary input.
- Use the user's argument, if provided, as an extra focus area or risk to inspect.

Review priorities:
- Behavioral regressions and broken contracts
- Plugin lifecycle issues: startup, reset, teardown, event ordering, and transport registration
- Thread-safety or main-thread violations
- SDF parse, import, implement pipeline regressions
- Missing or weak tests for the touched behavior
- Ignore style-only nits unless they hide a real defect

Working rules:
- Step past wiring code to the nearest logic that actually decides the behavior.
- Use nearby tests and immediate call sites only to confirm or refute issues in the staged diff.
- Do not invent problems that are not supported by the diff.

Output rules:
- Findings first, ordered by severity.
- For each finding, include the risk, why it matters, and concrete file references.
- Keep the summary brief and secondary.
- If no defects are found, say so explicitly and list any residual risks or testing gaps.
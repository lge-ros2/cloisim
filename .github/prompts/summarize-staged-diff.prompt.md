---
name: Summarize Staged Diff
description: Summarize the current Git staged diff in this workspace. Use when: you need a fast, descriptive explanation of what changed without turning it into a code review.
argument-hint: Optional focus area such as tests, API changes, lifecycle behavior, or user-visible impact
agent: agent
model: GPT-5 (copilot)
---
Summarize only the staged Git diff in this workspace.

Inputs:
- Treat the current staged diff as the required primary input.
- Use the user's argument, if provided, to weight the summary toward one concern or subsystem.

Working rules:
- Stay descriptive by default. Do not turn this into a bug hunt or a severity-ranked review.
- Group related hunks into a small number of change areas.
- Distinguish code, tests, scripts, docs, and assets when they change.
- Explain what changed and what surface it affects, but do not speculate about defects unless the user explicitly asks for risk analysis.
- If the diff leaves intent ambiguous, note that briefly instead of guessing.

Output shape:
1. `Overview:` one short paragraph on the main purpose of the staged diff.
2. `Change areas:` a flat list of the main modifications grouped by behavior or subsystem.
3. `Impact:` brief notes on affected tests, scripts, APIs, or user-visible behavior.
4. `Open intent:` only if the staged diff leaves a material ambiguity.
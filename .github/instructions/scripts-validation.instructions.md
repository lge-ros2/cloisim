---
name: Scripts Validation Workflow
description: "Use when editing Unity runtime code under Assets/Scripts. Reinforces narrow local routing, immediate post-edit validation, and avoidance of broad exploratory edits or unrelated cleanup."
applyTo: "Assets/Scripts/**"
---

# Scripts Validation Workflow

This instruction governs editing workflow for `Assets/Scripts/**`. It supplements subsystem-specific instructions and should keep changes local, testable, and quickly falsifiable.

## Before the First Edit

- Start from one concrete anchor: failing command, test, stack trace, file, symbol, or wrong behavior.
- Read only enough nearby code to identify the controlling path and state one falsifiable local hypothesis.
- Name one cheap check that could disprove that hypothesis before widening the search.
- If the first file mainly wires or forwards behavior, step one hop to the code that actually decides it.
- Do not map the whole subsystem before editing.

## First Edit Rule

- Make the smallest grounded change that tests or fixes the current hypothesis.
- Prefer a reversible probe over a broad rewrite when confidence is incomplete.
- Do not fold in adjacent refactors, naming cleanup, or style-only edits with the first change.

## Immediate Validation Rule

- After the first substantive edit, the next action must be one focused validation step when one exists.
- Prefer, in order:
  1. the failing repro or behavior-scoped command
  2. a narrow EditMode or unit test for the touched slice
  3. a slice-scoped compile, lint, or typecheck
  4. `git diff` only when no executable check exists
- Do not resume broad searching or continue patching before that first validation unless a concrete blocker prevents it.

## Scope Discipline

- If the first validation supports the hypothesis but exposes a local defect, repair the same slice and rerun the same check.
- If the first validation falsifies the hypothesis, move one nearby hop to the next controlling code path.
- If validation is ambiguous, do one nearby disambiguating read, then choose between local repair and a one-hop move.
- Avoid broad exploratory reads once an executable check exists.

## CLOiSim-Specific Expectations

- Route semantic changes through the owning pipeline or lifecycle boundary: SDF parse/import/implement, device lifecycle, plugin lifecycle, or core startup/reset flow.
- Treat startup, reset, and teardown as one contract when touching `Device`, `CLOiSimPlugin`, `PluginStartTracker`, or `BridgeManager` paths.
- Prefer targeted EditMode checks or subsystem-local repro commands over broad project validation.

## Avoid

- Broad repo exploration before naming a hypothesis
- Multiple simultaneous fix attempts for one failure
- Unrelated cleanup in touched files
- Diff-only validation when a narrower executable check exists
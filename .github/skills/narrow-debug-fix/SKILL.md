---
name: narrow-debug-fix
description: "Debug and fix a bug by starting from one concrete failure, forming a falsifiable local hypothesis, making the smallest grounded edit, and immediately running focused validation. Use when: debugging a failing test, regression, runtime error, compiler error, wrong behavior, or a review comment asking for a narrow fix."
argument-hint: "Describe the failing test, command, error, symbol, or behavior."
---

# Narrow Debug and Fix

Use this workflow to keep bug fixing local, testable, and falsifiable. It is optimized for this repository's style: narrow code reads, small edits, immediate validation, and no unrelated cleanup.

## When to Use

- Failing tests with a known test name, command, or log line
- Regressions after a recent code change
- Compiler, lint, or type errors confined to a touched slice
- Runtime exceptions with a nearby owner in code
- Incorrect behavior with a concrete file, symbol, or command anchor
- Review comments asking for a narrowly scoped repair

## Outcome

By the end of the workflow you should have:

- One identified controlling code path
- One falsifiable explanation for the failure
- One small edit that addresses the root cause or exposes the missing piece
- One focused validation result
- One short summary of residual risk or uncovered cases

## Procedure

### 1. Start from a Concrete Anchor

Prefer one of these anchors:

- A file named by the user
- A failing test name
- A failing command
- Exact error text
- A nearby symbol or call site

If the first file mainly forwards, registers, or wires behavior, step one hop to the code that directly computes, mutates, or controls it.

### 2. Read Only Enough Local Context

Gather just enough evidence to answer:

- What code path currently decides the behavior?
- What is the smallest nearby abstraction that owns that decision?
- What cheap check could prove this theory wrong?

Stop once you can name:

- One falsifiable local hypothesis
- One cheap discriminating check
- One smallest plausible edit

### 3. State the Working Hypothesis

Write the hypothesis as a claim that can fail. Examples:

- This guard drops the request before the plugin registers.
- This conversion uses the wrong axis order for nested models.
- This reset path forgets to clear the cached message.

Avoid carrying multiple competing theories unless one nearby read is required to distinguish them.

### 4. Choose the Cheapest Discriminating Check

Prefer checks in this order:

1. The failing behavior or command itself
2. A narrow test covering the touched slice
3. A slice-scoped compile, lint, or typecheck
4. A repo script that exercises the same subsystem

`git diff` is not validation when an executable check exists.

### 5. Make the Smallest Grounded Edit

- Fix the root cause rather than the visible symptom when the local evidence supports it.
- Preserve existing APIs and surrounding style.
- If confidence is incomplete, make a small reversible probe that exposes the control-flow gap or type mismatch.
- Do not widen scope to adjacent cleanup during the first edit.

### 6. Validate Immediately After the First Substantive Edit

Run the same focused check chosen in step 4 before doing more reading or patching.

Interpret the result like this:

- Validation passes: only make adjacent follow-up edits that are now clearly required, then rerun the same check.
- Validation fails but still supports the same hypothesis: repair that same slice and rerun the same check.
- Validation falsifies the hypothesis: step one nearby hop to the next controlling code path and repeat.
- Validation is ambiguous: do one nearby disambiguating read, then choose between local repair and a one-hop move.

### 7. Close Out

Finish with at least one executable post-edit validation when possible.

Report:

- What changed
- What check passed or failed
- Any residual risk, missing coverage, or follow-up worth doing next

## Repo Guardrails

For CLOiSim-specific work:

- Route SDF behavior through the parse -> import -> implement pipeline instead of scene-only patches.
- Preserve plugin lifecycle contracts: `OnAwake()` -> `OnStart()` -> `Started` -> `OnReset()` / `OnDestroy()`.
- Do not rename scene roots: `Core`, `Props`, `World`, `Lights`, `Roads`, `UI`.
- Use tabs and existing C# brace style in Unity scripts.
- Favor narrow EditMode or subsystem-scoped checks before broad project validation.

## Completion Checklist

- [ ] I started from one concrete failure, file, symbol, or command.
- [ ] I identified the code that directly controls the behavior.
- [ ] I can state one falsifiable local hypothesis.
- [ ] I chose one cheap check that could disprove it.
- [ ] My first edit was the smallest plausible change or probe.
- [ ] I validated immediately after that edit.
- [ ] I avoided unrelated refactors or cleanup.
- [ ] I finished with a short outcome and remaining risk.
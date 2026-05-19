---
name: Stack Trace To Hypothesis
description: Turn a stack trace, Unity console error, test failure, or failing log excerpt into one local hypothesis and the cheapest discriminating check. Use when: you have error text but do not yet know the owning code path.
argument-hint: Paste the stack trace, failing log, or error excerpt
agent: agent
model: GPT-5 (copilot)
---
Turn the provided stack trace, console error, or failing log into one narrowly scoped debugging lead.

Inputs:
- Treat the pasted trace or log as the required primary input.
- Use workspace search only to identify the nearest owning code path, immediate call site, or nearby test.

Working rules:
- Ignore broad brainstorming.
- Step past framework, Unity, test harness, and wiring frames to the first repository frame that plausibly decides behavior.
- Produce one local hypothesis, not a list of possibilities.
- Choose the cheapest discriminating check that could prove the hypothesis wrong.
- If the trace is too weak to support a real hypothesis, say exactly what one extra log line, frame, symbol, or command output would disambiguate it.
- Do not propose a fix unless the user explicitly asks for one.

Output shape:
1. `Owning path:` the most relevant repo file, symbol, or subsystem.
2. `Local hypothesis:` one falsifiable claim about why this failed.
3. `Cheapest check:` the smallest command, test, repro step, or code read that could disprove the hypothesis.
4. `Why this check:` one short sentence.
5. `Missing signal:` only if the trace is too ambiguous.
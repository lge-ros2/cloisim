# Claude Code Assistant Context: CLOiSim

Shared architecture, verified Unity/package versions, lifecycle details, physics config, and directory map live in [ARCHITECTURE.md](ARCHITECTURE.md) — read that first. This file only adds Claude-Code-specific behavior. When project documentation and checked-in code disagree, follow the code.

## Claude Code Cost Optimization (Harness Engineering)

Claude Code's default model is **Haiku** for this project (fast, cheap queries). For heavier lifting—planning, implementation, code review—use workflow scripts that route to smarter, slower models. This is "harness engineering": pairing the right model to the right task to minimize cost.

### Model Tiers & CLOiSim Tasks

| Model | Tier | Latency | Best for | CLOiSim uses |
|---|---|---|---|---|
| Claude Haiku 4.5 | Cheap | ~1–2s | Quick questions, grep, searches, minor edits | Default: file search, navigation, small fixes |
| Claude Sonnet 4.6 | Mid | ~3–5s | Implementation, PR review, refactoring | Feature work, bug fixes, diff review |
| Claude Opus 4.6 | High | ~5–10s | Planning, architecture, complex reasoning | System design, agentic tasks, trade-off analysis |

### Workflow Scripts

Three workflows live in `.claude/workflows/`:

- **`plan.js`** (Opus 4.8): Takes a task description, produces a structured implementation plan with file paths, trade-offs, and verification steps. Phases: Explore, Plan, Review. Use when you need to scope work or make architectural decisions.
- **`implement.js`** (Sonnet 4.6): Reads files, implements a feature or fix, runs basic checks. Phases: Read, Implement, Verify. Use for feature work, bug fixes, refactoring.
- **`review.js`** (Sonnet 4.6 lead; Haiku spot-checks): Reviews diffs across code-quality dimensions, verifies findings. Use for PR review, correctness checking.

### How to Use

**From Claude Code CLI/UI:**

```bash
# Use the planning workflow (Opus 4.8) for architecture/design
/workflows plan "Add a new sensor type for thermal imaging"

# Use the implementation workflow (Sonnet 4.6) for a bug fix
/workflows implement "Fix the race condition in BridgeManager port allocation"

# Use the review workflow for code review
/workflows review "Review this diff for correctness and performance"
```

**Inline in a session:**

- Default: use `/` queries, file search, `/grep` — Haiku handles these fine.
- For complex reasoning: `@plan` (plan mode) or `@opus` to explicitly switch to Opus 4.8.
- For typical editing: stay on Haiku; workflows will escalate automatically.

### Cost Savings

Routing `~50%` of queries to Haiku + `~40%` to Sonnet + `~10%` to Opus reduces average token cost by ~60% vs. always using Opus. On CLOiSim's large codebase (SDF pipeline, multi-layer architecture, asset streaming), Haiku handles 80% of navigation and search; Sonnet handles the implementation; Opus optimizes only architectural decisions.

### CLOiSim-Specific Context in Workflows

Workflows automatically inherit this file plus `ARCHITECTURE.md` — they know about:
- The SDF parse → import → implement pipeline
- Plugin lifecycle and thread management
- Coordinate system conversions (SDF right-hand ↔ Unity left-hand)
- Device message passing and port allocation
- NetMQ transport patterns

No need to repeat that context; workflows use it to make better-scoped plans and implementations.

## Domain-Specific Instructions

@.github/instructions/core.instructions.md
@.github/instructions/plugins.instructions.md
@.github/instructions/devices.instructions.md
@.github/instructions/sdf-pipeline.instructions.md
@.github/instructions/shaders.instructions.md
@.github/instructions/ui.instructions.md
@.github/instructions/scripts-validation.instructions.md

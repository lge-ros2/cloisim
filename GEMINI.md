# Gemini Code Assistant Context: CLOiSim

Shared architecture, verified Unity/package versions, lifecycle details, physics config, and directory map live in [ARCHITECTURE.md](ARCHITECTURE.md) — read that first. This file only adds Gemini-specific notes. When project documentation and checked-in code disagree, follow the code.

## Notes for Gemini

- This context is written for Gemini Code Assistant (Gemini 3.1 Pro/Preview class models).
- `ARCHITECTURE.md` is the deeply analyzed, actual state of the repository — treat it as overriding stale project documentation (READMEs, older version numbers, etc.).
- For per-subsystem depth (core, plugins, devices, SDF pipeline, shaders, UI), see `.github/instructions/*.instructions.md`. Gemini does not auto-load these via `@`-include the way Claude Code does — open them directly when working in that subsystem.

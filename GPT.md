# GPT-5.4 Working Context: CLOiSim

Shared architecture, verified Unity/package versions, lifecycle details, physics config, and directory map live in [ARCHITECTURE.md](ARCHITECTURE.md) — read that first. This file only adds GPT-specific reasoning guidance. When project documentation and checked-in code disagree, follow the code.

## Suggested reasoning strategy for GPT-5.4 when editing this repo

When asked to change behavior, first classify the request into one of these buckets:

1. **Boot/runtime orchestration** → inspect `Main.cs`, `Core/Modules`, `Core/Services`
2. **SDF compatibility/import bug** → inspect `Tools/SDF/Parser`, `Import`, `Implement`
3. **Sensor behavior or transport** → inspect `Devices/*`, matching `CLOiSimPlugins/*`, and `BridgeManager`
4. **Control/UI behavior** → inspect `UI/*` and WebSocket services
5. **World state/reset/save** → inspect `SimulationWorld`, `WorldSaver`, reset paths in `Main`

Then preserve existing patterns:

- Unity serialization first
- explicit reset support
- plugin lifecycle compliance
- UI/log feedback
- minimal API breakage

## Operational constraints GPT-5.4 should respect

- Prefer code over stale prose when they conflict (see `ARCHITECTURE.md` §2 for how version drift has bitten this repo before — always re-verify `ProjectSettings/ProjectVersion.txt` and `Packages/manifest.json` rather than trusting any doc's cached numbers).
- Preserve serialized fields and scene object names (`Core`, `World`, `Lights`, `Roads`, `UI` — see `ARCHITECTURE.md` §10).
- Treat plugin teardown as important — `CLOiSimPlugin` performs thread shutdown, transport disposal, and port deregistration in `OnDestroy()`. Changes should not leak threads, sockets, or allocated ports.
- Maintain startup visibility — the project already reports progress and errors through Unity logs, `UIController` messages, and plugin startup summaries. New runtime behavior should continue to surface failures and progress instead of failing silently.
- Avoid bypassing the SDF pipeline — the codebase is organized around SDF parse → import → implement. Feature work that changes world/model semantics should usually be expressed through that path rather than through one-off scene-only logic.

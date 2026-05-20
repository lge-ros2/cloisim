---
name: Explain Plugin Lifecycle Slice
description: Explain one CLOiSim plugin lifecycle segment using the real control flow and contracts in the repo. Use when: debugging, modifying, or testing plugin startup, transport registration, reset, threading, or teardown.
argument-hint: Name the lifecycle slice, plugin, or symbol to explain, for example Awake -> OnStart, RegisterTxDevice, PluginStartTracker, or OnDestroy
agent: agent
model: GPT-5 (copilot)
---
Explain one CLOiSim plugin lifecycle slice in engineering terms.

Inputs:
- Treat the user's argument or selected code as the required primary input.
- If neither identifies a concrete lifecycle slice, ask for one narrow target before continuing.

Working rules:
- Trace the real control flow through the owning code path instead of giving a broad architecture overview.
- Use the nearest relevant lifecycle code in `Assets/Scripts/CLOiSimPlugins/Modules/Base/CLOiSimPlugin.cs`, `Assets/Scripts/CLOiSimPlugins/Modules/Base/CLOiSimPlugin.Transport.cs`, `Assets/Scripts/CLOiSimPlugins/Modules/Base/CLOiSimPlugin.Thread.cs`, `Assets/Scripts/CLOiSimPlugins/Modules/Base/CLOiSimPluginThread.cs`, `Assets/Scripts/Core/PluginStartTracker.cs`, and `Assets/Scripts/Main.cs` when those files are relevant to the requested slice.
- Explain ordering, state changes, events, transport registration, thread start or stop, and reset or teardown behavior only as they relate to the requested slice.
- Surface lifecycle contracts and known failure points when they matter, especially that startup success is not real until `OnStart()` completes.
- Optimize for an engineer who needs to debug or modify the code, not for a high-level product summary.

Output shape:
1. `Slice:` the exact lifecycle segment being explained.
2. `Owning path:` the key files, symbols, or subsystems that control it.
3. `Control flow:` an ordered explanation of what runs and why.
4. `Contracts:` the invariants or ordering requirements that must hold.
5. `Failure points:` only the local ways this slice can go wrong.
# EXECUTION TOKENS — PER-FLOW IDENTITY AND STOP

*Branch: `feature/execution-token` (8 phases). Status: complete, manually verified.*

## Why

Before this branch, Dash had exactly one way to stop anything: `DashGraph.Stop()`, which swept
every node of the graph (`Nodes.ForEach(n => n.Stop())`). Because nodes are graph-level
singletons shared by every flow running through them, there was no way to say *"stop THIS
run but not THAT one"* — two concurrent flows through the same nodes were indistinguishable.
Worse, "stop" was not a real teardown:

- **Sequencer deadlock** — a stopped flow that occupied an `EventSequencer` slot never released
  it; every later event on that sequencer queued forever (no timeout, no cleanup path existed).
- **Orphaned spawns** — objects spawned by an interrupted flow stayed behind, untracked.
- **Tween leaks** — `AnimateWithPresetNode` had an empty `Stop_Internal`, `RetargetAdvancedNode`
  had none; their tweens survived stop and fired their downstream flows anyway.
- **Per-target stop was amputated** — `StopAnimationsNode` had been dead (body commented out)
  since a 2021 refactor removed `StopActiveTweens`; even when alive it leaked `ExecutionCount`
  on every kill.
- **killOnNullEncounter over-killed** — one flow's dead target killed every flow's animation on
  that node.

## The design

Each flow entering a graph gets a **`GraphExecution`** — an object owning all per-run state —
carried on `NodeFlowData.execution` and propagated automatically through every `Clone()` and
fan-out. Nodes stay (mostly) stateless; the execution owns:

| State | Purpose |
|---|---|
| `id` (`ExecutionId`, int-backed struct) | identity, logging |
| frame map (`node -> open frame count`, `TotalFrames`) | which nodes this flow is running right now |
| tween list (`(owner node, tween)` pairs) | what to kill on stop; owner lets kills prune the node's list |
| disposables (keyed teardown actions) | external claims: sequencer slots, spawned objects |
| `IsStopped` latch | one-way; gates `Execute` / `OnExecuteOutput` / `OnExecuteEnd` |

Executions are minted at flow origins (`ExecuteGraphInput`, `SendEvent`, plus a safety net in
`NodeBase.Execute` for hand-built flow data) and registered on the graph. The registry's
lifetime rule needs no completion callback: **a flow in flight always holds at least one open
frame** (async waits keep the waiting node's frame open; hops are synchronous), so
`TotalFrames == 0` observed from the main thread means *completed* — such entries are pruned
on each mint.

**Teardown semantics:** `GraphExecution.Stop()` kills the flow's tweens (`Kill(false)` — no
`OnComplete`, nothing resumes), releases its frames (node `ExecutionCount` stays honest),
latches `IsStopped`, then runs disposables newest-first. Natural completion runs **no**
disposables — a finished flow keeps its products; teardown applies only to interrupted flows.

## The stop ladder

Three scopes, each stopping exactly its own things:

| Scope | API | What dies | What survives |
|---|---|---|---|
| **Target** | `controller.StopAnimations(target)`, `graph.StopAnimations(target)`, `StopAnimationsNode` | tweens animating that target, across all flows (frames closed exactly) | the flows themselves, their other branches, their products |
| **Flow** | `controller.Stop(execution)`, `execution.Stop()`, `StopNode` with `StopMode.FLOW` | one flow: its tweens, frames, external claims (disposables run) | every concurrent flow, finished flows' products |
| **Graph** | `controller.Stop()`, `graph.Stop()`, `StopNode` with `StopMode.GRAPH`, `stopOnDisable` | every in-flight flow (full per-execution teardown) + node-level sweep | completed flows' products |

Callers obtain a flow handle at start:

```c#
GraphExecution execution = controller.ExecuteInput("MyInput", flowData);
// ... later:
controller.Stop(execution);          // tears down exactly that flow
```

Inside a graph, a `StopNode` set to `StopMode.FLOW` stops the flow running it (via
`p_flowData.execution`) — `FLOW` was appended to the end of the ordinal-serialized enum, so
existing assets keep their values.

## Addressable executions

Flows can also be found and stopped without holding a handle. Every execution is stamped at
mint time with its origin — `OriginType` (INPUT / EVENT / NONE), `OriginName` and
`OriginTarget` (the flow's TARGET at start; retargeting later does not change it). A flow that
arrives already carrying an execution keeps its original origin through events and subgraphs.

```c#
GraphExecution execution = controller.SendEvent("Popup");   // event sends return the handle too

controller.Stop(executionId);                    // by id (registry lookup)
controller.StopExecutionsByInput("Run");         // every live flow started from input "Run"
controller.StopExecutionsByEvent("Popup");       // every live cascade of event "Popup"
controller.StopExecutionsByTarget(transform);    // every live flow STARTED ON this target
```

`StopExecutionsByTarget` is per-target FLOW stop — full teardown of the runs started on that
target — as opposed to `StopAnimations(target)`, which only kills tweens and leaves the flows
running. The `StopExecutionsBy*` methods snapshot matches before stopping (a disposal may
synchronously start new flows) and return the number of flows stopped. All of these exist on
`DashGraph` with `DashController` passthroughs.

## Change log by phase

1. **Identity plumbing** — `ExecutionId`, `GraphExecution`; `NodeFlowData.execution` propagated
   by `Clone()`; minting at all flow origins; `SendCustomEventNode` forwards identity even when
   `sendData` is off. Removed dead `STOP_MODE` reserved name.
2. **Frame map** — `OnExecuteEnd()` → `OnExecuteEnd(NodeFlowData)` at all 54 call sites
   (obsolete parameterless shim kept for third-party nodes); `GraphExecution` tracks per-node
   open frames in lockstep with `ExecutionCount`.
3. **Tween tracking** — executions track the tweens they schedule, parallel to per-node lists;
   fixed the `AnimateWithPresetNode` / `RetargetAdvancedNode` stop leaks; removed dead
   `DashGraph._activeTweens`.
4. **Per-flow stop** — `GraphExecution.Stop()`, `IsStopped` gates, `StopMode.FLOW`,
   `ExecuteGraphInput(..., out GraphExecution)`, `DashController.Stop()/Stop(execution)/ExecuteInput`.
5. **Consolidation** — single `_activeTweens` on `NodeBase` (six duplicate lists and six
   identical `Stop_Internal` overrides deleted) with `TrackTween`/`UntrackTween` helpers;
   `(owner, tween)` pairs let kills prune node lists (closes a pooled-tween reuse hazard);
   `killOnNullEncounter` (17 sites) now kills only the current flow's tweens on that node.
6. **Disposables + registry** — keyed disposables on `GraphExecution`; sequencer claims
   register cancel-or-end teardown (`EventSequencer.CancelEvent` added; `EndEventNode`
   unregisters on natural release) fixing the deadlock; spawn nodes register despawn
   (pool `Return`/`Destroy`); `DashGraph._executions` registry with the frames>0 lifetime
   rule; whole-graph `Stop()` tears down in-flight executions before the node sweep;
   `NodeBase.Execute` gated on stopped executions (count-leak fix).
7. **Per-target stop** — `GraphExecution.KillTweensByTarget` with guarded per-kill frame
   closing (exact accounting; the pre-2021 version leaked `ExecutionCount`);
   `DashGraph.StopAnimations` / `DashController.StopAnimations`; `StopAnimationsNode` restored.
8. **Addressable executions** — origin stamping (`ExecutionOriginType` + name + initial
   target) on every mint; `SendEvent` returns the flow handle (graph and controller);
   registry queries `GetExecution(id)` / `Stop(id)` / `StopExecutionsByInput` / `ByEvent` /
   `ByTarget` with snapshot-then-stop iteration safety.

## Custom node migration

- Call `OnExecuteEnd(p_flowData)` instead of `OnExecuteEnd()` (old overload compiles with a
  deprecation warning; such nodes do not participate in per-flow stop).
- Book tweens with `TrackTween(tween, p_flowData)` / `UntrackTween(tween, p_flowData)` instead
  of a private list; delete tween-killing `Stop_Internal` overrides (the base handles it).
- Register `p_flowData.execution?.RegisterDisposable(...)` for external claims that must be
  released if the flow is stopped; unregister by key if the claim can be released naturally.

## Known limits / parked decisions

- **`DashCore.SendEvent` (global) returns no handle** — one global send reaches many
  controllers; when the flow data carries no execution each controller's clone mints its own,
  so there is no single handle to return. Use `StopExecutionsByEvent(name)` per controller.
- **No completion callback yet** (`execution.OnComplete`) — reachable now via the registry's
  frame rule, but not implemented.
- **`hasErrorsInExecution` is still node-level** and never resets — moving it to execution
  scope is a semantic change (an error would halt the whole flow) awaiting a decision.
- **`StoreStateNode` does not restore on stop** — auto-reverting transforms during teardown is
  a strong semantic, parked deliberately.
- **Cross-controller events share one execution** — a global event carries its origin
  execution to every controller; stopping it stops the cascade everywhere (v1 semantics).
- The whole-graph node sweep (`Nodes.ForEach(n => n.Stop())`) is retained after per-execution
  teardown as a belt-and-suspenders pass for execution-less flows (editor preview, legacy
  third-party nodes).

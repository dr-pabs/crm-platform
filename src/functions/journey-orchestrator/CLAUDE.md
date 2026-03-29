# journey-orchestrator — CLAUDE.md

All rules in `/CLAUDE.md` apply. This is an Azure Durable Functions project.

## Durable Functions Rules
- Orchestrators must be deterministic — no DateTime.UtcNow, no random, no I/O directly in orchestrator code.
- All I/O (DB reads, Service Bus, HTTP) goes in Activity functions, not the orchestrator.
- Orchestrators must handle replay correctly — check Durable Functions replay rules before any change.
- All external events must be idempotent — the orchestrator may receive the same event more than once.
- Store the DurableFunctionInstanceId on the related SQL record immediately after StartNewAsync.

## Key Patterns
- Use `context.CreateTimer` for SLA/wait deadlines — never Thread.Sleep or Task.Delay.
- External events raised via `RaiseEventAsync` from other services using the stored InstanceId.

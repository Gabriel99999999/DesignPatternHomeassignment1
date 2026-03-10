# ADR 0001: Concurrency Model Selection

## Status

Accepted – 10 March 2026

## Context

Assignment constraints require:

- A **bounded concurrency** model (no unbounded thread spawning).
- At least one shared resource with explicit thread-safety guarantees.
- Evidence of correctness under concurrency via automated tests.

The workflow runner must process image jobs (blur or grayscale) pulled from disk while persisting lifecycle metadata and notifying observers.

## Decision

Adopt `ConcurrentWorkflowRunner` backed by:

- **Bounded channel** (`Channel.CreateBounded`) that limits in-flight jobs to `QueueCapacity`.
- **Fixed worker pool** sized via `WorkflowRunnerOptions.WorkerCount`, each executing `ProcessLoopAsync`.
- **ObserverHub** dispatch thread to isolate observers from job processing and maintain asynchronous fan-out.
- Shared resources (`InMemoryJobRepository`, `ThreadSafeJobMetrics`) are implemented with `ConcurrentDictionary` and locking-free counters; their responsibilities include persisting status transitions and aggregating metrics.

Tests `WorkflowRunner.Tests/WorkflowRunnerTests.Runs_All_Jobs_With_Bounded_Concurrency` and `ObserverHubPatternTests.ObserverHub_Notifies_All_Observers_Even_When_One_Fails` verify concurrency invariants and failure isolation.

## Consequences

- **Pros**
  - Predictable memory/CPU usage under load thanks to bounded channel and worker pool.
  - Deterministic lifecycle ordering, simplifying tests and metrics.
  - Observers cannot stall the runner; failures are contained.
- **Cons**
  - Requires careful disposal to flush ObserverHub (handled via `await runner.DisposeAsync()` patterns).
  - Scaling beyond a single process would need distributed coordination (future work).

## Alternatives Considered

1. **TPL Dataflow** – richer primitives but introduces heavier dependencies; overkill for two job types.
2. **Unbounded Task creation** – rejected due to assignment constraints and risk of resource exhaustion.

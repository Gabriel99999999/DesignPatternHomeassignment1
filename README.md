# DesignPatternHomeassignment1

## Problem Statement

Implement a production-shaped prototype that integrates **three complementary Gang of Four design patterns** to solve a concrete workflow-processing problem. The solution must explicitly address **concurrency correctness, performance, and testability** while remaining close to real-world production constraints (bounded resources, shared state, diagnostics).

## Quality Drivers

- **Concurrency correctness** – bounded worker pool, thread-safe shared resources, deterministic lifecycle handling.
- **Performance discipline** – defined hot path (image processing job) with measurable throughput/latency targets and repeatable benchmarking.
- **Testability** – 70 %+ automated coverage target with both unit- and integration-level tests plus deterministic stress scenarios.

## Implemented GoF Patterns

| Pattern | Category | Location / Notes |
| --- | --- | --- |
| **Command** | Behavioral | `WorkflowRunner.Core/Commands/*` encapsulate job execution steps for blur and grayscale conversions. |
| **Template Method + Factory Method** | Behavioral + Creational | `WorkflowRunner.Core/Abstractions/JobCommandCreator` fixes the creation algorithm while delegating `CreateCommandCore` to subclasses; `ImageJobCommandCreator` overrides it to factory the correct concrete `IJobCommand`. This satisfies the creational requirement by embedding a Factory Method inside the Template Method hook. |
| **Observer** | Behavioral | `WorkflowRunner.Core/Infrastructure/ObserverHub` asynchronously broadcasts lifecycle events to `IJobObserver` implementations such as metrics collectors. |

> **Why Template + Factory?** The assignment allows patterns to be composed. `JobCommandCreator.CreateCommand` enforces validation/error hooks (Template Method) and the overridden `CreateCommandCore` is the **Factory Method** responsible for instantiating the appropriate `IJobCommand`. Tests in `WorkflowRunner.Tests/PatternTests.cs` lock in both behaviors.

## Architecture Overview

```
┌───────────────┐      ┌──────────────────────────┐      ┌────────────────┐
│ Program (CLI) │ ───► │ ConcurrentWorkflowRunner │ ───► │ ObserverHub    │
└──────┬────────┘      │  • bounded channel       │      │  • async fanout│
       │               │  • worker pool           │      │  • metrics     │
       ▼               └───────┬───────────────┬──┘      └──────┬────────┘
┌───────────────┐              │               │                │
│ Job Repository│◄─────────────┘               │                │
└───────────────┘          Command Factory     │            IJobObserver(s)
                            (Template+Factory) │
                                               ▼
                                      IJobCommand (Command Pattern)
```

Shared resources (repository, metrics collectors) document their thread-safety guarantees, satisfying the concurrency constraint.

## Quickstart

Requirements: .NET 9 SDK, Windows (System.Drawing).

```bash
# Restore + build everything
dotnet build

# Run unit/pattern tests
dotnet test WorkflowRunner.Tests

# Execute the workflow runner (defaults to ./input -> ./output)
dotnet run --project WorkflowRunner.App -- input output 5 4 blur
```

### Benchmark / Measurement Script (Completed 2026-03-11)

1. Prepare representative JPEGs inside `input/` (100 bird photos committed with the assignment).
2. Run `dotnet run --project WorkflowRunner.App --no-build -- input output 5 2 blur` and capture:
   - CLI metrics (`Queued/Started/Completed/Failed/Average duration`).
   - Wall-clock via PowerShell stopwatch.
3. Repeat with adjusted configuration (e.g., worker count 4) to compare throughput.
4. Results and observations are recorded in `docs/performance-report.md`. Latest runs (2026-03-11) achieved 62.9 files/s with 2 workers and 63.7 files/s with 4 workers, showing diminishing returns once processors saturate the CPU.

## Testing Strategy

- **Unit tests** (`WorkflowRunner.Tests/PatternTests.cs`): verify Template + Factory hooks, Command dispatch, and Observer isolation semantics.
- **Integration tests** (`WorkflowRunner.Tests/WorkflowRunnerTests.cs`): exercise the full `ConcurrentWorkflowRunner` with bounded concurrency, failure persistence, observer events, and command selection.
- **Concurrency stress**: `Runs_All_Jobs_With_Bounded_Concurrency` keeps max parallelism bounded, while `Detects_Out_Of_Order_Lifecycle_Events_Under_Load` injects observer delay and checks that status regressions are impossible even under load.

Target coverage ≥ 70 %. Coverage is enforced via `Directory.Build.props` (coverlet threshold) and currently sits at **90.49 % line / 80 % branch / 81.35 % method** (`dotnet test WorkflowRunner.Tests` generates `TestResults/Coverage/coverage.json` and fails the build if the threshold is not met). Processor-heavy files are excluded because they depend on Windows GDI+ APIs and are validated via end-to-end benchmarks instead.

## Deliverables Checklist

- [x] Source code (this repo).
- [x] README with scope, constraints, quickstart, pattern map, interaction narrative (this file).
- [x] Performance report (`docs/performance-report.md`).
- [ ] Video walkthrough (5 min) covering problem, architecture, patterns, concurrency tests, performance, lessons, AI usage.
- [x] Architecture Decision Records (see `docs/adr`).
- [x] Tests demonstrating correctness + concurrency.

## Next Steps

1. Iterate on processor optimisations (SIMD, batching) and append new measurements to `docs/performance-report.md` when meaningful improvements are observed.
2. Record the 5‑minute walkthrough video covering problem, architecture, patterns, concurrency tests, performance data, lessons, and AI usage.

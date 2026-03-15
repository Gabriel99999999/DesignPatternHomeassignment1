# DesignPatternHomeassignment1

## 1. Problem Statement

This project implements a production-shaped image workflow runner that processes many jobs concurrently and applies two operations:

- Blur
- Grayscale

The assignment focus is:

- explicit use of GoF patterns in a coherent architecture
- concurrency correctness (bounded resources, deterministic lifecycle updates)
- measurable performance and repeatable benchmark methodology
- automated testing with coverage >= 70%

## 2. Solution Scope and Constraints

- Language/runtime: C# / .NET 9
- Platform: Windows (uses `System.Drawing`)
- Input format: `.jpg` / `.jpeg`
- Runtime model: bounded channel + fixed worker pool
- Shared state: thread-safe repository and metrics collector

## 3. Implemented Patterns (Where and How)

### Command

- `WorkflowRunner.Core/Abstractions/IJobCommand.cs`
- `WorkflowRunner.Core/Commands/BlurImageCommand.cs`
- `WorkflowRunner.Core/Commands/GrayscaleImageCommand.cs`

Each job execution is wrapped in an `IJobCommand`. The runner executes commands through the interface and does not depend on concrete operation classes.

### Template Method

- `WorkflowRunner.Core/Abstractions/JobCommandCreator.cs`

`CreateCommand` defines a fixed creation flow with hooks:

- `OnBeforeCreate`
- `CreateCommandCore`
- `OnAfterCreate`
- `OnCreateFailed`

Concrete creators fill in only the variable part (`CreateCommandCore`).

### Factory Method

- `WorkflowRunner.Core/Factories/ImageJobCommandCreator.cs`
- `WorkflowRunner.Core/Runtime/ConcurrentWorkflowRunner.cs`
- `WorkflowRunner.App/Program.cs`

`ImageJobCommandCreator.CreateCommandCore` chooses the concrete command (`BlurImageCommand` or `GrayscaleImageCommand`) based on `ImageOperation`.  
The runner requests commands through the abstract creator, so it stays decoupled from concrete command types.

### Observer

- `WorkflowRunner.Core/Abstractions/IJobObserver.cs`
- `WorkflowRunner.Core/Infrastructure/ObserverHub.cs`
- `WorkflowRunner.Core/Infrastructure/ThreadSafeJobMetrics.cs`
- `WorkflowRunner.Core/Runtime/ConcurrentWorkflowRunner.cs`

The runner publishes lifecycle events (`Queued`, `Running`, `Completed`, `Failed`).  
`ObserverHub` fans out events asynchronously to observers and isolates observer failures.

## 4. Architecture Walkthrough

1. `WorkflowRunner.App/Program.cs` creates infrastructure (`InMemoryJobRepository`, `ThreadSafeJobMetrics`), processors, and `ImageJobCommandCreator`.
2. `ConcurrentWorkflowRunner` is created with:
   - `WorkflowRunnerOptions` (`WorkerCount`, `QueueCapacity`)
   - command creator
   - repository
   - observers
3. Jobs are enqueued (`EnqueueAsync`) and immediately marked `Queued`.
4. Worker tasks consume the bounded channel.
5. For each job, runner marks `Running`, creates the concrete command, executes it, then persists `Completed` or `Failed`.
6. Every status change is published as a `JobEvent` through `ObserverHub`.
7. `DisposeAsync` closes queue processing and observer dispatch cleanly.

## 5. Concurrency Model and Correctness Tests

Concurrency model:

- bounded `Channel<ImageJob>` (`BoundedChannelFullMode.Wait`)
- fixed number of worker tasks
- thread-safe repository (`ConcurrentDictionary`)
- thread-safe metrics (`Interlocked`, per-job stopwatches)

Key correctness and stress tests:

- `WorkflowRunner.Tests/WorkflowRunnerTests.cs::Runs_All_Jobs_With_Bounded_Concurrency`
  - verifies in-flight work never exceeds configured worker count
- `WorkflowRunner.Tests/WorkflowRunnerTests.cs::Detects_Out_Of_Order_Lifecycle_Events_Under_Load`
  - load test designed to expose ordering/race issues in lifecycle events
- `WorkflowRunner.Tests/PatternTests.cs::ObserverHub_Notifies_All_Observers_Even_When_One_Fails`
  - verifies observer failure isolation
- `WorkflowRunner.Tests/WorkflowRunnerTests.cs::Persists_Failed_Jobs_When_Command_Throws`
  - verifies failure path persistence and metrics

## 6. Performance Results and Methodology

Detailed report: `docs/performance-report.md`

Latest recorded runs (from report):

- 2026-03-11, 100 JPGs, 2 workers: 62.9 files/s
- 2026-03-11, 100 JPGs, 4 workers: 63.7 files/s

Method summary:

1. warm-up run
2. measured run(s) with fixed input set
3. capture CLI metrics (`Queued`, `Started`, `Completed`, `Failed`, average duration)
4. capture wall-clock time
5. compute throughput and compare configurations

## 7. Test and Coverage Status

Command used:

```bash
dotnet test WorkflowRunner.Tests/WorkflowRunner.Tests.csproj
```

Latest local result:

- Tests: 16 passed, 0 failed
- Coverage (`WorkflowRunner.Core`):
  - Line: 93.66%
  - Branch: 80.00%
  - Method: 89.83%

Coverage output files:

- `TestResults/Coverage/coverage.json`
- `TestResults/Coverage/coverage.info`

## 8. Quickstart

Prerequisites:

- .NET 9 SDK
- Windows runtime (for `System.Drawing`)

Build and test:

```bash
dotnet build
dotnet test WorkflowRunner.Tests/WorkflowRunner.Tests.csproj
```

Run app:

```bash
dotnet run --project WorkflowRunner.App
```

Notes:

- Current `Program.cs` uses fixed relative folders:
  - input: `../../../../input`
  - output: `../../../../output`
- It processes only `.jpg` / `.jpeg`.


## 9. AI Usage

We used this project as a direct experiment to learn how AI-assisted development works in practice.

Our process was:

- generate initial code with AI
- manually review and validate what was generated
- request targeted improvements and refinements from AI
- use AI as an additional check for deliverable completeness

In short: AI helped us produce and iterate faster, while final acceptance stayed with our own review and verification.

# ADR 0002: Pattern Stack (Command + Template/Factory + Observer)

## Status

Accepted – 10 March 2026

## Context

The assignment mandates exactly three complementary Gang of Four patterns with at least one creational or structural member. Existing workflow requirements include:

- Encapsulating image-processing operations.
- Providing an extensible way to create job commands for new `ImageOperation` values.
- Broadcasting lifecycle events to metrics/observers without tight coupling.

## Decision

Select the following trio:

1. **Command** – `IJobCommand` abstractions with concrete `BlurImageCommand` and `GrayscaleImageCommand`.
2. **Template Method + Factory Method** – `JobCommandCreator.CreateCommand` orchestrates validation and error hooks, while `ImageJobCommandCreator.CreateCommandCore` functions as the Factory Method that instantiates the appropriate Command. This combination covers the creational requirement.
3. **Observer** – `ObserverHub` multiplexes `JobEvent` notifications to implementations like `ThreadSafeJobMetrics`.

Factory and Template Method are intentionally combined: Template handles the invariant algorithm, Factory Method supplies concrete command creation without leaking object construction to clients.

## Consequences

- **Pros**
  - New operations require only a new `IJobCommand` + `ImageOperation` entry; `ImageJobCommandCreator` can be extended without touching consumers.
  - Observers can be added/removed without affecting the runner, satisfying testability and instrumentation goals.
  - Template/Factory layering centralizes error handling (e.g., `OnCreateFailed`) and simplifies testing, as shown in `WorkflowRunner.Tests/PatternTests`.
- **Cons**
  - Tight coupling between Template and Factory responsibilities may obscure the separate pattern identities unless thoroughly documented (addressed in README + tests).
  - Additional boilerplate for each new observer (registration and disposal) but acceptable given assignment scope.

## Alternatives Considered

1. **Abstract Factory for processor stacks** – rejected because only two operations exist; Factory Method is lighter.
2. **Mediator instead of Observer** – unnecessary since observers do not coordinate actions, only consume events.

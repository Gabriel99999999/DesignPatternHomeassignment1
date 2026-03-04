namespace WorkflowRunner.Core.Domain;

public sealed record JobEvent(
    Guid JobId,
    JobStatus Status,
    DateTimeOffset OccurredAt,
    string Message,
    string? Error = null);

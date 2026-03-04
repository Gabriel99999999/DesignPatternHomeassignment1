namespace WorkflowRunner.Core.Domain;

public sealed record JobRecord(
    ImageJob Job,
    JobStatus Status,
    string? OutputPath,
    string? ErrorMessage,
    DateTimeOffset UpdatedAt);

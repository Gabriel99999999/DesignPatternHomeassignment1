namespace WorkflowRunner.Core.Domain;

public sealed record ImageJob(
    Guid Id,
    string SourcePath,
    string TargetPath,
    int BlurRadius);

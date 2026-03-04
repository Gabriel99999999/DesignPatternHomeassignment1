namespace WorkflowRunner.Core.Abstractions;

public interface IJobMetrics
{
    long QueuedCount { get; }
    long StartedCount { get; }
    long CompletedCount { get; }
    long FailedCount { get; }
    TimeSpan AverageDuration { get; }
}

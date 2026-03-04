using WorkflowRunner.Core.Domain;

namespace WorkflowRunner.Core.Abstractions;

public interface IJobCommand
{
    Guid JobId { get; }
    Task<JobExecutionResult> ExecuteAsync(CancellationToken cancellationToken);
}

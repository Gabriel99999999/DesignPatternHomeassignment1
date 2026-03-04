using WorkflowRunner.Core.Domain;

namespace WorkflowRunner.Core.Abstractions;

public interface IJobObserver
{
    Task OnJobEventAsync(JobEvent jobEvent, CancellationToken cancellationToken);
}

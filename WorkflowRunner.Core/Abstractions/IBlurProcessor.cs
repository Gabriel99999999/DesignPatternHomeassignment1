using WorkflowRunner.Core.Domain;

namespace WorkflowRunner.Core.Abstractions;

public interface IBlurProcessor
{
    Task<string> BlurAsync(ImageJob job, CancellationToken cancellationToken);
}

using WorkflowRunner.Core.Domain;

namespace WorkflowRunner.Core.Abstractions;

public interface IGrayscaleProcessor
{
    Task<string> ConvertAsync(ImageJob job, CancellationToken cancellationToken);
}

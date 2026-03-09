using WorkflowRunner.Core.Abstractions;
using WorkflowRunner.Core.Domain;

namespace WorkflowRunner.Core.Commands;

public sealed class GrayscaleImageCommand : IJobCommand
{
    private readonly ImageJob _job;
    private readonly IGrayscaleProcessor _grayscaleProcessor;

    public GrayscaleImageCommand(ImageJob job, IGrayscaleProcessor grayscaleProcessor)
    {
        _job = job;
        _grayscaleProcessor = grayscaleProcessor;
    }

    public Guid JobId => _job.Id;

    public async Task<JobExecutionResult> ExecuteAsync(CancellationToken cancellationToken)
    {
        var outputPath = await _grayscaleProcessor.ConvertAsync(_job, cancellationToken).ConfigureAwait(false);
        return new JobExecutionResult(outputPath);
    }
}

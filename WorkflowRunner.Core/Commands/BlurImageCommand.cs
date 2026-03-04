using WorkflowRunner.Core.Abstractions;
using WorkflowRunner.Core.Domain;

namespace WorkflowRunner.Core.Commands;

public sealed class BlurImageCommand : IJobCommand
{
    private readonly ImageJob _job;
    private readonly IBlurProcessor _blurProcessor;

    public BlurImageCommand(ImageJob job, IBlurProcessor blurProcessor)
    {
        _job = job;
        _blurProcessor = blurProcessor;
    }

    public Guid JobId => _job.Id;

    public async Task<JobExecutionResult> ExecuteAsync(CancellationToken cancellationToken)
    {
        var outputPath = await _blurProcessor.BlurAsync(_job, cancellationToken).ConfigureAwait(false);
        return new JobExecutionResult(outputPath);
    }
}

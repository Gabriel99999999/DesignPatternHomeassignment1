using WorkflowRunner.Core.Abstractions;
using WorkflowRunner.Core.Commands;
using WorkflowRunner.Core.Domain;

namespace WorkflowRunner.Core.Factories;

public sealed class BlurJobCommandFactory : IJobCommandFactory
{
    private readonly IBlurProcessor _blurProcessor;

    public BlurJobCommandFactory(IBlurProcessor blurProcessor)
    {
        _blurProcessor = blurProcessor;
    }

    public IJobCommand Create(ImageJob job)
    {
        return new BlurImageCommand(job, _blurProcessor);
    }
}

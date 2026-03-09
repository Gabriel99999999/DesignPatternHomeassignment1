using WorkflowRunner.Core.Abstractions;
using WorkflowRunner.Core.Commands;
using WorkflowRunner.Core.Domain;

namespace WorkflowRunner.Core.Factories;

public sealed class ImageJobCommandCreator : JobCommandCreator
{
    private readonly IBlurProcessor _blurProcessor;
    private readonly IGrayscaleProcessor _grayscaleProcessor;

    public ImageJobCommandCreator(
        IBlurProcessor blurProcessor,
        IGrayscaleProcessor grayscaleProcessor)
    {
        _blurProcessor = blurProcessor;
        _grayscaleProcessor = grayscaleProcessor;
    }

    protected override IJobCommand CreateCommandCore(ImageJob job)
    {
        return job.Operation switch
        {
            ImageOperation.Blur => new BlurImageCommand(job, _blurProcessor),
            ImageOperation.Grayscale => new GrayscaleImageCommand(job, _grayscaleProcessor),
            _ => throw new InvalidOperationException($"Unsupported operation: {job.Operation}")
        };
    }
}

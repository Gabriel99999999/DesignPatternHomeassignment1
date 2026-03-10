using System.Collections.Concurrent;
using WorkflowRunner.Core.Abstractions;
using WorkflowRunner.Core.Commands;
using WorkflowRunner.Core.Domain;
using WorkflowRunner.Core.Factories;
using WorkflowRunner.Core.Infrastructure;

namespace WorkflowRunner.Tests;

public sealed class JobCommandCreatorPatternTests
{
    [Fact]
    public void Template_Method_Invokes_Hooks_In_Order()
    {
        var job = CreateJob(ImageOperation.Blur);
        var creator = new TrackingCreator();

        var command = creator.CreateCommand(job);

        var stub = Assert.IsType<StubCommand>(command);
        Assert.Equal(job.Id, stub.JobId);
        Assert.Equal(new[] { "before", "core", "after" }, creator.Events);
    }

    [Fact]
    public void Template_Method_Notifies_On_Create_Failure()
    {
        var job = CreateJob(ImageOperation.Grayscale);
        var creator = new TrackingCreator(throwOnCore: true);

        var exception = Assert.Throws<InvalidOperationException>(() => creator.CreateCommand(job));

        Assert.Equal("Core failure", exception.Message);
        Assert.Equal(new[] { "before", "core", "failed" }, creator.Events);
    }

    private static ImageJob CreateJob(ImageOperation operation)
    {
        return new ImageJob(Guid.NewGuid(), "source.jpg", "target.jpg", 3, operation);
    }

    private sealed class TrackingCreator : JobCommandCreator
    {
        private readonly bool _throwOnCore;

        public TrackingCreator(bool throwOnCore = false)
        {
            _throwOnCore = throwOnCore;
        }

        public List<string> Events { get; } = new();

        protected override void OnBeforeCreate(ImageJob job)
        {
            Events.Add("before");
        }

        protected override IJobCommand CreateCommandCore(ImageJob job)
        {
            Events.Add("core");

            if (_throwOnCore)
            {
                throw new InvalidOperationException("Core failure");
            }

            return new StubCommand(job.Id);
        }

        protected override void OnAfterCreate(ImageJob job, IJobCommand command)
        {
            Events.Add("after");
        }

        protected override void OnCreateFailed(ImageJob job, Exception exception)
        {
            Events.Add("failed");
        }
    }

    private sealed class StubCommand : IJobCommand
    {
        public StubCommand(Guid jobId)
        {
            JobId = jobId;
        }

        public Guid JobId { get; }

        public Task<JobExecutionResult> ExecuteAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(new JobExecutionResult("ok"));
        }
    }
}

public sealed class ImageJobCommandCreatorPatternTests
{
    [Fact]
    public async Task Factory_Produces_Blur_Command_For_Blur_Jobs()
    {
        var blurProcessor = new RecordingBlurProcessor();
        var grayscaleProcessor = new RecordingGrayscaleProcessor();
        var creator = new ImageJobCommandCreator(blurProcessor, grayscaleProcessor);
        var job = CreateJob(ImageOperation.Blur);

        var command = creator.CreateCommand(job);
        var blurCommand = Assert.IsType<BlurImageCommand>(command);
        Assert.Equal(job.Id, blurCommand.JobId);

        var result = await blurCommand.ExecuteAsync(CancellationToken.None);

        var recordedJob = Assert.Single(blurProcessor.Calls);
        Assert.Same(job, recordedJob);
        Assert.Empty(grayscaleProcessor.Calls);
        Assert.Equal($"{job.TargetPath}-blurred", result.OutputPath);
    }

    [Fact]
    public async Task Factory_Produces_Grayscale_Command_For_Grayscale_Jobs()
    {
        var blurProcessor = new RecordingBlurProcessor();
        var grayscaleProcessor = new RecordingGrayscaleProcessor();
        var creator = new ImageJobCommandCreator(blurProcessor, grayscaleProcessor);
        var job = CreateJob(ImageOperation.Grayscale);

        var command = creator.CreateCommand(job);
        var grayscaleCommand = Assert.IsType<GrayscaleImageCommand>(command);
        Assert.Equal(job.Id, grayscaleCommand.JobId);

        var result = await grayscaleCommand.ExecuteAsync(CancellationToken.None);

        var recordedJob = Assert.Single(grayscaleProcessor.Calls);
        Assert.Same(job, recordedJob);
        Assert.Empty(blurProcessor.Calls);
        Assert.Equal($"{job.TargetPath}-grayscale", result.OutputPath);
    }

    [Fact]
    public void Factory_Throws_For_Unsupported_Operation()
    {
        var creator = new ImageJobCommandCreator(new RecordingBlurProcessor(), new RecordingGrayscaleProcessor());
        var job = CreateJob((ImageOperation)999);

        var exception = Assert.Throws<InvalidOperationException>(() => creator.CreateCommand(job));
        Assert.Contains("Unsupported operation", exception.Message);
    }

    private static ImageJob CreateJob(ImageOperation operation)
    {
        return new ImageJob(Guid.NewGuid(), "source.jpg", "target.jpg", 5, operation);
    }

    private sealed class RecordingBlurProcessor : IBlurProcessor
    {
        private readonly List<ImageJob> _calls = new();

        public IReadOnlyList<ImageJob> Calls => _calls;

        public Task<string> BlurAsync(ImageJob job, CancellationToken cancellationToken)
        {
            _calls.Add(job);
            return Task.FromResult($"{job.TargetPath}-blurred");
        }
    }

    private sealed class RecordingGrayscaleProcessor : IGrayscaleProcessor
    {
        private readonly List<ImageJob> _calls = new();

        public IReadOnlyList<ImageJob> Calls => _calls;

        public Task<string> ConvertAsync(ImageJob job, CancellationToken cancellationToken)
        {
            _calls.Add(job);
            return Task.FromResult($"{job.TargetPath}-grayscale");
        }
    }
}

public sealed class ObserverHubPatternTests
{
    [Fact]
    public async Task ObserverHub_Notifies_All_Observers_Even_When_One_Fails()
    {
        var failingObserver = new ThrowingObserver();
        var collectingObserver = new CollectingObserver();
        var hub = new ObserverHub(new IJobObserver[] { failingObserver, collectingObserver });

        var jobEvent = new JobEvent(Guid.NewGuid(), JobStatus.Completed, DateTimeOffset.UtcNow, "done");

        try
        {
            await hub.PublishAsync(jobEvent, CancellationToken.None);
        }
        finally
        {
            await hub.DisposeAsync();
        }

        Assert.Equal(1, failingObserver.CallCount);
        Assert.Contains(jobEvent, collectingObserver.Events);
    }

    private sealed class ThrowingObserver : IJobObserver
    {
        private int _callCount;

        public int CallCount => Volatile.Read(ref _callCount);

        public Task OnJobEventAsync(JobEvent jobEvent, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _callCount);
            throw new InvalidOperationException("Simulated observer failure.");
        }
    }

    private sealed class CollectingObserver : IJobObserver
    {
        private readonly ConcurrentQueue<JobEvent> _events = new();

        public IReadOnlyCollection<JobEvent> Events => _events.ToArray();

        public Task OnJobEventAsync(JobEvent jobEvent, CancellationToken cancellationToken)
        {
            _events.Enqueue(jobEvent);
            return Task.CompletedTask;
        }
    }
}

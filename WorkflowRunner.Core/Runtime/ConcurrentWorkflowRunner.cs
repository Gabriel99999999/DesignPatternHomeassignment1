using System.Threading.Channels;
using WorkflowRunner.Core.Abstractions;
using WorkflowRunner.Core.Domain;
using WorkflowRunner.Core.Infrastructure;

namespace WorkflowRunner.Core.Runtime;

public sealed class ConcurrentWorkflowRunner : IAsyncDisposable
{
    private readonly JobCommandCreator _commandCreator;
    private readonly IJobRepository _repository;
    private readonly ObserverHub _observerHub;
    private readonly Channel<ImageJob> _queue;
    private readonly Task[] _workers;

    public ConcurrentWorkflowRunner(
        WorkflowRunnerOptions options,
        JobCommandCreator commandCreator,
        IJobRepository repository,
        IReadOnlyCollection<IJobObserver> observers)
    {
        _commandCreator = commandCreator;
        _repository = repository;
        _observerHub = new ObserverHub(observers);

        _queue = Channel.CreateBounded<ImageJob>(new BoundedChannelOptions(options.QueueCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        });

        _workers = Enumerable.Range(0, options.WorkerCount)
            .Select(_ => Task.Run(ProcessLoopAsync))
            .ToArray();
    }

    public Task EnqueueAsync(ImageJob job, CancellationToken cancellationToken)
    {
        _repository.Upsert(new JobRecord(job, JobStatus.Queued, null, null, DateTimeOffset.UtcNow));
        _ = _observerHub.PublishAsync(
            new JobEvent(job.Id, JobStatus.Queued, DateTimeOffset.UtcNow, "Job queued."),
            cancellationToken);

        return _queue.Writer.WriteAsync(job, cancellationToken).AsTask();
    }

    public void Complete()
    {
        _queue.Writer.TryComplete();
    }

    public Task Completion => Task.WhenAll(_workers);

    public async ValueTask DisposeAsync()
    {
        Complete();
        await Completion.ConfigureAwait(false);
        await _observerHub.DisposeAsync().ConfigureAwait(false);
    }

    private async Task ProcessLoopAsync()
    {
        await foreach (var job in _queue.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            await ExecuteSingleJobAsync(job).ConfigureAwait(false);
        }
    }

    private async Task ExecuteSingleJobAsync(ImageJob job)
    {
        _repository.Upsert(new JobRecord(job, JobStatus.Running, null, null, DateTimeOffset.UtcNow));
        await _observerHub.PublishAsync(
            new JobEvent(job.Id, JobStatus.Running, DateTimeOffset.UtcNow, "Job started."),
            CancellationToken.None).ConfigureAwait(false);

        try
        {
            var command = _commandCreator.CreateCommand(job);
            var result = await command.ExecuteAsync(CancellationToken.None).ConfigureAwait(false);

            _repository.Upsert(new JobRecord(job, JobStatus.Completed, result.OutputPath, null, DateTimeOffset.UtcNow));
            await _observerHub.PublishAsync(
                new JobEvent(job.Id, JobStatus.Completed, DateTimeOffset.UtcNow, "Job completed."),
                CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _repository.Upsert(new JobRecord(job, JobStatus.Failed, null, ex.Message, DateTimeOffset.UtcNow));
            await _observerHub.PublishAsync(
                new JobEvent(job.Id, JobStatus.Failed, DateTimeOffset.UtcNow, "Job failed.", ex.Message),
                CancellationToken.None).ConfigureAwait(false);
        }
    }
}

using System.Collections.Concurrent;
using System.Threading;
using WorkflowRunner.Core.Abstractions;
using WorkflowRunner.Core.Domain;
using WorkflowRunner.Core.Factories;
using WorkflowRunner.Core.Infrastructure;
using WorkflowRunner.Core.Runtime;

namespace WorkflowRunner.Tests;

public sealed class WorkflowRunnerTests
{
    [Fact]
    public async Task Runs_All_Jobs_With_Bounded_Concurrency()
    {
        var tracker = new ConcurrencyTrackingBlurProcessor(delayMs: 40);
        var runner = new ConcurrentWorkflowRunner(
            new WorkflowRunnerOptions { WorkerCount = 3, QueueCapacity = 20 },
            new ImageJobCommandCreator(tracker, new NoOpGrayscaleProcessor()),
            new InMemoryJobRepository(),
            new IJobObserver[] { new ThreadSafeJobMetrics() });

        try
        {
            for (var i = 0; i < 20; i++)
            {
                await runner.EnqueueAsync(new ImageJob(Guid.NewGuid(), "in", "out", 3, ImageOperation.Blur), CancellationToken.None);
            }

            runner.Complete();
            await runner.Completion;

            Assert.True(tracker.MaxObservedConcurrency <= 3);
        }
        finally
        {
            await runner.DisposeAsync();
        }
    }

    [Fact]
    public async Task Persists_Failed_Jobs_When_Command_Throws()
    {
        var repository = new InMemoryJobRepository();
        var metrics = new ThreadSafeJobMetrics();
        var runner = new ConcurrentWorkflowRunner(
            new WorkflowRunnerOptions { WorkerCount = 2, QueueCapacity = 10 },
            new ImageJobCommandCreator(new FailingBlurProcessor(), new NoOpGrayscaleProcessor()),
            repository,
            new IJobObserver[] { metrics });

        var job = new ImageJob(Guid.NewGuid(), "in", "out", 3, ImageOperation.Blur);

        try
        {
            await runner.EnqueueAsync(job, CancellationToken.None);
            runner.Complete();
            await runner.Completion;
        }
        finally
        {
            await runner.DisposeAsync();
        }

        Assert.True(repository.TryGet(job.Id, out var record));
        Assert.NotNull(record);
        Assert.Equal(JobStatus.Failed, record!.Status);
        Assert.Equal(1, metrics.FailedCount);
    }

    [Fact]
    public async Task Observer_Receives_Lifecycle_Events()
    {
        var collector = new CollectingObserver();
        var runner = new ConcurrentWorkflowRunner(
            new WorkflowRunnerOptions { WorkerCount = 1, QueueCapacity = 5 },
            new ImageJobCommandCreator(new ConcurrencyTrackingBlurProcessor(delayMs: 5), new NoOpGrayscaleProcessor()),
            new InMemoryJobRepository(),
            new IJobObserver[] { collector });

        var job = new ImageJob(Guid.NewGuid(), "in", "out", 1, ImageOperation.Blur);

        try
        {
            await runner.EnqueueAsync(job, CancellationToken.None);
            runner.Complete();
            await runner.Completion;
        }
        finally
        {
            await runner.DisposeAsync();
        }

        Assert.Contains(collector.Events, e => e.JobId == job.Id && e.Status == JobStatus.Queued);
        Assert.Contains(collector.Events, e => e.JobId == job.Id && e.Status == JobStatus.Running);
        Assert.Contains(collector.Events, e => e.JobId == job.Id && e.Status == JobStatus.Completed);
    }

    [Fact]
    public async Task Uses_Grayscale_Command_For_Grayscale_Jobs()
    {
        var blur = new ConcurrencyTrackingBlurProcessor(delayMs: 1);
        var grayscale = new TrackingGrayscaleProcessor();
        var runner = new ConcurrentWorkflowRunner(
            new WorkflowRunnerOptions { WorkerCount = 1, QueueCapacity = 5 },
            new ImageJobCommandCreator(blur, grayscale),
            new InMemoryJobRepository(),
            new IJobObserver[] { new ThreadSafeJobMetrics() });

        var job = new ImageJob(Guid.NewGuid(), "in", "out", 1, ImageOperation.Grayscale);

        try
        {
            await runner.EnqueueAsync(job, CancellationToken.None);
            runner.Complete();
            await runner.Completion;
        }
        finally
        {
            await runner.DisposeAsync();
        }

        Assert.Equal(0, blur.CallCount);
        Assert.Equal(1, grayscale.CallCount);
    }

    private sealed class ConcurrencyTrackingBlurProcessor : IBlurProcessor
    {
        private readonly int _delayMs;
        private int _inFlight;
        private int _maxObservedConcurrency;
        private int _callCount;

        public ConcurrencyTrackingBlurProcessor(int delayMs)
        {
            _delayMs = delayMs;
        }

        public int MaxObservedConcurrency => Volatile.Read(ref _maxObservedConcurrency);
        public int CallCount => Volatile.Read(ref _callCount);

        public async Task<string> BlurAsync(ImageJob job, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _callCount);

            var nowInFlight = Interlocked.Increment(ref _inFlight);
            UpdateMax(nowInFlight);

            await Task.Delay(_delayMs, cancellationToken);
            Interlocked.Decrement(ref _inFlight);
            return $"{job.TargetPath}-ok";
        }

        private void UpdateMax(int current)
        {
            while (true)
            {
                var snapshot = Volatile.Read(ref _maxObservedConcurrency);
                if (current <= snapshot)
                {
                    return;
                }

                if (Interlocked.CompareExchange(ref _maxObservedConcurrency, current, snapshot) == snapshot)
                {
                    return;
                }
            }
        }
    }

    private sealed class FailingBlurProcessor : IBlurProcessor
    {
        public Task<string> BlurAsync(ImageJob job, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Simulated processing failure.");
        }
    }

    private sealed class NoOpGrayscaleProcessor : IGrayscaleProcessor
    {
        public Task<string> ConvertAsync(ImageJob job, CancellationToken cancellationToken)
        {
            return Task.FromResult($"{job.TargetPath}-gray");
        }
    }

    private sealed class TrackingGrayscaleProcessor : IGrayscaleProcessor
    {
        private int _callCount;

        public int CallCount => Volatile.Read(ref _callCount);

        public Task<string> ConvertAsync(ImageJob job, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _callCount);
            return Task.FromResult($"{job.TargetPath}-gray");
        }
    }

    private sealed class CollectingObserver : IJobObserver
    {
        public ConcurrentBag<JobEvent> Events { get; } = new();

        public Task OnJobEventAsync(JobEvent jobEvent, CancellationToken cancellationToken)
        {
            Events.Add(jobEvent);
            return Task.CompletedTask;
        }
    }
}

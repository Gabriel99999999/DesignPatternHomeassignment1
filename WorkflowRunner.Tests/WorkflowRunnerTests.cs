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
            new BlurJobCommandFactory(tracker),
            new InMemoryJobRepository(),
            new IJobObserver[] { new ThreadSafeJobMetrics() });

        try
        {
            for (var i = 0; i < 20; i++)
            {
                await runner.EnqueueAsync(new ImageJob(Guid.NewGuid(), "in", "out", 3), CancellationToken.None);
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
            new BlurJobCommandFactory(new FailingBlurProcessor()),
            repository,
            new IJobObserver[] { metrics });

        var job = new ImageJob(Guid.NewGuid(), "in", "out", 3);

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
            new BlurJobCommandFactory(new ConcurrencyTrackingBlurProcessor(delayMs: 5)),
            new InMemoryJobRepository(),
            new IJobObserver[] { collector });

        var job = new ImageJob(Guid.NewGuid(), "in", "out", 1);

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

    private sealed class ConcurrencyTrackingBlurProcessor : IBlurProcessor
    {
        private readonly int _delayMs;
        private int _inFlight;
        private int _maxObservedConcurrency;

        public ConcurrencyTrackingBlurProcessor(int delayMs)
        {
            _delayMs = delayMs;
        }

        public int MaxObservedConcurrency => Volatile.Read(ref _maxObservedConcurrency);

        public async Task<string> BlurAsync(ImageJob job, CancellationToken cancellationToken)
        {
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

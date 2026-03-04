using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using WorkflowRunner.Core.Abstractions;
using WorkflowRunner.Core.Domain;

namespace WorkflowRunner.Core.Infrastructure;

public sealed class ThreadSafeJobMetrics : IJobObserver, IJobMetrics
{
    private readonly ConcurrentDictionary<Guid, Stopwatch> _stopwatches = new();
    private long _queued;
    private long _started;
    private long _completed;
    private long _failed;
    private long _durationTicks;

    public long QueuedCount => Interlocked.Read(ref _queued);
    public long StartedCount => Interlocked.Read(ref _started);
    public long CompletedCount => Interlocked.Read(ref _completed);
    public long FailedCount => Interlocked.Read(ref _failed);

    public TimeSpan AverageDuration
    {
        get
        {
            var completed = CompletedCount;
            if (completed == 0)
            {
                return TimeSpan.Zero;
            }

            var avgTicks = Interlocked.Read(ref _durationTicks) / completed;
            return TimeSpan.FromTicks(avgTicks);
        }
    }

    public Task OnJobEventAsync(JobEvent jobEvent, CancellationToken cancellationToken)
    {
        switch (jobEvent.Status)
        {
            case JobStatus.Queued:
                Interlocked.Increment(ref _queued);
                break;
            case JobStatus.Running:
                Interlocked.Increment(ref _started);
                _stopwatches[jobEvent.JobId] = Stopwatch.StartNew();
                break;
            case JobStatus.Completed:
                Interlocked.Increment(ref _completed);
                if (_stopwatches.TryRemove(jobEvent.JobId, out var sw))
                {
                    sw.Stop();
                    Interlocked.Add(ref _durationTicks, sw.ElapsedTicks);
                }
                break;
            case JobStatus.Failed:
                Interlocked.Increment(ref _failed);
                _stopwatches.TryRemove(jobEvent.JobId, out _);
                break;
        }

        return Task.CompletedTask;
    }
}

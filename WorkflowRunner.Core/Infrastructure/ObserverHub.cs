using System.Threading.Channels;
using WorkflowRunner.Core.Abstractions;
using WorkflowRunner.Core.Domain;

namespace WorkflowRunner.Core.Infrastructure;

public sealed class ObserverHub : IAsyncDisposable
{
    private readonly IReadOnlyCollection<IJobObserver> _observers;
    private readonly Channel<JobEvent> _events;
    private readonly Task _dispatcherTask;

    public ObserverHub(IReadOnlyCollection<IJobObserver> observers)
    {
        _observers = observers;
        _events = Channel.CreateUnbounded<JobEvent>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        });

        _dispatcherTask = Task.Run(DispatchAsync);
    }

    public ValueTask PublishAsync(JobEvent jobEvent, CancellationToken cancellationToken)
    {
        if (_events.Writer.TryWrite(jobEvent))
        {
            return ValueTask.CompletedTask;
        }

        return _events.Writer.WriteAsync(jobEvent, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        _events.Writer.TryComplete();
        await _dispatcherTask.ConfigureAwait(false);
    }

    private async Task DispatchAsync()
    {
        await foreach (var jobEvent in _events.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            foreach (var observer in _observers)
            {
                try
                {
                    await observer.OnJobEventAsync(jobEvent, CancellationToken.None).ConfigureAwait(false);
                }
                catch
                {
                    // Keep observers isolated from the execution pipeline.
                }
            }
        }
    }
}

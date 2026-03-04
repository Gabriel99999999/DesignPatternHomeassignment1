namespace WorkflowRunner.Core.Runtime;

public sealed class WorkflowRunnerOptions
{
    private int _workerCount = Environment.ProcessorCount;
    private int _queueCapacity = 1024;

    public int WorkerCount
    {
        get => _workerCount;
        init => _workerCount = value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(WorkerCount));
    }

    public int QueueCapacity
    {
        get => _queueCapacity;
        init => _queueCapacity = value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(QueueCapacity));
    }
}

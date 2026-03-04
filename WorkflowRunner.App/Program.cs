using WorkflowRunner.Core.Domain;
using WorkflowRunner.Core.Factories;
using WorkflowRunner.Core.Infrastructure;
using WorkflowRunner.Core.Processing;
using WorkflowRunner.Core.Runtime;

var options = new WorkflowRunnerOptions
{
    WorkerCount = 4,
    QueueCapacity = 64
};

var repository = new InMemoryJobRepository();
var metrics = new ThreadSafeJobMetrics();
var processor = new SimulatedBlurProcessor();
var factory = new BlurJobCommandFactory(processor);

await using var runner = new ConcurrentWorkflowRunner(
    options,
    factory,
    repository,
    new[] { metrics });

for (var i = 0; i < 100; i++)
{
    var job = new ImageJob(
        Guid.NewGuid(),
        $"input/image_{i:D3}.jpg",
        $"output/image_{i:D3}.jpg",
        BlurRadius: 3 + (i % 4));

    await runner.EnqueueAsync(job, CancellationToken.None);
}

runner.Complete();
await runner.Completion;

Console.WriteLine($"Queued: {metrics.QueuedCount}");
Console.WriteLine($"Started: {metrics.StartedCount}");
Console.WriteLine($"Completed: {metrics.CompletedCount}");
Console.WriteLine($"Failed: {metrics.FailedCount}");
Console.WriteLine($"Average duration: {metrics.AverageDuration.TotalMilliseconds:F2} ms");
Console.WriteLine($"Persisted records: {repository.Snapshot().Count}");

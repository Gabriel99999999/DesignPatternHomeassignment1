using WorkflowRunner.Core.Domain;
using WorkflowRunner.Core.Factories;
using WorkflowRunner.Core.Infrastructure;
using WorkflowRunner.Core.Processing;
using WorkflowRunner.Core.Runtime;

var inputDirectory = Path.GetFullPath("../../../../input");
var outputDirectory = Path.GetFullPath("../../../../output");
var blurRadius = 9;
var workerCount = 4;

if (!Directory.Exists(inputDirectory))
{
    Console.WriteLine($"Input directory does not exist: {inputDirectory}");
    return;
}

Directory.CreateDirectory(outputDirectory);

var imageFiles = Directory
    .EnumerateFiles(inputDirectory)
    .Where(IsJpeg)
    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
    .ToArray();

if (imageFiles.Length == 0)
{
    Console.WriteLine($"No JPG/JPEG files found in: {inputDirectory}");
    return;
}

var options = new WorkflowRunnerOptions
{
    WorkerCount = workerCount,
    QueueCapacity = Math.Max(16, imageFiles.Length)
};

var repository = new InMemoryJobRepository();
var metrics = new ThreadSafeJobMetrics();
var processor = new BlurProcessor();
var factory = new BlurJobCommandFactory(processor);

await using var runner = new ConcurrentWorkflowRunner(
    options,
    factory,
    repository,
    new[] { metrics });

foreach (var sourcePath in imageFiles)
{
    var fileName = Path.GetFileNameWithoutExtension(sourcePath);
    var targetPath = Path.Combine(outputDirectory, $"{fileName}_blurred.jpg");

    var job = new ImageJob(
        Guid.NewGuid(),
        sourcePath,
        targetPath,
        blurRadius);

    await runner.EnqueueAsync(job, CancellationToken.None);
}

runner.Complete();
await runner.Completion;

Console.WriteLine($"Input: {inputDirectory}");
Console.WriteLine($"Output: {outputDirectory}");
Console.WriteLine($"Files: {imageFiles.Length}");
Console.WriteLine($"Queued: {metrics.QueuedCount}");
Console.WriteLine($"Started: {metrics.StartedCount}");
Console.WriteLine($"Completed: {metrics.CompletedCount}");
Console.WriteLine($"Failed: {metrics.FailedCount}");
Console.WriteLine($"Average duration: {metrics.AverageDuration.TotalMilliseconds:F2} ms");
Console.WriteLine($"Persisted records: {repository.Snapshot().Count}");

static bool IsJpeg(string path)
{
    var extension = Path.GetExtension(path);
    return extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
        || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase);
}

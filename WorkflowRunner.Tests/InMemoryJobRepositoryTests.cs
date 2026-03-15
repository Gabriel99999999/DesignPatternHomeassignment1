using WorkflowRunner.Core.Domain;
using WorkflowRunner.Core.Infrastructure;

namespace WorkflowRunner.Tests;

public sealed class InMemoryJobRepositoryTests
{
    [Fact]
    public void Snapshot_Returns_All_Records()
    {
        var repository = new InMemoryJobRepository();
        var job1 = new ImageJob(Guid.NewGuid(), "in1", "out1", 1, ImageOperation.Blur);
        var job2 = new ImageJob(Guid.NewGuid(), "in2", "out2", 2, ImageOperation.Grayscale);
        var record1 = new JobRecord(job1, JobStatus.Queued, null, null, DateTimeOffset.UtcNow);
        var record2 = new JobRecord(job2, JobStatus.Completed, "out", null, DateTimeOffset.UtcNow);

        repository.Upsert(record1);
        repository.Upsert(record2);

        var snapshot = repository.Snapshot();

        Assert.Equal(2, snapshot.Count);
        Assert.Contains(snapshot, r => r.Job.Id == job1.Id && r.Status == JobStatus.Queued);
        Assert.Contains(snapshot, r => r.Job.Id == job2.Id && r.Status == JobStatus.Completed);

        Assert.True(repository.TryGet(job1.Id, out var retrieved1));
        Assert.Equal(record1, retrieved1);

        Assert.True(repository.TryGet(job2.Id, out var retrieved2));
        Assert.Equal(record2, retrieved2);
    }
}

using System.Collections.Concurrent;
using WorkflowRunner.Core.Abstractions;
using WorkflowRunner.Core.Domain;

namespace WorkflowRunner.Core.Infrastructure;

public sealed class InMemoryJobRepository : IJobRepository
{
    private readonly ConcurrentDictionary<Guid, JobRecord> _records = new();

    public void Upsert(JobRecord record)
    {
        _records.AddOrUpdate(record.Job.Id, record, (_, _) => record);
    }

    public bool TryGet(Guid jobId, out JobRecord? record)
    {
        var found = _records.TryGetValue(jobId, out var existing);
        record = existing;
        return found;
    }

    public IReadOnlyCollection<JobRecord> Snapshot()
    {
        return _records.Values.ToArray();
    }
}

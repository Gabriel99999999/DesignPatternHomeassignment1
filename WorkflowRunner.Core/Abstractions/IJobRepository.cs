using WorkflowRunner.Core.Domain;

namespace WorkflowRunner.Core.Abstractions;

public interface IJobRepository
{
    void Upsert(JobRecord record);
    bool TryGet(Guid jobId, out JobRecord? record);
    IReadOnlyCollection<JobRecord> Snapshot();
}

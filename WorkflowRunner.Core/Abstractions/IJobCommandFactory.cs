using WorkflowRunner.Core.Domain;

namespace WorkflowRunner.Core.Abstractions;

public interface IJobCommandFactory
{
    IJobCommand Create(ImageJob job);
}

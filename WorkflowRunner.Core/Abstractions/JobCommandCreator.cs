using WorkflowRunner.Core.Domain;

namespace WorkflowRunner.Core.Abstractions;

public abstract class JobCommandCreator
{
    public IJobCommand CreateCommand(ImageJob job)
    {
        ArgumentNullException.ThrowIfNull(job);

        OnBeforeCreate(job);

        try
        {
            var command = CreateCommandCore(job);
            ArgumentNullException.ThrowIfNull(command);

            OnAfterCreate(job, command);
            return command;
        }
        catch (Exception ex)
        {
            OnCreateFailed(job, ex);
            throw;
        }
    }

    protected abstract IJobCommand CreateCommandCore(ImageJob job);

    protected virtual void OnBeforeCreate(ImageJob job)
    {
    }

    protected virtual void OnAfterCreate(ImageJob job, IJobCommand command)
    {
    }

    protected virtual void OnCreateFailed(ImageJob job, Exception exception)
    {
    }
}

using System.Security.Cryptography;
using System.Text;
using WorkflowRunner.Core.Abstractions;
using WorkflowRunner.Core.Domain;

namespace WorkflowRunner.Core.Processing;

public sealed class SimulatedBlurProcessor : IBlurProcessor
{
    public async Task<string> BlurAsync(ImageJob job, CancellationToken cancellationToken)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(15 + job.BlurRadius), cancellationToken).ConfigureAwait(false);

        var payload = $"{job.SourcePath}:{job.TargetPath}:{job.BlurRadius}";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
        return $"{job.TargetPath}#{hash[..8]}";
    }
}

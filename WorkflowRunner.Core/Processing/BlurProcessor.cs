using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using WorkflowRunner.Core.Abstractions;
using WorkflowRunner.Core.Domain;

namespace WorkflowRunner.Core.Processing;

public sealed class BlurProcessor : IBlurProcessor
{
    public Task<string> BlurAsync(ImageJob job, CancellationToken cancellationToken)
    {
        if (!File.Exists(job.SourcePath))
        {
            throw new FileNotFoundException($"Input image was not found: {job.SourcePath}", job.SourcePath);
        }

        var radius = Math.Max(1, job.BlurRadius);
        Directory.CreateDirectory(Path.GetDirectoryName(job.TargetPath) ?? ".");

        using var sourceOriginal = new Bitmap(job.SourcePath);
        using var source = EnsureRgb24(sourceOriginal);
        using var blurred = ApplyBoxBlur(source, radius, cancellationToken);

        blurred.Save(job.TargetPath, ImageFormat.Jpeg);
        return Task.FromResult(job.TargetPath);
    }

    private static Bitmap EnsureRgb24(Bitmap source)
    {
        if (source.PixelFormat == PixelFormat.Format24bppRgb)
        {
            return (Bitmap)source.Clone();
        }

        var converted = new Bitmap(source.Width, source.Height, PixelFormat.Format24bppRgb);
        using var graphics = Graphics.FromImage(converted);
        graphics.DrawImage(source, 0, 0, source.Width, source.Height);
        return converted;
    }

    private static Bitmap ApplyBoxBlur(Bitmap source, int radius, CancellationToken cancellationToken)
    {
        var width = source.Width;
        var height = source.Height;

        var target = new Bitmap(width, height, PixelFormat.Format24bppRgb);
        var rectangle = new Rectangle(0, 0, width, height);

        var sourceData = source.LockBits(rectangle, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
        var targetData = target.LockBits(rectangle, ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);

        try
        {
            var sourceBytes = sourceData.Stride * height;
            var targetBytes = targetData.Stride * height;
            var sourceBuffer = new byte[sourceBytes];
            var targetBuffer = new byte[targetBytes];

            Marshal.Copy(sourceData.Scan0, sourceBuffer, 0, sourceBytes);

            for (var y = 0; y < height; y++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var yMin = Math.Max(0, y - radius);
                var yMax = Math.Min(height - 1, y + radius);

                for (var x = 0; x < width; x++)
                {
                    var xMin = Math.Max(0, x - radius);
                    var xMax = Math.Min(width - 1, x + radius);

                    var red = 0;
                    var green = 0;
                    var blue = 0;
                    var count = 0;

                    for (var sampleY = yMin; sampleY <= yMax; sampleY++)
                    {
                        var rowOffset = sampleY * sourceData.Stride;
                        for (var sampleX = xMin; sampleX <= xMax; sampleX++)
                        {
                            var index = rowOffset + (sampleX * 3);
                            blue += sourceBuffer[index];
                            green += sourceBuffer[index + 1];
                            red += sourceBuffer[index + 2];
                            count++;
                        }
                    }

                    var targetIndex = (y * targetData.Stride) + (x * 3);
                    targetBuffer[targetIndex] = (byte)(blue / count);
                    targetBuffer[targetIndex + 1] = (byte)(green / count);
                    targetBuffer[targetIndex + 2] = (byte)(red / count);
                }
            }

            Marshal.Copy(targetBuffer, 0, targetData.Scan0, targetBytes);
            return target;
        }
        finally
        {
            source.UnlockBits(sourceData);
            target.UnlockBits(targetData);
        }
    }
}

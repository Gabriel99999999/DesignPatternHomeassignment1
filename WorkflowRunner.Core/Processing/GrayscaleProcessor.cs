using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using WorkflowRunner.Core.Abstractions;
using WorkflowRunner.Core.Domain;

namespace WorkflowRunner.Core.Processing;

public sealed class GrayscaleProcessor : IGrayscaleProcessor
{
    public Task<string> ConvertAsync(ImageJob job, CancellationToken cancellationToken)
    {
        if (!File.Exists(job.SourcePath))
        {
            throw new FileNotFoundException($"Input image was not found: {job.SourcePath}", job.SourcePath);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(job.TargetPath) ?? ".");

        using var sourceOriginal = new Bitmap(job.SourcePath);
        using var source = EnsureRgb24(sourceOriginal);
        using var grayscale = ConvertToGrayscale(source, cancellationToken);

        grayscale.Save(job.TargetPath, ImageFormat.Jpeg);
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

    private static Bitmap ConvertToGrayscale(Bitmap source, CancellationToken cancellationToken)
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
                var rowOffset = y * sourceData.Stride;

                for (var x = 0; x < width; x++)
                {
                    var index = rowOffset + (x * 3);
                    var blue = sourceBuffer[index];
                    var green = sourceBuffer[index + 1];
                    var red = sourceBuffer[index + 2];

                    var gray = (byte)Math.Clamp((0.114 * blue) + (0.587 * green) + (0.299 * red), 0, 255);
                    targetBuffer[index] = gray;
                    targetBuffer[index + 1] = gray;
                    targetBuffer[index + 2] = gray;
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

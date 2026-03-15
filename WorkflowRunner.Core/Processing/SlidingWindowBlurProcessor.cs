using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using WorkflowRunner.Core.Abstractions;
using WorkflowRunner.Core.Domain;

namespace WorkflowRunner.Core.Processing;

public sealed class SlidingWindowBlurProcessor : IBlurProcessor
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
        using var blurred = ApplySlidingWindowBlur(source, radius, cancellationToken);

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

    private static Bitmap ApplySlidingWindowBlur(Bitmap source, int radius, CancellationToken cancellationToken)
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
            var horizontalBuffer = new byte[sourceBytes];
            var targetBuffer = new byte[targetBytes];

            Marshal.Copy(sourceData.Scan0, sourceBuffer, 0, sourceBytes);

            ApplyHorizontalPass(sourceBuffer, horizontalBuffer, width, height, sourceData.Stride, radius, cancellationToken);
            ApplyVerticalPass(horizontalBuffer, targetBuffer, width, height, sourceData.Stride, targetData.Stride, radius, cancellationToken);

            Marshal.Copy(targetBuffer, 0, targetData.Scan0, targetBytes);
            return target;
        }
        finally
        {
            source.UnlockBits(sourceData);
            target.UnlockBits(targetData);
        }
    }

    private static void ApplyHorizontalPass(
        byte[] sourceBuffer,
        byte[] targetBuffer,
        int width,
        int height,
        int stride,
        int radius,
        CancellationToken cancellationToken)
    {
        for (var y = 0; y < height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var rowOffset = y * stride;
            var blue = 0;
            var green = 0;
            var red = 0;

            for (var sampleX = -radius; sampleX <= radius; sampleX++)
            {
                var clampedX = Math.Clamp(sampleX, 0, width - 1);
                var index = rowOffset + (clampedX * 3);
                blue += sourceBuffer[index];
                green += sourceBuffer[index + 1];
                red += sourceBuffer[index + 2];
            }

            var kernelSize = (radius * 2) + 1;
            for (var x = 0; x < width; x++)
            {
                var targetIndex = rowOffset + (x * 3);
                targetBuffer[targetIndex] = (byte)(blue / kernelSize);
                targetBuffer[targetIndex + 1] = (byte)(green / kernelSize);
                targetBuffer[targetIndex + 2] = (byte)(red / kernelSize);

                if (x == width - 1)
                {
                    continue;
                }

                var outgoingX = Math.Clamp(x - radius, 0, width - 1);
                var incomingX = Math.Clamp(x + radius + 1, 0, width - 1);
                var outgoingIndex = rowOffset + (outgoingX * 3);
                var incomingIndex = rowOffset + (incomingX * 3);

                blue += sourceBuffer[incomingIndex] - sourceBuffer[outgoingIndex];
                green += sourceBuffer[incomingIndex + 1] - sourceBuffer[outgoingIndex + 1];
                red += sourceBuffer[incomingIndex + 2] - sourceBuffer[outgoingIndex + 2];
            }
        }
    }

    private static void ApplyVerticalPass(
        byte[] sourceBuffer,
        byte[] targetBuffer,
        int width,
        int height,
        int sourceStride,
        int targetStride,
        int radius,
        CancellationToken cancellationToken)
    {
        var kernelSize = (radius * 2) + 1;

        for (var x = 0; x < width; x++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var blue = 0;
            var green = 0;
            var red = 0;

            for (var sampleY = -radius; sampleY <= radius; sampleY++)
            {
                var clampedY = Math.Clamp(sampleY, 0, height - 1);
                var index = (clampedY * sourceStride) + (x * 3);
                blue += sourceBuffer[index];
                green += sourceBuffer[index + 1];
                red += sourceBuffer[index + 2];
            }

            for (var y = 0; y < height; y++)
            {
                var targetIndex = (y * targetStride) + (x * 3);
                targetBuffer[targetIndex] = (byte)(blue / kernelSize);
                targetBuffer[targetIndex + 1] = (byte)(green / kernelSize);
                targetBuffer[targetIndex + 2] = (byte)(red / kernelSize);

                if (y == height - 1)
                {
                    continue;
                }

                var outgoingY = Math.Clamp(y - radius, 0, height - 1);
                var incomingY = Math.Clamp(y + radius + 1, 0, height - 1);
                var outgoingIndex = (outgoingY * sourceStride) + (x * 3);
                var incomingIndex = (incomingY * sourceStride) + (x * 3);

                blue += sourceBuffer[incomingIndex] - sourceBuffer[outgoingIndex];
                green += sourceBuffer[incomingIndex + 1] - sourceBuffer[outgoingIndex + 1];
                red += sourceBuffer[incomingIndex + 2] - sourceBuffer[outgoingIndex + 2];
            }
        }
    }
}

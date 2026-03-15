using System.Drawing;
using System.Drawing.Imaging;
using WorkflowRunner.Core.Domain;
using WorkflowRunner.Core.Processing;

namespace WorkflowRunner.Tests;

public sealed class ProcessingTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(
        Path.GetTempPath(),
        "WorkflowRunnerTests",
        Guid.NewGuid().ToString("N"));

    public ProcessingTests()
    {
        Directory.CreateDirectory(_tempDirectory);
    }

    [Fact]
    public async Task BlurProcessor_Blurs_Image_File()
    {
        var source = CreateSampleImage("blur-source.png");
        var target = Path.Combine(_tempDirectory, "blur-target.jpg");
        var job = new ImageJob(Guid.NewGuid(), source, target, 2, ImageOperation.Blur);
        var processor = new BlurProcessor();

        var outputPath = await processor.BlurAsync(job, CancellationToken.None);

        Assert.Equal(target, outputPath);
        Assert.True(File.Exists(target));
        using var blurred = new Bitmap(target);
        Assert.Equal(PixelFormat.Format24bppRgb, blurred.PixelFormat);
    }

    [Fact]
    public async Task GrayscaleProcessor_Writes_Grayscale_Image()
    {
        var source = CreateSampleImage("grayscale-source.png");
        var target = Path.Combine(_tempDirectory, "grayscale-target.jpg");
        var job = new ImageJob(Guid.NewGuid(), source, target, 1, ImageOperation.Grayscale);
        var processor = new GrayscaleProcessor();

        var outputPath = await processor.ConvertAsync(job, CancellationToken.None);

        Assert.Equal(target, outputPath);
        Assert.True(File.Exists(target));
        using var grayscale = new Bitmap(target);
        var pixel = grayscale.GetPixel(0, 0);
        Assert.True(pixel.R == pixel.G && pixel.G == pixel.B);
    }

    [Fact]
    public async Task Processors_Throw_When_Source_Missing()
    {
        var missingPath = Path.Combine(_tempDirectory, "missing.png");
        var blurJob = new ImageJob(Guid.NewGuid(), missingPath, Path.Combine(_tempDirectory, "any.jpg"), 1, ImageOperation.Blur);
        var grayscaleJob = new ImageJob(Guid.NewGuid(), missingPath, Path.Combine(_tempDirectory, "any2.jpg"), 1, ImageOperation.Grayscale);

        var blurProcessor = new BlurProcessor();
        var grayscaleProcessor = new GrayscaleProcessor();

        await Assert.ThrowsAsync<FileNotFoundException>(() => blurProcessor.BlurAsync(blurJob, CancellationToken.None));
        await Assert.ThrowsAsync<FileNotFoundException>(() => grayscaleProcessor.ConvertAsync(grayscaleJob, CancellationToken.None));
    }

    private string CreateSampleImage(string fileName)
    {
        var path = Path.Combine(_tempDirectory, fileName);
        using var bitmap = new Bitmap(2, 2);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.CornflowerBlue);
        bitmap.Save(path, ImageFormat.Png);
        return path;
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup failures in tests.
        }
    }
}

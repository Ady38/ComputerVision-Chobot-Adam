using ComputerVision.Inputs;
using ComputerVision.Models;
using ComputerVision.ProcessingSteps;

namespace ComputerVision.Tests;

public class ImageContractTests
{

    /// <summary>
    /// Verifies rejection of dimensions outside the image contract.
    /// </summary>
    /// <param name="width">The image width in pixels.</param>
    /// <param name="height">The image height in pixels.</param>
    [Theory]
    [InlineData(0, 96)]
    [InlineData(15, 96)]
    [InlineData(641, 96)]
    [InlineData(128, 0)]
    [InlineData(128, 15)]
    [InlineData(128, 641)]
    public void InvalidDimensionsFailBeforeProcessing(int width, int height)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new YUVImage(new
            byte[128 * 96], width, height));
    }

    /// <summary>
    /// Verifies that null or incomplete pixel buffers cannot form an image.
    /// </summary>
    [Fact]
    public void MissingOrShortBuffersAreRejected()
    {
        Assert.Throws<ArgumentNullException>(() => new YUVImage(null!, 128,
            96));
        Assert.Throws<ArgumentException>(() => new YUVImage(new byte[(128 *
            96) -
            1], 128, 96));
    }

    /// <summary>
    /// Verifies that the luma span shares Y bytes and excludes trailing chroma.
    /// </summary>
    [Fact]
    public void LumaAliasesOnlyTheYPlane()
    {
        byte[] buffer = new byte[128 * 96 * 3 / 2];
        Array.Fill(buffer, (byte)77);
        var image = new YUVImage(buffer, 128, 96);
        image.Luma.Fill(42);
        Assert.Equal(128 * 96, image.PixelCount);
        Assert.All(buffer[..image.PixelCount], value => Assert.Equal(42,
            value));
        Assert.All(buffer[image.PixelCount..], value => Assert.Equal(77,
            value));
    }

    /// <summary>
    /// Checks file loading against complete and truncated Y planes.
    /// </summary>
    /// <param name="count">
    /// The number of bytes written to the temporary input file.
    /// </param>
    /// <param name="valid">
    /// Whether the input is expected to be accepted.
    /// </param>
    [Theory]
    [InlineData((128 * 96) - 1, false)]
    [InlineData(128 * 96, true)]
    [InlineData(128 * 96 * 3 / 2, true)]
    public void FileLoaderRequiresACompleteYPlane(int count, bool valid)
    {
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(path, Enumerable.Repeat((byte)42,
                count).ToArray());
            YUVImage? image = ImageLoader.LoadFromFile(path, 128, 96);
            Assert.Equal(valid, image is not null);

            if (image is not null)
            {
                Assert.Equal(128 * 96, image.Bytes.Length);
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// Verifies that changing a cloned image does not change its source.
    /// </summary>
    [Fact]
    public void CloneOwnsItsBuffer()
    {
        var original = new YUVImage(Fixtures.Line(), 128, 96);
        var copy = (YUVImage)original.Clone();
        copy.Bytes[0] = 0;
        Assert.Equal(255, original.Bytes[0]);
        Assert.NotSame(original.Histogram, copy.Histogram);
    }

    /// <summary>
    /// Checks resized output, preserved capture dimensions and source
    /// ownership.
    /// </summary>
    [Fact]
    public void ResizeKeepsSourceDimensionsAndDoesNotTouchSource()
    {
        byte[] source = new byte[256 * 192 * 3 / 2];
        Array.Fill(source, (byte)90);
        var context = new ComputerVision.Pipeline.PipelineContext
        {
            Image = new(source, 256, 192)
        };
        new ResizeBilinearStep(128, 96).Execute(context);
        Assert.Equal(256, context.SourceWidth);
        Assert.Equal(192, context.SourceHeight);
        Assert.Equal(128 * 96, context.Image!.Bytes.Length);
        Assert.All(context.Image.Bytes, value => Assert.Equal(90, value));
        Assert.All(source, value => Assert.Equal(90, value));
    }

    /// <summary>
    /// Verifies rejection of unsupported resize dimensions.
    /// </summary>
    [Fact]
    public void ResizeRejectsMixedUpsamplingAndInvalidTarget()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ResizeBilinearStep(
            8, 8));
        Assert.Throws<ArgumentException>(() => new ResizeBilinearStep(256,
            48).Execute(Fixtures.Context(Fixtures.Line())));
    }
}

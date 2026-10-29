using ComputerVision.Helpers;
using ComputerVision.Models;
using ComputerVision.Pipeline;
using ComputerVision.ProcessingSteps;

namespace ComputerVision.Tests;

public class ProcessingTests
{

    /// <summary>
    /// Checks rejection of invalid or nonfinite gamma values.
    /// </summary>
    /// <param name="gamma">The finite, positive gamma exponent.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void InvalidGammaIsRejected(float gamma)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new
            GammaCorrectionStep(gamma));
    }

    /// <summary>
    /// Verifies that separate gamma stages retain independent lookup tables.
    /// </summary>
    [Fact]
    public void GammaInstancesDoNotChangeEachOther()
    {
        var first = new GammaCorrectionStep(.5f);
        var second = new GammaCorrectionStep(2);
        Parallel.For(0, 200, i =>
        {
            PipelineContext context =
                Fixtures.Context(Enumerable.Repeat((byte)64,
                128 * 96).ToArray());
            (i % 2 == 0 ? first : second).Execute(context);
            Assert.Equal(i % 2 == 0 ? 128 : 16, context.Image!.Bytes[0]);
        });
        first.Gamma = 1;
        PipelineContext again = Fixtures.Context(Enumerable.Repeat((byte)64,
            128 * 96).ToArray());
        second.Execute(again);
        Assert.Equal(16, again.Image!.Bytes[0]);
    }

    /// <summary>
    /// Compares Y-only and YUV input and verifies unchanged chroma bytes.
    /// </summary>
    /// <param name="smooth">
    /// Whether Gaussian smoothing is applied to the histogram.
    /// </param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ChromaNeverChangesThresholdOrLine(bool smooth)
    {
        byte[] y = Fixtures.Line(dark: 40, light: 200);
        byte[] yuv = new byte[y.Length * 3 / 2];
        y.CopyTo(yuv, 0);
        new Random(713).NextBytes(yuv.AsSpan(y.Length));
        byte[] chroma = yuv[y.Length..];
        LineDetectionSettings settings = Fixtures.Settings(smooth) with
        {
            GammaCorrection = true
        };
        PipelineContext gray =
            LineDetectionPipelineFactory.Create(settings).Process(new
            YUVImage(y, 128, 96), Fixtures.Coordinates());
        PipelineContext color =
            LineDetectionPipelineFactory.Create(settings).Process(new
            YUVImage(yuv, 128, 96), Fixtures.Coordinates());
        Assert.Equal(gray.Image!.Bytes, color.Image!.Luma.ToArray());
        Assert.Equal(gray.Image.Histogram.OtsuThreshold,
            color.Image.Histogram.OtsuThreshold);
        Assert.Equal(Fixtures.Points(gray), Fixtures.Points(color));
        Assert.Equal(chroma, yuv[y.Length..]);
    }

    /// <summary>
    /// Verifies that rejecting a frame only overwrites the Y plane.
    /// </summary>
    [Fact]
    public void LowContrastRejectionPreservesChroma()
    {
        byte[] bytes = Enumerable.Repeat((byte)100, 128 * 96 * 3 / 2).ToArray();
        bytes[128 * 96] = 42;
        PipelineContext context = Fixtures.Context(bytes);
        new HistogramUpdateStep(null).Execute(context);
        new OtsuThresholdStep(40).Execute(context);
        Assert.All(bytes[..(128 * 96)], b => Assert.Equal(255, b));
        Assert.Equal(42, bytes[128 * 96]);
    }

    /// <summary>
    /// Checks histogram storage reuse and fresh counts after a new frame.
    /// </summary>
    [Fact]
    public void HistogramReusesArraysAndResetsCounts()
    {
        var image = new YUVImage(Fixtures.Line(), 128, 96);
        YUVImageHistogram histogram = image.Histogram;
        float[] raw = histogram.Histogram;
        float[] normalized = histogram.NormalizedHistogram;
        float[] cumulative = histogram.CumulativeNormalizedHistogram;
        image.UpdateHistogram(null);
        Assert.Equal(128 * 96, raw.Sum());
        image.Luma.Fill(42);
        image.UpdateHistogram(5);
        Assert.Same(raw, histogram.Histogram);
        Assert.Same(normalized, histogram.NormalizedHistogram);
        Assert.Same(cumulative, histogram.CumulativeNormalizedHistogram);
        Assert.Equal(0, raw[0]);
        Assert.Equal(128 * 96, raw[42]);
        Assert.InRange(cumulative[^1], .99999f, 1.00001f);
        Assert.Equal(1f, normalized.Max());
    }

    /// <summary>
    /// Protects zero calling-thread allocations in the warmed small-image path.
    /// </summary>
    [Fact]
    public void WarmHistogramAndSmallGaussianDoNotAllocatePerFrame()
    {
        var context = new PipelineContext
        {
            Image = new YUVImage(new byte[16 * 16], 16, 16)
        };
        var histogram = new HistogramUpdateStep(5);
        var filter = new GaussianFilterStep(1);

        for (int i = 0; i < 30; i++)
        {
            filter.Execute(context);
            histogram.Execute(context);
        }

        long before = GC.GetAllocatedBytesForCurrentThread();

        for (int i = 0; i < 100; i++)
        {
            filter.Execute(context);
            histogram.Execute(context);
        }

        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, allocated);
    }

    /// <summary>
    /// Compares separable filtering with an independent replicated-border
    /// reference.
    /// </summary>
    /// <param name="width">The image width in pixels.</param>
    /// <param name="height">The image height in pixels.</param>
    /// <param name="sigma">
    /// The positive Gaussian standard deviation in sample units.
    /// </param>
    [Theory]
    [InlineData(2, 3, 5f)]
    [InlineData(16, 16, .1f)]
    [InlineData(128, 96, 1f)]
    [InlineData(256, 256, 1f)]
    public void GaussianMatchesIndependentBorderReference(int width, int height,
        float sigma)
    {
        float[] source = new float[width * height];
        var random = new Random(84);

        for (int i = 0; i < source.Length; i++)
        {
            source[i] = random.Next(256);
        }

        float[] actual = new float[source.Length];
        float[] expected = new float[source.Length];
        int radius = (int)Math.Ceiling(3 * sigma);
        double[] kernel = Enumerable.Range(-radius,
            (2 * radius) + 1).Select(k => Math.Exp(-(double)k * k / (2.0 * sigma
            * sigma))).ToArray();
        double sum = kernel.Sum();

        for (int k = 0; k < kernel.Length; k++)
        {
            kernel[k] /= sum;
        }

        GaussianFilterSeparableParallelHelper.HorizontalConvolution(sigma,
            source, actual, width, height);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                double value = 0;

                for (int k = -radius; k <= radius; k++)
                {
                    value += source[(y * width) + Math.Clamp(x + k, 0,
                        width - 1)] * kernel[k + radius];
                }

                expected[(y * width) + x] = (float)value;
            }
        }

        for (int i = 0; i < actual.Length; i++)
        {
            Assert.InRange(Math.Abs(actual[i] - expected[i]), 0, .0002f);
        }

        GaussianFilterSeparableParallelHelper.VerticalConvolution(sigma, source,
            actual, width, height);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                double value = 0;

                for (int k = -radius; k <= radius; k++)
                {
                    value += source[(Math.Clamp(y + k, 0,
                        height - 1) * width) + x] * kernel[k + radius];
                }

                expected[(y * width) + x] = (float)value;
            }
        }

        for (int i = 0; i < actual.Length; i++)
        {
            Assert.InRange(Math.Abs(actual[i] - expected[i]), 0, .0002f);
        }
    }

    /// <summary>
    /// Checks rejection of invalid Gaussian widths.
    /// </summary>
    /// <param name="sigma">
    /// The positive Gaussian standard deviation in sample units.
    /// </param>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void InvalidSigmaIsRejected(float sigma)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            GaussianFilterSeparableParallelHelper.CreateGaussianKernel(sigma));
    }
}

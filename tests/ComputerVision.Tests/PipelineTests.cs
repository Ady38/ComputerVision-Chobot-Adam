using ComputerVision.Models;
using ComputerVision.Pipeline;
using ComputerVision.ProcessingSteps;
using System.Drawing;

namespace ComputerVision.Tests;

public class PipelineTests
{

    /// <summary>
    /// Checks that a synthetic dark line survives thresholding at its known
    /// centre.
    /// </summary>
    /// <param name="smooth">
    /// Whether Gaussian smoothing is applied to the histogram.
    /// </param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BlackLineSurvivesOtsuAndHasCorrectCenter(bool smooth)
    {
        PipelineContext result =
            LineDetectionPipelineFactory.Create(Fixtures.Settings(
            smooth)).Process(new YUVImage(Fixtures.Line(), 128, 96),
            Fixtures.Coordinates());
        Assert.Equal(10, Fixtures.Points(result).Length);
        Assert.All(Fixtures.Points(result), p => Assert.InRange(p.X, 63, 65));
        Assert.NotNull(result.Offset);
        Assert.InRange(Math.Abs(result.Offset.Value.X), 0, .06f);
    }

    /// <summary>
    /// Verifies that constant frames never become valid line observations.
    /// </summary>
    /// <param name="intensity">
    /// The constant brightness value from zero to 255.
    /// </param>
    /// <param name="smooth">
    /// Whether Gaussian smoothing is applied to the histogram.
    /// </param>
    [Theory]
    [InlineData(0, false)]
    [InlineData(128, false)]
    [InlineData(255, false)]
    [InlineData(0, true)]
    [InlineData(128, true)]
    [InlineData(255, true)]
    public void ConstantFrameHasNoLineEvenWithZeroMargin(byte intensity,
        bool smooth)
    {
        LineDetectionSettings settings = Fixtures.Settings(smooth) with
        {
            OtsuThresholdBackgroundMargin = 0
        };
        PipelineContext result =
            LineDetectionPipelineFactory.Create(settings).Process(new
            YUVImage(Enumerable.Repeat(intensity, 128 * 96).ToArray(), 128, 96),
            Fixtures.Coordinates());
        Assert.Empty(Fixtures.Points(result));
        Assert.Null(result.Offset);
        Assert.Null(result.HeadingAngle);
    }

    /// <summary>
    /// Checks that losing a line clears new outputs and preserves old results.
    /// </summary>
    [Fact]
    public void LostLineDoesNotReuseLastResult()
    {
        ImageProcessingPipeline pipeline =
            LineDetectionPipelineFactory.Create(Fixtures.Settings());
        PipelineContext first =
            pipeline.Process(new YUVImage(Fixtures.Line(40), 128, 96),
            Fixtures.Coordinates());
        (int X, int Y)[] previousPoints = Fixtures.Points(first);
        PointF? previousOffset = first.Offset;
        PipelineContext lost =
            pipeline.Process(new YUVImage(Enumerable.Repeat((byte)255,
            128 * 96).ToArray(), 128, 96), Fixtures.Coordinates());
        Assert.Null(lost.Offset);
        Assert.Null(lost.HeadingAngle);
        Assert.Empty(Fixtures.Points(lost));
        Assert.Equal(previousPoints, Fixtures.Points(first));
        Assert.Equal(previousOffset, first.Offset);
    }

    /// <summary>
    /// Verifies rejection of a synthetic line with insufficient contrast.
    /// </summary>
    [Fact]
    public void LowContrastDoesNotCreateALine()
    {
        PipelineContext result =
            LineDetectionPipelineFactory.Create(Fixtures.Settings())
            .Process(new YUVImage(Fixtures.Line(dark: 120, light: 130), 128,
            96), Fixtures.Coordinates());
        Assert.Empty(Fixtures.Points(result));
        Assert.Null(result.Offset);
    }

    /// <summary>
    /// Checks that requested sample rows are distinct and inside the image.
    /// </summary>
    /// <param name="count">The requested number of sampled rows.</param>
    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(96)]
    public void SampleRowsStayInBounds(int count)
    {
        PipelineContext context = Fixtures.Context(Fixtures.Line());
        new SobelEdgeStepAdaptive(count).Execute(context);
        Assert.Equal(count, Fixtures.Points(context).Length);
        Assert.Equal(count,
            Fixtures.Points(context).Select(p => p.Y).Distinct().Count());
    }

    /// <summary>
    /// Checks isolation of mutable state between separate processing pipelines.
    /// </summary>
    [Fact]
    public void IndependentPipelinesCanRunConcurrently()
    {
        Parallel.For(0, 12, worker =>
        {
            ImageProcessingPipeline pipeline =
                LineDetectionPipelineFactory.Create(Fixtures
                .Settings());
            int center = worker % 2 == 0 ? 30 : 90;

            for (int frame = 0; frame < 40; frame++)
            {
                PipelineContext result =
                    pipeline.Process(new YUVImage(Fixtures.Line(
                    center), 128, 96), Fixtures.Coordinates());
                Assert.Equal(10, Fixtures.Points(result).Length);
                Assert.All(Fixtures.Points(result), p => Assert.InRange(p.X,
                    center - 1, center + 1));
            }

        });
    }

    /// <summary>
    /// Verifies that output-size validation rejects too many sample rows.
    /// </summary>
    [Fact]
    public void FactoryRejectsMoreSampleRowsThanImageHeight()
    {
        LineDetectionSettings settings = Fixtures.Settings() with
        {
            SobelEdgeNumberOfPoints = 97
        };
        Assert.Throws<System.ComponentModel.DataAnnotations
            .ValidationException>(() => settings.GetOutputSize(128, 96));
    }
}

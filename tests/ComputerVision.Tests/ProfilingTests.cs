using ComputerVision.Inputs;
using ComputerVision.Models;
using ComputerVision.Pipeline;
using ComputerVision.ProcessingSteps;

namespace ComputerVision.Tests;

public class ProfilingTests
{

    /// <summary>
    /// Checks fractional timing results for both pipeline entry points.
    /// </summary>
    /// <param name="fromDisk">
    /// Whether to exercise disk loading instead of an in-memory image.
    /// </param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ProfilingHandlesBothMemoryAndDiskInputs(bool fromDisk)
    {
        var pipeline = new ProfilingImageProcessingPipeline();
        string? temporary = null;
        try
        {
            if (fromDisk)
            {
                temporary = Path.GetTempFileName();
                File.WriteAllBytes(temporary, Fixtures.Line());
                pipeline.AddStep(new ImageLoadFromDiskStep());
            }

            pipeline.AddSteps(LineDetectionPipelineFactory.CreateSteps(Fixtures
                .Settings()));
            PipelineContext result =
                fromDisk ? pipeline.Process(new ImageLoadOptions(
                temporary!, 128, 96),
                Fixtures.Coordinates()) : pipeline.Process(new YUVImage(Fixtures
                .Line(), 128, 96), Fixtures.Coordinates());
            Assert.NotNull(result.ProfilingResult);
            Assert.Equal(fromDisk ? 5 : 4,
                result.ProfilingResult.StepResults.Count);
            Assert.True(result.ProfilingResult.ElapsedMilliseconds > 0);
            Assert.True(result.ProfilingResult.ElapsedMilliseconds >= result
                .ProfilingResult.StepResults.Sum(s => s.ElapsedMilliseconds));
            Assert.Equal(10, Fixtures.Points(result).Length);
        }
        finally
        {
            if (temporary is not null)
            {
                File.Delete(temporary);
            }
        }
    }
}

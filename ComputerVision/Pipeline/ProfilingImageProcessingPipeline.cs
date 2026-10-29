using ComputerVision.Inputs;
using ComputerVision.Models;
using ComputerVision.ProcessingSteps;
using ComputerVision.Utils;
using System.Diagnostics;

namespace ComputerVision.Pipeline;

/// <summary>
/// Diagnostic pipeline. Profiling allocates records and must not be used for
/// allocation benchmarks.
/// </summary>
public class ProfilingImageProcessingPipeline: ImageProcessingPipeline
{

    /// <summary>
    /// Wraps a stage with a timer and appends it.
    /// </summary>
    /// <param name="step">The stage to append to the pipeline.</param>
    public override void AddStep(IPipelineStep step)
    {
        base.AddStep(new ProfilingWrapperStep(step));
    }

    /// <summary>
    /// Wraps each supplied stage with a timer and appends it.
    /// </summary>
    /// <param name="steps">The stages to append in execution order.</param>
    public override void AddSteps(IEnumerable<IPipelineStep> steps)
    {
        foreach (IPipelineStep step in steps)
        {
            AddStep(step);
        }
    }

    /// <summary>
    /// Processes one frame and records total and per-stage elapsed times.
    /// </summary>
    /// <param name="options">
    /// The source file path and capture dimensions.
    /// </param>
    /// <param name="coordinates">
    /// The mapping between processed pixels and observation coordinates.
    /// </param>
    /// <returns>
    /// The completed context, including any missing or invalid observations.
    /// </returns>
    /// <remarks>
    /// Disk input requires an ImageLoadFromDiskStep. Image processing may
    /// overwrite Y.
    /// </remarks>
    public override PipelineContext Process(ImageLoadOptions options,
        CoordTrans coordinates)
    {
        long start = Stopwatch.GetTimestamp();
        PipelineContext result = base.Process(options, coordinates);
        result.ProfilingResult!.ElapsedMilliseconds = Stopwatch.GetElapsedTime(
            start).TotalMilliseconds;

        return result;
    }

    /// <summary>
    /// Processes one frame and records total and per-stage elapsed times.
    /// </summary>
    /// <param name="image">The image whose packed Y plane is processed.</param>
    /// <param name="coordinates">
    /// The mapping between processed pixels and observation coordinates.
    /// </param>
    /// <returns>
    /// The completed context, including any missing or invalid observations.
    /// </returns>
    /// <remarks>
    /// Disk input requires an ImageLoadFromDiskStep. Image processing may
    /// overwrite Y.
    /// </remarks>
    public override PipelineContext Process(YUVImage image,
        CoordTrans coordinates)
    {
        long start = Stopwatch.GetTimestamp();
        PipelineContext result = base.Process(image, coordinates);
        result.ProfilingResult!.ElapsedMilliseconds = Stopwatch.GetElapsedTime(
            start).TotalMilliseconds;

        return result;
    }

    /// <summary>
    /// Initializes a frame context and its profiling result.
    /// </summary>
    /// <param name="options">
    /// The source file path and capture dimensions.
    /// </param>
    /// <param name="coordinates">
    /// The mapping between processed pixels and observation coordinates.
    /// </param>
    /// <returns>
    /// The context used by subsequent processing stages.
    /// </returns>
    protected override PipelineContext CreateContext(ImageLoadOptions options,
        CoordTrans coordinates)
    {
        PipelineContext context = base.CreateContext(options, coordinates);
        context.ProfilingResult = new();

        return context;
    }

    /// <summary>
    /// Initializes a frame context and its profiling result.
    /// </summary>
    /// <param name="image">The image whose packed Y plane is processed.</param>
    /// <param name="coordinates">
    /// The mapping between processed pixels and observation coordinates.
    /// </param>
    /// <returns>
    /// The context used by subsequent processing stages.
    /// </returns>
    protected override PipelineContext CreateContext(YUVImage image,
        CoordTrans coordinates)
    {
        PipelineContext context = base.CreateContext(image, coordinates);
        context.ProfilingResult = new();

        return context;
    }
}

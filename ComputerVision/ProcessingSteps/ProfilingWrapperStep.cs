using ComputerVision.Models;
using ComputerVision.Pipeline;
using System.Diagnostics;

namespace ComputerVision.ProcessingSteps;

/// <summary>
/// Wraps a stage for diagnostic elapsed-time measurement.
/// </summary>
internal sealed class ProfilingWrapperStep: IPipelineStep
{
    private readonly IPipelineStep _innerStep;
    private readonly Stopwatch _stopwatch = new();

    /// <summary>
    /// Wraps a stage for diagnostic elapsed-time measurement.
    /// </summary>
    /// <param name="innerStep">
    /// The stage whose elapsed time is measured.
    /// </param>
    public ProfilingWrapperStep(IPipelineStep innerStep)
    {
        _innerStep = innerStep;
    }

    public string Name => "Profiling Wrapper Step";
    public string NameAbb => "Profiling";

    /// <summary>
    /// Runs the inner stage and appends a fractional-millisecond timing.
    /// </summary>
    /// <param name="context">
    /// The frame context containing the image and stage results.
    /// </param>
    public void Execute(PipelineContext context)
    {
        _stopwatch.Restart();
        _innerStep.Execute(context);
        _stopwatch.Stop();
        context.ProfilingResult!.StepResults.Add(new StepProfilingResult(
            _innerStep.Name, _stopwatch.Elapsed.TotalMilliseconds));
    }
}

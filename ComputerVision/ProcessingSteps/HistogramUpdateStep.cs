using ComputerVision.Pipeline;

namespace ComputerVision.ProcessingSteps;

/// <summary>
/// Configures optional histogram smoothing.
/// </summary>
public class HistogramUpdateStep: IPipelineStep
{
    private readonly float? _sigma;

    /// <summary>
    /// Configures optional histogram smoothing.
    /// </summary>
    /// <param name="sigma">
    /// The histogram smoothing width in intensity levels; null disables it.
    /// </param>
    public HistogramUpdateStep(float? sigma)
    {
        _sigma = sigma;
    }

    public string Name => "Update Histogram";
    public string NameAbb => "HistUpd";

    /// <summary>
    /// Refreshes the histogram for the current image, if one is present.
    /// </summary>
    /// <param name="context">
    /// The frame context containing the image and stage results.
    /// </param>
    public void Execute(PipelineContext context)
    {
        if (context == null || context.Image == null)
        {
            return;
        }

        context.Image.UpdateHistogram(_sigma);
    }
}

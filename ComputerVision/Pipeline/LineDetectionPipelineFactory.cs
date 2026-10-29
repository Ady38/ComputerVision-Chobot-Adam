using ComputerVision.ProcessingSteps;

namespace ComputerVision.Pipeline;

/// <summary>
/// Public stage composition matching FRIXbotV4/Tools/ComputerVisionHelper.cs.
/// Existing pipelines remain unchanged. The returned stages own managed
/// buffers. Serialize calls on the same pipeline/image. Separate pipelines own
/// separate working state.
/// </summary>
public static class LineDetectionPipelineFactory
{

    /// <summary>
    /// Constructs stages in the shared robot-compatible processing order.
    /// </summary>
    /// <param name="settings">
    /// The validated options used to construct the processing stages.
    /// </param>
    /// <param name="calibration">
    /// A provider returning the current calibration, or null when unavailable.
    /// </param>
    /// <returns>
    /// The stages, each owning its mutable processing state.
    /// </returns>
    public static IReadOnlyList<IPipelineStep> CreateSteps(
        LineDetectionSettings settings,
        Func<Calibration.GroundCalibration?>? calibration = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.Validate();
        List<IPipelineStep> steps = [];

        if (settings.ResizeImage)
        {
            steps.Add(new ResizeBilinearStep(settings.ImageResizeWidth,
                settings.ImageResizeHeight));
        }

        if (settings.GammaCorrection)
        {
            steps.Add(new GammaCorrectionStep(settings.Gamma));
        }

        if (settings.GaussianFilterImage)
        {
            steps.Add(new GaussianFilterStep(settings
                .GaussianFilterImageSigma));
        }

        steps.Add(new HistogramUpdateStep(settings.GaussianFilterHistogram ?
            settings.GaussianFilterHistogramSigma : null));

        if (settings.OtsuThreshold)
        {
            steps.Add(new OtsuThresholdStep(settings
                .OtsuThresholdBackgroundMargin));
        }

        if (settings.SobelEdge)
        {
            steps.Add(settings.AdaptiveSobelEdge ? new SobelEdgeStepAdaptive(
                settings.SobelEdgeNumberOfPoints) : new SobelEdgeStep(settings
                .SobelEdgeNumberOfPoints));
        }

        if (settings.ApplyHomography)
        {
            steps.Add(new HomographyStep(calibration ?? (() => null)));
        }

        if (settings.FitBezierCubicCurve || (settings.ApplyHomography
            && settings.FitGroundBezier))
        {
            steps.Add(new BezierCurveFitStep(settings.FitBezierCubicCurve,
                settings.ApplyHomography && settings.FitGroundBezier));
        }

        if (settings.OffsetAndAngleStep)
        {
            steps.Add(new OffsetAndAngleStep());
        }

        return steps;
    }

    /// <summary>
    /// Builds a reusable pipeline from validated stage settings.
    /// </summary>
    /// <param name="settings">
    /// The validated options used to construct the processing stages.
    /// </param>
    /// <param name="calibration">
    /// A provider returning the current calibration, or null when unavailable.
    /// </param>
    /// <returns>
    /// A new pipeline to be executed sequentially on its own images.
    /// </returns>
    public static ImageProcessingPipeline Create(LineDetectionSettings settings,
        Func<Calibration.GroundCalibration?>? calibration = null)
    {
        var pipeline = new ImageProcessingPipeline();
        pipeline.AddSteps(CreateSteps(settings, calibration));

        return pipeline;
    }
}

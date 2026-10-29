using ComputerVision.Inputs;
using ComputerVision.Models;
using ComputerVision.Utils;

namespace ComputerVision.Pipeline;

public class ImageProcessingPipeline
{
    private readonly List<IPipelineStep> _steps = [];

    /// <summary>
    /// Appends a stage to the execution sequence.
    /// </summary>
    /// <param name="step">The stage to append to the pipeline.</param>
    public virtual void AddStep(IPipelineStep step)
    {
        _steps.Add(step);
    }

    /// <summary>
    /// Appends stages in their enumeration order.
    /// </summary>
    /// <param name="steps">The stages to append in execution order.</param>
    public virtual void AddSteps(IEnumerable<IPipelineStep> steps)
    {
        _steps.AddRange(steps);
    }

    /// <summary>
    /// Creates a frame context and executes every configured stage.
    /// </summary>
    /// <param name="imageLoadOptions">
    /// The source file path and capture dimensions.
    /// </param>
    /// <param name="ct">
    /// The mapping between processed pixels and observation coordinates.
    /// </param>
    /// <returns>
    /// The completed context, including any missing or invalid observations.
    /// </returns>
    /// <remarks>
    /// Disk input requires an ImageLoadFromDiskStep. Image processing may
    /// overwrite Y.
    /// </remarks>
    public virtual PipelineContext Process(ImageLoadOptions imageLoadOptions,
        CoordTrans ct)
    {
        PipelineContext context = CreateContext(imageLoadOptions, ct);

        foreach (IPipelineStep step in _steps)
        {
            step.Execute(context);
        }

        return context;
    }

    /// <summary>
    /// Creates a frame context and executes every configured stage.
    /// </summary>
    /// <param name="image">The image whose packed Y plane is processed.</param>
    /// <param name="ct">
    /// The mapping between processed pixels and observation coordinates.
    /// </param>
    /// <returns>
    /// The completed context, including any missing or invalid observations.
    /// </returns>
    /// <remarks>
    /// Disk input requires an ImageLoadFromDiskStep. Image processing may
    /// overwrite Y.
    /// </remarks>
    public virtual PipelineContext Process(YUVImage image, CoordTrans ct)
    {
        PipelineContext context = CreateContext(image, ct);

        foreach (IPipelineStep step in _steps)
        {
            step.Execute(context);
        }

        return context;
    }

    /// <summary>
    /// Initializes a context from the supplied image or disk options.
    /// </summary>
    /// <param name="options">
    /// The source file path and capture dimensions.
    /// </param>
    /// <param name="ct">
    /// The mapping between processed pixels and observation coordinates.
    /// </param>
    /// <returns>
    /// The context used by subsequent processing stages.
    /// </returns>
    protected virtual PipelineContext CreateContext(ImageLoadOptions options,
        CoordTrans ct)
    {
        return new PipelineContext
        {
            ImageLoadOptions = options,
            CoordTrans = ct
        };
    }

    /// <summary>
    /// Initializes a context from the supplied image or disk options.
    /// </summary>
    /// <param name="image">The image whose packed Y plane is processed.</param>
    /// <param name="ct">
    /// The mapping between processed pixels and observation coordinates.
    /// </param>
    /// <returns>
    /// The context used by subsequent processing stages.
    /// </returns>
    protected virtual PipelineContext CreateContext(YUVImage image,
        CoordTrans ct)
    {
        return new PipelineContext
        {
            Image = image,
            SourceWidth = image.Width,
            SourceHeight = image.Height,
            CoordTrans = ct
        };
    }

    /// <summary>
    /// Removes all configured pipeline stages.
    /// </summary>
    public void Clear()
    {
        _steps.Clear();
    }
}

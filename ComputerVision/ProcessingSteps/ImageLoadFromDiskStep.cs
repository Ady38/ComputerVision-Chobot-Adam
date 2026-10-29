using ComputerVision.Inputs;
using ComputerVision.Models;
using ComputerVision.Pipeline;

namespace ComputerVision.ProcessingSteps;

/// <summary>
/// Configures raw-file loading and optional preservation of the input.
/// </summary>
public class ImageLoadFromDiskStep: IPipelineStep
{
    private readonly bool _keepOriginalImage;

    /// <summary>
    /// Configures raw-file loading and optional preservation of the input.
    /// </summary>
    /// <param name="keepOriginalImage">
    /// Whether to retain a separate copy of the loaded input.
    /// </param>
    public ImageLoadFromDiskStep(bool keepOriginalImage = false)
    {
        _keepOriginalImage = keepOriginalImage;
    }

    public string Name => "Image Load from Disk";
    public string NameAbb => "ImgLoad";

    /// <summary>
    /// Loads the requested Y plane and records its original dimensions.
    /// </summary>
    /// <param name="context">
    /// The frame context containing the image and stage results.
    /// </param>
    public void Execute(PipelineContext context)
    {
        if (context.ImageLoadOptions == null)
        {
            throw new InvalidOperationException(
                "ImageLoadSettings must be provided in the PipelineContext.");
        }

        context.Image = ImageLoader.LoadFromFile(context.ImageLoadOptions.Path,
            context.ImageLoadOptions.Width, context.ImageLoadOptions.Height)!
            ?? throw new InvalidOperationException(
            $"Failed to load image from path: {context.ImageLoadOptions.Path}");
        context.SourceWidth = context.Image.Width;
        context.SourceHeight = context.Image.Height;

        if (_keepOriginalImage)
        {
            context.OriginalImage = (YUVImage)context.Image.Clone();
        }
    }
}

using ComputerVision.Models;
using ComputerVision.Pipeline;

namespace ComputerVision.ProcessingSteps;

/// <summary>
/// Downsamples the Y plane. A result owns its buffer and can outlive the next
/// call.
/// </summary>
public class ResizeBilinearStep: IPipelineStep
{
    public string Name => "Bilinear Resize Step";
    public string NameAbb => "ResBil";

    public int Width
    {
        get;
        set
        {
            YUVImage.ValidateDimensions(value, YUVImage.MIN_SIZE);
            field = value;
        }
    }

    public int Height
    {
        get;
        set
        {
            YUVImage.ValidateDimensions(YUVImage.MIN_SIZE, value);
            field = value;
        }
    }

    /// <summary>
    /// Configures the output dimensions of a downsampling stage.
    /// </summary>
    /// <param name="width">The image width in pixels.</param>
    /// <param name="height">The image height in pixels.</param>
    public ResizeBilinearStep(int width, int height)
    {
        Width = width;
        Height = height;
    }

    /// <summary>
    /// Downsamples Y into a new image while retaining capture dimensions.
    /// </summary>
    /// <param name="context">
    /// The frame context containing the image and stage results.
    /// </param>
    public void Execute(PipelineContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Image is not { } source)
        {
            return;
        }

        if (Width > source.Width || Height > source.Height)
        {
            throw new ArgumentException(
                "Resize only supports downsampling in both dimensions.");
        }

        if (Width == source.Width && Height == source.Height)
        {
            return;
        }

        if (context.SourceWidth == 0 && context.SourceHeight == 0)
        {
            context.SourceWidth = source.Width;
            context.SourceHeight = source.Height;
        }

        Span<byte> pixels = source.Luma;
        byte[] resized = new byte[Width * Height];
        float xRatio = (float)source.Width / Width,
            yRatio = (float)source.Height / Height;

        for (int y = 0; y < Height; y++)
        {
            float sourceY = y * yRatio;
            int y0 = (int)sourceY, y1 = Math.Min(y0 + 1, source.Height - 1);
            float yLerp = sourceY - y0;

            for (int x = 0; x < Width; x++)
            {
                float sourceX = x * xRatio;
                int x0 = (int)sourceX, x1 = Math.Min(x0 + 1, source.Width - 1);
                float xLerp = sourceX - x0;
                float top =
                    pixels[(y0 * source.Width) + x0] + (xLerp * (pixels[(y0
                    * source.Width) + x1] - pixels[(y0 * source.Width) + x0]));
                float bottom = pixels[(y1 * source.Width) + x0] + (xLerp * (
                    pixels[(y1 * source.Width) + x1] - pixels[(y1 * source
                        .Width) +
                    x0]));
                resized[(y * Width) + x] =
                    (byte)(top + (yLerp * (bottom - top)) +
                    0.5f);
            }
        }

        context.Image = new YUVImage(resized, Width, Height);
    }
}

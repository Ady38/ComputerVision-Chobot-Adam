using ComputerVision.Helpers;
using ComputerVision.Models;
using ComputerVision.Pipeline;

namespace ComputerVision.ProcessingSteps;

/// <summary>
/// Thresholds only the packed Y plane; four pixels on each row edge remain
/// white.
/// </summary>
public class OtsuThresholdStep: IPipelineStep
{
    public string Name => "Otsu Threshold";
    public string NameAbb => "OtsuThr";

    public int BackgroundMargin
    {
        get;
        set
        {
            if (value is < 0 or > 255)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            field = value;
        }
    }

    /// <summary>
    /// Configures the required histogram peak separation.
    /// </summary>
    /// <param name="backgroundMargin">
    /// The minimum separation between histogram peak intensities.
    /// </param>
    public OtsuThresholdStep(int backgroundMargin)
    {
        BackgroundMargin = backgroundMargin;
    }

    /// <summary>
    /// Thresholds the Y plane, rejecting constant or low-contrast frames.
    /// </summary>
    /// <param name="context">
    /// The frame context containing the image and stage results.
    /// </param>
    /// <remarks>
    /// Values at or below the threshold become black. Four side pixels stay
    /// white.
    /// </remarks>
    public void Execute(PipelineContext? context)
    {
        if (context?.Image is not { } image)
        {
            return;
        }

        YUVImageHistogram histogram = image.Histogram;
        OtsuThresholdHelper.GetOtsuThreshold(histogram);
        Span<byte> pixels = image.Luma;

        if (histogram.RightMaxIndex <= histogram.LeftMaxIndex
            || histogram.RightMaxIndex - histogram.LeftMaxIndex <
            BackgroundMargin)
        {
            pixels.Fill(255);

            return;
        }

        Span<byte> lut = stackalloc byte[256];

        for (int i = 0; i < lut.Length; i++)
        {
            lut[i] = i <= histogram.OtsuThreshold ? (byte)0 : (byte)255;
        }

        for (int y = 0; y < image.Height; y++)
        {
            Span<byte> row = pixels.Slice(y * image.Width, image.Width);
            row[..4].Fill(255);

            for (int x = 4; x < image.Width - 4; x++)
            {
                row[x] = lut[row[x]];
            }

            row[^4..].Fill(255);
        }
    }
}

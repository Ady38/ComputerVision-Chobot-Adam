using ComputerVision.Models;
using ComputerVision.Pipeline;
using ComputerVision.Utils;

namespace ComputerVision.Tests;

internal static class Fixtures
{
    public const int Width = 128, Height = 96;

    /// <summary>
    /// Creates a vertical synthetic line with known centre and contrast.
    /// </summary>
    /// <param name="center">
    /// The horizontal centre of the synthetic line in pixels.
    /// </param>
    /// <param name="dark">
    /// The brightness assigned to the synthetic line.
    /// </param>
    /// <param name="light">
    /// The brightness assigned to the synthetic background.
    /// </param>
    /// <returns>
    /// The packed 128 by 96 grayscale pixels.
    /// </returns>
    public static byte[] Line(int center = 64, byte dark = 0, byte light = 255)
    {
        byte[] data = new byte[Width * Height];
        Array.Fill(data, light);

        for (int y = 0; y < Height; y++)
        {
            for (int x = center - 6; x < center + 6; x++)
            {
                data[(y * Width) + x] = dark;
            }
        }

        return data;
    }

    /// <summary>
    /// Creates the standard coordinate mapping used by the tests.
    /// </summary>
    /// <returns>
    /// The configured legacy observation mapping.
    /// </returns>
    public static CoordTrans Coordinates()
    {
        return new(0, 128, 96, 0, -3.25f, 3.25f, 0, 4.6f);
    }

    /// <summary>
    /// Creates a test context that shares the supplied pixel buffer.
    /// </summary>
    /// <param name="data">The packed test-image bytes.</param>
    /// <returns>
    /// The context with its image and coordinate mapping.
    /// </returns>
    public static PipelineContext Context(byte[] data)
    {
        return new()
        {
            Image = new(data, Width, Height),
            CoordTrans = Coordinates()
        };
    }

    /// <summary>
    /// Creates detector settings for deterministic synthetic-image tests.
    /// </summary>
    /// <param name="smooth">
    /// Whether Gaussian smoothing is applied to the histogram.
    /// </param>
    /// <returns>
    /// Settings with gamma disabled and optional histogram smoothing.
    /// </returns>
    public static LineDetectionSettings Settings(bool smooth = true)
    {
        return new()
        {
            GammaCorrection = false,
            GaussianFilterHistogram = smooth,
            OtsuThresholdBackgroundMargin = 40
        };
    }

    /// <summary>
    /// Flattens detection clusters into comparable integer coordinates.
    /// </summary>
    /// <param name="result">The completed frame context.</param>
    /// <returns>
    /// The ordered X/Y samples, or an empty array if none exist.
    /// </returns>
    public static (int X, int Y)[] Points(PipelineContext result)
    {
        return result.CurvePointClusters?.SelectMany(c => c).Select(p => (p.X,
            p.Y)).ToArray() ?? [];
    }
}

using ComputerVision.Models;
using ComputerVision.Pipeline;

namespace ComputerVision.ProcessingSteps;

/// <summary>
/// In-place gamma correction of Y only. Each stage owns its lookup table.
/// </summary>
public class GammaCorrectionStep: IPipelineStep
{
    public string Name => "Gamma correction";
    public string NameAbb => "GammaCorr";

    private readonly byte[] _lookup = new byte[256];
    public float Gamma
    {
        get;
        set
        {
            if (!float.IsFinite(value) || value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value),
                    "Gamma must be finite and positive.");
            }

            if (value == field)
            {
                return;
            }

            for (int i = 0; i < _lookup.Length; i++)
            {
                _lookup[i] = (byte)Math.Clamp((int)((255.0 * Math.Pow(i * (1.0 /
                    255.0), value)) + 0.5), 0, 255);
            }

            field = value;
        }

    } = float.NaN;

    /// <summary>
    /// Creates an instance-owned brightness lookup table.
    /// </summary>
    /// <param name="gamma">The finite, positive gamma exponent.</param>
    public GammaCorrectionStep(float gamma)
    {
        Gamma = gamma;
    }

    /// <summary>
    /// Applies the current gamma lookup table to Y pixels in place.
    /// </summary>
    /// <param name="context">
    /// The frame context containing the image and stage results.
    /// </param>
    public void Execute(PipelineContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        YUVImage image = context.Image
            ?? throw new ArgumentException("An image is required.",
            nameof(context));
        Span<byte> pixels = image.Luma;
        byte[] lookup = _lookup;

        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = lookup[pixels[i]];
        }
    }
}

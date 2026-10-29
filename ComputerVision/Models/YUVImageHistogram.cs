using ComputerVision.Helpers;

namespace ComputerVision.Models;

/// <summary>
/// Reusable storage belonging to one image. Arrays update in place.
/// </summary>
public class YUVImageHistogram
{
    public float[] Histogram { get; } = new float[256];
    public float[] NormalizedHistogram { get; } = new float[256];
    public float[] CumulativeNormalizedHistogram { get; } = new float[256];

    private readonly int[] _counts = new int[256];
    private float[]? _filtered;
    private float[]? _kernel;
    private float? _sigma;
    public byte OtsuThreshold
    {
        get; set;
    }

    public float LeftMax
    {
        get; set;
    }

    public int LeftMaxIndex
    {
        get; set;
    }

    public float RightMax
    {
        get; set;
    }

    public int RightMaxIndex
    {
        get; set;
    }

    /// <summary>
    /// Recounts Y pixels and updates raw, peak-normalized and cumulative bins.
    /// </summary>
    /// <param name="image">The image whose packed Y plane is processed.</param>
    /// <param name="sigma">
    /// Optional Gaussian width in intensity levels; null skips smoothing.
    /// </param>
    internal void UpdateHistogram(YUVImage image, float? sigma)
    {
        if (sigma.HasValue && (_kernel is null || sigma != _sigma))
        {
            _kernel = GaussianFilterSeparableParallelHelper
                .CreateGaussianKernel(sigma.Value);
            _sigma = sigma;
        }

        // Integer increments avoid a floating-point dependency chain in uniform
        // regions.
        Array.Clear(_counts);
        int[] counts = _counts;

        foreach (byte pixel in image.Luma)
        {
            counts[pixel]++;
        }

        float[] raw = Histogram;

        for (int i = 0; i < raw.Length; i++)
        {
            raw[i] = counts[i];
        }

        float[] source = raw;

        if (sigma.HasValue)
        {
            _filtered ??= new float[256];
            GaussianFilterSeparableParallelHelper.HorizontalConvolution(
                _kernel!, Histogram, _filtered, 256, 1);
            source = _filtered;
        }

        float maximum = 0, sum = 0;

        for (int i = 0; i < source.Length; i++)
        {
            maximum = Math.Max(maximum, source[i]);
            sum += source[i];
            CumulativeNormalizedHistogram[i] = sum;
        }

        float inverseMaximum = maximum > 0 ? 1f / maximum : 0;
        float inverseSum = sum > 0 ? 1f / sum : 0;

        for (int i = 0; i < source.Length; i++)
        {
            NormalizedHistogram[i] = source[i] * inverseMaximum;
            CumulativeNormalizedHistogram[i] *= inverseSum;
        }
    }
}

namespace ComputerVision.Helpers;

/// <summary>
/// Separable Gaussian convolution with replicated borders.
/// </summary>
/// <remarks>
/// Stages retain their own kernel. Small images run sequentially; large images
/// may use workers.
/// </remarks>
public static class GaussianFilterSeparableParallelHelper
{

    // A policy threshold, not a performance guarantee: benchmark on the target
    // device.
    private const int ParallelPixelThreshold = 64 * 64;

    /// <summary>
    /// Filters the image horizontally with replicated boundary samples.
    /// </summary>
    /// <param name="sigma">
    /// The positive Gaussian standard deviation in sample units.
    /// </param>
    /// <param name="src">
    /// The packed source samples; must not share the destination array.
    /// </param>
    /// <param name="dst">
    /// The destination buffer receiving the filtered samples.
    /// </param>
    /// <param name="width">The image width in pixels.</param>
    /// <param name="height">The image height in pixels.</param>
    /// <remarks>
    /// The sigma overload creates a kernel; the kernel overload reuses one.
    /// </remarks>
    public static void HorizontalConvolution(float sigma, float[] src,
        float[] dst, int width, int height)
    {
        HorizontalConvolution(CreateGaussianKernel(sigma), src, dst, width,
            height);
    }

    /// <summary>
    /// Filters the image vertically with replicated boundary samples.
    /// </summary>
    /// <param name="sigma">
    /// The positive Gaussian standard deviation in sample units.
    /// </param>
    /// <param name="src">
    /// The packed source samples; must not share the destination array.
    /// </param>
    /// <param name="dst">
    /// The destination buffer receiving the filtered samples.
    /// </param>
    /// <param name="width">The image width in pixels.</param>
    /// <param name="height">The image height in pixels.</param>
    /// <remarks>
    /// The sigma overload creates a kernel; the kernel overload reuses one.
    /// </remarks>
    public static void VerticalConvolution(float sigma, float[] src,
        float[] dst, int width, int height)
    {
        VerticalConvolution(CreateGaussianKernel(sigma), src, dst, width,
            height);
    }

    /// <summary>
    /// Filters the image horizontally with replicated boundary samples.
    /// </summary>
    /// <param name="kernel">
    /// The odd-length, normalized convolution kernel.
    /// </param>
    /// <param name="src">
    /// The packed source samples; must not share the destination array.
    /// </param>
    /// <param name="dst">
    /// The destination buffer receiving the filtered samples.
    /// </param>
    /// <param name="width">The image width in pixels.</param>
    /// <param name="height">The image height in pixels.</param>
    /// <remarks>
    /// The sigma overload creates a kernel; the kernel overload reuses one.
    /// </remarks>
    internal static void HorizontalConvolution(float[] kernel, float[] src,
        float[] dst, int width, int height)
    {
        Validate(src, dst, width, height);

        if ((long)width * height >= ParallelPixelThreshold && height > 1
            && Environment.ProcessorCount > 1)
        {
            HorizontalParallel(kernel, src, dst, width, height);
        }
        else
        {
            for (int row = 0; row < height; row++)
            {
                FilterRow(kernel, src, dst, width, row);
            }
        }
    }

    /// <summary>
    /// Filters the image vertically with replicated boundary samples.
    /// </summary>
    /// <param name="kernel">
    /// The odd-length, normalized convolution kernel.
    /// </param>
    /// <param name="src">
    /// The packed source samples; must not share the destination array.
    /// </param>
    /// <param name="dst">
    /// The destination buffer receiving the filtered samples.
    /// </param>
    /// <param name="width">The image width in pixels.</param>
    /// <param name="height">The image height in pixels.</param>
    /// <remarks>
    /// The sigma overload creates a kernel; the kernel overload reuses one.
    /// </remarks>
    internal static void VerticalConvolution(float[] kernel, float[] src,
        float[] dst, int width, int height)
    {
        Validate(src, dst, width, height);

        if ((long)width * height >= ParallelPixelThreshold && width > 1
            && Environment.ProcessorCount > 1)
        {
            VerticalParallel(kernel, src, dst, width, height);
        }
        else
        {
            for (int col = 0; col < width; col++)
            {
                FilterColumn(kernel, src, dst, width, height, col);
            }
        }
    }

    // Keep capturing delegates out of the small-image path.
    /// <summary>
    /// Distributes row filtering across worker threads.
    /// </summary>
    /// <param name="kernel">
    /// The odd-length, normalized convolution kernel.
    /// </param>
    /// <param name="src">
    /// The packed source samples; must not share the destination array.
    /// </param>
    /// <param name="dst">
    /// The destination buffer receiving the filtered samples.
    /// </param>
    /// <param name="width">The image width in pixels.</param>
    /// <param name="height">The image height in pixels.</param>
    private static void HorizontalParallel(float[] kernel, float[] src,
        float[] dst, int width, int height)
    {
        Parallel.For(0, height, row => FilterRow(kernel, src, dst, width, row));
    }

    /// <summary>
    /// Distributes column filtering across worker threads.
    /// </summary>
    /// <param name="kernel">
    /// The odd-length, normalized convolution kernel.
    /// </param>
    /// <param name="src">
    /// The packed source samples; must not share the destination array.
    /// </param>
    /// <param name="dst">
    /// The destination buffer receiving the filtered samples.
    /// </param>
    /// <param name="width">The image width in pixels.</param>
    /// <param name="height">The image height in pixels.</param>
    private static void VerticalParallel(float[] kernel, float[] src,
        float[] dst, int width, int height)
    {
        Parallel.For(0, width, col => FilterColumn(kernel, src, dst, width,
            height, col));
    }

    /// <summary>
    /// Filters one row, separating interior samples from replicated borders.
    /// </summary>
    /// <param name="kernel">
    /// The odd-length, normalized convolution kernel.
    /// </param>
    /// <param name="src">
    /// The packed source samples; must not share the destination array.
    /// </param>
    /// <param name="dst">
    /// The destination buffer receiving the filtered samples.
    /// </param>
    /// <param name="width">The image width in pixels.</param>
    /// <param name="row">The zero-based image row.</param>
    private static void FilterRow(float[] kernel, float[] src, float[] dst,
        int width, int row)
    {
        int offset = row * width, radius = kernel.Length / 2;
        int leftEnd = Math.Min(radius, width);
        int rightStart = Math.Max(leftEnd, width - radius);

        for (int col = 0; col < leftEnd; col++)
        {
            FilterHorizontalBorder(kernel, src, dst, width, offset, col);
        }

        for (int col = leftEnd; col < rightStart; col++)
        {
            float sum = 0;
            int start = offset + col - radius;

            for (int k = 0; k < kernel.Length; k++)
            {
                sum += src[start + k] * kernel[k];
            }

            dst[offset + col] = sum;
        }

        for (int col = rightStart; col < width; col++)
        {
            FilterHorizontalBorder(kernel, src, dst, width, offset, col);
        }
    }

    /// <summary>
    /// Filters one boundary pixel with clamped horizontal neighbours.
    /// </summary>
    /// <param name="kernel">
    /// The odd-length, normalized convolution kernel.
    /// </param>
    /// <param name="src">
    /// The packed source samples; must not share the destination array.
    /// </param>
    /// <param name="dst">
    /// The destination buffer receiving the filtered samples.
    /// </param>
    /// <param name="width">The image width in pixels.</param>
    /// <param name="offset">The first sample index of the current row.</param>
    /// <param name="col">The zero-based image column.</param>
    private static void FilterHorizontalBorder(float[] kernel, float[] src,
        float[] dst, int width, int offset, int col)
    {
        int radius = kernel.Length / 2;
        float sum = 0;

        for (int k = -radius; k <= radius; k++)
        {
            sum += src[offset + Math.Clamp(col + k, 0,
                width - 1)] * kernel[k + radius];
        }

        dst[offset + col] = sum;
    }

    /// <summary>
    /// Filters one column, separating interior samples from replicated borders.
    /// </summary>
    /// <param name="kernel">
    /// The odd-length, normalized convolution kernel.
    /// </param>
    /// <param name="src">
    /// The packed source samples; must not share the destination array.
    /// </param>
    /// <param name="dst">
    /// The destination buffer receiving the filtered samples.
    /// </param>
    /// <param name="width">The image width in pixels.</param>
    /// <param name="height">The image height in pixels.</param>
    /// <param name="col">The zero-based image column.</param>
    private static void FilterColumn(float[] kernel, float[] src, float[] dst,
        int width, int height, int col)
    {
        int radius = kernel.Length / 2;
        int topEnd = Math.Min(radius, height);
        int bottomStart = Math.Max(topEnd, height - radius);

        for (int row = 0; row < topEnd; row++)
        {
            FilterVerticalBorder(kernel, src, dst, width, height, col, row);
        }

        for (int row = topEnd; row < bottomStart; row++)
        {
            float sum = 0;
            int start = ((row - radius) * width) + col;

            for (int k = 0; k < kernel.Length; k++)
            {
                sum += src[start + (k * width)] * kernel[k];
            }

            dst[(row * width) + col] = sum;
        }

        for (int row = bottomStart; row < height; row++)
        {
            FilterVerticalBorder(kernel, src, dst, width, height, col, row);
        }
    }

    /// <summary>
    /// Filters one boundary pixel with clamped vertical neighbours.
    /// </summary>
    /// <param name="kernel">
    /// The odd-length, normalized convolution kernel.
    /// </param>
    /// <param name="src">
    /// The packed source samples; must not share the destination array.
    /// </param>
    /// <param name="dst">
    /// The destination buffer receiving the filtered samples.
    /// </param>
    /// <param name="width">The image width in pixels.</param>
    /// <param name="height">The image height in pixels.</param>
    /// <param name="col">The zero-based image column.</param>
    /// <param name="row">The zero-based image row.</param>
    private static void FilterVerticalBorder(float[] kernel, float[] src,
        float[] dst, int width, int height, int col, int row)
    {
        int radius = kernel.Length / 2;
        float sum = 0;

        for (int k = -radius; k <= radius; k++)
        {
            sum += src[(Math.Clamp(row + k, 0,
                height - 1) * width) + col] * kernel[k + radius];
        }

        dst[(row * width) + col] = sum;
    }

    /// <summary>
    /// Checks positive dimensions, sufficient storage and distinct buffers.
    /// </summary>
    /// <param name="src">
    /// The packed source samples; must not share the destination array.
    /// </param>
    /// <param name="dst">
    /// The destination buffer receiving the filtered samples.
    /// </param>
    /// <param name="width">The image width in pixels.</param>
    /// <param name="height">The image height in pixels.</param>
    private static void Validate(float[] src, float[] dst, int width,
        int height)
    {
        ArgumentNullException.ThrowIfNull(src);
        ArgumentNullException.ThrowIfNull(dst);

        if (width <= 0 || height <= 0 || (long)width * height > src.Length
            || (long)width * height > dst.Length)
        {
            throw new ArgumentException("Positive dimensions and sufficiently "
                + "large buffers are " + "required.");
        }

        if (ReferenceEquals(src, dst))
        {
            throw new ArgumentException("Convolution requires distinct source "
                + "and destination " + "buffers.");
        }
    }

    /// <summary>
    /// Builds a normalized Gaussian kernel with radius ceil(3 * sigma).
    /// </summary>
    /// <param name="sigma">
    /// The finite standard deviation, greater than zero and at most 640.
    /// </param>
    /// <returns>
    /// The odd-length kernel whose coefficients sum approximately to one.
    /// </returns>
    public static float[] CreateGaussianKernel(float sigma)
    {
        if (!float.IsFinite(sigma) || sigma <= 0
            || sigma > Models.YUVImage.MAX_SIZE)
        {
            throw new ArgumentOutOfRangeException(nameof(sigma));
        }

        int radius = (int)Math.Ceiling(3 * sigma);
        float[] kernel = new float[(2 * radius) + 1];
        double sum = 0, twoSigmaSq = 2.0 * sigma * sigma;

        for (int i = 0; i < kernel.Length; i++)
        {
            int x = i - radius;
            double value = Math.Exp(-(x * x) / twoSigmaSq);
            kernel[i] = (float)value;
            sum += value;
        }

        for (int i = 0; i < kernel.Length; i++)
        {
            kernel[i] /= (float)sum;
        }

        return kernel;
    }
}

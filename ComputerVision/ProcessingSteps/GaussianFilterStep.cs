using ComputerVision.Helpers;
using ComputerVision.Models;
using ComputerVision.Pipeline;

namespace ComputerVision.ProcessingSteps;

/// <summary>
/// Creates a spatial Gaussian stage with a reusable kernel.
/// </summary>
public sealed class GaussianFilterStep: IPipelineStep
{

    private readonly float[] _kernel;
    private float[]? _srcBuffer;
    private float[]? _tmpBuffer;
    private float[]? _dstBuffer;

    /// <summary>
    /// Creates a spatial Gaussian stage with a reusable kernel.
    /// </summary>
    /// <param name="sigma">
    /// The Gaussian standard deviation in image pixels.
    /// </param>
    public GaussianFilterStep(float sigma)
    {
        _kernel = GaussianFilterSeparableParallelHelper
            .CreateGaussianKernel(sigma);
    }

    public string Name => "Gaussian filter separable";
    public string NameAbb => "GaussFilt";

    /// <summary>
    /// Smooths the Y plane using reusable buffers and separable convolution.
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

        YUVImage image = context.Image;
        byte[] bytes = image.Bytes;
        int width = image.Width;
        int height = image.Height;
        int size = width * height;
        EnsureBuffers(size);
        float[] src = _srcBuffer!;
        float[] tmp = _tmpBuffer!;
        float[] dst = _dstBuffer!;

        for (int i = 0; i < size; i++)
        {
            src[i] = bytes[i];
        }

        GaussianFilterSeparableParallelHelper.HorizontalConvolution(_kernel,
            src, tmp, width, height);
        GaussianFilterSeparableParallelHelper.VerticalConvolution(_kernel, tmp,
            dst, width, height);

        for (int i = 0; i < size; i++)
        {
            float val = dst[i];

            if (val < 0f)
            {
                val = 0f;
            }
            else if (val > 255f)
            {
                val = 255f;
            }

            bytes[i] = (byte)(val + 0.5f);
        }
    }

    /// <summary>
    /// Allocates scratch buffers only when the required pixel count changes.
    /// </summary>
    /// <param name="size">
    /// The number of pixels for which scratch buffers are required.
    /// </param>
    private void EnsureBuffers(int size)
    {
        if (_srcBuffer == null || _srcBuffer.Length != size)
        {
            _srcBuffer = new float[size];
        }

        if (_tmpBuffer == null || _tmpBuffer.Length != size)
        {
            _tmpBuffer = new float[size];
        }

        if (_dstBuffer == null || _dstBuffer.Length != size)
        {
            _dstBuffer = new float[size];
        }
    }
}

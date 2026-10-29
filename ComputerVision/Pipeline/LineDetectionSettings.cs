using ComputerVision.Models;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace ComputerVision.Pipeline;

/// <summary>
/// Reusable settings for the line-detection stages used by FRIXbotV4. Defaults
/// originate in FRIXbotV4/appsettings.json (2026-09-11). Camera acquisition and
/// robot control settings belong to the host application.
/// </summary>
public sealed record LineDetectionSettings
{
    [Display(Name = "Resize image", GroupName = "Resize")]
    public bool ResizeImage
    {
        get; set;
    }

    [Display(Name = "Width (pixels)", GroupName = "Resize"), Range(16, 640)]
    public int ImageResizeWidth { get; set; } = 128;

    [Display(Name = "Height (pixels)", GroupName = "Resize"), Range(16, 640)]
    public int ImageResizeHeight { get; set; } = 96;

    [Display(Name = "Gamma correction", GroupName = "Gamma")]
    public bool GammaCorrection { get; set; } = true;

    [Display(Name = "Gamma", GroupName = "Gamma"), Range(.25, 2.0)]
    public float Gamma { get; set; } = .5f;

    [Display(Name = "Filter image", GroupName = "Gaussian")]
    public bool GaussianFilterImage
    {
        get; set;
    }

    [Display(Name = "Image sigma (pixels)", GroupName = "Gaussian"), Range(.1,
        20.0)]
    public float GaussianFilterImageSigma { get; set; } = 3;

    [Display(Name = "Filter histogram", GroupName = "Histogram")]
    public bool GaussianFilterHistogram { get; set; } = true;

    [Display(Name = "Histogram sigma (intensity levels)",
        GroupName = "Histogram"), Range(.1, 20.0)]
    public float GaussianFilterHistogramSigma { get; set; } = 5;

    [Display(Name = "Otsu threshold", GroupName = "Threshold")]
    public bool OtsuThreshold { get; set; } = true;

    [Display(Name = "Background margin (intensity levels)",
        GroupName = "Threshold"), Range(0, 255)]
    public int OtsuThresholdBackgroundMargin { get; set; } = 40;

    [Display(Name = "Detect line centres", GroupName = "Detection")]
    public bool SobelEdge { get; set; } = true;

    [Display(Name = "Adaptive edge detection", GroupName = "Detection")]
    public bool AdaptiveSobelEdge { get; set; } = true;

    [Display(Name = "Sample rows", GroupName = "Detection"), Range(1, 640)]
    public int SobelEdgeNumberOfPoints { get; set; } = 10;

    [Display(Name = "Fit detected line (image only)", GroupName = "Detection")]
    public bool FitBezierCubicCurve
    {
        get; set;
    }

    [Display(Name = "Calculate offset and angle", GroupName = "Detection")]
    public bool OffsetAndAngleStep { get; set; } = true;

    [Display(Name = "Apply homography to detected points",
        GroupName = "Homography")]
    public bool ApplyHomography
    {
        get; set;
    }

    [Display(Name = "Fit ground-plane Bezier", GroupName = "Homography")]
    public bool FitGroundBezier { get; set; } = true;

    [Display(Name = "Lateral minimum (cm)", GroupName = "Observation mapping")]
    public float FOWXmin { get; set; } = -3.25f;

    [Display(Name = "Lateral maximum (cm)", GroupName = "Observation mapping")]
    public float FOWXmax { get; set; } = 3.25f;

    [Display(Name = "Forward minimum (cm)", GroupName = "Observation mapping")]
    public float FOWYmin
    {
        get; set;
    }

    [Display(Name = "Forward maximum (cm)", GroupName = "Observation mapping")]
    public float FOWYmax { get; set; } = 4.6f;

    /// <summary>
    /// Checks configured ranges, finite values and observation-map bounds.
    /// </summary>
    public void Validate()
    {
        Validator.ValidateObject(this, new ValidationContext(this),
            validateAllProperties: true);

        foreach (PropertyInfo property in GetType().GetProperties())
        {
            if (property.GetValue(this) is float value && !float.IsFinite(
                value))
            {
                throw new ValidationException(
                    $"{property.Name} must be finite.");
            }
        }

        if (FOWXmin >= FOWXmax || FOWYmin >= FOWYmax)
        {
            throw new ValidationException(
                "Observation mapping minima must be less than maxima.");
        }
    }

    /// <summary>
    /// Validates capture size, downsampling and row count for these settings.
    /// </summary>
    /// <param name="sourceWidth">The original capture width in pixels.</param>
    /// <param name="sourceHeight">
    /// The original capture height in pixels.
    /// </param>
    /// <returns>
    /// The width and height after the optional resize.
    /// </returns>
    public (int Width, int Height) GetOutputSize(int sourceWidth,
        int sourceHeight)
    {
        Validate();

        if (sourceWidth < YUVImage.MIN_SIZE || sourceWidth > YUVImage.MAX_SIZE
            || sourceHeight < YUVImage.MIN_SIZE
            || sourceHeight > YUVImage.MAX_SIZE)
        {
            throw new ValidationException(
                $"Camera dimensions must be {YUVImage.MIN_SIZE}–" +
                $"{YUVImage.MAX_SIZE} pixels.");
        }

        // ResizeBilinearStep only downsamples. Reject a mixed up/down resize
        // rather
        // than accepting dimensions that imply an unintended aspect ratio
        // change.
        if (ResizeImage && (ImageResizeWidth > sourceWidth
            || ImageResizeHeight > sourceHeight))
        {
            throw new ValidationException(
                "Resize dimensions must not exceed the camera dimensions.");
        }

        (int, int) size = ResizeImage ? (ImageResizeWidth,
            ImageResizeHeight) : (sourceWidth, sourceHeight);

        if (SobelEdge && SobelEdgeNumberOfPoints > size.Item2)
        {
            throw new ValidationException(
                "Sample rows must not exceed the processed image height.");
        }

        return size;
    }
}

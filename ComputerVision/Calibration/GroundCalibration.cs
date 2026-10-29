using System.Numerics;

namespace ComputerVision.Calibration;

/// <summary>
/// Immutable measured pixel-to-plane calibration. The host validates camera
/// mount/lens identity.
/// </summary>
public sealed class GroundCalibration
{
    private readonly Vector2[] _outline;
    public PlanarHomography Mapping
    {
        get;
    }

    public int SourceWidth
    {
        get;
    }

    public int SourceHeight
    {
        get;
    }

    public string CoordinateFrame
    {
        get;
    }

    public string PlaneCoordinateFrame
    {
        get;
    }

    public GroundPlanePose? PlaneToRobot
    {
        get;
    }

    public GroundProjectionLimits ProjectionLimits
    {
        get;
    }

    /// <summary>
    /// Validates and stores measured pixel-to-plane calibration.
    /// </summary>
    /// <param name="mapping">
    /// The immutable mapping from source pixels to plane centimetres.
    /// </param>
    /// <param name="sourceWidth">The original capture width in pixels.</param>
    /// <param name="sourceHeight">
    /// The original capture height in pixels.
    /// </param>
    /// <param name="clockwisePixelOutline">
    /// Four convex calibration corners in clockwise image order.
    /// </param>
    /// <param name="coordinateFrame">
    /// The explicit name of the measured plane coordinate frame.
    /// </param>
    /// <param name="planeToRobot">
    /// Optional measured placement relative to the robot axle.
    /// </param>
    /// <param name="projectionLimits">
    /// Optional distance and horizon limits for valid projections.
    /// </param>
    public GroundCalibration(PlanarHomography mapping, int sourceWidth,
        int sourceHeight, IReadOnlyList<Vector2> clockwisePixelOutline,
        string coordinateFrame, GroundPlanePose? planeToRobot = null,
        GroundProjectionLimits? projectionLimits = null)
    {
        ArgumentNullException.ThrowIfNull(mapping);
        ArgumentException.ThrowIfNullOrWhiteSpace(coordinateFrame);

        if (sourceWidth < 2 || sourceHeight < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceWidth));
        }

        if (clockwisePixelOutline.Count != 4)
        {
            throw new ArgumentException(
                "Four convex outline corners are required.");
        }

        _outline = clockwisePixelOutline.ToArray();

        for (int i = 0; i < 4; i++)
        {
            Vector2 p = _outline[i];

            if (!float.IsFinite(p.X) || !float.IsFinite(p.Y) || p.X < 0
                || p.Y < 0 || p.X >= sourceWidth || p.Y >= sourceHeight)
            {
                throw new ArgumentException(
                    "Calibration outline must be inside the source image.");
            }

            if (Cross(_outline[(i + 1) % 4] - p,
                _outline[(i + 2) % 4] - _outline[(i + 1) % 4]) <= 0)
            {
                throw new ArgumentException(
                    "Calibration outline must be convex and " +
                    "clockwise in image " + "coordinates.");
            }
        }

        // An invertible homography still has a horizon. The trusted convex
        // region must not cross it.
        if (!mapping.IsOnSameFinitePlane(_outline))
        {
            throw new ArgumentException(
                "Calibration outline crosses the projective horizon.");
        }

        Mapping = mapping;
        SourceWidth = sourceWidth;
        SourceHeight = sourceHeight;
        planeToRobot?.Validate();
        PlaneToRobot = planeToRobot;
        ProjectionLimits = projectionLimits ?? new();
        ProjectionLimits.Validate();
        PlaneCoordinateFrame = coordinateFrame;
        CoordinateFrame = planeToRobot is null ? coordinateFrame :
            GroundPlanePose.RobotCoordinateFrame;
    }

    /// <summary>
    /// Maps a pixel to the plane and optionally into the robot axle frame.
    /// </summary>
    /// <param name="pixel">
    /// The position in original, unresized image pixels.
    /// </param>
    /// <param name="point">
    /// Receives the output point in centimetres; use only on success.
    /// </param>
    /// <returns>
    /// True if the resulting coordinates are finite.
    /// </returns>
    /// <remarks>
    /// This transform does not enforce source bounds or projection limits.
    /// </remarks>
    public bool TryMap(Vector2 pixel, out Vector2 point)
    {
        if (!Mapping.TryMap(pixel, out point))
        {
            return false;
        }

        if (PlaneToRobot is { } pose)
        {
            point = pose.ToRobot(point);
        }

        return float.IsFinite(point.X) && float.IsFinite(point.Y);
    }

    /// <summary>
    /// Accepts a projection within source, horizon and output-range limits.
    /// </summary>
    /// <param name="pixel">
    /// The position in original, unresized image pixels.
    /// </param>
    /// <param name="point">
    /// Receives the usable projected point in output-frame centimetres.
    /// </param>
    /// <returns>
    /// True if the point is usable; false leaves the output at its default.
    /// </returns>
    public bool TryProject(Vector2 pixel, out Vector2 point)
    {
        point = default;

        if (pixel.X < 0 || pixel.X >= SourceWidth || pixel.Y < 0
            || pixel.Y >= SourceHeight || !Mapping.IsOnUsablePlane(pixel,
            _outline[0], ProjectionLimits.HorizonMarginPixels) || !TryMap(pixel,
            out Vector2 mapped))
        {
            return false;
        }

        double squared =
            ((double)mapped.X * mapped.X) + ((double)mapped.Y * mapped
            .Y);

        if (squared > ProjectionLimits.MaximumDistanceCm * ProjectionLimits
            .MaximumDistanceCm)
        {
            return false;
        }

        point = mapped;

        return true;
    }

    /// <summary>
    /// Tests membership of the measured calibration quadrilateral.
    /// </summary>
    /// <param name="pixel">
    /// The position in original, unresized image pixels.
    /// </param>
    /// <returns>
    /// True for a point inside the outline, with the existing edge tolerance.
    /// </returns>
    /// <remarks>
    /// This is a reference-outline check, not the detector's valid-region mask.
    /// </remarks>
    public bool Contains(Vector2 pixel)
    {
        if (!float.IsFinite(pixel.X) || !float.IsFinite(pixel.Y))
        {
            return false;
        }

        for (int i = 0; i < 4; i++)
        {
            if (Cross(_outline[(i + 1) % 4] - _outline[i],
                pixel - _outline[i]) < -.01f)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Calculates the signed two-dimensional cross product.
    /// </summary>
    /// <param name="a">The first vector.</param>
    /// <param name="b">The second vector.</param>
    /// <returns>
    /// The scalar a.X * b.Y - a.Y * b.X.
    /// </returns>
    private static float Cross(Vector2 a, Vector2 b)
    {
        return (a.X * b.Y) - (a.Y * b.X);
    }
}

/// <summary>
/// Usable projection region, independent of calibration board size. Values use
/// cm/source pixels.
/// </summary>
public sealed record GroundProjectionLimits
{
    public double MaximumDistanceCm { get; init; } = 100;
    public double HorizonMarginPixels { get; init; } = 1;

    /// <summary>
    /// Rejects nonfinite limits, nonpositive range or negative horizon margin.
    /// </summary>
    public void Validate()
    {
        if (!double.IsFinite(MaximumDistanceCm) || MaximumDistanceCm <= 0
            || !double.IsFinite(HorizonMarginPixels) || HorizonMarginPixels < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(
                GroundProjectionLimits));
        }
    }
}

public enum GroundProjectionStatus
{
    Disabled,
    MissingCalibration,
    SourceSizeMismatch,
    NoDetection,
    NoCalibratedPoints,
    Applied
}

/// <summary>
/// Stores selected ground samples and their source-detection counts.
/// </summary>
/// <param name="Points">
/// The copied, read-only samples in the named coordinate frame.
/// </param>
/// <param name="CoordinateFrame">
/// The explicit name of the output coordinate frame.
/// </param>
/// <param name="DetectedCount">
/// The total number of image-space detections before selection.
/// </param>
/// <param name="SelectedClusterCount">
/// The number of detections in the selected source cluster.
/// </param>
public sealed record GroundProjection(IReadOnlyList<Vector2> Points,
    string CoordinateFrame, int DetectedCount, int SelectedClusterCount);

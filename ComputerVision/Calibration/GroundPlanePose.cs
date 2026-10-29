using System.Numerics;

namespace ComputerVision.Calibration;

/// <summary>
/// Measured rigid placement of a metric calibration plane in the robot axle
/// frame.
/// </summary>
public sealed record GroundPlanePose
{
    public const string RobotCoordinateFrame = "robot-axle-X-forward-Y-left-cm";
    public required float ForwardCm
    {
        get; init;
    }

    public required float LeftCm
    {
        get; init;
    }

    /// <summary>
    /// Angle from robot +X to plane +X; positive turns toward robot +Y (left).
    /// </summary>
    public required float YawDegrees
    {
        get; init;
    }

    /// <summary>
    /// Rejects nonfinite measured translation or yaw values.
    /// </summary>
    public void Validate()
    {
        if (!float.IsFinite(ForwardCm) || !float.IsFinite(LeftCm)
            || !float.IsFinite(YawDegrees))
        {
            throw new ArgumentException("Board placement must contain finite "
                + "forward, left and yaw " + "measurements.");
        }
    }

    /// <summary>
    /// Rotates and translates a plane point into the robot axle frame.
    /// </summary>
    /// <param name="point">
    /// The point in the measured plane frame, in centimetres.
    /// </param>
    /// <returns>
    /// The robot-frame point in centimetres, with X forward and Y left.
    /// </returns>
    public Vector2 ToRobot(Vector2 point)
    {
        double radians = YawDegrees * (Math.PI / 180);
        double c = Math.Cos(radians), s = Math.Sin(radians);

        return new((float)(ForwardCm + (c * point.X) - (s * point.Y)),
            (float)(LeftCm + (s * point.X) + (c * point.Y)));
    }
}

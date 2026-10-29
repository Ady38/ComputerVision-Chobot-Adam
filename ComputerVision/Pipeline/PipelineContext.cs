using ComputerVision.GraphicalObjects;
using ComputerVision.Inputs;
using ComputerVision.Models;
using ComputerVision.Utils;
using MathNet.Numerics.LinearAlgebra;
using PointF = System.Drawing.PointF;

namespace ComputerVision.Pipeline;

public class PipelineContext
{

    // Capture size is retained before processing stages resize the image.
    public int SourceWidth
    {
        get; set;
    }

    public int SourceHeight
    {
        get; set;
    }

    public Calibration.GroundProjectionStatus GroundStatus
    {
        get; set;
    }

    public Calibration.GroundProjection? GroundPoints
    {
        get; set;
    }

    public System.Numerics.Vector2? GroundMean
    {
        get; set;
    }

    public Calibration.GroundBezier? GroundCurve
    {
        get; set;
    }

    public CoordTrans? CoordTrans
    {
        get; set;
    }

    public ImageLoadOptions? ImageLoadOptions
    {
        get; set;
    }

    public YUVImage? OriginalImage { get; set; } = null;
    public YUVImage? Image { get; set; } = null;
    public List<List<Point>>? CurvePointClusters { get; set; } = null;
    public List<Vector<float>>? CurvePointsWorld { get; set; } = null;
    public BezierCurve? BezierCurve
    {
        get; set;
    }

    public PointF? Offset { get; set; } = null;
    public float? HeadingAngle { get; set; } = null;
    public ImageProfilingResult? ProfilingResult
    {
        get; internal set;
    }
}

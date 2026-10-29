using ComputerVision.Calibration;
using ComputerVision.GraphicalObjects;
using ComputerVision.Models;
using ComputerVision.Pipeline;
using System.Numerics;
using Vector = MathNet.Numerics.LinearAlgebra.Vector<float>;

namespace ComputerVision.ProcessingSteps;

/// <summary>
/// Configures independent fitting of image and ground curves.
/// </summary>
public class BezierCurveFitStep: IPipelineStep
{
    private readonly bool _fitGroundCurve; private readonly bool _fitImageCurve;

    /// <summary>
    /// Configures independent fitting of image and ground curves.
    /// </summary>
    /// <param name="fitImageCurve">
    /// Whether to fit the legacy observation-space curve.
    /// </param>
    /// <param name="fitGroundCurve">
    /// Whether to fit a curve through calibrated ground points.
    /// </param>
    public BezierCurveFitStep(bool fitImageCurve = true,
        bool fitGroundCurve = true)
    {
        _fitGroundCurve = fitGroundCurve;
        _fitImageCurve = fitImageCurve;
    }

    public string Name => "Bezier Curve Fitting";
    public string NameAbb => "BezierFit";

    /// <summary>
    /// Fits requested curves and clears unavailable curve outputs.
    /// </summary>
    /// <param name="context">
    /// The frame context containing the image and stage results.
    /// </param>
    public void Execute(PipelineContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.GroundCurve = _fitGroundCurve
            && context.GroundPoints is { } ground ? GroundBezier.Fit(ground
            .Points) : null;
        context.BezierCurve = null;
        context.CurvePointsWorld = null;

        if (!_fitImageCurve || context.CoordTrans is not { } coordinates
            || context.CurvePointClusters is not { Count: > 0 } clusters)
        {
            return;
        }

        List<Point> points = clusters.MaxBy(c => c.Count)!;

        if (points.Count < 2)
        {
            return;
        }

        var world = points.Select(coordinates.FromUVtoXYVectorFloat).ToList();
        context.CurvePointsWorld = world;
        var fit = GroundBezier.Fit(world.Select(p => new Vector2(p[0],
            p[1])).ToArray());

        if (fit is null)
        {
            return;
        }

        context.BezierCurve = new BezierCurve([Position(fit.P0),
            Position(fit.P1), Position(fit.P2), Position(fit.P3)]);
    }

    /// <summary>
    /// Adapts a planar fitted control to the legacy homogeneous convention.
    /// </summary>
    /// <param name="p">The fitted two-dimensional control.</param>
    /// <returns>
    /// A MathNet vector containing X, Y and position weight one.
    /// </returns>
    private static Vector Position(Vector2 p)
    {

        // Every fitted control is a position with homogeneous weight one.
        return Vector.Build.DenseOfArray([p.X, p.Y, 1]);
    }
}

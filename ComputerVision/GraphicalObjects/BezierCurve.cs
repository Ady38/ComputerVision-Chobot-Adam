using ComputerVision.Utils;
using MathNet.Numerics.LinearAlgebra;

namespace ComputerVision.GraphicalObjects;

/// <summary>
/// Creates a legacy curve and calculates its display samples.
/// </summary>
public sealed class BezierCurve: CurveBase
{

    /// <summary>
    /// Creates a legacy curve and calculates its display samples.
    /// </summary>
    /// <param name="parControlPoints">
    /// The ordered control vectors retained by the curve.
    /// </param>
    public BezierCurve(List<Vector<float>> parControlPoints) : base(parControlPoints)
    {
    }

    /// <summary>
    /// Resamples the controls and refreshes segment and total lengths.
    /// </summary>
    protected override void RecalculateCurve()
    {

        // get curve points
        curvePoints = DeCasteljauAlgorithm.GetCurvePoints(controlPoints, curvePrecision);

        if (curvePoints == null)
        {
            return;
        }

        // calculate curve length
        segmentLengths = [];

        for (int i = 0; i < curvePoints.Count - 1; i++)
        {
            segmentLengths.Add(CoordTrans.CalculateDistanceBetweenPoints(
                curvePoints[i], curvePoints[i + 1]));
        }

        length = segmentLengths.Sum();
    }

    /// <summary>
    /// Evaluates the curve through the common De Casteljau implementation.
    /// </summary>
    /// <param name="time">
    /// The curve parameter; zero and one select its endpoints.
    /// </param>
    /// <param name="angle">
    /// Receives the tangent bearing in radians from positive X.
    /// </param>
    /// <returns>
    /// The legacy X/Y vector produced by the evaluator.
    /// </returns>
    public override Vector<float> GetPointAndAngleOnCurve(float time,
        out float angle)
    {
        return DeCasteljauAlgorithm.GetDeCasteljauPoint(controlPoints, time,
            out angle);
    }
}

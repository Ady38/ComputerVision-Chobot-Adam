using System.Numerics;
using MathNetVector = MathNet.Numerics.LinearAlgebra.Vector<float>;

namespace ComputerVision.GraphicalObjects;

/// <summary>
/// Evaluates planar Bezier curves with one De Casteljau interpolation loop.
/// </summary>
public static class DeCasteljauAlgorithm
{
    private const int StackPointLimit = 32;

    /// <summary>
    /// Samples a legacy curve at equally spaced parameter values.
    /// </summary>
    /// <param name="curve">The curve whose control points are sampled.</param>
    /// <param name="pointCount">The sample count, including both ends.</param>
    /// <returns>
    /// The sampled legacy vectors, or null for fewer than two controls.
    /// </returns>
    public static List<MathNetVector>? GetCurvePoints(BezierCurve? curve,
        int pointCount)
    {
        ArgumentNullException.ThrowIfNull(curve);

        return GetCurvePoints(curve.ControlPoints, pointCount);
    }

    /// <summary>
    /// Samples control points uniformly in parameter space, not arc length.
    /// </summary>
    /// <param name="bezierCurvePoints">
    /// Ordered legacy X/Y control vectors.
    /// </param>
    /// <param name="pointCount">
    /// At least two samples, including the ends.
    /// </param>
    /// <returns>
    /// The samples, or null when fewer than two controls exist.
    /// </returns>
    public static List<MathNetVector>? GetCurvePoints(List<MathNetVector>
        bezierCurvePoints, int pointCount)
    {
        if (pointCount < 2)
        {
            throw new ApplicationException(
                "Request at least two curve samples.");
        }

        if (bezierCurvePoints == null || bezierCurvePoints.Count < 2)
        {
            return null;
        }

        var result = new List<MathNetVector>(pointCount);

        for (int i = 0; i < pointCount; i++)
        {
            float time = i / (float)(pointCount - 1);
            result.Add(GetDeCasteljauPoint(bezierCurvePoints, time, out _));
        }

        return result;
    }

    /// <summary>
    /// Adapts legacy MathNet controls to the shared planar evaluator. The third
    /// output component remains zero for legacy compatibility.
    /// </summary>
    /// <param name="points">
    /// Ordered vectors with at least X and Y components.
    /// </param>
    /// <param name="time">The curve parameter; 0 and 1 select the ends.</param>
    /// <param name="angle">
    /// Receives the tangent bearing in radians from +X.
    /// </param>
    /// <returns>
    /// A legacy vector; a single control is returned unchanged.
    /// </returns>
    public static MathNetVector GetDeCasteljauPoint(List<MathNetVector> points,
        float time, out float angle)
    {
        ArgumentNullException.ThrowIfNull(points);

        if (points.Count == 0)
        {
            throw new ArgumentException("A control point is required.",
                nameof(points));
        }

        if (points.Count == 1)
        {
            angle = 0;

            return points[0];
        }

        // Typical cubic curves need four stack-resident working points.
        Span<Vector2> working = points.Count <= StackPointLimit ? stackalloc
            Vector2[points.Count] : new Vector2[points.Count];

        for (int i = 0; i < points.Count; i++)
        {
            working[i] = new Vector2(points[i][0], points[i][1]);
        }

        Vector2 point = EvaluateInPlace(working, time, out angle);

        return MathNetVector.Build.DenseOfArray([point.X, point.Y, 0]);
    }

    /// <summary>
    /// Evaluates a planar Bezier curve without changing its controls.
    /// Parameters outside 0 to 1 extrapolate the curve.
    /// </summary>
    /// <param name="points">One or more controls in their curve order.</param>
    /// <param name="time">The parameter, normally between 0 and 1.</param>
    /// <param name="angle">
    /// Receives the tangent bearing in radians from +X; zero for one control or
    /// a zero tangent.
    /// </param>
    /// <returns>
    /// The interpolated or extrapolated two-dimensional point.
    /// </returns>
    public static Vector2 Evaluate(ReadOnlySpan<Vector2> points, float time,
        out float angle)
    {
        if (points.IsEmpty)
        {
            throw new ArgumentException("A control point is required.",
                nameof(points));
        }

        // Bound stack usage while supporting curves of higher degree.
        Span<Vector2> working = points.Length <= StackPointLimit ? stackalloc
            Vector2[points.Length] : new Vector2[points.Length];
        points.CopyTo(working);

        return EvaluateInPlace(working, time, out angle);
    }

    /// <summary>
    /// Reduces a working polygon by successive linear interpolation.
    /// </summary>
    /// <param name="working">
    /// Nonempty scratch points, overwritten in place.
    /// </param>
    /// <param name="time">The interpolation or extrapolation parameter.</param>
    /// <param name="angle">
    /// Receives the tangent bearing in radians from +X.
    /// </param>
    /// <returns>
    /// The final point of the De Casteljau construction.
    /// </returns>
    private static Vector2 EvaluateInPlace(Span<Vector2> working, float time,
        out float angle)
    {
        angle = 0;

        for (int count = working.Length; count > 1; count--)
        {

            // The final segment has the direction of the curve derivative.
            if (count == 2)
            {
                Vector2 tangent = working[1] - working[0];
                angle = (float)Math.Atan2(tangent.Y, tangent.X);
            }

            for (int i = 0; i < count - 1; i++)
            {
                working[i] = Vector2.Lerp(working[i], working[i + 1], time);
            }
        }

        return working[0];
    }
}

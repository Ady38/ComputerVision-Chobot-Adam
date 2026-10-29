using ComputerVision.GraphicalObjects;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Factorization;
using System.Numerics;

namespace ComputerVision.Calibration;

/// <summary>
/// Stores a diagnostic ground-plane cubic in centimetres.
/// </summary>
/// <param name="P0">The first cubic endpoint in centimetres.</param>
/// <param name="P1">The first interior control in centimetres.</param>
/// <param name="P2">The second interior control in centimetres.</param>
/// <param name="P3">The final cubic endpoint in centimetres.</param>
public sealed record GroundBezier(Vector2 P0, Vector2 P1, Vector2 P2,
    Vector2 P3)
{

    /// <summary>
    /// Evaluates the cubic through the shared De Casteljau implementation.
    /// </summary>
    /// <param name="t">
    /// The parameter; values outside zero to one extrapolate the curve.
    /// </param>
    /// <returns>
    /// The curve point in the same frame as its controls.
    /// </returns>
    public Vector2 Evaluate(float t)
    {
        ReadOnlySpan<Vector2> controls = [P0, P1, P2, P3];

        return DeCasteljauAlgorithm.Evaluate(controls, t, out _);
    }

    /// <summary>
    /// Fits a cubic with fixed endpoints and chord-length parameters.
    /// </summary>
    /// <param name="points">Ordered finite plane points in centimetres.</param>
    /// <returns>
    /// The fitted curve, or null if usable distinct samples are insufficient.
    /// </returns>
    /// <remarks>
    /// Two points form a straight cubic. Three are interpolated via a
    /// quadratic.
    /// </remarks>
    public static GroundBezier? Fit(IReadOnlyList<Vector2> points)
    {
        if (points.Any(p => !float.IsFinite(p.X) || !float.IsFinite(p.Y)))
        {
            throw new ArgumentException("Curve samples must be finite.");
        }

        // Consecutive duplicate detections contribute to the mean, but add no
        // fit constraint.
        Vector2[] p = points.Where((v,
            i) => i == 0 || v != points[i - 1]).ToArray();

        if (p.Length < 2)
        {
            return null;
        }

        Vector2 start = p[0];
        Vector2 end = p[^1];

        if (p.Length == 2)
        {
            return CreateStraight(start, end);
        }

        double[] distance = new double[p.Length];

        for (int i = 1; i < p.Length; i++)
        {
            distance[i] = distance[i - 1] + Vector2.Distance(p[i - 1], p[i]);
        }

        double length = distance[^1];

        if (!double.IsFinite(length) || length <= 1e-9)
        {
            return null;
        }

        if (p.Length == 3)
        {

            // Interpolate the middle sample with a quadratic, then elevate it
            // to a cubic.
            float t = (float)(distance[1] / length), u = 1 - t;

            if (t <= 1e-6f || u <= 1e-6f)
            {
                return CreateStraight(start, end);
            }

            Vector2 q = (p[1] - (u * u * start) - (t * t * end)) / (2 * u * t);

            return new(start, Vector2.Lerp(start, q, 2f / 3), Vector2.Lerp(end,
                q, 2f / 3), end);
        }

        // Fixed endpoints, chord-length parameters, least-squares interior
        // controls.
        Matrix<double> a = Matrix<double>.Build.Dense(p.Length - 2, 2);
        Matrix<double> b = Matrix<double>.Build.Dense(p.Length - 2, 2);

        for (int i = 1; i < p.Length - 1; i++)
        {
            double t = distance[i] / length, u = 1 - t;
            a[i - 1, 0] = 3 * u * u * t;
            a[i - 1, 1] = 3 * u * t * t;
            b[i - 1, 0] = p[i].X - (u * u * u * start.X) - (t * t * t * end.X);
            b[i - 1, 1] = p[i].Y - (u * u * u * start.Y) - (t * t * t * end.Y);
        }

        Svd<double> svd = a.Svd(true);

        if (svd.S[1] <= svd.S[0] * 1e-10)
        {
            return CreateStraight(start, end);
        }

        Matrix<double> controls = svd.Solve(b);
        var p1 = new Vector2((float)controls[0, 0], (float)controls[0, 1]);
        var p2 = new Vector2((float)controls[1, 0], (float)controls[1, 1]);

        if (!float.IsFinite(p1.X) || !float.IsFinite(p1.Y)
            || !float.IsFinite(p2.X) || !float.IsFinite(p2.Y))
        {
            return null;
        }

        return new(start, p1, p2, end);
    }

    /// <summary>
    /// Places cubic controls uniformly along a straight segment.
    /// </summary>
    /// <param name="start">
    /// The first endpoint in the curve's coordinate frame.
    /// </param>
    /// <param name="end">
    /// The last endpoint in the curve's coordinate frame.
    /// </param>
    /// <returns>
    /// The straight cubic joining the endpoints.
    /// </returns>
    private static GroundBezier CreateStraight(Vector2 start, Vector2 end)
    {
        return new(start, Vector2.Lerp(start, end, 1f / 3), Vector2.Lerp(start,
            end, 2f / 3), end);
    }
}

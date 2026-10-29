using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Factorization;
using System.Numerics;

namespace ComputerVision.Calibration;

/// <summary>
/// Pairs a measured image pixel with its ground-plane position.
/// </summary>
/// <param name="Pixel">
/// The measured position in raw source-image pixels.
/// </param>
/// <param name="GroundCm">
/// The corresponding plane position in centimetres.
/// </param>
public readonly record struct PlaneCorrespondence(Vector2 Pixel,
    Vector2 GroundCm);

/// <summary>
/// Pixel-to-plane mapping fitted from measured correspondences. No camera or
/// renderer geometry.
/// </summary>
public sealed class PlanarHomography
{
    private readonly Matrix<double> _matrix;

    /// <summary>
    /// Retains a validated, normalized homography matrix.
    /// </summary>
    /// <param name="matrix">
    /// The validated matrix owned by this mapping instance.
    /// </param>
    private PlanarHomography(Matrix<double> matrix)
    {
        _matrix = matrix;
    }

    /// <summary>
    /// Copies and normalizes a finite, nonsingular 3 by 3 homography.
    /// </summary>
    /// <param name="values">
    /// A 3 by 3 array of finite homography coefficients.
    /// </param>
    /// <returns>
    /// An independent mapping containing the validated coefficients.
    /// </returns>
    public static PlanarHomography FromMatrix(double[][] values)
    {
        if (values is not { Length: 3 }
            || values.Any(row => row is not { Length: 3 }
            || row.Any(v => !double.IsFinite(v))))
        {
            throw new ArgumentException(
                "Homography matrix must contain nine finite values.");
        }

        Matrix<double> h = Matrix<double>.Build.Dense(3, 3, (r,
            c) => values[r][c]);
        double norm = h.FrobeniusNorm();

        if (!double.IsFinite(norm) || norm == 0)
        {
            throw new ArgumentException("Homography matrix is invalid.");
        }

        h /= norm;
        MathNet.Numerics.LinearAlgebra.Vector<double> s = h.Svd(false).S;

        if (s[2] <= s[0] * 1e-12)
        {
            throw new ArgumentException(
                "Homography matrix is singular or ill-conditioned.");
        }

        return new(h);
    }

    /// <summary>
    /// Checks that all reference points lie on one finite horizon side.
    /// </summary>
    /// <param name="points">The ordered two-dimensional input samples.</param>
    /// <returns>
    /// True if at least one point exists and every projection is usable.
    /// </returns>
    internal bool IsOnSameFinitePlane(IEnumerable<Vector2> points)
    {
        int sign = 0;

        foreach (Vector2 p in points)
        {
            if (!TryMap(p, out _))
            {
                return false;
            }

            int next = Math.Sign((_matrix[2, 0] * p.X) + (_matrix[2,
                1] * p.Y) + _matrix[2, 2]);

            if (sign != 0 && sign != next)
            {
                return false;
            }

            sign = next;
        }

        return sign != 0;
    }

    /// <summary>
    /// Checks the calibrated horizon side and its source-pixel margin.
    /// </summary>
    /// <param name="pixel">
    /// The position in original, unresized image pixels.
    /// </param>
    /// <param name="reference">
    /// A known point on the usable side of the projective horizon.
    /// </param>
    /// <param name="horizonMarginPixels">
    /// The minimum distance to the horizon in source pixels.
    /// </param>
    /// <returns>
    /// True when the pixel is finite, on the correct side and far enough away.
    /// </returns>
    internal bool IsOnUsablePlane(Vector2 pixel, Vector2 reference,
        double horizonMarginPixels)
    {
        if (!float.IsFinite(pixel.X) || !float.IsFinite(pixel.Y))
        {
            return false;
        }

        double w = (_matrix[2, 0] * pixel.X) + (_matrix[2,
            1] * pixel.Y) + _matrix[2,
            2];
        double referenceW = (_matrix[2, 0] * reference.X) + (_matrix[2,
            1] * reference.Y) + _matrix[2, 2];

        if (w == 0 || Math.Sign(w) != Math.Sign(referenceW))
        {
            return false;
        }

        double normalLength = Math.Sqrt((_matrix[2, 0] * _matrix[2,
            0]) + (_matrix[2,
            1] * _matrix[2, 1]));

        // Affine maps have no finite image horizon.
        return normalLength == 0
            || Math.Abs(w) / normalLength >= horizonMarginPixels;
    }

    /// <summary>
    /// Fits a homography using normalized direct linear transformation.
    /// </summary>
    /// <param name="points">
    /// At least four measured, nondegenerate pixel-to-plane correspondences.
    /// </param>
    /// <returns>
    /// The fitted mapping from raw image pixels to plane centimetres.
    /// </returns>
    public static PlanarHomography Fit(IReadOnlyList<PlaneCorrespondence>
        points)
    {
        if (points.Count < 4)
        {
            throw new ArgumentException(
                "Select at least four non-collinear correspondences.");
        }

        Vector2[] pixels = points.Select(p => p.Pixel).ToArray();
        Vector2[] ground = points.Select(p => p.GroundCm).ToArray();
        Matrix<double> source = Normalize(pixels);
        Matrix<double> destination = Normalize(ground);

        // Normalized direct linear transform: A h = 0. The last right singular
        // vector is h.
        Matrix<double> a = Matrix<double>.Build.Dense(points.Count * 2, 9);

        for (int i = 0; i < points.Count; i++)
        {
            double u = (source[0, 0] * pixels[i].X) + source[0, 2];
            double v = (source[1, 1] * pixels[i].Y) + source[1, 2];
            double x = (destination[0, 0] * ground[i].X) + destination[0, 2];
            double y = (destination[1, 1] * ground[i].Y) + destination[1, 2];
            a.SetRow(2 * i, [-u, -v, -1, 0, 0, 0, x * u, x * v, x]);
            a.SetRow((2 * i) + 1, [0, 0, 0, -u, -v, -1, y * u, y * v, y]);
        }

        Svd<double> svd = a.Svd(true);

        if (svd.S[7] < svd.S[0] * 1e-10)
        {
            throw new ArgumentException("The selected points are degenerate. "
                + "Spread them across the " + "board.");
        }

        Matrix<double> normalized = Matrix<double>.Build.Dense(3, 3, (r,
            c) => svd.VT[8,
            (r * 3) + c]);

        if (Math.Abs(normalized.Determinant()) < 1e-12)
        {
            throw new ArgumentException(
                "The selected points cannot define an invertible mapping.");
        }

        Matrix<double> h = destination.Inverse() * normalized * source;
        h /= h.FrobeniusNorm();

        return new(h);
    }

    /// <summary>
    /// Applies the projective mapping and rejects a near-zero denominator.
    /// </summary>
    /// <param name="pixel">
    /// The position in original, unresized image pixels.
    /// </param>
    /// <param name="groundCm">
    /// Receives the mapped plane point in centimetres on success.
    /// </param>
    /// <returns>
    /// True when the projected plane coordinates are finite.
    /// </returns>
    public bool TryMap(Vector2 pixel, out Vector2 groundCm)
    {
        groundCm = default;

        if (!float.IsFinite(pixel.X) || !float.IsFinite(pixel.Y))
        {
            return false;
        }

        double w = (_matrix[2, 0] * pixel.X) + (_matrix[2,
            1] * pixel.Y) + _matrix[2,
            2];
        double scale = Math.Abs(_matrix[2, 0] * pixel.X) + Math.Abs(_matrix[2,
            1] * pixel.Y) + Math.Abs(_matrix[2, 2]);

        if (Math.Abs(w) <= 1e-10 * Math.Max(scale, 1e-15))
        {
            return false;
        }

        groundCm = new((float)(((_matrix[0, 0] * pixel.X) + (_matrix[0,
            1] * pixel.Y) + _matrix[0, 2]) / w), (float)(((_matrix[1,
            0] * pixel.X) + (_matrix[1, 1] * pixel.Y) + _matrix[1, 2]) / w));

        return float.IsFinite(groundCm.X) && float.IsFinite(groundCm.Y);
    }

    /// <summary>
    /// Builds a centering and scale transform for calibration fitting.
    /// </summary>
    /// <param name="points">Finite, noncoincident points to normalize.</param>
    /// <returns>
    /// A 3 by 3 matrix giving root-mean-square radius sqrt(2).
    /// </returns>
    private static Matrix<double> Normalize(Vector2[] points)
    {
        if (points.Any(p => !float.IsFinite(p.X) || !float.IsFinite(p.Y)))
        {
            throw new ArgumentException(
                "Calibration coordinates must be finite.");
        }

        double x = points.Average(p => (double)p.X),
            y = points.Average(p => (double)p.Y);
        double rms = Math.Sqrt(points.Average(p => Math.Pow(p.X - x,
            2) + Math.Pow(p.Y - y, 2)));

        if (rms < 1e-9)
        {
            throw new ArgumentException("Calibration points must be distinct.");
        }

        double s = Math.Sqrt(2) / rms;

        return Matrix<double>.Build.DenseOfArray(new[,] { { s, 0, -s * x },
            { 0, s, -s * y }, { 0, 0, 1d } });
    }
}

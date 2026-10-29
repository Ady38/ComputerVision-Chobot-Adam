using MathNet.Numerics.LinearAlgebra;
using System.Drawing;
using CVPoint = ComputerVision.Models.Point;

namespace ComputerVision.Utils;

/// <summary>
/// Configures a linear mapping between pixel and observation rectangles.
/// </summary>
public class CoordTrans
{

    /// <summary>
    /// Configures a linear mapping between pixel and observation rectangles.
    /// </summary>
    /// <param name="parUmin">
    /// The image coordinate mapped to the minimum X value.
    /// </param>
    /// <param name="parUmax">
    /// The image coordinate mapped to the maximum X value.
    /// </param>
    /// <param name="parVmin">
    /// The image coordinate mapped to the minimum Y value.
    /// </param>
    /// <param name="parVmax">
    /// The image coordinate mapped to the maximum Y value.
    /// </param>
    /// <param name="parXmin">
    /// The observation X value at the first horizontal endpoint.
    /// </param>
    /// <param name="parXmax">
    /// The observation X value at the second horizontal endpoint.
    /// </param>
    /// <param name="parYmin">
    /// The observation Y value at the first vertical endpoint.
    /// </param>
    /// <param name="parYmax">
    /// The observation Y value at the second vertical endpoint.
    /// </param>
    public CoordTrans(int parUmin, int parUmax, int parVmin, int parVmax,
        float parXmin, float parXmax, float parYmin, float parYmax)
    {
        Xmin = parXmin;
        Xmax = parXmax;
        Ymin = parYmin;
        Ymax = parYmax;
        Umin = parUmin;
        Umax = parUmax;
        Vmin = parVmin;
        Vmax = parVmax;
    }

    public float Xmin
    {
        get; set;
    }

    public float Xmax
    {
        get; set;
    }

    public float Ymin
    {
        get; set;
    }

    public float Ymax
    {
        get; set;
    }

    public float Umin
    {
        get; set;
    }

    public float Umax
    {
        get; set;
    }

    public float Vmin
    {
        get; set;
    }

    public float Vmax
    {
        get; set;
    }

    public float XRange => Math.Abs(Xmax - Xmin);

    public float YRange => Math.Abs(Ymax - Ymin);

    public float URange => Math.Abs(Umax - Umin);

    public float VRange => Math.Abs(Vmax - Vmin);

    /// <summary>
    /// Maps integer image coordinates to observation coordinates.
    /// </summary>
    /// <param name="p">The source point in image pixels.</param>
    /// <returns>
    /// The point in the configured observation rectangle.
    /// </returns>
    public PointF FromUVtoXY(Point p)
    {
        return new PointF(((p.X - Umin) / (float)(Umax - Umin) * (Xmax -
            Xmin)) +
            Xmin, ((p.Y - Vmin) / (float)(Vmax - Vmin) * (Ymax - Ymin)) + Ymin);
    }

    /// <summary>
    /// Maps observation coordinates to truncated integer image coordinates.
    /// </summary>
    /// <param name="p">The source point in observation coordinates.</param>
    /// <returns>
    /// The mapped pixel point.
    /// </returns>
    public Point FromXYtoUV(PointF p)
    {
        return new Point((int)(((p.X - Xmin) / (Xmax - Xmin) * (Umax - Umin)) +
            Umin), (int)(((p.Y - Ymin) / (Ymax - Ymin) * (Vmax - Vmin)) +
                Vmin));
    }

    /// <summary>
    /// Maps observation coordinates to fractional image coordinates.
    /// </summary>
    /// <param name="p">The source point in observation coordinates.</param>
    /// <returns>
    /// The mapped pixel point without integer truncation.
    /// </returns>
    public PointF FromXYtoUVF(PointF p)
    {
        return new PointF(((p.X - Xmin) / (Xmax - Xmin) * (Umax - Umin)) + Umin,
            ((p.Y - Ymin) / (Ymax - Ymin) * (Vmax - Vmin)) + Vmin);
    }

    /// <summary>
    /// Maps image pixels to a homogeneous observation-space vector.
    /// </summary>
    /// <param name="p">The source point in image pixels.</param>
    /// <returns>
    /// The mapped X and Y with homogeneous component one.
    /// </returns>
    public Vector<float> FromUVtoXYVectorFloat(CVPoint p)
    {
        Vector<float> v = Vector<float>.Build.Dense(3);
        v[0] = ((p.X - Umin) / (float)(Umax - Umin) * (Xmax - Xmin)) + Xmin;
        v[1] = ((p.Y - Vmin) / (float)(Vmax - Vmin) * (Ymax - Ymin)) + Ymin;
        v[2] = 1f;

        return v;
    }

    /// <summary>
    /// Maps a legacy observation vector to truncated image coordinates.
    /// </summary>
    /// <param name="p">
    /// The source observation vector; X and Y are its first two entries.
    /// </param>
    /// <returns>
    /// The mapped integer pixel point.
    /// </returns>
    public Point FromXYVectorFloatToUV(Vector<float> p)
    {
        return new Point((int)(((p[0] - Xmin) / (Xmax - Xmin) * (Umax - Umin)) +
            Umin), (int)(((p[1] - Ymin) / (Ymax - Ymin) * (Vmax - Vmin)) +
                Vmin));
    }

    /// <summary>
    /// Maps a legacy observation vector to fractional image coordinates.
    /// </summary>
    /// <param name="p">
    /// The source observation vector; X and Y are its first two entries.
    /// </param>
    /// <returns>
    /// The mapped fractional pixel point.
    /// </returns>
    public PointF FromXYVectorFloatToUVF(Vector<float> p)
    {
        return new PointF(((p[0] - Xmin) / (Xmax - Xmin) * (Umax - Umin)) +
            Umin,
            ((p[1] - Ymin) / (Ymax - Ymin) * (Vmax - Vmin)) + Vmin);
    }

    /// <summary>
    /// Validates a three-component observation vector and maps it to pixels.
    /// </summary>
    /// <param name="v">
    /// A three-component vector in observation coordinates.
    /// </param>
    /// <returns>
    /// The mapped fractional pixel point.
    /// </returns>
    public PointF GetUVF(Vector<float> v)
    {
        if (v == null || v.Count != 3)
        {
            throw new ApplicationException($"Wrong vertex data input!");
        }

        return new PointF((((float)v[0] - Xmin) / (Xmax - Xmin) * (Umax - Umin))
            + Umin, (((float)v[1] - Ymin) / (Ymax - Ymin) * (Vmax - Vmin)) +
            Vmin);
    }

    /// <summary>
    /// Maps an observation vector to image pixels using rounding.
    /// </summary>
    /// <param name="v">
    /// A three-component vector in observation coordinates.
    /// </param>
    /// <returns>
    /// The rounded integer pixel point.
    /// </returns>
    public Point GetUV(Vector<float> v)
    {
        PointF p = GetUVF(v);

        return new Point((int)Math.Round(p.X), (int)Math.Round(p.Y));
    }

    /// <summary>
    /// Adds a pixel offset after converting it to observation units.
    /// </summary>
    /// <param name="point">The observation vector to shift.</param>
    /// <param name="offset">The displacement in image pixels.</param>
    /// <returns>
    /// A shifted copy with homogeneous component one.
    /// </returns>
    public Vector<float> MatrixFWithPointOffset(Vector<float> point,
        Point offset)
    {
        float ratioX = (Umax - Umin) / (Xmax - Xmin);
        float ratioY = (Vmax - Vmin) / (Ymax - Ymin);
        Vector<float> v = Vector<float>.Build.Dense(3);
        v[0] = point[0] + (offset.X / ratioX);
        v[1] = point[1] + (offset.Y / ratioY);
        v[2] = 1f;

        return v;
    }

    /// <summary>
    /// Calculates the bearing from the reference point to the endpoint.
    /// </summary>
    /// <param name="refPoint">
    /// The reference point in observation coordinates.
    /// </param>
    /// <param name="endPoint">
    /// The other point in observation coordinates.
    /// </param>
    /// <returns>
    /// The angle in radians from the positive X axis.
    /// </returns>
    public static float GetAngleBetweenPoints(Vector<float> refPoint,
        Vector<float> endPoint)
    {
        float dY = endPoint[1] - refPoint[1];
        float dX = endPoint[0] - refPoint[0];

        return (float)Math.Atan2(dY, dX);
    }

    /// <summary>
    /// Calculates Euclidean distance using the first two components.
    /// </summary>
    /// <param name="point1">
    /// The first point; the first two vector components are used.
    /// </param>
    /// <param name="point2">
    /// The second point; the first two vector components are used.
    /// </param>
    /// <returns>
    /// The distance in the input coordinate units.
    /// </returns>
    public static float CalculateDistanceBetweenPoints(Vector<float> point1,
        Vector<float> point2)
    {
        float deltaX = Math.Abs(point1[0] - point2[0]);
        float deltaY = Math.Abs(point1[1] - point2[1]);

        return (float)Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
    }

    /// <summary>
    /// Calculates Euclidean distance from reference to endpoint.
    /// </summary>
    /// <param name="refPoint">
    /// The reference point in observation coordinates.
    /// </param>
    /// <param name="endPoint">
    /// The other point in observation coordinates.
    /// </param>
    /// <returns>
    /// The distance in the input coordinate units.
    /// </returns>
    public static float GetDistanceBetweenPoints(Vector<float> refPoint,
        Vector<float> endPoint)
    {
        float dY = endPoint[1] - refPoint[1];
        float dX = endPoint[0] - refPoint[0];

        return (float)Math.Sqrt((dX * dX) + (dY * dY));
    }

    /// <summary>
    /// Rotates a homogeneous point about another observation-space point.
    /// </summary>
    /// <param name="refPoint">
    /// The reference point in observation coordinates.
    /// </param>
    /// <param name="endPoint">
    /// The other point in observation coordinates.
    /// </param>
    /// <param name="angle">The counterclockwise rotation in radians.</param>
    /// <returns>
    /// The rotated homogeneous point.
    /// </returns>
    public static Vector<float> RotatePointAroundPoint(Vector<float> refPoint,
        Vector<float> endPoint, float angle)
    {
        float shiftX = refPoint[0];
        float shiftY = refPoint[1];
        Matrix<float> matrixNegativeTranslate =
            BuildTranslationMatrix(-shiftX, -shiftY);
        Matrix<float> matrixRotate = BuildRotationMatrix(angle);
        Matrix<float> matrixPositiveTranslate =
            BuildTranslationMatrix(shiftX, shiftY);
        Matrix<float> matrixTransform = matrixPositiveTranslate * matrixRotate *
            matrixNegativeTranslate;

        return matrixTransform * endPoint;
    }

    /// <summary>
    /// Constructs a point at a radius and angle from the reference.
    /// </summary>
    /// <param name="refPoint">
    /// The reference point in observation coordinates.
    /// </param>
    /// <param name="radius">
    /// The distance from the reference point in observation units.
    /// </param>
    /// <param name="theta">
    /// The direction angle in radians from the positive X axis.
    /// </param>
    /// <returns>
    /// The new homogeneous position with weight one.
    /// </returns>
    public static Vector<float> LinkedNodeRotation(Vector<float> refPoint,
        float radius, float theta)
    {
        float origX = refPoint[0];
        float origY = refPoint[1];
        float newX = radius * (float)Math.Cos(theta);
        float newY = radius * (float)Math.Sin(theta);
        Vector<float> v = Vector<float>.Build.Dense(3);
        v[0] = newX + origX;
        v[1] = newY + origY;
        v[2] = 1f;

        return v;
    }

    /// <summary>
    /// Creates a homogeneous 3 by 3 translation matrix.
    /// </summary>
    /// <param name="x">The horizontal translation in observation units.</param>
    /// <param name="y">The vertical translation in observation units.</param>
    /// <returns>
    /// The matrix for column-vector translation.
    /// </returns>
    public static Matrix<float> BuildTranslationMatrix(float x, float y)
    {
        Matrix<float> matrix = Matrix<float>.Build.DenseIdentity(3);
        matrix[0, 0] = 1f;
        matrix[0, 1] = 0f;
        matrix[0, 2] = x;
        matrix[1, 0] = 0f;
        matrix[1, 1] = 1f;
        matrix[1, 2] = y;
        matrix[2, 0] = 0f;
        matrix[2, 1] = 0f;
        matrix[2, 2] = 1f;

        return matrix;
    }

    /// <summary>
    /// Creates a homogeneous 3 by 3 rotation matrix.
    /// </summary>
    /// <param name="angle">The angle in radians.</param>
    /// <returns>
    /// The matrix for counterclockwise column-vector rotation.
    /// </returns>
    public static Matrix<float> BuildRotationMatrix(float angle)
    {
        Matrix<float> matrix = Matrix<float>.Build.DenseIdentity(3);
        matrix[0, 0] = (float)Math.Cos(angle);
        matrix[0, 1] = (float)-Math.Sin(angle);
        matrix[0, 2] = 0f;
        matrix[1, 0] = (float)Math.Sin(angle);
        matrix[1, 1] = (float)Math.Cos(angle);
        matrix[1, 2] = 0f;
        matrix[2, 0] = 0f;
        matrix[2, 1] = 0f;
        matrix[2, 2] = 1f;

        return matrix;
    }

    /// <summary>
    /// Creates a homogeneous 3 by 3 scaling matrix.
    /// </summary>
    /// <param name="xScale">
    /// The factor applied to horizontal coordinates.
    /// </param>
    /// <param name="yScale">The factor applied to vertical coordinates.</param>
    /// <returns>
    /// The matrix applying the supplied horizontal and vertical factors.
    /// </returns>
    public static Matrix<float> BuildScalingMatrix(float xScale, float yScale)
    {
        Matrix<float> matrix = Matrix<float>.Build.DenseIdentity(3);
        matrix[0, 0] = xScale;
        matrix[0, 1] = 0f;
        matrix[0, 2] = 0f;
        matrix[1, 0] = 0f;
        matrix[1, 1] = yScale;
        matrix[1, 2] = 0f;
        matrix[2, 0] = 0f;
        matrix[2, 1] = 0f;
        matrix[2, 2] = 1f;

        return matrix;
    }

    /// <summary>
    /// Interpolates or extrapolates corresponding vector components.
    /// </summary>
    /// <param name="a">The first vector.</param>
    /// <param name="b">The second vector.</param>
    /// <param name="t">The parameter, normally between zero and one.</param>
    /// <returns>
    /// The vector a + (b - a) * t.
    /// </returns>
    public static Vector<float> Lerp(Vector<float> a, Vector<float> b, float t)
    {
        return a + ((b - a) * t);
    }
}

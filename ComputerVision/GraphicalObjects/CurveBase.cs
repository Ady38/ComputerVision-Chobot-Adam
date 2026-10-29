using ComputerVision.Utils;
using MathNet.Numerics.LinearAlgebra;
using System.Drawing;

namespace ComputerVision.GraphicalObjects;

public abstract class CurveBase
{
    #region Properties
    protected readonly List<Vector<float>> controlPoints;
    protected List<Vector<float>>? curvePoints;
    protected List<float> segmentLengths;
    protected int curvePrecision = 70;
    protected float length = 0;
    public IReadOnlyList<Vector<float>>? CurvePoints => curvePoints;

    /// <summary>
    /// Curve control points
    /// </summary>
    public List<Vector<float>> ControlPoints
    {
        get
        {
            var newList = new List<Vector<float>>(controlPoints.Count);

            foreach (Vector<float> point in controlPoints)
            {
                newList.Add(point.Clone());
            }

            return newList;
        }
    }

    public IReadOnlyList<float> SegmentLengths => segmentLengths;

    /// <summary>
    /// Curve length
    /// </summary>
    public float Length => length;

    /// <summary>
    /// Curve precision
    /// </summary>
    public int RenderPrecision
    {
        get => curvePrecision;

        set
        {
            curvePrecision = value is < 5 or > 100 ? 70 : value;

            RecalculateCurve();
        }
    }

    /// <summary>
    /// Checks whether a control index appears in the current selection.
    /// </summary>
    /// <param name="controlPointIndex">
    /// The zero-based index to look up in the selection.
    /// </param>
    /// <returns>
    /// True if the control-point index is selected.
    /// </returns>
    public bool ControlPointIsSelected(int controlPointIndex)
    {
        if (SelectedControlPointIndices == null
            || SelectedControlPointIndices.Length == 0)
        {
            return false;
        }

        foreach (int index in SelectedControlPointIndices)
        {
            if (index == controlPointIndex)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// SelectedControlPointIndex
    /// </summary>
    public int? SelectedControlPointIndex
    {
        get
        {
            if (SelectedControlPointIndices != null
                && SelectedControlPointIndices.Length > 0)
            {
                return SelectedControlPointIndices[0];
            }

            return null;
        }

        set => SelectedControlPointIndices =
            value == null ? null : [value.Value];
    }

    /// <summary>
    /// SelectedControlPointIndices
    /// </summary>
    public int[]? SelectedControlPointIndices
    {
        get; set;
    }

    /// <summary>
    /// SelectedControlPoint
    /// </summary>
    public Vector<float>? SelectedControlPoint
    {
        get
        {
            if (SelectedControlPointIndex == null)
            {
                return null;
            }

            return controlPoints[SelectedControlPointIndex.Value];
        }
    }

    /// <summary>
    /// Matrix
    /// </summary>
    public Vector<float> this[int index]
    {
        get => controlPoints[index]; set =>
        controlPoints[index] = value;
    }

    #endregion
    #region Konstruktor

    /// <summary>
    /// Stores at least four controls and initializes sampled curve state.
    /// </summary>
    /// <param name="parControlPoints">
    /// The ordered control vectors retained by the curve.
    /// </param>
    public CurveBase(List<Vector<float>> parControlPoints)
    {
        if (parControlPoints.Count < 4)
        {
            throw new ArgumentException("Control points error");
        }

        controlPoints = parControlPoints;
        segmentLengths = [];
        curvePoints = [];
        SelectedControlPointIndices = [];
        RecalculateCurve();
    }

    #endregion

    /// <summary>
    /// Tests a control point against a small observation-space hit box.
    /// </summary>
    /// <param name="controlPoint">
    /// The control vector in observation coordinates.
    /// </param>
    /// <param name="p">The pointer position in image pixels.</param>
    /// <param name="ct">
    /// The mapping between processed pixels and observation coordinates.
    /// </param>
    /// <returns>
    /// True if the control falls within the four-unit square.
    /// </returns>
    private static bool IsHitByUV(Vector<float> controlPoint, Point p,
        CoordTrans ct)
    {
        Vector<float> xyMatrix = ct.FromUVtoXYVectorFloat(new ComputerVision
            .Models.Point(p.X, p.Y));
        PointF xyPoint = new((float)xyMatrix[0] - 2, (float)xyMatrix[1] - 2);
        RectangleF r = new(xyPoint, new Size(4, 4));
        PointF point = new((float)controlPoint[0], (float)controlPoint[1]);

        return r.Contains(point);
    }

    /// <summary>
    /// Refreshes derived curve samples and lengths after a control change.
    /// </summary>
    protected abstract void RecalculateCurve();

    /// <summary>
    /// Evaluates a curve point and tangent at the requested parameter.
    /// </summary>
    /// <param name="time">
    /// The curve parameter; zero and one select its endpoints.
    /// </param>
    /// <param name="angle">
    /// Receives the tangent bearing in radians from positive X.
    /// </param>
    /// <returns>
    /// The point in the curve's coordinate frame.
    /// </returns>
    public abstract Vector<float> GetPointAndAngleOnCurve(float time,
        out float angle);
    #region Public methods

    /// <summary>
    /// Appends a control and recalculates the curve.
    /// </summary>
    /// <param name="point">The control vector to retain.</param>
    public void Add(Vector<float> point)
    {
        controlPoints.Add(point);
        RecalculateCurve();
    }

    /// <summary>
    /// Inserts a control before the index and recalculates the curve.
    /// </summary>
    /// <param name="index">The zero-based control-point index.</param>
    /// <param name="point">The control vector to retain.</param>
    public void Insert(int index, Vector<float> point)
    {
        controlPoints.Insert(index, point);
        RecalculateCurve();
    }

    /// <summary>
    /// Removes a control and recalculates the curve.
    /// </summary>
    /// <param name="index">The zero-based control-point index.</param>
    /// <returns>
    /// The removed control vector.
    /// </returns>
    public Vector<float> Remove(int index)
    {
        Vector<float> removedPoint = controlPoints[index];
        controlPoints.RemoveAt(index);
        RecalculateCurve();

        return removedPoint;
    }

    /// <summary>
    /// Replaces a selected control and recalculates the curve.
    /// </summary>
    /// <param name="p">The replacement control vector.</param>
    /// <param name="vID">
    /// The control index; null leaves the curve unchanged.
    /// </param>
    public void Move(Vector<float> p, int? vID)
    {
        if (vID == null)
        {
            return;
        }

        controlPoints[vID.Value] = p;
        RecalculateCurve();
    }

    #endregion
}

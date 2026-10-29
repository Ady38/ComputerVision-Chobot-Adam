using ComputerVision.Calibration;
using ComputerVision.GraphicalObjects;
using System.Numerics;
using MathNetVector = MathNet.Numerics.LinearAlgebra.Vector<float>;

namespace ComputerVision.Tests;

public class DeCasteljauTests
{

    /// <summary>
    /// Checks cubic evaluation against an independent Bernstein polynomial.
    /// </summary>
    /// <param name="t">The interpolation or extrapolation parameter.</param>
    [Theory]
    [InlineData(-.25f)]
    [InlineData(0f)]
    [InlineData(.1f)]
    [InlineData(.5f)]
    [InlineData(1f)]
    [InlineData(1.25f)]
    public void CubicMatchesBernsteinReference(float t)
    {
        var curve = new GroundBezier(new(1, 2), new(3, 8), new(7, -4), new(11,
            3));
        float u = 1 - t;
        Vector2 expected =
            (u * u * u * curve.P0) + (3 * u * u * t * curve.P1) + (3 * u
            * t * t * curve.P2) + (t * t * t * curve.P3);
        Assert.InRange(Vector2.Distance(expected, curve.Evaluate(t)), 0,
            .00002f);
    }

    /// <summary>
    /// Checks point and tangent results for both supported vector interfaces.
    /// </summary>
    /// <param name="t">The parameter at which both APIs are compared.</param>
    [Theory]
    [InlineData(0f)]
    [InlineData(.35f)]
    [InlineData(1f)]
    public void LegacyAdapterPreservesCoordinatesAndTangent(float t)
    {
        Vector2[] controls = [new(0, 0), new(2, 5), new(6, 3), new(8, 0)];
        var original = (Vector2[])controls.Clone();
        var legacy = controls.Select(p => MathNetVector.Build.DenseOfArray([p.X,
            p.Y, 1])).ToList();
        float u = 1 - t;
        Vector2 derivative =
            (3 * u * u * (controls[1] - controls[0])) + (6 * u * t * (
            controls[2] - controls[1])) + (3 * t * t * (controls[3] -
            controls[2]));
        float expectedAngle = MathF.Atan2(derivative.Y, derivative.X);
        Vector2 point = DeCasteljauAlgorithm.Evaluate(controls, t,
            out float angle);
        MathNetVector adapted =
            DeCasteljauAlgorithm.GetDeCasteljauPoint(legacy, t,
            out float legacyAngle);
        Assert.Equal(original, controls);
        Assert.InRange(Math.Abs(angle - expectedAngle), 0, .00001f);
        Assert.InRange(Math.Abs(legacyAngle - expectedAngle), 0, .00001f);
        Assert.Equal(point.X, adapted[0]);
        Assert.Equal(point.Y, adapted[1]);
        Assert.Equal(0, adapted[2]);
        Assert.All(legacy, p => Assert.Equal(1, p[2]));
    }

    /// <summary>
    /// Checks a quadratic curve independently of the cubic ground type.
    /// </summary>
    [Fact]
    public void QuadraticHasKnownMidpoint()
    {
        Vector2[] controls = [new(0, 0), new(2, 4), new(4, 0)];
        Vector2 result = DeCasteljauAlgorithm.Evaluate(controls, .5f,
            out float angle);
        Assert.Equal(new Vector2(2, 2), result);
        Assert.Equal(0, angle);
    }

    /// <summary>
    /// Covers bounded stack storage and the higher-degree array fallback.
    /// </summary>
    /// <param name="count">The number of collinear control points.</param>
    [Theory]
    [InlineData(2)]
    [InlineData(32)]
    [InlineData(33)]
    public void CollinearControlsRemainLinear(int count)
    {
        Vector2[] controls = Enumerable.Range(0,
            count).Select(i => new Vector2(i,
            2 * i)).ToArray();
        Vector2 point = DeCasteljauAlgorithm.Evaluate(controls, .4f,
            out float angle);
        Assert.InRange(Vector2.Distance(point, new Vector2(.4f * (count - 1),
            .8f * (count - 1))), 0, .0001f);
        Assert.InRange(Math.Abs(angle - MathF.Atan2(2, 1)), 0, .0001f);
    }

    /// <summary>
    /// Verifies the single-point case and rejects empty control sets.
    /// </summary>
    [Fact]
    public void DegenerateInputsHaveExplicitBehaviour()
    {
        Vector2[] one = [new(4, 7)];
        Vector2 point = DeCasteljauAlgorithm.Evaluate(one, .2f,
            out float angle);
        Assert.Equal(one[0], point);
        Assert.Equal(0, angle);
        Assert.Throws<ArgumentException>(() => DeCasteljauAlgorithm.Evaluate([],
            .5f, out _));
        Assert.Throws<ArgumentException>(() => DeCasteljauAlgorithm
            .GetDeCasteljauPoint([], .5f, out _));
    }

    /// <summary>
    /// Verifies that uniform curve samples include both endpoints.
    /// </summary>
    [Fact]
    public void LegacySamplingIncludesBothEndpoints()
    {
        List<MathNetVector> controls = [MathNetVector.Build.DenseOfArray([1, 2,
            1]), MathNetVector.Build.DenseOfArray([9, 6, 1])];
        List<MathNetVector> points =
            DeCasteljauAlgorithm.GetCurvePoints(controls, 5)!;
        Assert.Equal(5, points.Count);
        Assert.Equal(1, points[0][0]);
        Assert.Equal(2, points[0][1]);
        Assert.Equal(9, points[^1][0]);
        Assert.Equal(6, points[^1][1]);
        Assert.Throws<ApplicationException>(() => DeCasteljauAlgorithm
            .GetCurvePoints(controls, 1));
    }

    /// <summary>
    /// Protects allocation-free evaluation of an existing ground cubic.
    /// </summary>
    [Fact]
    public void GroundEvaluationDoesNotAllocate()
    {
        var curve = new GroundBezier(new(0, 0), new(1, 3), new(4, 3), new(5,
            0));
        Vector2 result = default;

        for (int i = 0; i < 1000; i++)
        {
            result = curve.Evaluate(.3f);
        }

        long before = GC.GetAllocatedBytesForCurrentThread();

        for (int i = 0; i < 1000; i++)
        {
            result = curve.Evaluate(.3f);
        }

        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, allocated);
        Assert.True(float.IsFinite(result.X));
    }
}

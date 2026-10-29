using ComputerVision.Calibration;
using ComputerVision.Models;
using ComputerVision.Pipeline;
using System.Numerics;
using System.Text.Json;

namespace ComputerVision.ProcessingSteps;

/// <summary>
/// Projects detected pixels BEFORE fitting/averaging; never mutates pixels or
/// image-space controls.
/// </summary>
public sealed class HomographyStep: IPipelineStep
{
    public string Name => "Homography (detected points to ground)";
    public string NameAbb => "Homogr";

    private readonly Func<GroundCalibration?> _calibration;

    /// <summary>
    /// Creates a projection stage using the instance calibration provider.
    /// </summary>
    /// <param name="calibration">
    /// A provider returning the current calibration, or null when unavailable.
    /// </param>
    /// <remarks>
    /// The obsolete parameterless constructor snapshots the legacy default
    /// mapping.
    /// </remarks>
    public HomographyStep(Func<GroundCalibration?> calibration)
    {
        _calibration = calibration
            ?? throw new ArgumentNullException(nameof(calibration));
    }

    // Source-compatible bridge for the older LineDetection desktop host. New
    // pipelines never read this.
    // A legacy instance snapshots the default; later loads cannot change an
    // already running instance.
    private static PlanarHomography? s_legacyDefault;
    private readonly PlanarHomography? _legacyMapping;

    /// <summary>
    /// Creates a projection stage using the instance calibration provider.
    /// </summary>
    /// <remarks>
    /// The obsolete parameterless constructor snapshots the legacy default
    /// mapping.
    /// </remarks>
    [Obsolete("Pass an instance calibration provider; " +
        "place Homography before " + "BezierCurveFitStep and " +
        "OffsetAndAngleStep.")]
    public HomographyStep(): this(() => null)
    {
        _legacyMapping = Volatile.Read(ref s_legacyDefault);
    }

    /// <summary>
    /// Loads the obsolete default mapping from a JSON coefficient array.
    /// </summary>
    /// <param name="filePath">The path to the JSON matrix file.</param>
    /// <remarks>
    /// Existing stage instances retain their own snapshot of the default.
    /// </remarks>
    [Obsolete("Use PlanarHomography.FromMatrix and " +
        "pass a GroundCalibration to each " +
        "pipeline instance. Matrix maps raw " + "pixels to " + "cm.")]
    public static void LoadHomographyMatrix(string filePath)
    {
        Volatile.Write(ref s_legacyDefault, null);
        double[][] values =
            JsonSerializer.Deserialize<double[][]>(File.ReadAllText(
            filePath)) ?? throw new InvalidDataException(
            "Homography matrix is empty.");
        Volatile.Write(ref s_legacyDefault,
            PlanarHomography.FromMatrix(values));
    }

    /// <summary>
    /// Projects the longest usable section of the largest detection cluster.
    /// </summary>
    /// <param name="context">
    /// The frame context containing the image and stage results.
    /// </param>
    /// <remarks>
    /// Ground outputs are reset each frame. Legacy image-space outputs are
    /// preserved.
    /// </remarks>
    public void Execute(PipelineContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.GroundPoints = null;
        context.GroundMean = null;
        context.GroundCurve = null;
        context.GroundStatus = GroundProjectionStatus.MissingCalibration;
        GroundCalibration? fit = _calibration();

        if (fit is null && _legacyMapping is not null
            && context.SourceWidth >= 2 && context.SourceHeight >= 2)
        {
            fit = new(_legacyMapping, context.SourceWidth, context.SourceHeight,
                [new(0, 0), new(context.SourceWidth - 1, 0),
                new(context.SourceWidth - 1, context.SourceHeight - 1), new(0,
                context.SourceHeight - 1)], "ground-plane-cm");
        }

        if (fit is null)
        {
            return;
        }

        if (context.SourceWidth != fit.SourceWidth
            || context.SourceHeight != fit.SourceHeight)
        {
            context.GroundStatus = GroundProjectionStatus.SourceSizeMismatch;

            return;
        }

        context.GroundStatus = GroundProjectionStatus.NoDetection;

        if (context.Image is not { Width: > 0, Height: > 0 } image
            || context.CurvePointClusters is not { Count: > 0 } clusters)
        {
            return;
        }

        List<Point> cluster = clusters.MaxBy(c => c.Count)!;

        if (cluster.Count == 0)
        {
            return;
        }

        List<Vector2> longest = [], run = [];

        foreach (Point p in cluster)
        {

            // Matches ResizeBilinearStep: source = destination * sourceSize /
            // destinationSize.
            var raw = new Vector2((float)p.X * fit.SourceWidth / image.Width,
                (float)p.Y * fit.SourceHeight / image.Height);

            if (p.X >= 0 && p.X < image.Width && p.Y >= 0 && p.Y < image.Height
                && fit.TryProject(raw, out Vector2 ground))
            {
                run.Add(ground);
            }
            else
            {
                if (run.Count > longest.Count)
                {
                    longest = run;
                }

                run = [];
            }
        }

        if (run.Count > longest.Count)
        {
            longest = run;
        }

        context.GroundStatus = GroundProjectionStatus.NoCalibratedPoints;

        if (longest.Count == 0)
        {
            return;
        }

        context.GroundPoints = new(longest.AsReadOnly(), fit.CoordinateFrame,
            clusters.Sum(c => c.Count), cluster.Count);
        context.GroundStatus = GroundProjectionStatus.Applied;
    }
}

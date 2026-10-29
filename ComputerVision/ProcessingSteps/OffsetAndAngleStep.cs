using ComputerVision.Models;
using ComputerVision.Pipeline;
using ComputerVision.Utils;
using System.Drawing;

namespace ComputerVision.ProcessingSteps;

public class OffsetAndAngleStep: IPipelineStep
{
    public string Name => "Offset And Angle";
    public string NameAbb => "OffsetAngle";

    /// <summary>
    /// Calculates the legacy mean bearing and the separate ground mean.
    /// </summary>
    /// <param name="context">
    /// The frame context containing the image and stage results.
    /// </param>
    /// <remarks>
    /// The heading is a bearing to the mean point, not the curve tangent.
    /// </remarks>
    public void Execute(PipelineContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Preserve legacy lateral/forward Offset for existing controllers.
        // GroundMean has its own named frame.
        context.GroundMean = context.GroundPoints is { Points.Count: > 0 }
            ground ? new System.Numerics.Vector2((float)ground.Points.Average(p
            => (double)p.X), (float)ground.Points.Average(p => (double)p.Y)) :
            null;
        context.Offset = null;
        context.HeadingAngle = null;

        if (context == null || context.CoordTrans == null)
        {
            return;
        }

        List<List<Models.Point>>? clusters = context.CurvePointClusters;

        if (clusters == null || clusters.Count == 0)
        {
            return;
        }

        CoordTrans coordTrans = context.CoordTrans;

        // Calculate average position across all points in all clusters in world
        // space
        float sumX = 0f;
        float sumY = 0f;
        int totalCount = 0;

        foreach (List<Models.Point> cluster in clusters)
        {
            foreach (Models.Point pt in cluster)
            {
                PointF pointWorld = coordTrans.FromUVtoXY(new(pt.X, pt.Y));
                sumX += pointWorld.X;
                sumY += pointWorld.Y;
                totalCount++;
            }
        }

        if (totalCount == 0)
        {
            return;
        }

        float avgX = sumX / totalCount;
        float avgY = sumY / totalCount;

        // Offset: average deviation from origin in world space (X = lateral, Y
        // = forward)
        context.Offset = new System.Drawing.PointF(avgX, avgY);

        // Heading angle: angle of the average world point from the origin, in
        // degrees
        // Measured from the positive Y axis (forward direction), clockwise
        // positive
        context.HeadingAngle = (float)(Math.Atan2(avgX,
            avgY) * (180.0 / Math.PI));
    }
}

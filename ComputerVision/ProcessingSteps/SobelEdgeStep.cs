using ComputerVision.Models;
using ComputerVision.Pipeline;

namespace ComputerVision.ProcessingSteps;

/// <summary>
/// Configures the fixed row-sampling edge detector.
/// </summary>
public class SobelEdgeStep: IPipelineStep
{
    private readonly int _numberOfPoints;

    /// <summary>
    /// Configures the fixed row-sampling edge detector.
    /// </summary>
    /// <param name="numberOfPoints">
    /// The number of image rows sampled for line centres.
    /// </param>
    public SobelEdgeStep(int numberOfPoints)
    {
        _numberOfPoints = numberOfPoints;
    }

    public string Name => "Sobel Edge Detection";
    public string NameAbb => "Sobel";

    /// <summary>
    /// Finds the first binary edge pair on each sampled row.
    /// </summary>
    /// <param name="context">
    /// The frame context containing the image and stage results.
    /// </param>
    public void Execute(PipelineContext context)
    {
        if (context == null)
        {
            return;
        }

        YUVImage? image = context.Image;

        if (image == null)
        {
            context.CurvePointClusters = null;

            return;
        }

        int width = image.Width;
        int height = image.Height;

        if (_numberOfPoints < 1 || _numberOfPoints > height)
        {
            context.CurvePointClusters = null;

            return;
        }

        byte[] bytes = image.Bytes;
        var result = new List<Point>(_numberOfPoints);
        float precision = (float)height / _numberOfPoints;

        for (int p = 0; p < _numberOfPoints; p++)
        {
            int yy = (int)(p * precision);

            if (yy >= height)
            {
                yy = height - 1;
            }

            int rowOffset = yy * width;
            int indexLeft = 0;
            int edgeCount = 0;

            for (int x = 3; x < width - 3; x++)
            {
                int center = rowOffset + x;
                int sum = -bytes[center - 3] - bytes[center - 2] - bytes[center
                    - 1] + bytes[center] + bytes[center + 1] + bytes[center +
                    2];

                if (sum is <= -765 or >= 765)
                {
                    edgeCount++;

                    if (edgeCount == 1)
                    {
                        indexLeft = x;
                    }
                    else if (edgeCount == 2)
                    {
                        int indexRight = x;
                        result.Add(new Point((indexLeft + indexRight) >> 1,
                            yy));
                        break;
                    }
                }
            }
        }

        context.CurvePointClusters = result.Count > 0 ? [result] : null;
    }
}

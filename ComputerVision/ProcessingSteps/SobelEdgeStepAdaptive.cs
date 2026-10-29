using ComputerVision.Models;
using ComputerVision.Pipeline;
using ComputerVision.Utils;

namespace ComputerVision.ProcessingSteps;

/// <summary>
/// Configures row sampling and within-frame line-width adaptation.
/// </summary>
public class SobelEdgeStepAdaptive: IPipelineStep
{
    private readonly int _numberOfPoints;

    /// <summary>
    /// Configures row sampling and within-frame line-width adaptation.
    /// </summary>
    /// <param name="numberOfPoints">
    /// The number of image rows sampled for line centres.
    /// </param>
    public SobelEdgeStepAdaptive(int numberOfPoints)
    {
        _numberOfPoints = numberOfPoints;
    }

    public string Name => "Adaptive Sobel Edge Detection";
    public string NameAbb => "SobelAdapt";

    /// <summary>
    /// Collects centre points, splitting clusters after missing rows.
    /// </summary>
    /// <param name="context">
    /// The frame context containing the image and stage results.
    /// </param>
    /// <remarks>
    /// Width estimates belong to this frame; there is no temporal tracking.
    /// </remarks>
    public void Execute(PipelineContext? context)
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
        float summedWidths = 0;
        int widthCount = 0;
        context.CurvePointClusters = [];
        List<List<Point>> clusters = context.CurvePointClusters;
        List<Point>? currentCluster = null;
        bool prevIterationFoundPoint = false;
        float precision = (float)height / _numberOfPoints;

        // Use integer row positions derived from the sample index.
        for (int p = 0; p < _numberOfPoints; p++)
        {
            int y = height - 1 - (int)(p * precision);

            if (y < 0)
            {
                break;
            }

            int yOffset = y * width;
            int edgeCount = 0;
            int indexLeft = 0;
            float mean = widthCount > 0 ? summedWidths / widthCount : float.NaN;
            bool foundPoint = false;

            // Cache row start/end limits
            int xStart = 3;
            int xEnd = width - 3;

            for (int x = xStart; x < xEnd; x++)
            {
                int center = x + yOffset;
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
                        float centerX;
                        int regionWidth = indexRight - indexLeft;

                        if (indexLeft <= 4)
                        {
                            if (float.IsNaN(mean) || mean <= 0f)
                            {
                                centerX = indexLeft + (regionWidth * 0.5f);
                            }
                            else
                            {
                                float error = regionWidth - mean;
                                centerX = indexLeft + ((regionWidth + error) *
                                    0.5f);
                            }

                            if (centerX < 0f)
                            {
                                break;
                            }
                        }
                        else if (indexRight >= width - 5)
                        {
                            if (float.IsNaN(mean) || mean <= 0f)
                            {
                                centerX = indexLeft + (regionWidth * 0.5f);
                            }
                            else
                            {
                                float error = mean - regionWidth;
                                centerX = indexLeft + ((regionWidth + error) *
                                    0.5f);
                            }

                            if (centerX > width)
                            {
                                break;
                            }
                        }
                        else
                        {
                            centerX = (indexLeft + indexRight) * 0.5f;
                        }

                        var pt = new Point((int)centerX, y);

                        if (!prevIterationFoundPoint || currentCluster == null)
                        {
                            currentCluster = new List<Point>(_numberOfPoints);
                            clusters.Add(currentCluster);
                        }

                        currentCluster.Add(pt);
                        prevIterationFoundPoint = true;
                        foundPoint = true;

                        if ((indexLeft > 4 && indexRight < width - 5)
                            || widthCount == 0)
                        {
                            summedWidths += regionWidth;
                            widthCount++;
                        }

                        // We only care about the first pair of edges in this
                        // row.
                        break;
                    }
                }
            }

            if (!foundPoint)
            {
                prevIterationFoundPoint = false;
            }
        }
    }
}

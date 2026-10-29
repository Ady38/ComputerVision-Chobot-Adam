using ComputerVision.Models;

namespace ComputerVision.Helpers;

public static class OtsuThresholdHelper
{

    /// <summary>
    /// Chooses an inclusive dark-class split and updates histogram peaks.
    /// </summary>
    /// <param name="histogramObject">
    /// The refreshed histogram used to choose a binary threshold.
    /// </param>
    public static void GetOtsuThreshold(YUVImageHistogram histogramObject)
    {
        ArgumentNullException.ThrowIfNull(histogramObject);
        float[] h = histogramObject.NormalizedHistogram;
        int occupied = 0, onlyIntensity = 0;
        double totalWeight = 0, totalSum = 0;

        for (int i = 0; i < 256; i++)
        {
            if (histogramObject.Histogram[i] > 0)
            {
                occupied++;
                onlyIntensity = i;
            }

            totalWeight += h[i];
            totalSum += i * h[i];
        }

        // Smoothing must not manufacture a line in a constant image.
        if (occupied < 2)
        {
            SetDegenerate(histogramObject, onlyIntensity);

            return;
        }

        double backgroundWeight = 0, backgroundSum = 0, bestVariance = -1;
        byte bestThreshold = 0;

        for (int i = 0; i < 255; i++)
        {
            backgroundWeight += h[i];
            backgroundSum += i * h[i];

            if (backgroundWeight <= 0)
            {
                continue;
            }

            double foregroundWeight = totalWeight - backgroundWeight;

            if (foregroundWeight <= 0)
            {
                break;
            }

            double difference =
                (backgroundSum / backgroundWeight) - ((totalSum -
                backgroundSum) / foregroundWeight);
            double variance = backgroundWeight * foregroundWeight * difference
                * difference;

            if (variance > bestVariance)
            {
                bestVariance = variance;
                bestThreshold = (byte)i;
            }
        }

        if (bestVariance < 0)
        {
            SetDegenerate(histogramObject, onlyIntensity);

            return;
        }

        histogramObject.OtsuThreshold = bestThreshold;
        float leftMax = -1, rightMax = -1;
        int left = 0, right = 255;

        for (int i = 0; i <= bestThreshold; i++)
        {
            if (h[i] > leftMax)
            {
                leftMax = h[i];
                left = i;
            }
        }

        for (int i = bestThreshold + 1; i < 256; i++)
        {
            if (h[i] > rightMax)
            {
                rightMax = h[i];
                right = i;
            }
        }

        histogramObject.LeftMax = leftMax;
        histogramObject.RightMax = rightMax;
        histogramObject.LeftMaxIndex = left;
        histogramObject.RightMaxIndex = right;
    }

    /// <summary>
    /// Records coincident class peaks so constant frames are rejected.
    /// </summary>
    /// <param name="histogram">
    /// The histogram whose threshold metadata is updated.
    /// </param>
    /// <param name="intensity">
    /// The constant brightness value from zero to 255.
    /// </param>
    private static void SetDegenerate(YUVImageHistogram histogram,
        int intensity)
    {
        histogram.OtsuThreshold = (byte)intensity;
        histogram.LeftMaxIndex = histogram.RightMaxIndex = intensity;
        histogram.LeftMax = histogram.RightMax = histogram
            .NormalizedHistogram[intensity];
    }
}

namespace ComputerVision.Utils;

public class Statistics
{
    private static readonly float[] s_studentsTDistributionCriticalValues =
        [12.706f, 4.303f, 3.182f, 2.776f, 2.571f, 2.447f, 2.365f, 2.306f,
        2.262f, 2.228f, 2.201f, 2.179f, 2.160f, 2.145f, 2.131f, 2.120f, 2.110f,
        2.101f, 2.093f, 2.086f, 2.080f, 2.074f, 2.069f, 2.064f, 2.060f, 2.056f,
        2.052f, 2.048f, 2.045f];
    private float _summedValues;
    private float _summedValuesSquared;

    public float Mean => _summedValues / NumberOfValues;
    public float StandardDeviation => (float)Math.Sqrt((_summedValuesSquared -
        (_summedValues * _summedValues / NumberOfValues)) / (NumberOfValues -
        1));

    /// <summary>
    /// Computes the existing Student-t or normal mean-interval estimate.
    /// </summary>
    /// <returns>
    /// The lower and upper bounds; NaN values indicate insufficient samples.
    /// </returns>
    public (float, float) ConfidenceInterval95()
    {
        if (NumberOfValues == 0)
        {
            return (float.NaN, float.NaN);
        }

        if (NumberOfValues < 30)
        {
            float t = s_studentsTDistributionCriticalValues[NumberOfValues - 1];
            float ht = t * StandardDeviation / (float)Math.Sqrt(NumberOfValues);

            return (Mean - ht, Mean + ht);
        }

        float hz = StandardDeviation * 1.96f / (float)Math.Sqrt(NumberOfValues);

        return (Mean - hz, Mean + hz);
    }

    /// <summary>
    /// Accumulates a sample, its square and the sample count.
    /// </summary>
    /// <param name="value">The value to add.</param>
    public void AddValue(float value)
    {
        _summedValues += value;
        _summedValuesSquared += value * value;
        NumberOfValues++;
    }

    public int NumberOfValues
    {
        get; private set;
    }

    /// <summary>
    /// Resets all accumulated sample statistics.
    /// </summary>
    public void Clear()
    {
        _summedValues = 0;
        _summedValuesSquared = 0;
        NumberOfValues = 0;
    }
}

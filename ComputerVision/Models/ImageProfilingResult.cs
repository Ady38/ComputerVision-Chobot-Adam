namespace ComputerVision.Models;

public class ImageProfilingResult
{
    public double ElapsedMilliseconds
    {
        get; internal set;
    }

    public List<StepProfilingResult> StepResults { get; } = [];
}

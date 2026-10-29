namespace ComputerVision.Pipeline;

public interface IPipelineStep
{
    string Name
    {
        get;
    }

    string NameAbb
    {
        get;
    }

    /// <summary>
    /// Processes the current frame and updates this stage's context fields.
    /// </summary>
    /// <param name="context">
    /// The frame context containing the image and stage results.
    /// </param>
    void Execute(PipelineContext context);
}

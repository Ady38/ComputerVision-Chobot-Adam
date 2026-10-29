namespace ComputerVision.Models;

/// <summary>
/// Stores one stage's elapsed processing time.
/// </summary>
/// <param name="StepName">The human-readable processing-stage name.</param>
/// <param name="ElapsedMilliseconds">
/// The measured time, including fractional milliseconds.
/// </param>
public record StepProfilingResult(string StepName, double ElapsedMilliseconds);

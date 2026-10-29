namespace ComputerVision.Inputs;

/// <summary>
/// Specifies a raw-image file and its capture dimensions.
/// </summary>
/// <param name="Path">The raw-image file path.</param>
/// <param name="Width">The image width in pixels.</param>
/// <param name="Height">The image height in pixels.</param>
public record ImageLoadOptions(string Path, int Width, int Height);

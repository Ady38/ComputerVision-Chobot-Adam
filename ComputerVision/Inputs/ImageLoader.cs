using ComputerVision.Models;

namespace ComputerVision.Inputs;

public static class ImageLoader
{

    /// <summary>
    /// Reads exactly one packed Y plane and ignores trailing file bytes.
    /// </summary>
    /// <param name="path">
    /// The raw-image file path; no PNG or JPEG decoding is performed.
    /// </param>
    /// <param name="width">The image width in pixels.</param>
    /// <param name="height">The image height in pixels.</param>
    /// <returns>
    /// The loaded image, or null if reading fails with an I/O error.
    /// </returns>
    public static YUVImage? LoadFromFile(string path, int width, int height)
    {
        YUVImage.ValidateDimensions(width, height);
        byte[] imageBytes = new byte[width * height];
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.Read, bufferSize: width * height,
                FileOptions.SequentialScan);

            // Partial reads must never become valid images; trailing YUV chroma
            // is ignored.
            fs.ReadExactly(imageBytes);

            return new YUVImage(imageBytes, width, height);
        }
        catch (IOException)
        {
            return null;
        }
    }
}

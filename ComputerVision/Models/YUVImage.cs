namespace ComputerVision.Models;

/// <summary>
/// Holds a caller-owned packed brightness plane and reusable histogram state.
/// </summary>
public sealed class YUVImage: ICloneable
{

    /// <summary>
    /// The smallest supported width or height in pixels.
    /// </summary>
    public static readonly int MIN_SIZE = 16;

    /// <summary>
    /// The largest supported width or height in pixels.
    /// </summary>
    public static readonly int MAX_SIZE = 640;

    /// <summary>
    /// Gets or sets an optional display label; cloning does not copy it.
    /// </summary>
    public string ImageName { get; set; } = string.Empty;

    /// <summary>
    /// Gets the number of packed pixels in each row.
    /// </summary>
    public int Width
    {
        get;
    }

    /// <summary>
    /// Gets the number of image rows, ordered from top to bottom.
    /// </summary>
    public int Height
    {
        get;
    }

    /// <summary>
    /// Gets the shared mutable buffer, including any trailing chroma bytes.
    /// </summary>
    public byte[] Bytes
    {
        get;
    }

    /// <summary>
    /// Gets the number of brightness samples, excluding trailing chroma.
    /// </summary>
    public int PixelCount => Width * Height;

    /// <summary>
    /// Packed, top-down Y plane. Trailing U/V bytes are not image intensities.
    /// </summary>
    public Span<byte> Luma => Bytes.AsSpan(0, PixelCount);

    /// <summary>
    /// Gets lazily allocated histogram storage. Call UpdateHistogram to refresh
    /// its values after changing pixels.
    /// </summary>
    public YUVImageHistogram Histogram => field ??= new();

    /// <summary>
    /// Creates an image that shares the caller's packed Y buffer.
    /// </summary>
    /// <param name="imageBytes">
    /// The caller-owned packed Y buffer, with optional trailing chroma.
    /// </param>
    /// <param name="width">The width, from 16 through 640 pixels.</param>
    /// <param name="height">The height, from 16 through 640 pixels.</param>
    /// <remarks>
    /// The buffer is not copied. Its first width times height bytes are Y.
    /// </remarks>
    public YUVImage(byte[] imageBytes, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(imageBytes);
        ValidateDimensions(width, height);

        if (imageBytes.Length < width * height)
        {
            throw new ArgumentException(
                "The buffer must contain at least width " +
                "* height packed Y " + "bytes.", nameof(imageBytes));
        }

        Width = width;
        Height = height;
        Bytes = imageBytes;
    }

    /// <summary>
    /// Rejects image dimensions outside the supported inclusive range.
    /// </summary>
    /// <param name="width">The image width in pixels.</param>
    /// <param name="height">The image height in pixels.</param>
    internal static void ValidateDimensions(int width, int height)
    {
        if (width < MIN_SIZE || width > MAX_SIZE)
        {
            throw new ArgumentOutOfRangeException(nameof(width),
                $"Width must be {MIN_SIZE}–{MAX_SIZE}.");
        }

        if (height < MIN_SIZE || height > MAX_SIZE)
        {
            throw new ArgumentOutOfRangeException(nameof(height),
                $"Height must be {MIN_SIZE}–{MAX_SIZE}.");
        }
    }

    /// <summary>
    /// Rebuilds the reusable histogram from the current Y pixels.
    /// </summary>
    /// <param name="sigma">
    /// Optional smoothing width in intensity levels; null disables smoothing.
    /// </param>
    public void UpdateHistogram(float? sigma)
    {
        Histogram.UpdateHistogram(this, sigma);
    }

    /// <summary>
    /// Copies the pixels and dimensions into an independent image.
    /// </summary>
    /// <returns>
    /// A new image with its own buffer, no cached histogram and an empty name.
    /// </returns>
    public object Clone()
    {
        return new YUVImage((byte[])Bytes.Clone(), Width, Height);
    }

    /// <summary>
    /// Asynchronously writes the buffer, replacing any existing file.
    /// </summary>
    /// <param name="path">The destination file path.</param>
    /// <param name="bytes">
    /// The bytes to write; null or empty buffers are rejected.
    /// </param>
    /// <returns>
    /// A task yielding false for an empty buffer or an I/O failure, otherwise
    /// true.
    /// </returns>
    /// <remarks>
    /// Invalid paths and access errors outside IOException can propagate.
    /// </remarks>
    public static async Task<bool> TrySaveBytesAsync(string path, byte[] bytes)
    {
        if (bytes == null || bytes.Length == 0)
        {
            return false;
        }

        try
        {
            await File.WriteAllBytesAsync(path, bytes);

            return true;
        }
        catch (IOException)
        {

            // Report I/O failures such as a locked file or a full disk.
            return false;
        }
    }
}

using ComputerVision.Models;
using ComputerVision.Pipeline;
using ComputerVision.ProcessingSteps;
using ComputerVision.Utils;
using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;

internal static class Program
{

    /// <summary>
    /// Dispatches dataset import, evaluation or benchmark commands.
    /// </summary>
    /// <param name="args">
    /// The command-line command and its positional arguments.
    /// </param>
    /// <returns>
    /// Zero on success; one after an error is printed.
    /// </returns>
    private static int Main(string[] args)
    {
        try
        {
            if (args.Length < 1 || args[0] is "--help" or "-h")
            {
                Console.WriteLine("ComputerVision.Runner (.NET 10)\n  " +
                    "import <archive.zip> <data-directory> " +
                    "<source-width> <source-height>\n  " +
                    "evaluate <data-directory> " +
                    "<report.json>\n  benchmark " +
                    "<data-directory> <report.json> " + "[complete-cycles=10] "
                    + "[default|gaussian|new-image]\n  " +
                    "benchmark-zip <archive.zip> " +
                    "<report.json> [complete-cycles=10] " +
                    "[source-width=640] " +
                    "[source-height=480]\nBenchmark in " +
                    "Release. Images load before timing. The " +
                    "pipeline always receives pristine " + "pixels.");

                return 0;
            }

            if (args[0] == "import" && args.Length == 5)
            {
                Import(args[1], args[2], int.Parse(args[3]),
                    int.Parse(args[4]));

                return 0;
            }

            if (args.Length < 3)
            {
                throw new ArgumentException("Use --help for arguments.");
            }

            if (args[0] == "evaluate" && args.Length == 3)
            {
                Evaluate(Load(args[1]), args[2]);
            }
            else if (args[0] == "benchmark" && args.Length <= 5)
            {
                int cycles = args.Length > 3 ? int.Parse(args[3],
                    CultureInfo.InvariantCulture) : 10;
                string mode = args.Length > 4 ? args[4] : "default";

                if (mode is not ("default" or "gaussian" or "new-image"))
                {
                    throw new ArgumentException("Invalid benchmark mode.");
                }

                Benchmark(Load(args[1]), args[2], cycles, mode, false);
            }
            else if (args[0] == "benchmark-zip" && args.Length is 3 or 4 or 6)
            {
                int cycles = args.Length > 3 ? int.Parse(args[3],
                    CultureInfo.InvariantCulture) : 10;
                int width = args.Length == 6 ? int.Parse(args[4]) : 640;
                int height = args.Length == 6 ? int.Parse(args[5]) : 480;
                Benchmark(ReadArchive(args[1], width, height), args[2], cycles,
                    "resize", true);
            }
            else
            {
                throw new ArgumentException("Use --help for arguments.");
            }

            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error.Message);

            return 1;
        }
    }

    /// <summary>
    /// Creates the runner's readable, case-insensitive JSON configuration.
    /// </summary>
    /// <returns>
    /// The serializer settings used by manifests and reports.
    /// </returns>
    private static JsonSerializerOptions JsonOptions()
    {
        return new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };
    }

    /// <summary>
    /// Calculates a SHA-256 identity for an in-memory byte buffer.
    /// </summary>
    /// <param name="bytes">The byte buffer to process.</param>
    /// <returns>
    /// The uppercase hexadecimal digest.
    /// </returns>
    private static string Hash(byte[] bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes));
    }

    /// <summary>
    /// Calculates a SHA-256 identity by reading a file stream.
    /// </summary>
    /// <param name="path">The file whose bytes are hashed.</param>
    /// <returns>
    /// The uppercase hexadecimal digest.
    /// </returns>
    private static string FileHash(string path)
    {
        using FileStream stream = File.OpenRead(path);

        return Convert.ToHexString(SHA256.HashData(stream));
    }

    /// <summary>
    /// Creates the output directory and writes an indented JSON report.
    /// </summary>
    /// <param name="path">The report destination.</param>
    /// <param name="value">The report object to serialize.</param>
    private static void Save(string path, object value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(
            path))!);
        File.WriteAllText(path, JsonSerializer.Serialize(value, JsonOptions()));
        Console.WriteLine($"Saved: {path}");
    }

    /// <summary>
    /// Reads ordered raw ZIP entries and extracts each packed Y plane.
    /// </summary>
    /// <param name="path">The original raw-frame ZIP archive.</param>
    /// <param name="width">The image width in pixels.</param>
    /// <param name="height">The image height in pixels.</param>
    /// <returns>
    /// The source metadata and pristine Y buffers.
    /// </returns>
    private static LoadedData ReadArchive(string path, int width, int height)
    {
        if (width is < 16 or > 640 || height is < 16 or > 640)
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }

        using ZipArchive archive = ZipFile.OpenRead(path);
        var scenes = new List<Scene>();
        var pixels = new List<byte[]>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (ZipArchiveEntry? entry in archive.Entries.Where(e =>
            !string.IsNullOrEmpty(e
            .Name)).OrderBy(e => e.FullName, StringComparer.Ordinal))
        {
            if (entry.Length != width * height
                && entry.Length != width * height * 3 / 2)
            {
                throw new InvalidDataException(
                    $"Unexpected raw image length: {entry.FullName} " +
                    $"({entry.Length}). " +
                    "Specify the correct source dimensions.");
            }

            string name = Path.GetFileNameWithoutExtension(entry.Name) + ".y";

            if (!names.Add(name))
            {
                throw new InvalidDataException(
                    $"Duplicate output name: {name}");
            }

            byte[] original = new byte[(int)entry.Length];
            using (Stream stream = entry.Open())
            {
                stream.ReadExactly(original);
            }

            // The archive stores packed YUV420. Vision uses Y; keep the
            // original hash for provenance.
            byte[] y = original.AsSpan(0, width * height).ToArray();
            pixels.Add(y);
            scenes.Add(new(name, entry.FullName, Hash(original), Hash(y),
                null));
        }

        if (scenes.Count == 0)
        {
            throw new InvalidDataException("Empty dataset.");
        }

        return new(new(width, height, width, height, Path.GetFileName(path),
            FileHash(path), "Original packed Y plane, chroma excluded.",
            scenes), pixels.ToArray());
    }

    /// <summary>
    /// Prepares all original frames as 128 by 96 Y files with provenance.
    /// </summary>
    /// <param name="archive">
    /// The original ZIP archive containing raw camera frames.
    /// </param>
    /// <param name="directory">A new or empty destination directory.</param>
    /// <param name="sourceWidth">The original capture width in pixels.</param>
    /// <param name="sourceHeight">
    /// The original capture height in pixels.
    /// </param>
    private static void Import(string archive, string directory,
        int sourceWidth, int sourceHeight)
    {

        // Refuse to overwrite an existing dataset or its manually added
        // annotations.
        if (Directory.Exists(directory)
            && Directory.EnumerateFileSystemEntries(directory).Any())
        {
            throw new IOException("Import destination must be empty.");
        }

        LoadedData data = ReadArchive(archive, sourceWidth, sourceHeight);
        Directory.CreateDirectory(directory);
        var resize = new ResizeBilinearStep(128, 96);
        var scenes = new List<Scene>();

        for (int i = 0; i < data.Pixels.Length; i++)
        {
            var context = new PipelineContext
            {
                Image = new(data.Pixels[i], sourceWidth, sourceHeight)
            };
            resize.Execute(context);
            byte[] bytes = context.Image!.Bytes;
            File.WriteAllBytes(Path.Combine(directory,
                data.Manifest.Scenes[i].File), bytes);
            scenes.Add(data.Manifest.Scenes[i] with
            {
                PreparedSha256 = Hash(
                bytes)
            });
        }

        Save(Path.Combine(directory, "manifest.json"),
            data.Manifest with
            {
                Width = 128,
                Height = 96,
                Preparation = "Y only; ResizeBilinearStep 640x480 -> " +
            "128x96 (or explicitly supplied source " +
            "size); no gamma, denoising or " +
            "thresholding. CenterByRow=null means " +
            "UNANNOTATED, not absent " + "line.",
                Scenes = scenes
            });
        Console.WriteLine($"Imported all {scenes.Count} images.");
    }

    /// <summary>
    /// Validates the prepared dataset's metadata, labels, lengths and hashes.
    /// </summary>
    /// <param name="directory">
    /// The dataset directory containing images and a manifest.
    /// </param>
    /// <returns>
    /// The verified manifest and image buffers.
    /// </returns>
    private static LoadedData Load(string directory)
    {
        Dataset manifest =
            JsonSerializer.Deserialize<Dataset>(File.ReadAllText(Path
            .Combine(directory, "manifest.json")), JsonOptions())
            ?? throw new InvalidDataException("Missing manifest.");

        if (manifest.Scenes.Count == 0)
        {
            throw new InvalidDataException("No scenes.");
        }

        byte[][] pixels = manifest.Scenes.Select(scene =>
        {
            if (scene.CenterByRow is { } labels
                && (labels.Length != manifest.Height
                || labels.Any(x => x.HasValue && (!double.IsFinite(x.Value)
                || x.Value < 0 || x.Value >= manifest.Width))))
            {
                throw new InvalidDataException(
                    $"Invalid reference coordinates: {scene.File}");
            }

            if (scene.File != Path.GetFileName(scene.File))
            {
                throw new InvalidDataException(
                    "Dataset filenames must be local basenames.");
            }

            byte[] bytes = File.ReadAllBytes(Path.Combine(directory,
                scene.File));

            if (bytes.Length != manifest.Width * manifest.Height
                || Hash(bytes) != scene.PreparedSha256)
            {
                throw new InvalidDataException(
                    $"Dataset length/hash mismatch: {scene.File}");
            }

            return bytes;
        }).ToArray();

        return new(manifest, pixels);
    }

    /// <summary>
    /// Creates the standard mapping for 128 by 96 experiment output.
    /// </summary>
    /// <returns>
    /// The legacy lateral and forward observation mapping.
    /// </returns>
    private static CoordTrans Coordinates()
    {
        return new(0, 128, 96, 0, -3.25f, 3.25f, 0, 4.6f);
    }

    /// <summary>
    /// Creates the common runner settings with optional resize and filtering.
    /// </summary>
    /// <param name="resize">
    /// Whether to downsample camera-size input to 128 by 96.
    /// </param>
    /// <param name="gaussian">
    /// Whether to enable spatial Gaussian smoothing.
    /// </param>
    /// <returns>
    /// A settings instance for the selected experiment.
    /// </returns>
    private static LineDetectionSettings Settings(bool resize = false,
        bool gaussian = false)
    {
        return new()
        {
            ResizeImage = resize,
            ImageResizeWidth = 128,
            ImageResizeHeight = 96,
            GaussianFilterImage = gaussian
        };
    }

    /// <summary>
    /// Processes each frame and writes detections and available label errors.
    /// </summary>
    /// <param name="data">
    /// The verified dataset and its pristine image buffers.
    /// </param>
    /// <param name="output">The destination path for the JSON report.</param>
    private static void Evaluate(LoadedData data, string output)
    {
        LineDetectionSettings settings = Settings();
        ImageProcessingPipeline pipeline =
            LineDetectionPipelineFactory.Create(settings);
        var reports = new List<object>();
        CoordTrans coordinates = Coordinates();

        for (int index = 0; index < data.Manifest.Scenes.Count; index++)
        {
            Scene scene = data.Manifest.Scenes[index];
            PipelineContext context = pipeline.Process(new YUVImage((byte[])data
                .Pixels[index].Clone(), data.Manifest.Width,
                data.Manifest.Height), coordinates);
            Point[] points =
                context.CurvePointClusters?.SelectMany(c => c).ToArray(
                ) ?? [];
            var errors = new List<double>();
            int falsePoints = 0, missedRows = 0, expectedRows = 0;

            if (scene.CenterByRow is { } truth)
            {
                for (int row = 0; row < settings.SobelEdgeNumberOfPoints; row++)
                {
                    int y = data.Manifest.Height - 1 - (int)(row * ((float)data
                        .Manifest.Height / settings.SobelEdgeNumberOfPoints));
                    Point[] found = points.Where(p => p.Y == y).ToArray();

                    if (truth[y] is { } expected)
                    {
                        expectedRows++;

                        if (found.Length == 0)
                        {
                            missedRows++;
                        }
                        else
                        {
                            errors.Add(Math.Abs(found[0].X - expected));
                        }
                    }
                    else
                    {
                        falsePoints += found.Length;
                    }
                }
            }

            reports.Add(new
            {
                scene.File,
                Annotated = scene.CenterByRow is not null,
                DetectedPoints = points.Length,
                ExpectedRows = scene.CenterByRow is not null ? (int?
                )expectedRows : null,
                MissedRows = scene.CenterByRow is not null ? (int?)missedRows
                : null,
                FalsePoints = scene.CenterByRow is not null ? (int?
                )falsePoints : null,
                MeanAbsoluteErrorPixels = errors.Count > 0 ? (double?)errors
                .Average() : null,
                MaximumErrorPixels = errors.Count > 0 ? (
                double?)errors.Max() : null,
                HasLine = context.Offset.HasValue,
                Offset = context.Offset is { } p ? new
                {
                    p.X,
                    p.Y
                } : null,
                context.HeadingAngle,
                Threshold = context.Image!.Histogram.OtsuThreshold,
                ProcessedYHash = Hash(context.Image.Bytes),
                Points = points.Select(p => new { p.X, p.Y }).ToArray()
            });
        }

        Save(output, new
        {
            data.Manifest.SourceArchiveSha256,
            Settings = settings,
            Scenes = reports
        });
    }

    /// <summary>
    /// Warms up and measures equal numbers of pristine dataset frames.
    /// </summary>
    /// <param name="data">
    /// The verified dataset and its pristine image buffers.
    /// </param>
    /// <param name="output">The destination path for the JSON report.</param>
    /// <param name="cycles">
    /// The number of complete dataset repetitions to measure.
    /// </param>
    /// <param name="mode">
    /// The benchmark mode selecting smoothing or input allocation.
    /// </param>
    /// <param name="resize">
    /// Whether to downsample camera-size input to 128 by 96.
    /// </param>
    /// <remarks>
    /// Reports exclude file I/O and camera/control costs. Modes differ in input
    /// allocation.
    /// </remarks>
    private static void Benchmark(LoadedData data, string output, int cycles,
        string mode, bool resize)
    {
        if (cycles is < 1 or > 1000)
        {
            throw new ArgumentOutOfRangeException(nameof(cycles),
                "Use 1–1000 complete dataset cycles.");
        }

        LineDetectionSettings settings = Settings(resize, mode == "gaussian");
        ImageProcessingPipeline pipeline =
            LineDetectionPipelineFactory.Create(settings);
        CoordTrans coordinates = Coordinates();
        var image = new YUVImage(new byte[data.Manifest.Width * data.Manifest
            .Height], data.Manifest.Width, data.Manifest.Height);
        int warmup = Math.Max(1006, data.Pixels.Length * 2);
        int iterations = checked(cycles * data.Pixels.Length);

        for (int i = 0; i < warmup; i++)
        {
            data.Pixels[i % data.Pixels.Length].CopyTo(image.Bytes, 0);
            pipeline.Process(image, coordinates);
        }

        GC.Collect(2, GCCollectionMode.Forced, blocking: true);
        double[] timings = new double[iterations];
        long allocated = 0;
        int checksum = 0;

        for (int i = 0; i < iterations; i++)
        {
            byte[] source = data.Pixels[i % data.Pixels.Length];

            if (mode != "new-image")
            {
                source.CopyTo(image.Bytes, 0);
            }

            long beforeAllocation = GC.GetAllocatedBytesForCurrentThread();
            long start = Stopwatch.GetTimestamp();
            YUVImage input =
                mode == "new-image" ? new YUVImage((byte[])source.Clone(
                ), data.Manifest.Width, data.Manifest.Height) : image;
            PipelineContext result = pipeline.Process(input, coordinates);
            timings[i] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            allocated += GC.GetAllocatedBytesForCurrentThread() -
                beforeAllocation;
            checksum += result.CurvePointClusters?.Sum(c => c.Count) ?? 0;
        }

        Array.Sort(timings);
        Save(output, new
        {
            CapturedUtc = DateTimeOffset.UtcNow,
            Mode = mode,
            DatasetSha256 = data.Manifest.SourceArchiveSha256,
            LibrarySha256 = FileHash(typeof(LineDetectionPipelineFactory)
            .Assembly.Location),
            Runtime = RuntimeInformation.FrameworkDescription,
            OS = RuntimeInformation.OSDescription,
            TieredCompilation = Environment.GetEnvironmentVariable(
            "DOTNET_TieredCompilation") ?? "runtime default",
            Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
            Environment.ProcessorCount,
            SourceWidth = data.Manifest.Width,
            SourceHeight = data.Manifest.Height,
            OutputWidth = 128,
            OutputHeight = 96,
            Settings = settings,
            SceneCount = data.Pixels.Length,
            Cycles = cycles,
            Iterations = iterations,
            Warmup = warmup,
            MedianMs = Percentile(timings, .5),
            P95Ms = Percentile(timings,
            .95),
            P99Ms = Percentile(timings, .99),
            MeanMs = timings.Average(),
            MaximumMs = timings[^1],
            AllocatedBytesPerFrameCallingThread = (double)allocated /
            iterations,
            WorkingSetBytes = Environment.WorkingSet,
            Checksum = checksum,
            Notes = "Release; all dataset images repeated " +
            "equally. File I/O and console excluded. " +
            "Pristine image reset excluded except " +
            "new-image mode, which includes input " +
            "allocation/copy. Resize included only " +
            "in resize mode. Allocations exclude " +
            "worker threads. Camera/UI/control " +
            "excluded. Desktop results do not " + "predict RPi " + "speed."
        });
    }

    /// <summary>
    /// Selects the nearest-rank percentile from sorted timing samples.
    /// </summary>
    /// <param name="timings">
    /// The nonempty timing samples sorted in ascending order.
    /// </param>
    /// <param name="probability">
    /// The requested cumulative proportion, such as 0.95.
    /// </param>
    /// <returns>
    /// The selected elapsed time in milliseconds.
    /// </returns>
    private static double Percentile(double[] timings, double probability)
    {
        int index = Math.Clamp((int)Math.Ceiling(probability * timings.Length)
            - 1, 0, timings.Length - 1);

        return timings[index];
    }
}

/// <summary>
/// Pairs a dataset manifest with pristine Y-plane buffers.
/// </summary>
/// <param name="Manifest">
/// The dataset dimensions, provenance and scene metadata.
/// </param>
/// <param name="Pixels">
/// The pristine Y-plane buffers in manifest scene order.
/// </param>
internal sealed record LoadedData(Dataset Manifest, byte[][] Pixels);

/// <summary>
/// Records dataset dimensions, source identity and scene metadata.
/// </summary>
/// <param name="Width">The image width in pixels.</param>
/// <param name="Height">The image height in pixels.</param>
/// <param name="SourceWidth">The source image width before preparation.</param>
/// <param name="SourceHeight">
/// The source image height before preparation.
/// </param>
/// <param name="SourceArchive">The name of the original ZIP archive.</param>
/// <param name="SourceArchiveSha256">
/// The SHA-256 identity of the original archive.
/// </param>
/// <param name="Preparation">
/// The description of the transformation applied to the images.
/// </param>
/// <param name="Scenes">
/// The ordered image metadata and optional reference annotations.
/// </param>
internal sealed record Dataset(int Width, int Height, int SourceWidth,
    int SourceHeight,
    string SourceArchive, string SourceArchiveSha256, string Preparation,
    List<Scene> Scenes);

/// <summary>
/// Records image provenance and optional row-by-row reference centres.
/// </summary>
/// <param name="File">The prepared image filename, without a directory.</param>
/// <param name="SourceEntry">
/// The original filename within the source archive.
/// </param>
/// <param name="SourceSha256">
/// The SHA-256 of the original archive entry.
/// </param>
/// <param name="PreparedSha256">
/// The SHA-256 of the prepared image bytes.
/// </param>
/// <param name="CenterByRow">
/// Optional per-row reference centres; null means unannotated.
/// </param>
internal sealed record Scene(string File, string SourceEntry,
    string SourceSha256,
    string PreparedSha256, double?[]? CenterByRow);

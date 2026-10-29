# How the vision pipeline works

## 1. Images and ownership

`Models/YUVImage.cs` holds the image dimensions and a caller-supplied `byte[]`. Each dimension must be between 16 and 640, inclusive. The array must contain at least `Width * Height` bytes.

The first `Width * Height` bytes are a packed, top-down Y (brightness) plane. Pixel `(x, y)` is at `y * Width + x`: `(0, 0)` is the top-left corner, x increases rightwards and y increases downwards. There is no row padding. A camera that supplies a larger row stride needs an adapter that copies the Y samples into this layout.

`Luma` is a span over this Y plane. `Bytes` exposes the complete array, which may also contain trailing U/V colour data. Gamma, histogram, Gaussian filtering and thresholding use only Y. They do not interpret those trailing bytes as extra pixels. A resize produces a new Y-only image; it does not resize the colour planes.

The constructor does **not** copy the array. Gamma, image filtering and thresholding operate in place. Reusing an already thresholded frame as the input to another run changes the experiment. Reload, clone or restore pristine pixels first. The runner does this for you. With resizing, later stages operate on the resized image in `context.Image`; do not assume it is still the input object.

The histogram is allocated lazily and its arrays are reused. Keeping a reference to `image.Histogram.Histogram` does not preserve a historical snapshot; copy it if needed. Pipeline result contexts and detection lists are created per call, but an image reference retained in a result can still refer to a buffer reused by the caller.

## 2. Composition and settings

`Pipeline/LineDetectionPipelineFactory.cs` constructs stages from `LineDetectionSettings`. Each stage implements `IPipelineStep.Execute(PipelineContext)`. A `PipelineContext` carries the current image, source dimensions, detected points and optional derived outputs between stages.

The factory validates the settings. Also call `settings.GetOutputSize(sourceWidth, sourceHeight)` when the input size is known: this checks downsampling constraints and whether the sample-row count fits the processed height. The factory alone does not know the incoming image dimensions.

| Order | Stage | Default | What it changes |
| --- | --- | --- | --- |
| 1 | `ResizeBilinearStep` | Off; target 128 × 96 | Replaces the image with a downsampled Y image |
| 2 | `GammaCorrectionStep` | On; gamma 0.5 | Changes brightness in place |
| 3 | `GaussianFilterStep` | Off; sigma 3 | Smooths image pixels |
| 4 | `HistogramUpdateStep` | Always present; histogram sigma 5 | Rebuilds histogram, optionally smooths its bins |
| 5 | `OtsuThresholdStep` | On; background margin 40 | Produces black/white pixels or rejects low contrast |
| 6 | `SobelEdgeStepAdaptive` | On; 10 sampled rows | Produces centre-point clusters |
| 7 | `HomographyStep` | Off | Projects suitable detections onto a calibrated plane |
| 8 | `BezierCurveFitStep` | Image fit off; ground fit requested when homography is on | Fits optional curves |
| 9 | `OffsetAndAngleStep` | On | Calculates legacy offset/angle and optional ground mean |

`AdaptiveSobelEdge = false` selects the older `SobelEdgeStep` when detection is enabled. Turning off Otsu does not adapt the existing detector's edge thresholds to grayscale input. Supply equivalent binary preprocessing if you replace thresholding.

Settings are read when steps are constructed; changing the settings object later does not reconfigure an existing pipeline. Rebuild it between frames. Gamma is an **instance** property on `GammaCorrectionStep`, not a static global setting.

Use one pipeline sequentially. Its stages and scratch buffers are mutable; do not process two frames concurrently through the same instance. Independent pipelines with separate images can be run concurrently. Avoid shared static mutable state in new stages.

## 3. Brightness and smoothing

Gamma correction maps each intensity using approximately `round(255 * (input / 255)^gamma)`. Gamma below 1 brightens intermediate values; gamma above 1 darkens them. The stage precomputes a 256-entry lookup table so it does not evaluate a power for every pixel in every frame. Its direct API requires a finite, positive gamma; the settings model restricts the configurable range to 0.25–2.0.

Image Gaussian filtering and histogram Gaussian filtering solve different problems. The image filter smooths spatial neighbours and may remove noise while also blurring a thin line. Histogram smoothing spreads counts between nearby intensity levels; it does not blur neighbouring image pixels.

The separable image filter performs horizontal and vertical passes. Kernels and scratch arrays are retained by the stage. The helper uses sequential convolution for fewer than 4,096 pixels and can use parallel execution above that size. This policy is not a guarantee that parallelism is best on a Raspberry Pi.

## 4. Histogram and Otsu threshold

`HistogramUpdateStep` must run after the brightness/image transformations whose pixels you intend to threshold. Running Otsu with an old histogram is a pipeline error.

`YUVImageHistogram.Histogram` contains raw counts. `NormalizedHistogram` contains the optionally smoothed histogram divided by its **maximum bin**, not by the total count. It is not a probability distribution that sums to 1. The cumulative histogram is normalized by the total. Otsu's helper calculates its own total weight, so maximum normalization is accounted for.

Otsu searches for a split with strong separation between the two intensity classes. The convention is:

```text
pixel <= threshold -> 0   (dark)
pixel >  threshold -> 255 (light)
```

The inclusive comparison is significant: a black-and-white frame can have a threshold of zero. Using `<` would incorrectly turn intensity zero into white.

The stage compares the positions of the strongest histogram bins on either side of the threshold. Their separation must be at least `BackgroundMargin`. Constant images are rejected even when the margin is zero. Rejection fills the Y plane with white; it is not a general confidence estimate for arbitrary scenes.

For accepted frames, the first and last **four pixels of every row** are forced white. This is part of the current algorithm and affects lines that touch the image boundary. It is not a marker exclusion mask.

## 5. Row sampling and centre points

The adaptive detector visits rows from bottom to top. For processed height `H`, sample count `N` and index `p` in `0..N-1`, its row is:

```text
y = H - 1 - floor(p * H / N)
```

At 128 × 96 with 10 samples the rows are `95, 86, 76, 67, 57, 47, 38, 28, 19, 9`. This is sparse row sampling, not a full two-dimensional segmentation algorithm.

On each row it scans left to right and evaluates a six-pixel horizontal difference: the sum of three samples on the right minus three on the left. On binary input, a magnitude of 765 represents a strong transition. It takes the first pair of such transitions and estimates their centre. Near image boundaries it can adjust that estimate using the mean width of earlier detections **within the same frame**. It stores integer centre coordinates, so fractional centre information is discarded.

A missing sample interrupts the current cluster. A later successful row begins a new cluster in `CurvePointClusters`. The algorithm does not choose between multiple candidate lines, remember a tracked path across frames, or classify markers. Two plausible transitions alone do not prove that the object is the driving line.

## 6. Output semantics

| Output | Meaning |
| --- | --- |
| `CurvePointClusters` | Groups of centre samples in processed-image pixels; can be null or empty |
| `Offset` | Nullable mean of all detected points mapped through `CoordTrans` |
| `HeadingAngle` | Nullable `atan2(meanX, meanY)` in degrees |
| `GroundStatus`, `GroundPoints` | Validity and samples in an explicitly named calibrated frame |
| `GroundMean`, `GroundCurve` | Optional calibrated mean and fitted ground curve |
| `BezierCurve` | Optional legacy image/observation curve |

The heading is a bearing from the observation origin to the mean point. **It is not the tangent of the detected curve.**

With the standard `CoordTrans`, positive `Offset.X` points right and positive `Offset.Y` points forward. `HeadingAngle` is measured from the positive forward axis, with positive values toward the right. Zero is a valid result. Missing detections yield null offset and angle in a fresh standard pipeline result; never silently replace null with zero and call that a centred line.

The library produces observations. It does not stop motors, choose a recovery manoeuvre or control speed. Those decisions belong to a host/controller.

## Coordinates and calibration

The legacy `CoordTrans` linearly maps an image rectangle to configured lateral/forward ranges. The settings' `FOWX*`/`FOWY*` values are used by the host when constructing this object; the pipeline does not automatically construct it from those settings.

Homography is a separate, optional mapping based on measured correspondences on a flat ground plane. `SourceWidth` and `SourceHeight` retain the capture dimensions before resizing, so calibration is checked against the source size. Ground outputs retain their own named coordinate frame and do not overwrite legacy outputs. In the measured robot ground frame, +X is forward and +Y is left; do not confuse that with legacy `Offset.X`/`Offset.Y`.

See the [homography reference](../ComputerVision/HOMOGRAPHY.md) for calibration, projection validity and ground-curve fitting. The concurrency and image-ownership contract is described above.

## 7. Loading from disk versus passing an image

The quick-start API passes a `YUVImage` directly. The overload accepting `ImageLoadOptions` creates a context but does not automatically load a file. For that overload, explicitly add `ImageLoadFromDiskStep` before the processing steps:

```csharp
var diskPipeline = new ImageProcessingPipeline();
diskPipeline.AddStep(new ImageLoadFromDiskStep(keepOriginalImage: true));
diskPipeline.AddSteps(LineDetectionPipelineFactory.CreateSteps(settings));
var diskResult = diskPipeline.Process(
    new ImageLoadOptions("data/course/frame0000.y", 128, 96), coordinates);
```

This snippet additionally needs `ComputerVision.Inputs` and `ComputerVision.ProcessingSteps`. `ImageLoader` reads exactly the required Y bytes, ignores trailing bytes and returns null for an I/O failure such as a truncated file. The disk-loading stage converts that failure into an exception. It does not decode PNG or JPEG.

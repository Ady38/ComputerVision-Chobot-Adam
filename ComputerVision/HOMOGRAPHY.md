# Calibrated line geometry

`HomographyStep` maps detected centreline samples from raw camera pixels to a named ground plane in centimetres. Run it after detection and before fitting/averaging. It does not warp images or transform an existing cubic's controls: under a perspective transform, those transformed controls generally do not describe the transformed curve.

## Usage

```csharp
using System.Numerics;
using ComputerVision.Calibration;
using ComputerVision.Pipeline;
using ComputerVision.Utils;

// Example measured TL, TR, BR, BL pixels and a known 4 x 3 cm board rectangle.
Vector2[] pixels = [new(24, 11), new(104, 11), new(107, 74), new(21, 74)];
var mapping = PlanarHomography.Fit([
    new(pixels[0], new(3, 0)), new(pixels[1], new(3, -4)),
    new(pixels[2], new(0, -4)), new(pixels[3], new(0, 0))]);
GroundCalibration? calibration = new(mapping, 128, 96, pixels,
    "board-bottom-left-X-up-Y-left-cm");
var settings = new LineDetectionSettings { ApplyHomography = true, FitGroundBezier = true };
var pipeline = LineDetectionPipelineFactory.Create(settings, () => calibration);
// rawImage is a top-down, unresized YUVImage from this calibrated camera.
var result = pipeline.Process(rawImage, new CoordTrans(0, 128, 96, 0, -3.25f, 3.25f, 0, 4.6f));
var samples = result.GroundPoints;
Vector2? mean = result.GroundMean;
GroundBezier? curve = result.GroundCurve;
```

The factory inserts Homography before `BezierCurveFitStep` and `OffsetAndAngleStep`. `ApplyHomography` defaults false to keep existing stage composition. `FitGroundBezier` defaults true when homography is enabled. `FitBezierCubicCurve` independently controls the legacy image curve; `OffsetAndAngleStep` controls both legacy offset and ground mean. Settings are serializable; calibration is supplied by the host, not a graphics dependency or a file path hidden in the library.

## Measured placement in the robot frame

`GroundCalibration` accepts an optional `planeToRobot` argument. Omit it until the plane's origin and orientation have been measured relative to the driving-wheel axle midpoint. For example (illustrative measurements only):

```csharp
var placement = new GroundPlanePose { ForwardCm = 12, LeftCm = 2, YawDegrees = 30 };
calibration = new GroundCalibration(mapping, 128, 96, pixels,
    "board-bottom-left-X-up-Y-left-cm", planeToRobot: placement);
```

Robot +X is forward and +Y is left. Translation is the plane origin in robot cm; positive yaw rotates plane +X toward robot left. `ToRobot` applies `R(yaw) * point + translation`. The stage uses `GroundCalibration.TryProject`: pixel-to-plane homography first, then this rigid placement and validity checks, then mean/cubic fitting. The rigid placement itself preserves lengths and curve shape.

`Mapping` and `PlaneCoordinateFrame` still describe the original calibration plane. `CoordinateFrame` describes effective output and becomes `GroundPlanePose.RobotCoordinateFrame` when placement exists. Null means unknown placement and preserves plane coordinates; explicit zero placement means measured coincident frames. All three pose fields are required and must be finite. The host owns measurement, persistence and camera compatibility checks; the library does not derive placement from rendering or simulator world coordinates.

## Output contract

- `GroundStatus` distinguishes disabled, missing calibration, source-size mismatch, missing detections, no calibrated samples, and success.
- `GroundPoints` owns a copied, read-only list in its explicit `CoordinateFrame`, plus detection/selection counts. Both consumers use precisely that list.
- `GroundMean` is the arithmetic mean after projection. Equal pixel-row sampling is not equal spacing in cm; this is not an arc-length-weighted mean.
- `GroundCurve` is a 2D cubic fitted after projection. Its controls have no ambiguous homogeneous component. `Evaluate(t)` uses De Casteljau, for `t` from 0 to 1. It is not an arc-length/time parameterization.
- The largest detected cluster supplies the longest continuous section with usable projections across the source image, including outside the calibration outline. The outline establishes the mapping and ground side of the projective horizon; it is not a detection mask. Excluded samples end a section, so invalid gaps are not joined.
- `Offset`, `HeadingAngle`, `CurvePointsWorld` and `BezierCurve` retain their legacy CoordTrans frame and selection rules. Ground outputs never overwrite them. Existing controllers therefore do not unexpectedly receive board-centred centimetres.

The frame's `SourceWidth` and `SourceHeight` describe the original camera image before pipeline resizing. Pipeline entry points set them; hosts manually constructing `PipelineContext` must supply them. Scaling back from the processed image follows `ResizeBilinearStep`'s source = destination * sourceSize / destinationSize convention. Calibration size must match the source size, not the processed size.

The host must also validate camera mount, optics and pixel orientation; matching resolution alone is insufficient. Replace the provider's calibration with null when invalid. Calibration changes do not require rebuilding the pipeline. No previous ground outputs are reused when projection fails or the line disappears.

`GroundCalibration.TryProject` checks finite source-image coordinates, the calibrated side of the horizon, a default 1 source-pixel horizon margin, and a default 100 cm radius in output coordinates. The radius is centred at the robot axle when measured placement exists, otherwise at the board origin. This usable region is independent of the calibration rectangle's extent. Hosts may supply `projectionLimits: new GroundProjectionLimits { MaximumDistanceCm = 100, HorizonMarginPixels = 1 }` to the constructor; omitted limits preserve these defaults. `Contains` only tests reference-rectangle membership. `TryMap` performs the coordinate transformation without these region checks; pipeline detection selection uses `TryProject`.

Mapping outside the measured rectangle assumes the same flat ground plane. Its accuracy depends on calibration quality and lens distortion; the validity checks do not establish a measurement-error bound.

`PlanarHomography.Fit` uses normalized DLT. `FromMatrix` copies and validates a finite, nonsingular 3 x 3 matrix. Projection rejects a near-zero denominator; a trusted calibration outline cannot cross the projective horizon. Calibration state is immutable and supplied per pipeline instance. Process frames sequentially through each pipeline instance because its stages have mutable working buffers. Separate pipelines with separate images can run independently.

## Obsolete compatibility entry points

The static `LoadHomographyMatrix` and parameterless constructor are obsolete compatibility entry points. The factory uses the instance calibration provider shown above. A parameterless instance snapshots the loaded matrix; later static loads cannot alter that instance. Loading reports invalid files or matrices. The matrix maps raw pixels to centimetres, not CoordTrans observation coordinates.

# Testing and debugging

## Run the existing suite

From the repository root:

```sh
dotnet restore ComputerVision.Student.slnx
dotnet build ComputerVision.Student.slnx -c Release --no-restore
dotnet test tests/ComputerVision.Tests/ComputerVision.Tests.csproj -c Release --no-build
```

The suite has 65 cases. A `[Theory]` with several `[InlineData]` values runs once for each row. No hardware or image dataset is required for these unit tests.

To run one test or list the available tests:

```sh
dotnet test tests/ComputerVision.Tests/ComputerVision.Tests.csproj -c Release --filter FullyQualifiedName~LostLineDoesNotReuseLastResult
dotnet test tests/ComputerVision.Tests/ComputerVision.Tests.csproj --list-tests
```

To save a test report:

```sh
dotnet test tests/ComputerVision.Tests/ComputerVision.Tests.csproj -c Release --logger "trx;LogFileName=tests.trx" --results-directory results/tests
```

Use `--no-build` only after building the current code in the same configuration. Otherwise it can test an old DLL and conceal a mistake.

## What the tests establish

| File | Examples of behaviour covered |
| --- | --- |
| `ImageContractTests.cs` | Image dimensions/buffer size, Y-plane ownership, truncated input and resizing |
| `ProcessingTests.cs` | Gamma validation/isolation, preservation of chroma, histogram reuse, Gaussian convolution against an independent reference |
| `PipelineTests.cs` | Known synthetic centre, constant and low-contrast rejection, lost-line behaviour, sample rows and independent pipelines |
| `ProfilingTests.cs` | Timing results for both processing entry points |
| `DeCasteljauTests.cs` | Independent polynomial/tangent references, legacy adaptation, endpoints, degenerate inputs and allocation-free cubic evaluation |

These tests protect the covered API contracts. They do not establish accuracy for every real track, marker rejection, temporal tracking or complete calibrated-geometry coverage.

## Process the real dataset

```sh
dotnet run --project ComputerVision.Runner -c Release -- evaluate data/course results/evaluation.json
```

This verifies lengths and SHA-256 hashes, runs every frame through the default settings, and writes a `Scenes` array. Inspect each scene's `Points`, `DetectedPoints`, `HasLine`, `Threshold`, `Offset` and `HeadingAngle`.

All supplied `CenterByRow` values are initially null, so the real frames have no manually verified reference centres. `Annotated: false` and null error fields are expected. The proportion of frames with an observation is **not accuracy**: the dataset includes no verified labels telling you whether those observations are correct.

`ProcessedYHash` can reveal changed output pixels, but matching hashes are not an independent correctness proof. A changed hash can be legitimate after an algorithm change; investigate the actual image and detections.

The GitHub workflow restores, builds, checks code style, runs the tests, and processes this dataset. It checks that the program runs with intact data; it does not assert a fixed number of detected points or a hard time limit on a shared CI machine. See [the code-style guide](CODE_STYLE.md) for the local formatting check and the documentation/line-length conventions.

## Debug a frame in stages

1. Confirm dimensions and the raw Y-plane layout.
2. Check the gamma setting and whether the input has already been processed.
3. Inspect the histogram before thresholding and the chosen Otsu split.
4. Inspect the binary image, including its four-pixel white side borders.
5. Inspect the sampled rows and the first edge pair on each row.
6. Compare pixel centres before investigating `CoordTrans` or calibrated outputs.

A breakpoint in `IPipelineStep.Execute` implementations is useful. To keep intermediate pixels, copy them at that point: retaining a reference to the shared byte array is insufficient. For large repeated experiments, disable debugger attachment and logging before measuring time.

## Common setup problems

| Symptom | Check |
| --- | --- |
| SDK cannot be found / `net10.0` unsupported | Install the .NET 10 SDK; run `dotnet --list-sdks` from the repository root |
| Package restore fails | First restore needs access to NuGet; check network/proxy configuration |
| File or manifest not found | Run from the root, or pass the correct dataset path |
| Dataset length/hash mismatch | Restore the affected course file; do not edit binary `.y` files as text |
| Different output on a second run | Restore pristine input pixels before calling the pipeline again |
| Constant image yields no line | Expected behaviour, including black and mid-gray constant images |
| Access to `GammaCorrectionStep.Gamma` fails | Gamma belongs to a stage instance; configure that instance or pass gamma to its constructor |
| `--no-build` appears to ignore an edit | Build the current configuration or remove `--no-build` |

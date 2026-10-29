# Measuring processing time and memory

## Run a measurement

Record the Git commit, machine/CPU, operating system, .NET runtime, build configuration, dataset, settings and command. When comparing configurations, use the **same machine** under similar load. A desktop result is not a measurement of Raspberry Pi Zero 2 W performance.

Build in Release and run without a debugger. Load data before timing. Keep camera capture, disk I/O, image display and motor control outside a library-only measurement, and state that exclusion. When measuring the complete robot loop, include those costs and label the result accordingly.

```sh
dotnet build ComputerVision.Student.slnx -c Release
dotnet run --project ComputerVision.Runner -c Release --no-build -- benchmark data/course results/baseline-1.json 10 default
```

Repeat the measurement several times. Compare medians and tail times across runs rather than selecting the fastest run. Interpret timing together with the detections produced by those settings.

Use distinct output filenames to preserve repeated measurements. Report files under `results/` are ignored by Git.

## Runner modes

```sh
dotnet run --project ComputerVision.Runner -c Release --no-build -- --help
dotnet run --project ComputerVision.Runner -c Release --no-build -- benchmark data/course results/default.json 10 default
dotnet run --project ComputerVision.Runner -c Release --no-build -- benchmark data/course results/gaussian.json 10 gaussian
dotnet run --project ComputerVision.Runner -c Release --no-build -- benchmark data/course results/new-image.json 10 new-image
```

| Mode | Included in timed interval | Purpose |
| --- | --- | --- |
| `default` | Standard pipeline; existing input image reused | Processing cost with reusable input storage |
| `gaussian` | Same, with image Gaussian smoothing enabled | Cost/benefit of spatial smoothing |
| `new-image` | New image allocation, pixel clone and standard pipeline | Cost when a host allocates each input frame |
| `benchmark-zip` | Resize from the original source size, followed by the standard pipeline | Cost of reducing a camera-size input |

The first three modes are intended for the supplied 128 × 96 prepared dataset. In reusable-input modes, pristine pixels are restored **outside** the timed interval. In `new-image`, that allocation and copy are inside it. Do not compare these modes as though they include identical work.

If you have the original archive, measure the resize path separately:

```sh
dotnet run --project ComputerVision.Runner -c Release --no-build -- benchmark-zip "DATASET SEMESTRALKA.zip" results/resize.json 10 640 480
```

ZIP reading and extraction into memory happen before timing. The raw archive is not required for the normal 128 × 96 experiment.

## Understand the JSON report

The runner warms up with at least 1,006 frames or two complete dataset passes, whichever is larger. It then processes each dataset frame equally often. Ten cycles of 503 images produce 5,030 measured intervals. The report includes settings, runtime/OS/architecture, logical processor count, dataset identity, compiled library hash and warm-up size.

- `MedianMs` describes a typical measured frame; `P95Ms` and `P99Ms` describe slower tail intervals.
- `MeanMs` and `MaximumMs` provide additional context. A single maximum can reflect unrelated scheduling.
- `AllocatedBytesPerFrameCallingThread` counts managed allocations made on the calling thread during the interval. **It excludes worker-thread allocations**, native allocations and memory already allocated before timing.
- `WorkingSetBytes` is the process working-set snapshot, including the runtime and loaded dataset. It is neither peak memory nor library-only memory.
- `Checksum` sums detected point counts to provide a quick output sanity check. It is not an accuracy metric.
- `LibrarySha256` identifies the measured DLL. Also record the Git commit: the report does not infer it.

The timed path uses the normal pipeline, not the diagnostic profiling wrapper. Runtime tiering can affect results; the report records `DOTNET_TieredCompilation` if you set it. Keep that policy the same for both versions. Avoid treating the report's rounded display or a small difference within run-to-run variation as proof of a speedup.

Useful calculations are `(old - new) / old * 100` for a time reduction and `old / new` for a speedup factor. State which one you report. `1000 / milliseconds` is only a processing-only throughput estimate, not the robot's camera/control frame rate.

## Find which stage is expensive

`ProfilingImageProcessingPipeline` wraps each stage and records fractional milliseconds:

```csharp
var profiler = new ProfilingImageProcessingPipeline();
profiler.AddSteps(LineDetectionPipelineFactory.CreateSteps(settings));
var profiledResult = profiler.Process(
    new YUVImage((byte[])original.Clone(), width, height), coordinates);

foreach (var step in profiledResult.ProfilingResult!.StepResults)
{
    Console.WriteLine($"{step.StepName}: {step.ElapsedMilliseconds:F4} ms");
}
```

This continues the variables and imports in the README example. Warm up before collecting stage timings. Profiling creates records and adds stopwatch overhead, so use it to locate costs. Do not use it to claim the allocation cost of the normal pipeline, and do not expect the sum of inner timings to equal total processing time exactly.

## Measurements on the robot

For an on-robot measurement, record the actual OS/runtime architecture, input resolution, settings, camera path, thermal/load conditions and whether capture is included. A .NET deployment and hardware test are separate from running these desktop experiments; this starter makes no claim of a verified robot frame rate.

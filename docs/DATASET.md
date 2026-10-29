# Dataset, image format and reference labels

## Prepared frames

`data/course/` contains 503 raw grayscale frames at 128 × 96. They contain brightness values before gamma correction, denoising or thresholding. The included files are sufficient for evaluation and benchmarking.

Each `.y` file contains exactly 12,288 bytes: one unsigned brightness value per pixel, left to right, then top to bottom. There is no header. The first byte is pixel `(0, 0)`; byte 128 begins row 1; byte 12,287 is pixel `(127, 95)`. Values range from 0 (black) to 255 (white).

Do not attempt to open `.y` files with a PNG/JPEG decoder. A raw-image viewer should be configured for 8-bit grayscale, width 128, height 96, no header and no row padding.

## Manifest and reproducibility

`manifest.json` records the source archive hash, dimensions, preparation method and an ordered `Scenes` list. Each scene includes its prepared filename, original archive entry, original-file SHA-256, prepared-file SHA-256 and optional reference labels. The runner validates every prepared file's size and hash before evaluation or benchmarking.

To import a ZIP of packed 640 × 480 raw YUV frames, use a new empty destination. Replace `frames.zip` with the archive path:

```sh
dotnet run --project ComputerVision.Runner -c Release -- import "frames.zip" data/reimported 640 480
```

The importer reads each frame's Y plane and resizes it to 128 × 96. It refuses to overwrite a nonempty directory. Keep imported datasets separate from `data/course` so the provided files and their checksums remain reproducible.

## Reference centres

Initially every frame has `CenterByRow: null`. That means **unannotated**, not “there is no line.” Baseline predictions are not ground truth.

To annotate a separate copy of the dataset, replace a scene's null field with an array of exactly 96 entries. Array index is image y. Each entry is either a number in `[0, 128)` giving the manually established centre x, or null meaning **no target line at that row**. Fractional centres are allowed. Do not use null within an annotated array to mean “I have not checked this row”; that would label the row as absent.

For example, a confirmed vertical line centred at x = 64 at every row has 96 entries all equal to 64. A confirmed blank image has 96 null entries. An unchecked image keeps the entire field null. The image hashes do not change when only annotations change.

Document who annotated the data, how ambiguous frames were handled and which frames were used for development versus final evaluation. Adjacent recorded frames can be similar; avoid treating near-duplicates on both sides of a split as strong evidence of generalization.

The evaluator checks the rows sampled by the default detector. It reports:

- `ExpectedRows`: sampled rows with a reference centre.
- `MissedRows`: expected sampled rows without a detected point.
- `FalsePoints`: detections on sampled rows labelled as having no line.
- `MeanAbsoluteErrorPixels` and `MaximumErrorPixels`: centre errors on matched sampled rows; null if there are no matched rows.

A low centre error alone can hide many missed detections. Report misses and false points alongside it. The runner's evaluator assumes the current row-sampling scheme and uses the first detected point on a row; if you change that output contract, adapt and document the evaluator rather than presenting incomparable numbers.

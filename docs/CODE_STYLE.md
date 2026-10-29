# Code style and numerical types

## Documentation and layout

Every explicit method and constructor in the library, runner and tests has
an XML `summary`. Each parameter has a matching, nonempty `param` entry.
Methods that return a value also have a `returns` entry. Constructors and
`void` methods do not need an artificial return description. Classes and
structs use explicit constructors with documentation on the constructor.
Positional records keep their parameter documentation on the declaration.

Describe the operation, units, ownership, nullable results and relevant
failure cases. For example, `YUVImage.TrySaveBytesAsync` returns false for an
empty buffer or an `IOException`; it does not catch every possible access or
argument error. Its parameter is named `bytes`, so the documentation uses
that exact name.

The common formatting rules are:

- Use four spaces and Allman braces on separate lines.
- Keep base types and interfaces on the declaration line, with no space before
  the colon and one after it: `public sealed class BezierCurve: CurveBase`.
- Constructor initializers use the same spacing and stay on one line:
  `public HomographyStep(): this(() => null)`. Keep the constructor's parameter
  list and `base(...)` or `this(...)` call together.
- Put braces around every `if` body, including a single return or throw.
- Separate completed control-flow blocks and logical code segments with
  an empty line. Keep `else`, `catch` and `finally` attached to their block.
- Put short explanatory comments above the code they explain, with an empty
  line before the comment. Explain a reason or constraint, not obvious syntax.
- Keep comments and XML documentation lines within 80 characters, including
  indentation and comment markers. Wrap long descriptions inside their XML
  tags. Code may exceed 80 characters; use line breaks where they aid reading.

`.editorconfig` records the editor-supported rules. It does not set a general
`max_line_length`, because that would also suggest a limit for code. The
80-character limit applies to comments only and needs a separate check;
do not assume the formatter wraps prose or XML documentation automatically.

## Shared editor configuration

The repository's `.editorconfig` defines these conventions:

- Name private/internal instance fields `_camelCase`, their static counterparts
  `s_camelCase`, and constants `PascalCase`.
- Use explicit types for built-in values and expressions whose types are not
  apparent. `var` is appropriate when the initializer states the type.
- Use ordinary bodies for methods and constructors; simple properties may
  use expression bodies or auto-properties.
- Use explicit class/struct constructors and an explicit `Program.Main`.
  Positional records remain data contracts with their generated properties,
  deconstruction and equality behaviour.
- Keep file-scoped namespaces, explicit accessibility, and parentheses where
  they clarify mixed operators.
- Keep nullable-initialization warnings enabled. Initialize required members
  rather than suppressing `CS8618` throughout the project.

Formatting options use plain values. Their warning severity is set through
`IDE0055`; language-style preferences may include a severity suffix. The
unused-parameter rule has a separate `IDE0060` severity setting. JSON and
YAML use two spaces for the dataset manifest and CI workflow.

Build-time code-style analysis is enabled in `Directory.Build.props`. Check
the full solution with:

```powershell
./tools/Check-CodeStyle.ps1
```

Run restore first on a new checkout. This script runs the SDK formatter and
accepts only its request to insert one space before a constructor-initializer
colon. Visual Studio's built-in formatter requires that space and does not
expose the type-inheritance spacing option for constructors. Its `IDE0055`
warning on a constructor colon without a preceding space is expected;
other formatting issues still fail
the check. Whole files and other formatting rules are not excluded.

On Windows, run the script in PowerShell; on Linux/macOS, use
`pwsh -File tools/Check-CodeStyle.ps1`. GitHub's Ubuntu runner supplies `pwsh`.
The optional `-WhitespaceOnly` switch checks layout without loading projects;
it does not replace the full style check. Reports are saved under `results/`.

To apply standard fixes, run `dotnet format ComputerVision.Student.slnx`,
review the diff, restore the documented constructor-colon spacing, and rerun
the tests. Formatter success does not establish complete XML documentation,
useful comments, or the line-length convention; review those too.

## Numerical types

Using one namespace everywhere would look simpler, but the numerical types
serve different purposes in this code:

| Role | Type | Reason |
| --- | --- | --- |
| Pixel indexes | `ComputerVision.Models.Point` | Integer image coordinates |
| Two-dimensional ground geometry | `System.Numerics.Vector2` | A fixed-size value type with X and Y |
| Calibration and least-squares fitting | MathNet `Matrix<double>` | Matrix factorization and SVD solving |
| Legacy curve/coordinate API | MathNet `Vector<float>` and `Matrix<float>` | Compatibility with existing consumers |

Microsoft documents [`Vector2` as a value type](https://learn.microsoft.com/en-us/dotnet/api/system.numerics.vector2?view=net-10.0),
while MathNet provides a [general-purpose vector class](https://numerics.mathdotnet.com/api/MathNet.Numerics.LinearAlgebra/Vector%601.htm).
Replacing every small geometry value with newly allocated dense vectors
would add object/storage work unless those buffers were explicitly reused.
That is an undesirable default for the Raspberry Pi target and is not needed
to share the curve algorithm.

Use `Vector2` for new two-dimensional geometry and MathNet where a matrix
solver is needed. Keep conversions at the legacy API boundary. The existing
MathNet interfaces retain their documented coordinate conventions.

This is a division of responsibilities, not a claim that all operations in
one library are faster than another. Measure an actual alternative on the
target hardware before changing the policy.

## One De Casteljau implementation

`GraphicalObjects/DeCasteljauAlgorithm.cs` contains the single
`EvaluateInPlace` interpolation loop. `GroundBezier.Evaluate` calls the shared
`Evaluate` entry point. The legacy `GetDeCasteljauPoint` method adapts MathNet
controls and uses the same loop. Curve sampling also goes through that path.

The remaining `Lerp` calls in curve fitting place or elevate control points.
They construct the curve; they are not additional curve-evaluation algorithms.

The evaluator copies controls into scratch storage and repeatedly interpolates
neighbours until one point remains. The last pair before that reduction gives
the tangent direction. The parameter is not arc length; values outside
0 to 1 extrapolate. Tangent angles are radians from positive X, unlike the
pipeline's legacy `HeadingAngle` in degrees from the forward axis.

Up to 32 working points use bounded stack storage; higher-degree curves use
an array. Evaluating an existing four-control ground cubic requires no managed
heap allocation. The legacy adapter still creates its MathNet result vector.
Its third component is zero;
only X and Y describe its sampled point. A single legacy control is returned
unchanged. New geometry code should use the explicit two-component interface.

An empty control set raises `ArgumentException`.
Control points are never overwritten. Tests compare
the shared evaluator against independent Bernstein formulas and analytic
tangents, check boundary cases and protect allocation-free ground evaluation.

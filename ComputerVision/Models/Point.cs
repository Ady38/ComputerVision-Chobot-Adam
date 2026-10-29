namespace ComputerVision.Models;

/// <summary>
/// Creates an integer image point.
/// </summary>
public readonly struct Point
{

    /// <summary>
    /// Creates an integer image point.
    /// </summary>
    /// <param name="x">The horizontal pixel coordinate.</param>
    /// <param name="y">The vertical pixel coordinate.</param>
    public Point(int x, int y)
    {
        X = x;
        Y = y;
    }

    public int X
    {
        get;
    }

    public int Y
    {
        get;
    }

    /// <summary>
    /// Formats the two integer coordinates.
    /// </summary>
    /// <returns>
    /// The coordinates in the form (X, Y).
    /// </returns>
    public override string ToString()
    {
        return $"({X}, {Y})";
    }
}
